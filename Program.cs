/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using FlowSharpServiceInterfaces;

namespace FlowSharp
{
    static partial class Program
    {
        private const string UnhandledExceptionCaption = "FlowSharp Unhandled Exception";

        [STAThread]
        static void Main(string[] args)
        {
            Tuple<string, string> startup = ResolveStartupArguments(args);
            string modules = startup.Item1;
            string startupDiagram = startup.Item2;

            ConfigureGlobalExceptionHandling();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Bootstrap(modules);
            IFlowSharpService flowSharpService = ServiceManager.Get<IFlowSharpService>();

            if (!String.IsNullOrWhiteSpace(startupDiagram))
            {
                flowSharpService.FlowSharpInitialized += (sndr, evt) =>
                {
                    ServiceManager.Get<IFlowSharpCanvasService>().LoadDiagrams(startupDiagram);
                };
            }

            Icon icon = Properties.Resources.FlowSharp;
            Form form = flowSharpService.CreateDockingForm(icon);
            Application.Run(form);
        }

        private static void ConfigureGlobalExceptionHandling()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sndr, args) => ShowUnhandledExceptionDialog(args.Exception, canContinue: true);
            AppDomain.CurrentDomain.UnhandledException += (sndr, args) =>
            {
                Exception ex = args.ExceptionObject as Exception ?? new Exception("Unhandled non-exception object thrown.");
                ShowUnhandledExceptionDialog(ex, canContinue: false);
            };
        }

        private static void ShowUnhandledExceptionDialog(Exception exception, bool canContinue)
        {
            try
            {
                using (var dialog = new UnhandledExceptionDialog(exception, canContinue))
                {
                    DialogResult result = dialog.ShowDialog();

                    if (!canContinue || result == DialogResult.Abort)
                    {
                        Environment.Exit(1);
                    }
                }
            }
            catch
            {
                MessageBox.Show(exception.ToString(), UnhandledExceptionCaption, MessageBoxButtons.OK, MessageBoxIcon.Error);

                if (!canContinue)
                {
                    Environment.Exit(1);
                }
            }
        }

        private static Tuple<string, string> ResolveStartupArguments(string[] args)
        {
            string modules = "modules.xml";
            string startupDiagram = null;

            if (args == null)
            {
                return Tuple.Create(modules, startupDiagram);
            }

            foreach (string arg in args)
            {
                if (String.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                if (String.Equals(Path.GetExtension(arg), ".fsd", StringComparison.OrdinalIgnoreCase))
                {
                    startupDiagram = arg;
                }
                else
                {
                    modules = arg;
                }
            }

            return Tuple.Create(modules, startupDiagram);
        }

        private static void ShowAnyExceptions(List<Exception> exceptions)
        {
            foreach (var ex in exceptions)
            {
                MessageBox.Show(ex.Message, "Module Finalizer Exception", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private sealed class UnhandledExceptionDialog : Form
        {
            public UnhandledExceptionDialog(Exception exception, bool canContinue)
            {
                Text = UnhandledExceptionCaption;
                StartPosition = FormStartPosition.CenterScreen;
                Size = new Size(980, 640);
                MinimumSize = new Size(700, 400);
                FormBorderStyle = FormBorderStyle.Sizable;
                MaximizeBox = true;
                MinimizeBox = true;

                var topLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 56,
                    Padding = new Padding(8),
                    Text = canContinue
                        ? "An unhandled exception occurred. You can continue or exit."
                        : "A fatal unhandled exception occurred. The application must exit."
                };

                var details = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    WordWrap = false,
                    Font = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point),
                    Text = exception?.ToString() ?? "Unknown exception."
                };

                var buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 48,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(8)
                };

                var exitButton = new Button
                {
                    Text = "Exit",
                    AutoSize = true,
                    DialogResult = DialogResult.Abort
                };
                buttons.Controls.Add(exitButton);

                if (canContinue)
                {
                    var continueButton = new Button
                    {
                        Text = "Continue",
                        AutoSize = true,
                        DialogResult = DialogResult.OK
                    };
                    buttons.Controls.Add(continueButton);
                    AcceptButton = continueButton;
                }
                else
                {
                    AcceptButton = exitButton;
                }

                CancelButton = exitButton;

                Controls.Add(details);
                Controls.Add(buttons);
                Controls.Add(topLabel);
            }
        }
    }
}
