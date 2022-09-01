﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

using Microsoft.CSharp;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.ModuleManagement;
using Clifton.Core.ServiceManagement;

using FlowSharpCodeServiceInterfaces;
using FlowSharpCodeShapeInterfaces;
using FlowSharpServiceInterfaces;
using FlowSharpLib;
// ReSharper disable UnusedMember.Local

namespace FlowSharpCodeCompilerService
{
    // TODO: Fold into Clifton.Core.ExtensionMethods
    public static class ExtMeth
    {
        /// <summary>
        /// Returns the right of all text after matching p2 with p1.
        /// </summary>
        public static string RightOfMatching(this string src, char p1, char p2)
        {
            var count = 0;
            var idx = 0;

            while (idx < src.Length)
            {
                if (src[idx] == p1)
                {
                    count = 1;
                    break;
                }

                ++idx;
            }

            while (idx < src.Length)
            {
                if (src[idx] == p1) ++count;

                if (src[idx] == p2)
                {
                    if (--count == 0)
                    {
                        break;
                    }
                }

                ++idx;
            }

            var ret = (idx < src.Length) ? src.Substring(idx) : string.Empty;

            return ret;
        }
    }

    public class CSharpCodeGeneratorService : ICodeGeneratorService
    {
        public StringBuilder CodeResult { get; protected set; }

        protected int indent = 12;

        public CSharpCodeGeneratorService()
        {
            CodeResult = new StringBuilder();
        }

        public void BeginIf(string code)
        {
            CodeResult.AppendLine(new string(' ', indent) + "if (" +code+")");
            CodeResult.AppendLine(new string(' ', indent) + "{");
            indent += 4;
        }

        public void Else()
        {
            indent = 0.MaxDelta(indent - 4);
            CodeResult.AppendLine(new string(' ', indent) + "}");
            CodeResult.AppendLine("else");
            CodeResult.AppendLine(new string(' ', indent) + "{");
            indent += 4;
        }

        public void EndIf()
        {
            indent = 0.MaxDelta(indent - 4);
            CodeResult.Append(new string(' ', indent) + "{");
        }

        public void BeginFor(string code)
        {
            CodeResult.AppendLine(new string(' ', indent) + "foreach (" + code + ")");
            CodeResult.AppendLine(new string(' ', indent) + "{");
            indent += 4;
        }

        public void EndFor()
        {
            indent = 0.MaxDelta(indent - 4);
            CodeResult.AppendLine(new string(' ', indent) + "}");
        }

        public void Statement(string code)
        {
            CodeResult.AppendLine(new string(' ', indent) + code +";");
        }
    }

    public class FlowSharpCodeCompilerModule : IModule
    {
        public void InitializeServices(IServiceManager serviceManager)
        {
            serviceManager.RegisterSingleton<IFlowSharpCodeCompilerService, FlowSharpCodeCompilerService>();
        }
    }

    public class FlowSharpCodeCompilerService : ServiceBase, IFlowSharpCodeCompilerService
    {
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        //private const int SW_HIDE = 0;
        //private const int SW_SHOW = 5;

        protected readonly Dictionary<string, string> tempToTextBoxMap = new Dictionary<string, string>();
        protected string exeFilename;
        protected CompilerResults results;
        protected Process runningProcess;

        public override void FinishedInitialization()
        {
            base.FinishedInitialization();
            InitializeBuildMenu();
        }

        public void Run()
        {
            TerminateRunningProcess();
            var outputWindow = ServiceManager.Get<IFlowSharpCodeOutputWindowService>();
            var fscSvc = ServiceManager.Get<IFlowSharpCodeService>();
            outputWindow.Clear();

            // Ever compiled?
            // TODO: Compile when code has changed!
            if (results == null || results.Errors.HasErrors)
            {
                Compile();
            }

            // If no errors:
            if (results != null && !results.Errors.HasErrors)
            {
                runningProcess = fscSvc.LaunchProcess(exeFilename, null,
                    stdout => outputWindow.WriteLine(stdout),
                    stderr => outputWindow.WriteLine(stderr));
            }
        }

        public void Stop()
        {
            TerminateRunningProcess();
        }

        public void Compile()
        {
            TerminateRunningProcess();

            var outputWindow = ServiceManager.Get<IFlowSharpCodeOutputWindowService>();
            outputWindow.Clear();

            IFlowSharpCanvasService canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
            IFlowSharpMenuService menuService = ServiceManager.Get<IFlowSharpMenuService>();
            IFlowSharpCodeService codeService = ServiceManager.Get<IFlowSharpCodeService>();
            BaseController canvasController = canvasService.ActiveController;
            tempToTextBoxMap.Clear();

            List<GraphicElement> compiledAssemblies = new List<GraphicElement>();
            bool ok = CompileAssemblies(canvasController, compiledAssemblies);

            if (!ok)
            {
                DeleteTempFiles();
                return;
            }

            List<string> refs = new List<string>();
            List<string> sources = new List<string>();

            // Add specific assembly references on the drawing.
            List<IAssemblyReferenceBox> references = GetReferences(canvasController);
            refs.AddRange(references.Select(r => r.Filename));

            List<GraphicElement> rootSourceShapes = GetSources(canvasController);
            rootSourceShapes.ForEach(root => GetReferencedAssemblies(root).Where(refassy => refassy is IAssemblyBox).ForEach(refassy => refs.Add(((IAssemblyBox)refassy).Filename)));

            // Get code for workflow boxes first, as this code will then be included in the rootSourceShape code listing.
            IEnumerable<GraphicElement> workflowShapes = canvasController.Elements.Where(el => el is IWorkflowBox);
            workflowShapes.ForEach(wf =>
            {
                string code = GetWorkflowCode(codeService, canvasController, wf);
                wf.Json["Code"] = code;
            });

            List<GraphicElement> excludeClassShapes = new List<GraphicElement>();
            List<GraphicElement> classes = GetClasses(canvasController);

            // Get CSharpClass shapes that contain DRAKON shapes.
            classes.Where(cls => HasDrakonShapes(canvasController, cls)).ForEach(cls =>
            {
                excludeClassShapes.AddRange(canvasController.Elements.Where(el => cls.DisplayRectangle.Contains(el.DisplayRectangle)));
                DrakonCodeTree dcg = new DrakonCodeTree();
                var workflowStart = codeService.FindStartOfWorkflow(canvasController, cls);
                codeService.ParseDrakonWorkflow(dcg, codeService, canvasController, workflowStart);
                var codeGenSvc = new CSharpCodeGeneratorService();
                dcg.GenerateCode(codeGenSvc);
                InsertCodeInRunWorkflowMethod(cls, codeGenSvc.CodeResult);
                string filename = CreateCodeFile(cls);
                sources.Add(filename);
            });

            rootSourceShapes.Where(root => !excludeClassShapes.Contains(root)).ForEach(root =>
            {
            // Get all other shapes that are not part of CSharpClass shapes:
            // TODO: Better Linq!
            if (!String.IsNullOrEmpty(GetCode(root)))
                {
                    string filename = CreateCodeFile(root);
                    sources.Add(filename);
                }
            });

            exeFilename = String.IsNullOrEmpty(menuService.Filename) ? "temp.exe" : Path.GetFileNameWithoutExtension(menuService.Filename) + ".exe";
            Compile(exeFilename, sources, refs, true);
            DeleteTempFiles();

            if (!results.Errors.HasErrors)
            {
                outputWindow.WriteLine("No Errors");
            }
        }

        protected void InsertCodeInRunWorkflowMethod(GraphicElement root, StringBuilder code)
        {
            var cls = (ICSharpClass)root;

            //string existingCode;
            // TODO: Verify that root.Json["Code"] defines the namespace, class, and stub, or figure out how to include "using" and field initialization and properties such that
            // we can create the namespace, class, and stub for the user.
            //if (root.Json.ContainsKey("Code"))
            //{
            //    existingCode = root.Json["Code"];
            //}

            //string before = existingCode.LeftOf("void RunWorkflow()");
            //string after = existingCode.RightOf("void RunWorkflow()").RightOfMatching('{', '}');
            //StringBuilder finalCode = new StringBuilder(before);
            var finalCode = new StringBuilder();
            finalCode.AppendLine("using System;");
            finalCode.AppendLine("using System.Linq;");
            finalCode.AppendLine();

            finalCode.AppendLine("namespace " + cls.NamespaceName);
            finalCode.AppendLine("{");
            finalCode.AppendLine("    public partial class " + cls.ClassName);
            finalCode.AppendLine("    {");
            finalCode.AppendLine(new string(' ', 8) + "public void " + cls.MethodName + "()");
            finalCode.AppendLine(new string(' ', 8) + "{");
            finalCode.AppendLine(new string(' ', 12) + code.ToString().Trim());
            finalCode.AppendLine(new string(' ', 8) + "}");
            finalCode.AppendLine("    }");
            finalCode.AppendLine("}");
            // finalCode.Append(after);
            root.Json["Code"] = finalCode.ToString();
        }

        protected bool HasDrakonShapes(BaseController canvasController, GraphicElement elClass)
        {
            return canvasController.Elements.Any(srcEl => srcEl != elClass &&
                elClass.DisplayRectangle.Contains(srcEl.DisplayRectangle) &&
                srcEl is IDrakonShape);
        }

        protected void InitializeBuildMenu()
        {
            ToolStripMenuItem buildToolStripMenuItem = new ToolStripMenuItem();
            ToolStripMenuItem mnuCompile = new ToolStripMenuItem();
            ToolStripMenuItem mnuRun = new ToolStripMenuItem();
            ToolStripMenuItem mnuStop = new ToolStripMenuItem();

            mnuCompile.Name = "mnuCompile";
            mnuCompile.ShortcutKeys = Keys.Alt | Keys.C;
            mnuCompile.Size = new System.Drawing.Size(165, 24);
            mnuCompile.Text = "&Compile";

            mnuRun.Name = "mnuRun";
            mnuRun.ShortcutKeys = Keys.Alt | Keys.R;
            mnuRun.Size = new System.Drawing.Size(165, 24);
            mnuRun.Text = "&Run";

            mnuStop.Name = "mnuStop";
            mnuStop.ShortcutKeys = Keys.Alt | Keys.S;
            // mnuStop.ShortcutKeys = Keys.Alt | Keys.R;
            mnuStop.Size = new System.Drawing.Size(165, 24);
            mnuStop.Text = "&Stop";

            buildToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { mnuCompile, mnuRun, mnuStop });
            buildToolStripMenuItem.Name = "buildToolStripMenuItem";
            buildToolStripMenuItem.Size = new System.Drawing.Size(37, 21);
            buildToolStripMenuItem.Text = "C#";

            mnuCompile.Click += OnCompile;
            mnuRun.Click += OnRun;
            mnuStop.Click += OnStop;

            ServiceManager.Get<IFlowSharpMenuService>().AddMenu(buildToolStripMenuItem);
        }

        protected void OnCompile(object sender, EventArgs e)
        {
            Compile();
        }

        protected void OnRun(object sender, EventArgs e)
        {
            Run();
        }

        protected void OnStop(object sender, EventArgs e)
        {
            Stop();
        }

        protected void TerminateRunningProcess()
        {
            var fscSvc = ServiceManager.Get<IFlowSharpCodeService>();
            fscSvc.TerminateProcess(runningProcess);
            runningProcess = null;
        }

        protected string CreateCodeFile(GraphicElement root)
        {
            string code = GetCode(root);
            string filename = Path.GetFileNameWithoutExtension(Path.GetTempFileName()) + ".cs";
            tempToTextBoxMap[filename] = root.Text;
            File.WriteAllText(filename, code);

            return filename;
        }

        public string GetWorkflowCode(IFlowSharpCodeService codeService, BaseController canvasController, GraphicElement wf)
        {
            StringBuilder sb = new StringBuilder();
            string packetName = Clifton.Core.ExtensionMethods.ExtensionMethods.LeftOf(wf.Text, "Workflow");
            GraphicElement elDefiningPacket = FindPacket(canvasController, packetName);

            if (elDefiningPacket == null)
            {
                ServiceManager.Get<IFlowSharpCodeOutputWindowService>().WriteLine("Workflow packet '" + packetName + "' must be defined.");
            }
            else
            {
                bool packetHasParameterlessConstructor = HasParameterlessConstructor(GetCode(elDefiningPacket), packetName);

                // TODO: Hardcoded for now for POC.
                sb.AppendLine("// This code has been auto-generated by FlowSharpCode");
                sb.AppendLine("// Do not modify this code -- your changes will be overwritten!");
                sb.AppendLine("namespace App");
                sb.AppendLine("{");
                sb.AppendLine("\tpublic partial class " + wf.Text);
                sb.AppendLine("\t{");

                if (packetHasParameterlessConstructor)
                {
                    sb.AppendLine("\t\tpublic static void Execute()");
                    sb.AppendLine("\t\t{");
                    sb.AppendLine("\t\t\tExecute(new " + packetName + "());");
                    sb.AppendLine("\t\t}");
                    sb.AppendLine();
                }

                sb.AppendLine("\t\tpublic static void Execute(" + packetName + " packet)");
                sb.AppendLine("\t\t{");
                sb.AppendLine("\t\t\t" + wf.Text + " workflow = new " + wf.Text + "();");

                // Fill in the workflow steps.
                GraphicElement el = codeService.FindStartOfWorkflow(canvasController, wf);
                GenerateCodeForWorkflow(codeService, sb, el, 3);

                // We're all done.
                sb.AppendLine("\t\t}");
                sb.AppendLine("\t}");
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        protected GraphicElement FindPacket(BaseController canvasController, string packetName)
        {
            GraphicElement elPacket = canvasController.Elements.SingleOrDefault(el => el.Text == packetName);

            return elPacket;
        }

        /// <summary>
        /// Returns true if the code block contains a parameterless constructor of the form "public [packetname]()" or no constructor at all.
        /// </summary>
        protected bool HasParameterlessConstructor(string code, string packetName)
        {
            string signature = "public " + packetName + "(";

            bool ret = !code.Contains(signature) ||
                code.AllIndexesOf(signature).Any(idx => code.Substring(idx).RightOf('(')[0] == ')');

            return ret;
        }

        protected void GenerateCodeForWorkflow(IFlowSharpCodeService codeService, StringBuilder sb, GraphicElement el, int indent)
        {
            var strIndent = new string('\t', indent);

            while (el != null)
            {
                if (el is IIfBox elBox)
                {

                    // True clause
                    var elTrue = codeService.GetTruePathFirstShape(elBox);
                    // False clause
                    var elFalse = codeService.GetFalsePathFirstShape(elBox);

                    if (elTrue != null)
                    {
                        sb.AppendLine(strIndent + "bool " + el.Text.ToLower() + " = workflow." + el.Text + "(packet);");
                        sb.AppendLine();
                        sb.AppendLine(strIndent + "if (" + el.Text.ToLower() + ")");
                        sb.AppendLine(strIndent + "{");
                        GenerateCodeForWorkflow(codeService, sb, elTrue, indent + 1);
                        sb.AppendLine(strIndent + "}");

                        if (elFalse != null)
                        {
                            sb.AppendLine(strIndent + "else");
                            sb.AppendLine(strIndent + "{");
                            GenerateCodeForWorkflow(codeService, sb, elFalse, indent + 1);
                            sb.AppendLine(strIndent + "}");
                        }
                    }
                    else if (elFalse != null)
                    {
                        sb.AppendLine(strIndent + "bool " + el.Text.ToLower() + " = workflow." + el.Text + "(packet);");
                        sb.AppendLine();
                        sb.AppendLine(strIndent + "if (!" + el.Text.ToLower() + ")");
                        sb.AppendLine(strIndent + "{");
                        GenerateCodeForWorkflow(codeService, sb, elFalse, indent + 1);
                        sb.AppendLine(strIndent + "}");
                    }

                    // TODO: How to join back up with workflows that rejoin from if-then-else?
                    break;
                }
                else
                {
                    sb.AppendLine(strIndent + "workflow." + el.Text + "(packet);");
                }

                el = codeService.NextElementInWorkflow(el);
            }
        }

        protected void DeleteTempFiles()
        {
            tempToTextBoxMap.ForEach(kvp => File.Delete(kvp.Key));
        }

        //private void MnuRun_Click(object sender, EventArgs e)
        //{
        //    // Ever compiled?
        //    if (results == null || results.Errors.HasErrors)
        //    {
        //        Compile();
        //    }

        //    // If no errors:
        //    if (results != null && !results.Errors.HasErrors)
        //    {
        //        //ProcessStartInfo psi = new ProcessStartInfo(exeFilename);
        //        //psi.UseShellExecute = true;     // must be true if we want to keep a console window open.
        //        var p = Process.Start(exeFilename);
        //        //p.WaitForExit();
        //        //p.Close();
        //        //Type program = compiledAssembly.GetType("WebServerDemo.Program");
        //        //MethodInfo main = program.GetMethod("Main");
        //        //main.Invoke(null, null);
        //    }
        //}

        protected bool CompileAssemblies(BaseController canvasController, List<GraphicElement> compiledAssemblies)
        {
            foreach (var elAssy in canvasController.Elements.Where(el => el is IAssemblyBox))
            {
                CompileAssembly(canvasController, elAssy, compiledAssemblies);
            }

            return true;
        }

        protected string CompileAssembly(BaseController canvasController, GraphicElement elAssy, List<GraphicElement> compiledAssemblies)
        {
            string assyFilename = ((IAssemblyBox)elAssy).Filename;

            if (!compiledAssemblies.Contains(elAssy))
            {
                // Add now, so we don't accidentally recurse infinitely.
                compiledAssemblies.Add(elAssy);

                var referencedAssemblies = GetReferencedAssemblies(elAssy);
                var refs = new List<string>();

                // Recurse into referenced assemblies that need compiling first.
                foreach (var el in referencedAssemblies)
                {
                    var refAssy = CompileAssembly(canvasController, el, compiledAssemblies);
                    refs.Add(refAssy);
                }

                var sources = GetSources(canvasController, elAssy);
                Compile(assyFilename, sources, refs);
            }

            return assyFilename;
        }

        protected List<GraphicElement> GetReferencedAssemblies(GraphicElement elAssy)
        {
            var refs = new List<GraphicElement>();

            // TODO: Qualify EndConnectedShape as being IAssemblyBox
            elAssy.Connections.Where(c => (c.ToElement is Connector con) && con.EndCap == AvailableLineCap.Arrow).ForEach(c =>
            {
                // Connector endpoint will reference ourselves, so exclude.
                if (((Connector)c.ToElement).EndConnectedShape == elAssy) return;
                var toAssy = ((Connector)c.ToElement).EndConnectedShape;
                refs.Add(toAssy);
            });

            // TODO: Qualify EndConnectedShape as being IAssemblyBox
            elAssy.Connections.Where(c => (c.ToElement is Connector con) && con.StartCap == AvailableLineCap.Arrow).ForEach(c =>
            {
                // Connector endpoint will reference ourselves, so exclude.
                if (((Connector)c.ToElement).StartConnectedShape == elAssy) return;
                var toAssy = ((Connector)c.ToElement).StartConnectedShape;
                refs.Add(toAssy);
            });

            return refs;
        }

        protected bool Compile(string assyFilename, List<string> sources, List<string> refs, bool generateExecutable = false)
        {
            var provider = new CSharpCodeProvider();
            var parameters = new CompilerParameters
            {
                IncludeDebugInformation = true,
                GenerateInMemory = false,
                GenerateExecutable = generateExecutable,
                OutputAssembly = assyFilename
            };

            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Core.dll");
            parameters.ReferencedAssemblies.Add("System.Data.dll");
            parameters.ReferencedAssemblies.Add("System.Data.Linq.dll");
            parameters.ReferencedAssemblies.Add("System.Drawing.dll");
            parameters.ReferencedAssemblies.Add("System.Net.dll");
            parameters.ReferencedAssemblies.Add("System.Net.Http.dll");
            // parameters.ReferencedAssemblies.Add("System.Speech.dll");
            parameters.ReferencedAssemblies.Add("System.Xml.dll");
            parameters.ReferencedAssemblies.Add("System.Xml.Linq.dll");
            // parameters.ReferencedAssemblies.Add("Clifton.Core.dll");
            // parameters.ReferencedAssemblies.Add("websocket-sharp.dll");
            parameters.ReferencedAssemblies.AddRange(refs.ToArray());

            if (generateExecutable)
            {
                parameters.MainClass = "App.Program";
            }

            // results = provider.CompileAssemblyFromSource(parameters, sources.ToArray());

            results = provider.CompileAssemblyFromFile(parameters, sources.ToArray());

            if (!results.Errors.HasErrors) return true;
            var sb = new StringBuilder();

            foreach (CompilerError error in results.Errors)
            {
                try
                {
                    sb.AppendLine($"Error ({tempToTextBoxMap[Path.GetFileNameWithoutExtension(error.FileName.RemoveWhitespace()) + ".cs"]} - {error.Line}): {error.ErrorText}");
                }
                catch
                {
                    sb.AppendLine(error.ErrorText);     // other errors, like "process in use", do not have an associated filename, so general catch-all here.
                }
            }

            // MessageBox.Show(sb.ToString(), assyFilename, MessageBoxButtons.OK, MessageBoxIcon.Error);
            ServiceManager.Get<IFlowSharpCodeOutputWindowService>().WriteLine(sb.ToString());

            return false;
        }

        protected List<IAssemblyReferenceBox> GetReferences(BaseController canvasController)
        {
            return canvasController.Elements.Where(el => el is IAssemblyReferenceBox).Cast<IAssemblyReferenceBox>().ToList();
        }

        protected List<GraphicElement> GetClasses(BaseController canvasController)
        {
            return canvasController.Elements.Where(el => el is ICSharpClass).ToList();
        }

        /// <summary>
        /// Returns only top level sources - those not contained within AssemblyBox shapes.
        /// </summary>
        protected List<GraphicElement> GetSources(BaseController canvasController)
        {
            var sourceList = new List<GraphicElement>();

            foreach (var srcEl in canvasController.Elements.Where(
                srcEl => !ContainedIn<IAssemblyBox>(canvasController, srcEl) &&
                !(srcEl is IFileBox)))
            {
                sourceList.Add(srcEl);
            }

            return sourceList;
        }

        protected bool ContainedIn<T>(BaseController canvasController, GraphicElement child)
        {
            return canvasController.Elements.Any(el => el is T && el.DisplayRectangle.Contains(child.DisplayRectangle));
        }

        /// <summary>
        /// Returns sources contained in an element (ie., AssemblyBox shape).
        /// </summary>
        protected List<string> GetSources(BaseController canvasController, GraphicElement elAssy)
        {
            var sourceList = new List<string>();

            foreach (GraphicElement srcEl in canvasController.Elements.Where(srcEl => elAssy.DisplayRectangle.Contains(srcEl.DisplayRectangle)))
            {
                var filename = Path.GetFileNameWithoutExtension(Path.GetTempFileName()) + ".cs";
                tempToTextBoxMap[filename] = srcEl.Text.RemoveWhitespace();
                File.WriteAllText(filename, GetCode(srcEl));
                sourceList.Add(filename);
            }

            return sourceList;
        }

        protected string GetCode(GraphicElement el)
        {
            el.Json.TryGetValue("Code", out var code);

            return code ?? "";
        }
    }
}
