using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

using Clifton.Core.Assertions;
using Clifton.Core.ExtensionMethods;
using Clifton.Core.ModuleManagement;
using Clifton.Core.ServiceManagement;
using Clifton.WinForm.ServiceInterfaces;

using FlowSharpLib;
using FlowSharpServiceInterfaces;
using FlowSharpCodeServiceInterfaces;
using FlowSharpCodeShapeInterfaces;

namespace FlowSharpCodeService
{
    public class FlowSharpCodeModule : IModule
    {
        public void InitializeServices(IServiceManager serviceManager)
        {
            serviceManager.RegisterSingleton<IFlowSharpCodeService, FlowSharpCodeService>();
        }
    }

    public class FlowSharpCodeService : ServiceBase, IFlowSharpCodeService
    {
        private const string LANGUAGE_CSHARP = "C#";
        private const string LANGUAGE_PYTHON = "Python";
        private const string LANGUAGE_JAVASCRIPT = "JavaScript";
        private const string LANGUAGE_HTML = "HTML";
        private const string LANGUAGE_CSS = "CSS";

        protected readonly ToolStripMenuItem mnuCSharp = new ToolStripMenuItem() { Name = "mnuCSharp", Text = "C#" };
        protected readonly ToolStripMenuItem mnuPython = new ToolStripMenuItem() { Name = "mnuPython", Text = "Python" };
        protected readonly ToolStripMenuItem mnuJavascript = new ToolStripMenuItem() { Name = "mnuJavascript", Text = "JavaScript" };
        protected readonly ToolStripMenuItem mnuHtml = new ToolStripMenuItem() { Name = "mnuHtml", Text = "Html" };
        protected readonly ToolStripMenuItem mnuCss = new ToolStripMenuItem() { Name = "mnuCss", Text = "Css" };
        protected readonly ToolStripMenuItem mnuOutput = new ToolStripMenuItem() { Name = "mnuOutput", Text = "Output" };

        protected Dictionary<string, int> fileCaretPos = new Dictionary<string, int>();

        public override void FinishedInitialization()
        {
            base.FinishedInitialization();
            var fss = ServiceManager.Get<IFlowSharpService>();
            var ces = ServiceManager.Get<IFlowSharpCodeEditorService>();
            var ses = ServiceManager.Get<IFlowSharpScintillaEditorService>();
            fss.FlowSharpInitialized += OnFlowSharpInitialized;
            fss.ContentResolver += OnContentResolver;
            fss.NewCanvas += OnNewCanvas;
            ces.TextChanged += OnCSharpEditorServiceTextChanged;
            ses.TextChanged += OnScintillaEditorServiceTextChanged;
            InitializeEditorsMenu();
        }

        // Workflow processing:

        public GraphicElement FindStartOfWorkflow(BaseController canvasController, GraphicElement wf)
        {
            var start = canvasController.Elements.FirstOrDefault(srcEl => wf.DisplayRectangle.Contains(srcEl.DisplayRectangle) &&
                !srcEl.IsConnector &&
                !srcEl.Connections.Any(c =>
                    new[] { GripType.TopMiddle, GripType.LeftMiddle, GripType.RightMiddle }
                        .Contains(c.ElementConnectionPoint.Type)) &&
                srcEl.Connections.Any(c =>
                    new[] { GripType.BottomMiddle }
                        .Contains(c.ElementConnectionPoint.Type)));

            return start;
        }

        protected GripType[] GetTrueConnections(IIfBox el)
        {
            var path = el.TruePath == TruePath.Down ? new[] { GripType.BottomMiddle } : new[] { GripType.LeftMiddle, GripType.RightMiddle };

            return path;
        }

        protected GripType[] GetFalseConnections(IIfBox el)
        {
            var path = el.TruePath == TruePath.Down ? new[] { GripType.LeftMiddle, GripType.RightMiddle } : new[] { GripType.BottomMiddle };

            return path;
        }

        // True path is always the bottom of the diamond.
        public GraphicElement GetTruePathFirstShape(IIfBox el)
        {
            var path = GetTrueConnections(el);
            GraphicElement trueStart = null;
            var connection = ((GraphicElement)el).Connections.FirstOrDefault(c => path.Contains(c.ElementConnectionPoint.Type));

            if (connection != null)
            {
                trueStart = ((Connector)connection.ToElement).EndConnectedShape;
            }

            return trueStart;
        }

        // False path is always the left or right point of the diamond.
        public GraphicElement GetFalsePathFirstShape(IIfBox el)
        {
            var path = GetFalseConnections(el);
            GraphicElement falseStart = null;
            var connection = ((GraphicElement)el).Connections.FirstOrDefault(c => path.Contains(c.ElementConnectionPoint.Type));

            if (connection != null)
            {
                falseStart = ((Connector)connection.ToElement).EndConnectedShape;
            }

            return falseStart;
        }

        /// <summary>
        /// Find the next shape connected to el.
        /// </summary>
        /// <param name="el"></param>
        /// <returns>The next connected shape or null if no connection exists.</returns>
        public GraphicElement NextElementInWorkflow(GraphicElement el)
        {
            // Always the shape connected to the bottom of the current shape:
            GraphicElement ret = null;

            var connection = el.Connections.FirstOrDefault(c => c.ElementConnectionPoint.Type == GripType.BottomMiddle);

            if (connection != null)
            {
                ret = ((Connector)connection.ToElement).EndConnectedShape;
            }

            return ret;
        }

        // ===================================================

        // Process Launcher:

        public Process LaunchProcess(string processName, string arguments, Action<string> onOutput, Action<string> onError = null)
        {
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.FileName = processName;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.CreateNoWindow = true;

            p.OutputDataReceived += (sndr, args) => { if (args.Data != null) onOutput(args.Data); };

            if (onError != null)
            {
                p.ErrorDataReceived += (sndr, args) => { if (args.Data != null) onError(args.Data); };
            }

            p.Start();

            // Interestingly, this has to be called after Start().
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            return p;
        }

        public void LaunchProcessAndWaitForExit(string processName, string arguments, Action<string> onOutput, Action<string> onError = null)
        {
            var proc = LaunchProcess(processName, arguments, onOutput, onError);
            proc.WaitForExit();
        }

        public void TerminateProcess(Process p)
        {
            Assert.SilentTry(() => p?.Kill());
        }

        // ===================================================

        // DRAKON workflow

        public  GraphicElement ParseDrakonWorkflow(DrakonCodeTree dcg, IFlowSharpCodeService codeService, BaseController canvasController, GraphicElement el, bool inCondition = false)
        {
            while (el != null)
            {
                // If we're in a conditional and we encounter a shape with multiple "merge" connections, then we assume (I think rightly so)
                // that this is the end of the conditional branch, and that code should continue at this point outside of the "if-else" statement.
                if (inCondition)
                {
                    var connections = el.Connections.Where(c => c.ElementConnectionPoint.Type == GripType.TopMiddle);

                    if (connections.Count() > 1)
                    {
                        return el;
                    }
                }

                switch (el)
                {
                    // All these if's.  Yuck.
                    case IBeginForLoopBox _:
                    {
                        var drakonLoop = new DrakonLoop() { Code = ParseCode(el) };
                        dcg.AddInstruction(drakonLoop);
                        var nextEl = codeService.NextElementInWorkflow(el);

                        if (nextEl != null)
                        {
                            el = ParseDrakonWorkflow(drakonLoop.LoopInstructions, codeService, canvasController, nextEl);
                        }
                        else
                        {
                            // TODO: error -- there are no further elements after the beginning for loop box!
                            ServiceManager.Get<IFlowSharpCodeOutputWindowService>().WriteLine("Error: Drakon start 'for' loop does not have any statements!");
                            return el;
                        }

                        break;
                    }
                    case IEndForLoopBox _:
                        return el;
                    case IIfBox elBox:
                    {
                        var drakonIf = new DrakonIf() { Code = ParseCode(el) };
                        dcg.AddInstruction(drakonIf);

                        var elTrue = codeService.GetTruePathFirstShape(elBox);
                        var elFalse = codeService.GetFalsePathFirstShape(elBox);

                        if (elTrue != null)
                        {
                            ParseDrakonWorkflow(drakonIf.TrueInstructions, codeService, canvasController, elTrue, true);
                        }

                        if (elFalse != null)
                        {
                            ParseDrakonWorkflow(drakonIf.FalseInstructions, codeService, canvasController, elFalse, true);
                        }
                        // dcg.AddInstruction(new DrakonEndIf());
                        break;
                    }
                    case IOutputBox _:
                        dcg.AddInstruction(new DrakonOutput() { Code = ParseCode(el) });
                        break;
                    default:
                        dcg.AddInstruction(new DrakonStatement() { Code = ParseCode(el) });
                        break;
                }
                el = codeService.NextElementInWorkflow(el);
            }
            return null;
        }

        protected string ParseCode(GraphicElement el)
        {
            // TODO: This is a mess.  Imagine what it will look like when we add more languages!
            if (el.Json.TryGetValue("python", out var ret)) return ret;
            if (!el.Json.TryGetValue("Code", out ret))
            {
                // Replace crlf with space and if element has 'python" code in Json, use that instead.
                ret = el.Text.Replace("\r", "").Replace("\n", " ");
            }
            return ret;
        }

        // ===================================================

        public void OutputWindowClosed()
        {
            mnuOutput.Checked = false;
        }

        public void EditorWindowClosed(string language)
        {
            // TODO: Fix this hardcoded language name and generalize with how editors are handled.
            if (language == LANGUAGE_CSHARP)
            {
                mnuCSharp.Checked = false;
            }
            else
            {
                // TODO: Fix this hardcoded language name and generalize with how editors are handled.
                switch (language.ToLower())
                {
                    case "python":
                        mnuPython.Checked = false;
                        break;

                    case "javascript":
                        mnuJavascript.Checked = false;
                        break;

                    case "html":
                        mnuHtml.Checked = false;
                        break;

                    case "css":
                        mnuCss.Checked = false;
                        break;
                }
            }
        }

        protected void InitializeEditorsMenu()
        {
            // TODO: Make this declarative, and put lexer configuration into an XML file or something.
            ToolStripMenuItem editorsToolStripMenuItem = new ToolStripMenuItem() { Name = "mnuEditors", Text = "Editors" };
            editorsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { mnuCSharp, mnuPython, mnuJavascript, mnuHtml, mnuCss, new ToolStripSeparator(), mnuOutput });
            ServiceManager.Get<IFlowSharpMenuService>().AddMenu(editorsToolStripMenuItem);

            mnuCSharp.Click += OnCreateCSharpEditor;
            mnuPython.Click += OnCreatePythonEditor;
            mnuJavascript.Click += OnCreateJavascriptEditor;
            mnuHtml.Click += OnCreateHtmlEditor;
            mnuCss.Click += OnCreateCssEditor;
            mnuOutput.Click += OnCreateOutputWindow;

            // mnuCSharp.Checked = true;

            ServiceManager.Get<IFlowSharpMenuService>().AddMenu(editorsToolStripMenuItem);
        }

        private void OnCreateCSharpEditor(object sender, EventArgs e)
        {
            mnuCSharp.Checked.Else(() => CreateCSharpEditor());
            mnuCSharp.Checked = true;
        }

        private void OnCreatePythonEditor(object sender, EventArgs e)
        {
            mnuPython.Checked.Else(() => CreateEditor(LANGUAGE_PYTHON));
            mnuPython.Checked = true;
        }

        private void OnCreateJavascriptEditor(object sender, EventArgs e)
        {
            mnuJavascript.Checked.Else(() => CreateEditor(LANGUAGE_JAVASCRIPT));
            mnuJavascript.Checked = true;
        }

        private void OnCreateHtmlEditor(object sender, EventArgs e)
        {
            mnuHtml.Checked.Else(() => CreateEditor(LANGUAGE_HTML));
            mnuHtml.Checked = true;
        }

        private void OnCreateCssEditor(object sender, EventArgs e)
        {
            mnuCss.Checked.Else(() => CreateEditor(LANGUAGE_CSS));
            mnuCss.Checked = true;
        }

        private void OnCreateOutputWindow(object sender, EventArgs e)
        {
            mnuOutput.Checked.Else(() => CreateOutputWindow());
            mnuOutput.Checked = true;
        }

        protected void OnFlowSharpInitialized(object sender, EventArgs args)
        {
             //IDockDocument csEditor = CreateCSharpEditor();
             //CreateEditor("python");
             //CreateOutputWindow();

            // Select C# editor, as it's the first tab in the code editor panel.
            // ServiceManager.Get<IDockingFormService>().SetActiveDocument(csEditor);
        }

        Control csDocEditor;

        protected IDockDocument CreateCSharpEditor()
        {
            var dockingService = ServiceManager.Get<IDockingFormService>();
            // Panel dock = dockingService.DockPanel;
            var d = FindDocument(dockingService, FlowSharpCodeServiceInterfaces.Constants.META_SCINTILLA_EDITOR);

            if (d == null)
            {
                var docCanvas = FindDocument(dockingService, FlowSharpServiceInterfaces.Constants.META_CANVAS);

                csDocEditor = docCanvas == null ? dockingService.CreateDocument(DockState.Document, "C# Editor", FlowSharpCodeServiceInterfaces.Constants.META_CSHARP_EDITOR) : dockingService.CreateDocument(docCanvas, DockAlignment.Bottom, "C# Editor", FlowSharpCodeServiceInterfaces.Constants.META_CSHARP_EDITOR, 0.50);
            }
            else
            {
                csDocEditor = dockingService.CreateDocument(d, DockState.Document, "C# Editor", FlowSharpCodeServiceInterfaces.Constants.META_CSHARP_EDITOR);
            }

            var pnlCsCodeEditor = new Panel() { Dock = DockStyle.Fill };
            csDocEditor.Controls.Add(pnlCsCodeEditor);

            var csCodeEditorService = ServiceManager.Get<IFlowSharpCodeEditorService>();
            csCodeEditorService.CreateEditor(pnlCsCodeEditor);
            csCodeEditorService.AddAssembly("Clifton.Core.dll");

            return (IDockDocument)csDocEditor;
        }

        protected void CreateEditor(string language)
        {
            Control docEditor;
            var dockingService = ServiceManager.Get<IDockingFormService>();
            var d = FindDocument(dockingService, FlowSharpCodeServiceInterfaces.Constants.META_CSHARP_EDITOR);

            if (d == null)
            {
                d = FindDocument(dockingService, FlowSharpCodeServiceInterfaces.Constants.META_SCINTILLA_EDITOR);

                if (d == null)
                {
                    d = FindDocument(dockingService, FlowSharpServiceInterfaces.Constants.META_CANVAS);

                    docEditor = d == null ? dockingService.CreateDocument(DockState.Document, language + " Editor", FlowSharpCodeServiceInterfaces.Constants.META_SCINTILLA_EDITOR) : dockingService.CreateDocument(d, DockAlignment.Bottom, language + " Editor", FlowSharpCodeServiceInterfaces.Constants.META_SCINTILLA_EDITOR, 0.50);
                }
                else
                {
                    docEditor = dockingService.CreateDocument(d, DockState.Document, language + " Editor", FlowSharpCodeServiceInterfaces.Constants.META_SCINTILLA_EDITOR);
                }
            }
            else
            {
                docEditor = dockingService.CreateDocument(d, DockState.Document, language + " Editor", FlowSharpCodeServiceInterfaces.Constants.META_SCINTILLA_EDITOR);
            }

            // Panel dock = dockingService.DockPanel;
            // Interestingly, this uses the current document page, which, I guess because the C# editor was created first, means its using that pane.
            //Control pyDocEditor = dockingService.CreateDocument(DockState.Document, "Python Editor", FlowSharpCodeServiceInterfaces.Constants.META_PYTHON_EDITOR);
            var pnlCodeEditor = new Panel() { Dock = DockStyle.Fill, Tag = language };
            if (docEditor != null)
            {
                docEditor.Controls.Add(pnlCodeEditor);
                ((IDockDocument)docEditor).Metadata += "," + language; // Add language to metadata so we know what editor to create.
            }
            var scintillaEditorService = ServiceManager.Get<IFlowSharpScintillaEditorService>();
            scintillaEditorService.CreateEditor(pnlCodeEditor, language);
        }

        protected void CreateOutputWindow()
        {
            //IDockingFormService dockingService = ServiceManager.Get<IDockingFormService>();
            //Panel dock = dockingService.DockPanel;
            //Control docCanvas = FindDocument(dockingService, FlowSharpServiceInterfaces.Constants.META_CANVAS);

            //Control outputWindow = dockingService.CreateDocument(docCanvas, DockAlignment.Bottom, "Output", FlowSharpCodeServiceInterfaces.Constants.META_OUTPUT, 0.50);
            //Control pnlOutputWindow = new Panel() { Dock = DockStyle.Fill };
            //outputWindow.Controls.Add(pnlOutputWindow);

            var outputWindowService = ServiceManager.Get<IFlowSharpCodeOutputWindowService>();
            outputWindowService.CreateOutputWindow();
            // outputWindowService.CreateOutputWindow(pnlOutputWindow);
        }

        protected void OnContentResolver(object sender, ContentLoadedEventArgs e)
        {
            switch (e.Metadata.LeftOf(","))
            {
                case FlowSharpCodeServiceInterfaces.Constants.META_CSHARP_EDITOR:
                    var pnlEditor = new Panel() { Dock = DockStyle.Fill, Tag = FlowSharpCodeServiceInterfaces.Constants.META_CSHARP_EDITOR};
                    e.DockContent.Controls.Add(pnlEditor);
                    e.DockContent.Text = "C# Editor";
                    var csCodeEditorService = ServiceManager.Get<IFlowSharpCodeEditorService>();
                    csCodeEditorService.CreateEditor(pnlEditor);
                    csCodeEditorService.AddAssembly("Clifton.Core.dll");
                    break;

                case FlowSharpCodeServiceInterfaces.Constants.META_OUTPUT:
                    var pnlOutputWindow = new Panel() { Dock = DockStyle.Fill, Tag = FlowSharpCodeServiceInterfaces.Constants.META_OUTPUT };
                    e.DockContent.Controls.Add(pnlOutputWindow);
                    e.DockContent.Text = "Output";
                    var outputWindowService = ServiceManager.Get<IFlowSharpCodeOutputWindowService>();
                    outputWindowService.CreateOutputWindow(pnlOutputWindow);
                    break;

                case FlowSharpCodeServiceInterfaces.Constants.META_SCINTILLA_EDITOR:
                    var language = e.Metadata.RightOf(",");
                    var pnlCodeEditor = new Panel() { Dock = DockStyle.Fill, Tag = language };
                    e.DockContent.Controls.Add(pnlCodeEditor);
                    e.DockContent.Text = language.CamelCase() + " Editor";

                    var scintillaEditorService = ServiceManager.Get<IFlowSharpScintillaEditorService>();
                    scintillaEditorService.CreateEditor(pnlCodeEditor, language);
                    break;
            }
        }

        protected void OnNewCanvas(object sender, NewCanvasEventArgs args)
        {
            args.Controller.ElementSelected += OnElementSelected;
        }

        protected void OnElementSelected(object controller, ElementEventArgs args)
        {
            //ElementProperties elementProperties = null;
            var csCodeEditorService = ServiceManager.Get<IFlowSharpCodeEditorService>();
            var editorService = ServiceManager.Get<IFlowSharpScintillaEditorService>();

            if (args.Element != null)
            {
                var el = args.Element;
                Trace.WriteLine("*** ON ELEMENT SELECTED " + el.Id.ToString());
                el.CreateProperties();

                if (!string.IsNullOrEmpty(csCodeEditorService.Filename))
                {
                    // Save last position.
                    var curpos = csCodeEditorService.GetPosition();
                    fileCaretPos[csCodeEditorService.Filename] = curpos;
                    Trace.WriteLine("*** " + csCodeEditorService.Filename + " => SET CURRENT POS: " + curpos);
                }

                el.Json.TryGetValue("Code", out var code);
                csCodeEditorService.SetText("C#", code ?? string.Empty);

                var fn = el.Id.ToString();               // Use something that is unique for this shape's code.
                Trace.WriteLine("*** " + fn + " => SET ID");
                csCodeEditorService.Filename = fn;

                // Set the last known position if we have one.
                if (fileCaretPos.TryGetValue(fn, out var pos))
                {
                    Trace.WriteLine("*** " + fn + " => SET PREVIOUS POS: " + pos);
                    csCodeEditorService.SetPosition(pos);
                }
                else
                {
                    // A newly seen document, set the caret to pos 0 so the editor doesn't retain
                    // the previous scrollbar location.
                    csCodeEditorService.SetPosition(0);
                }

                el.Json.TryGetValue("python", out code);
                editorService.SetText("python", code ?? string.Empty);
            }
            else
            {
                csCodeEditorService.SetText("C#", string.Empty);
                editorService.SetText("python", string.Empty);
            }
        }

        // TODO: We want to be able to associate many different code types with the same shape.
        // This requires setting the Json dictionary appropriately for whatever editor is generating the event.
        // Are we doing this the right why?

        protected void OnCSharpEditorServiceTextChanged(object sender, TextChangedEventArgs e)
        {
            var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
            if (canvasService.ActiveController.SelectedElements.Count != 1) return;
            var el = canvasService.ActiveController.SelectedElements[0];
            el.Json["Code"] = e.Text;           // Should we call this C# or CSharp?
            el.Json["TextChanged"] = true.ToString();
        }

        protected void OnScintillaEditorServiceTextChanged(object sender, TextChangedEventArgs e)
        {
            var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
            if (canvasService.ActiveController.SelectedElements.Count != 1) return;
            var el = canvasService.ActiveController.SelectedElements[0];
            el.Json[e.Language] = e.Text;         // TODO: Should we call this Script or something else?
            el.Json["TextChanged"] = true.ToString();
        }

        /// <summary>
        /// Traverse the root docking panel to find the IDockDocument child control with the specified metadata tag.
        /// For example, dock.Controls[1].Controls[1].Controls[2].Metadata is META_TOOLBOX.
        /// </summary>
        protected Control FindPanel(Control ctrl, string tag)
        {
            Control ret = null;

            // dock.Controls[1].Controls[1].Controls[2].Metadata <- this is the toolbox
            if (ctrl is IDockDocument idoc && idoc.Metadata == tag)
            {
                return ctrl;
            }
            foreach (Control c in ctrl.Controls)
            {
                ret = FindPanel(c, tag);
                if (ret != null)
                {
                    break;
                }
            }
            return ret;
        }

        /// <summary>
        /// Return the document (or null) that implements IDockDocument with the specified Metadata tag.
        /// </summary>
        protected Control FindDocument(IDockingFormService dockingService, string tag)
        {
            return (Control)dockingService.Documents.FirstOrDefault(d => d.Metadata.LeftOf(",") == tag);
        }
    }
}
