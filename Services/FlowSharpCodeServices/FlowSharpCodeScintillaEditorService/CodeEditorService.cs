using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using ScintillaNET;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.ModuleManagement;
using Clifton.Core.Semantics;
using Clifton.Core.ServiceManagement;
using Clifton.WinForm.ServiceInterfaces;

using FlowSharpCodeServiceInterfaces;

namespace FlowSharpCodeScintillaEditorService
{
    public class CodeEditorModule : IModule
    {
        public void InitializeServices(IServiceManager serviceManager)
        {
            serviceManager.RegisterSingleton<IFlowSharpScintillaEditorService, ScintillaCodeEditorService>();
            serviceManager.RegisterSingleton<IFlowSharpCodeEditorService, CSharpScintillaCodeEditorService>();
        }
    }

    public class ScintillaCodeEditorReceptor : IReceptor
    {
    }

    public abstract class ScintillaEditor : Scintilla
    {
        public string Language { get; set; }
        public Control ContainerParent { get; set; }
        public int LastCaretPosition { get; set; }

        public abstract void ConfigureLexer();
    }

    public class ScintillaCodeEditorService : ServiceBase, IFlowSharpScintillaEditorService
    {
        public event EventHandler<TextChangedEventArgs> TextChanged;
        public event EventHandler<TextChangedEventArgs> CSharpTextChanged;

        // Only one editor per language is allowed.
        // TODO: How would we handle multiple editors of the same language, associated with two or more shapes?
        protected Dictionary<string, ScintillaEditor> editors;

        public ScintillaCodeEditorService()
        {
            editors = new Dictionary<string, ScintillaEditor>();
        }

        public override void FinishedInitialization()
        {
            base.FinishedInitialization();
            var dockingService = ServiceManager.Get<IDockingFormService>();
            dockingService.DocumentClosing += (sndr, args) => OnDocumentClosing(sndr);
        }

        public void CreateEditor(Control parent, string language)
        {
            string normalizedLanguage = NormalizeLanguage(language);
            ScintillaEditor editor;

            switch (normalizedLanguage)
            {
                case "c#":
                    editor = CreateEditor<CSharpEditor>(parent);
                    break;

                case "python":
                    editor = CreateEditor<PythonEditor>(parent);
                    break;

                case "javascript":
                    editor = CreateEditor<JavascriptEditor>(parent);
                    break;

                case "html":
                    editor = CreateEditor<HtmlEditor>(parent);
                    break;

                case "css":
                    editor = CreateEditor<CssEditor>(parent);
                    break;

                default:
                    return;
            }

            editor.Language = normalizedLanguage;
            editors[editor.Language] = editor;
        }

        public void SetText(string language, string text)
        {
            // TODO: Set the focus to the appropriate editor.
            // If the editor doesn't exist, create it.

            if (editors.TryGetValue(NormalizeLanguage(language), out var editor))
            {
                editor.Text = text;
            }
        }

        public int GetPosition(string language)
        {
            if (!editors.TryGetValue(NormalizeLanguage(language), out var editor))
            {
                return 0;
            }

            editor.LastCaretPosition = editor.CurrentPosition;

            return editor.LastCaretPosition;
        }

        public void SetPosition(string language, int pos)
        {
            if (!editors.TryGetValue(NormalizeLanguage(language), out var editor))
            {
                return;
            }

            int boundedPosition = Math.Max(0, Math.Min(pos, editor.TextLength));
            editor.LastCaretPosition = boundedPosition;
            editor.GotoPosition(boundedPosition);
        }

        // Great resource: https://github.com/jacobslusser/ScintillaNET/wiki/Displaying-Line-Numbers
        // Dynamically resizing the line number gutter:
        /*
            private int maxLineNumberCharLength;
            private void scintilla_TextChanged(object sender, EventArgs e)
            {
                // Did the number of characters in the line number display change?
                // i.e. nnn VS nn, or nnnn VS nn, etc...
                var maxLineNumberCharLength = scintilla.Lines.Count.ToString().Length;
                if (maxLineNumberCharLength == this.maxLineNumberCharLength)
                    return;

                // Calculate the width required to display the last line number
                // and include some padding for good measure.
                const int padding = 2;
                scintilla.Margins[0].Width = scintilla.TextWidth(Style.LineNumber, new string('9', maxLineNumberCharLength + 1)) + padding;
                this.maxLineNumberCharLength = maxLineNumberCharLength;
            }
        */

        protected ScintillaEditor CreateEditor<T>(Control parent) where T: ScintillaEditor, new()
        {
            ScintillaEditor editor = new T();
            editor.Margins[0].Width = 32;           // Wider than default of 16 so line numbers > 100 display.  Not sure if > 1000 will work correctly though.
            editor.Dock = DockStyle.Fill;
            editor.ConfigureLexer();
            editor.TextChanged += OnTextChanged;
            editor.LostFocus += OnLostFocus;
            parent.Controls.Add(editor);
            editor.ContainerParent = parent;

            return editor;
        }

        protected void OnLostFocus(object sender, EventArgs e)
        {
            if (sender is ScintillaEditor editor)
            {
                editor.LastCaretPosition = editor.CurrentPosition;
            }
        }

        protected void OnTextChanged(object sender, EventArgs e)
        {
            ScintillaEditor editor = (ScintillaEditor)sender;
            var eventArgs = new TextChangedEventArgs() { Language = editor.Language, Text = editor.Text };

            if (editor.Language == "c#")
            {
                eventArgs.Language = "C#";
                CSharpTextChanged.Fire(this, eventArgs);
            }
            else
            {
                TextChanged.Fire(this, eventArgs);
            }
        }

        protected void Closed(string language)
        {
            string normalizedLanguage = NormalizeLanguage(language);
            if (!editors.TryGetValue(normalizedLanguage, out var editor)) return;
            editor.ContainerParent.Controls.Remove(editor);
            editors.Remove(normalizedLanguage);
            ServiceManager.Get<IFlowSharpCodeService>().EditorWindowClosed(normalizedLanguage == "c#" ? "C#" : normalizedLanguage);
        }

        protected void OnDocumentClosing(object document)
        {
            if (!(document is Control ctrl)) return;

            string metadata = ((IDockDocument)document).Metadata.LeftOf(",");

            if (ctrl.Controls.Count != 1 ||
                (metadata != Constants.META_SCINTILLA_EDITOR && metadata != Constants.META_CSHARP_EDITOR))
                return;
            if (ctrl.Controls[0].Controls.Count > 0 &&
                ctrl.Controls[0].Controls[0] is ScintillaEditor edi)
            {
                Closed(edi.Language);
            }
        }

        protected string NormalizeLanguage(string language)
        {
            switch ((language ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "csharp":
                case "c#":
                    return "c#";

                default:
                    return (language ?? string.Empty).Trim().ToLowerInvariant();
            }
        }
    }

    public class CSharpScintillaCodeEditorService : ServiceBase, IFlowSharpCodeEditorService
    {
        public event EventHandler<TextChangedEventArgs> TextChanged;

        public string Filename { get; set; }

        protected ScintillaCodeEditorService ScintillaService => ServiceManager.Get<IFlowSharpScintillaEditorService>() as ScintillaCodeEditorService;

        public override void FinishedInitialization()
        {
            base.FinishedInitialization();
            var scintillaService = ScintillaService;

            if (scintillaService != null)
            {
                scintillaService.CSharpTextChanged += OnCSharpTextChanged;
            }
        }

        public void CreateEditor(Control parent)
        {
            ScintillaService?.CreateEditor(parent, "C#");
        }

        public void AddAssembly(string filename)
        {
        }

        public void AddAssembly(Type t)
        {
        }

        public int GetPosition()
        {
            return ScintillaService?.GetPosition("C#") ?? 0;
        }

        public void SetPosition(int pos)
        {
            ScintillaService?.SetPosition("C#", pos);
        }

        public void SetText(string language, string text)
        {
            ScintillaService?.SetText("C#", text);
        }

        protected void OnCSharpTextChanged(object sender, TextChangedEventArgs e)
        {
            TextChanged.Fire(this, e);
        }
    }

    public class CSharpEditor : ScintillaEditor
    {
        public override void ConfigureLexer()
        {
            StyleResetDefault();
            Styles[Style.Default].Font = "Consolas";
            Styles[Style.Default].Size = 10;
            StyleClearAll();

            LexerName = "cpp";
            IndentWidth = 4;
            TabWidth = 4;
            SetKeywords(0,
                "abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach get goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed set short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while");
            SetKeywords(1,
                "add alias ascending async await by descending dynamic equals from global group into join let nameof on orderby partial remove select unmanaged value var where yield");
        }
    }

    public class JavascriptEditor : ScintillaEditor
    {
        public override void ConfigureLexer()
        {
            LexerName = "cpp";
        }
    }

    public class HtmlEditor : ScintillaEditor
    {
        public override void ConfigureLexer()
        {
            LexerName = "html";
        }
    }

    public class CssEditor : ScintillaEditor
    {
        public override void ConfigureLexer()
        {
            LexerName = "css";
        }
    }

    public class PythonEditor : ScintillaEditor
    {
        public override void ConfigureLexer()
        {
            // Reset the styles
            StyleResetDefault();
            Styles[Style.Default].Font = "Consolas";
            Styles[Style.Default].Size = 10;
            StyleClearAll(); // i.e. Apply to all

            // Set the lexer
            LexerName = "python";
            IndentWidth = 2;
            TabWidth = 2;

            CharAdded += AutoIndent;

            // Known lexer properties:
            // "tab.timmy.whinge.level",
            // "lexer.python.literals.binary",
            // "lexer.python.strings.u",
            // "lexer.python.strings.b",
            // "lexer.python.strings.over.newline",
            // "lexer.python.keywords2.no.sub.identifiers",
            // "fold.quotes.python",
            // "fold.compact",
            // "fold"

            // Some properties we like
            SetProperty("tab.timmy.whinge.level", "1");
            SetProperty("fold", "1");

            // Use margin 2 for fold markers
            Margins[2].Type = MarginType.Symbol;
            Margins[2].Mask = Marker.MaskFolders;
            Margins[2].Sensitive = true;
            Margins[2].Width = 20;

            // Reset folder markers
            for (int i = Marker.FolderEnd; i <= Marker.FolderOpen; i++)
            {
                Markers[i].SetForeColor(SystemColors.ControlLightLight);
                Markers[i].SetBackColor(SystemColors.ControlDark);
            }

            // Style the folder markers
            Markers[Marker.Folder].Symbol = MarkerSymbol.BoxPlus;
            Markers[Marker.Folder].SetBackColor(SystemColors.ControlText);
            Markers[Marker.FolderOpen].Symbol = MarkerSymbol.BoxMinus;
            Markers[Marker.FolderEnd].Symbol = MarkerSymbol.BoxPlusConnected;
            Markers[Marker.FolderEnd].SetBackColor(SystemColors.ControlText);
            Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
            Markers[Marker.FolderOpenMid].Symbol = MarkerSymbol.BoxMinusConnected;
            Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;

            // Enable automatic folding
            AutomaticFold = (AutomaticFold.Show | AutomaticFold.Click | AutomaticFold.Change);

            // Set the styles
            Styles[Style.Python.Default].ForeColor = Color.FromArgb(0x80, 0x80, 0x80);
            Styles[Style.Python.CommentLine].ForeColor = Color.FromArgb(0x00, 0x7F, 0x00);
            Styles[Style.Python.CommentLine].Italic = true;
            Styles[Style.Python.Number].ForeColor = Color.FromArgb(0x00, 0x7F, 0x7F);
            Styles[Style.Python.String].ForeColor = Color.FromArgb(0x7F, 0x00, 0x7F);
            Styles[Style.Python.Character].ForeColor = Color.FromArgb(0x7F, 0x00, 0x7F);
            Styles[Style.Python.Word].ForeColor = Color.FromArgb(0x00, 0x00, 0x7F);
            Styles[Style.Python.Word].Bold = true;
            Styles[Style.Python.Triple].ForeColor = Color.FromArgb(0x7F, 0x00, 0x00);
            Styles[Style.Python.TripleDouble].ForeColor = Color.FromArgb(0x7F, 0x00, 0x00);
            Styles[Style.Python.ClassName].ForeColor = Color.FromArgb(0x00, 0x00, 0xFF);
            Styles[Style.Python.ClassName].Bold = true;
            Styles[Style.Python.DefName].ForeColor = Color.FromArgb(0x00, 0x7F, 0x7F);
            Styles[Style.Python.DefName].Bold = true;
            Styles[Style.Python.Operator].Bold = true;
            // Styles[Style.Python.Identifier] ... your keywords styled here
            Styles[Style.Python.CommentBlock].ForeColor = Color.FromArgb(0x7F, 0x7F, 0x7F);
            Styles[Style.Python.CommentBlock].Italic = true;
            Styles[Style.Python.StringEol].ForeColor = Color.FromArgb(0x00, 0x00, 0x00);
            Styles[Style.Python.StringEol].BackColor = Color.FromArgb(0xE0, 0xC0, 0xE0);
            Styles[Style.Python.StringEol].FillLine = true;
            Styles[Style.Python.Word2].ForeColor = Color.FromArgb(0x40, 0x70, 0x90);
            Styles[Style.Python.Decorator].ForeColor = Color.FromArgb(0x80, 0x50, 0x00);

            // Important for Python
            ViewWhitespace = WhitespaceMode.VisibleAlways;

            // Keyword lists:
            // 0 "Keywords",
            // 1 "Highlighted identifiers"

            var python2 = "and as assert break class continue def del elif else except exec finally for from global if import in is lambda not or pass print raise return try while with yield";
            // var python3 = "False None True and as assert break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield";
            var cython = "cdef cimport cpdef";

            SetKeywords(0, python2 + " " + cython);
        }

        private void AutoIndent(object sender, CharAddedEventArgs e)
        {
            Line currentLine = Lines[CurrentLine];
            int currentPos = CurrentPosition;

            if (e.Char == '\r')
            {
                Line previousLine = Lines[CurrentLine - 1];

                if (previousLine.Text.Trim().EndsWith(":"))
                {
                    currentLine.Indentation = previousLine.Indentation + IndentWidth;
                    CurrentPosition = currentPos + currentLine.Indentation;
                    SelectionStart = CurrentPosition;
                }
                else
                {
                    currentLine.Indentation = previousLine.Indentation;
                    CurrentPosition = currentPos + currentLine.Indentation;
                    SelectionStart = CurrentPosition;
                }
            }
        }
    }
}
