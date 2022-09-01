using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using Microsoft.CSharp;

using Clifton.Core.Assertions;
using Clifton.Core.ExtensionMethods;
using Clifton.Core.ModuleManagement;
using Clifton.Core.Semantics;
using Clifton.Core.ServiceManagement;

using FlowSharpLib;
using FlowSharpCodeServiceInterfaces;
using FlowSharpCodeShapeInterfaces;
using FlowSharpHopeCommon;
using FlowSharpHopeServiceInterfaces;
using FlowSharpHopeShapeInterfaces;
using FlowSharpServiceInterfaces;

namespace FlowSharpHopeService
{
    public class HigherOrderProgrammingModule : IModule
    {
        public void InitializeServices(IServiceManager serviceManager)
        {
            serviceManager.RegisterSingleton<IHigherOrderProgrammingService, HigherOrderProgrammingService>();
        }
    }

    public class HigherOrderProgrammingService : ServiceBase, IHigherOrderProgrammingService
    {
        public bool RunnerLoaded => runner.Loaded;

        protected ToolStripMenuItem mnuBuild = new ToolStripMenuItem() { Name = "mnuBuild", Text = "Build" };
        protected ToolStripMenuItem mnuRun = new ToolStripMenuItem() { Name = "mnuRun", Text = "Run" };
        protected ToolStripMenuItem mnuStop = new ToolStripMenuItem() { Name = "mnuStop", Text = "Stop" };
        protected ToolStripMenuItem mnuShowAnimation = new ToolStripMenuItem() { Name = "mnuShowAnimation", Text = "Show Animation" };
        protected ToolStripMenuItem mnuShowActivation = new ToolStripMenuItem() { Name = "mnuShowActivation", Text = "Show Activation" };
        protected ToolStripMenuItem mnuShowRouting = new ToolStripMenuItem() { Name = "mnuShowRouting", Text = "Show Routing" };
        protected Dictionary<string, string> tempToTextBoxMap = new Dictionary<string, string>();
        protected IRunner runner;
        protected Animator animator;

        public override void FinishedInitialization()
        {
            // runner = new AppDomainRunner();
            runner = new StandAloneRunner(ServiceManager);
            // runner = new InAppRunner();
            animator = new Animator(ServiceManager);
            runner.Processing += animator.Animate;

            InitializeEditorsMenu();
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ReflectionOnlyAssemblyResolve;
            base.FinishedInitialization();
        }

        public void LoadHopeAssembly()
        {
            var menuService = ServiceManager.Get<IFlowSharpMenuService>();
            var filename = GetExeOrDllFilename(menuService.Filename);
            runner.Load(filename);
        }

        public void UnloadHopeAssembly()
        {
            Assert.SilentTry(() =>
            {
                runner.Unload();
                animator.RemoveCarriers();
            });
        }

        //public List<ReceptorDescription> DescribeReceptors()
        //{
        //}

        public void InstantiateReceptors()
        {
            var receptors = GetReceptors();
            receptors.Where(r=>r.Enabled).ForEach(r => runner.InstantiateReceptor(r.AgentName));
        }

        protected List<IAgentReceptor> GetReceptors()
        {
            var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
            var canvasController = canvasService.ActiveController;
            var receptors = GetReceptors(canvasController);
            return receptors;
        }

        public void EnableDisableReceptor(string typeName, bool state)
        {
            runner.EnableDisableReceptor(typeName, state);
        }

        public PropertyContainer DescribeSemanticType(string typeName)
        {
            var ret = runner.DescribeSemanticType(typeName);
            return ret;
        }

        public void Publish(string typeName, object st)
        {
            runner.Publish(typeName, st);
        }

        public void Publish(string typeName, string json)
        {
            runner.Publish(typeName, json);
        }

        protected void InitializeEditorsMenu()
        {
            var hopeToolStripMenuItem = new ToolStripMenuItem() { Name = "mnuHope", Text = "&Hope" };
            hopeToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[]
            {
                mnuBuild,
                mnuRun,
                mnuStop,
                new ToolStripSeparator(),
                mnuShowAnimation,
                mnuShowActivation,
                new ToolStripSeparator(),
                mnuShowRouting
            });
            ServiceManager.Get<IFlowSharpMenuService>().AddMenu(hopeToolStripMenuItem);
            mnuBuild.Click += OnHopeBuild;
            mnuRun.Click += OnHopeRun;
            mnuStop.Click += OnHopeStop;
            mnuShowRouting.Click += OnShowRouting;
            mnuShowAnimation.Click += (_, __) =>
            {
                mnuShowAnimation.Checked ^= true;
                animator.ShowAnimation = mnuShowAnimation.Checked;
            };

            mnuShowActivation.Click += (_, __) =>
            {
                mnuShowActivation.Checked ^= true;
                animator.ShowActivation = mnuShowActivation.Checked;
            };

            mnuShowAnimation.Checked = true;
            mnuShowActivation.Checked = true;
            animator.ShowAnimation = true;
            animator.ShowActivation = true;
        }

        protected void OnShowRouting(object sender, EventArgs e)
        {
            mnuShowRouting.Checked ^= true;
            mnuShowRouting.Checked.IfElse(ShowRouting, RemoveRouting);
        }

        protected void ShowRouting()
        {
            LoadIfNotLoaded();
            var receptors = GetReceptors();
            var descr = new List<ReceptorDescription>();
            receptors.Where(r => r.Enabled).ForEach(r =>
            {
                descr.AddRange(runner.DescribeReceptor(r.AgentName));
            });
            CreateConnectors(descr);
        }

        protected void RemoveRouting()
        {
            var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
            var canvasController = canvasService.ActiveController;
            var receptorConnections = canvasController.Elements.Where(el => el.Name == "_RCPTRCONN_").ToList();
            receptorConnections.ForEach(rc => canvasController.DeleteElement(rc));
        }

        protected void CreateConnectors(List<ReceptorDescription> descr)
        {
            var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
            var canvasController = canvasService.ActiveController;
            var canvas = canvasController.Canvas;

            descr.ForEach(d =>
            {
                // TODO: Deal with namespace handling better than this RightOof kludge.
                // TODO: Converting to lowercase is a bit of a kludge as well.
                GraphicElement elSrc = canvasController.Elements.SingleOrDefault(el => (el is IAgentReceptor) && el.Text.RemoveWhitespace().ToLower() == d.ReceptorTypeName.RightOf(".").ToLower());

                if (elSrc != null)
                {
                    d.Publishes.ForEach(p =>
                    {
                    // Get all receivers that receive the type being published.
                    var receivers = descr.Where(r => r.ReceivingSemanticType == p);

                        receivers.ForEach(r =>
                        {
                            // TODO: Deal with namespace handling better than this RightOof kludge.
                            // TODO: Converting to lowercase is a bit of a kludge as well.
                            GraphicElement elDest = canvasController.Elements.SingleOrDefault(el => (el is IAgentReceptor) && el.Text.RemoveWhitespace().ToLower() == r.ReceptorTypeName.RightOf(".").ToLower());

                            if (elDest == null) return;
                            var dc = new DiagonalConnector(canvas, elSrc.DisplayRectangle.Center(), elDest.DisplayRectangle.Center())
                            {
                                Name = "_RCPTRCONN_",
                                EndCap = AvailableLineCap.Arrow,
                                BorderPenColor = Color.Red
                            };
                            dc.UpdateProperties();
                            canvasController.Insert(dc);
                        });
                    });
                }
            });
        }

        protected void LoadIfNotLoaded()
        {
            if (!runner.Loaded)
            {
                LoadHopeAssembly();
            }
        }

        protected void OnHopeBuild(object sender, EventArgs e)
        {
            runner.Unload();
            Compile();
        }

        protected void OnHopeRun(object sender, EventArgs e)
        {
            LoadHopeAssembly();
            InstantiateReceptors();
        }

        protected void OnHopeStop(object sender, EventArgs e)
        {
            UnloadHopeAssembly();
        }

        private Assembly ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var dll = args.Name.LeftOf(',') + ".dll";
            var assy = Assembly.ReflectionOnlyLoadFrom(dll);
            return assy;
        }

        protected (List<Type> agents, List<string> errors) GetAgents(Assembly assy)
        {
            var agents = new List<Type>();
            var errors = new List<string>();

            try
            {
                agents = assy.GetTypes().Where(t => t.IsClass && t.IsPublic && t.GetInterfaces().Any(i=>i.Name==nameof(IReceptor))).ToList();
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var loadEx in ex.LoaderExceptions)
                {
                    errors.AddIfUnique(loadEx.Message);
                }
            }
            catch (Exception ex)
            {
                errors.AddIfUnique(ex.Message);
            }

            return (agents, errors);
        }

        protected List<IAgentReceptor> GetReceptors(BaseController canvasController)
        {
            var receptors = new List<IAgentReceptor>();
            receptors.AddRange(canvasController.Elements.Where(srcEl => srcEl is IAgentReceptor).Cast<IAgentReceptor>().Where(agent=>agent.Enabled));

            return receptors;
        }

        protected void Compile()
        {
            tempToTextBoxMap.Clear();
            var outputWindow = ServiceManager.Get<IFlowSharpCodeOutputWindowService>();
            outputWindow.Clear();

            var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
            var menuService = ServiceManager.Get<IFlowSharpMenuService>();
            var codeService = ServiceManager.Get<IFlowSharpCodeService>();
            var canvasController = canvasService.ActiveController;

            var refs = GetCanvasReferences(canvasController);
            var shapeSources = GetCanvasSources(canvasController);
            var sourceFilenames = GetSourceFiles(shapeSources);

            var isStandAlone = runner is StandAloneRunner;
            var filename = GetExeOrDllFilename(menuService.Filename);
            var results = Compile(filename, sourceFilenames, refs, isStandAlone);
            DeleteTempFiles(sourceFilenames);

            if (!results.Errors.HasErrors)
            {
                outputWindow.WriteLine("No Errors");
            }
        }

        protected string GetExeOrDllFilename(string fn)
        {
            // TODO: We should really check if the any of the C# shape code-behind contains App.Main
            var isStandAlone = runner is StandAloneRunner;
            var ext = isStandAlone ? ".exe" : ".dll";
            var filename = string.IsNullOrEmpty(fn) ? "temp" + ext : Path.GetFileNameWithoutExtension(fn) + ext;
            return filename;
        }

        protected void DeleteTempFiles(List<string> files)
        {
            // files.ForEach(fn => File.Delete(fn));
        }

        protected List<string> GetCanvasReferences(BaseController canvasController)
        {
            var refs = new List<string>();
            var references = GetReferences(canvasController);
            refs.AddRange(references.Select(r => r.Filename));
            return refs;
        }

        /// <summary>
        /// Returns only top level sources - those not contained within AssemblyBox shapes.
        /// </summary>
        protected List<GraphicElement> GetCanvasSources(BaseController canvasController)
        {
            var sourceList = new List<GraphicElement>();

            foreach (var srcEl in canvasController.Elements.Where(
                srcEl => !ContainedIn<IAssemblyBox>(canvasController, srcEl) /* && !(srcEl is IFileBox) */ ))
            {
                sourceList.Add(srcEl);
            }

            return sourceList;
        }

        protected bool ContainedIn<T>(BaseController canvasController, GraphicElement child)
        {
            return canvasController.Elements.Any(el => el is T && el.DisplayRectangle.Contains(child.DisplayRectangle));
        }

        protected List<string> GetSourceFiles(List<GraphicElement> shapeSources)
        {
            var files = new List<string>();
            shapeSources.ForEach(shape =>
                {
                    // Get all other shapes that are not part of CSharpClass shapes:
                    // TODO: Better Linq!
                    string code = GetCode(shape);
                    if (!String.IsNullOrEmpty(code))
                    {
                        string filename = CreateCodeFile(code, shape.Text);
                        files.Add(filename);
                    }
                });

            return files;
        }

        protected string GetCode(GraphicElement el)
        {
            el.Json.TryGetValue("Code", out var code);
            return code ?? "";
        }

        protected List<IAssemblyReferenceBox> GetReferences(BaseController canvasController)
        {
            return canvasController.Elements.Where(el => el is IAssemblyReferenceBox).Cast<IAssemblyReferenceBox>().ToList();
        }

        protected string CreateCodeFile(string code, string shapeText)
        {
            // var filename = Path.GetFileNameWithoutExtension(Path.GetTempFileName()) + ".cs";
            var filename = Path.GetFileNameWithoutExtension(shapeText.RemoveWhitespace()) + ".cs";
            File.WriteAllText(filename, code);
            tempToTextBoxMap[filename] = shapeText;

            return filename;
        }

        protected CompilerResults Compile(string assyFilename, List<string> sources, List<string> refs, bool generateExecutable = false)
        {
            // https://stackoverflow.com/questions/31639602/using-c-sharp-6-features-with-codedomprovider-rosyln
            // The built-in CodeDOM provider doesn't support C# 6. Use this one instead:
            // https://www.nuget.org/packages/Microsoft.CodeDom.Providers.DotNetCompilerPlatform/
            // var options = new Dictionary<string, string>() { { "CompilerVersion", "v7.0" } };
            var provider = new CSharpCodeProvider();
            var parameters = new CompilerParameters
            {
                IncludeDebugInformation = true,
                GenerateInMemory = false,
                GenerateExecutable = generateExecutable,
                CompilerOptions = "/t:winexe",
            };

            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("System.Core.dll");
            parameters.ReferencedAssemblies.Add("System.Data.dll");
            parameters.ReferencedAssemblies.Add("System.Data.Linq.dll");
            parameters.ReferencedAssemblies.Add("System.Design.dll");
            parameters.ReferencedAssemblies.Add("System.Drawing.dll");
            parameters.ReferencedAssemblies.Add("System.Net.dll");
            parameters.ReferencedAssemblies.Add("System.Windows.Forms.dll");
            parameters.ReferencedAssemblies.Add("System.Xml.dll");
            parameters.ReferencedAssemblies.Add("System.Xml.Linq.dll");

            parameters.ReferencedAssemblies.Add("System.Speech.dll");

            //parameters.ReferencedAssemblies.Add("HopeRunner.dll");
            //parameters.ReferencedAssemblies.Add("HopeRunnerAppDomainInterface.dll");

            // parameters.ReferencedAssemblies.Add("System.Xml.dll");
            // parameters.ReferencedAssemblies.Add("System.Xml.Linq.dll");
            // parameters.ReferencedAssemblies.Add("Clifton.Core.dll");
            // parameters.ReferencedAssemblies.Add("websocket-sharp.dll");
            parameters.ReferencedAssemblies.AddRange(refs.ToArray());
            parameters.OutputAssembly = assyFilename;

            if (generateExecutable)
            {
                parameters.MainClass = "App.Program";
            }

            // results = provider.CompileAssemblyFromSource(parameters, sources.ToArray());

            var results = provider.CompileAssemblyFromFile(parameters, sources.ToArray());

            if (!results.Errors.HasErrors) return results;
            var sb = new StringBuilder();

            foreach (CompilerError error in results.Errors)
            {
                try
                {
                    sb.AppendLine(string.Format("Error ({0} - {1}): {2}", tempToTextBoxMap[Path.GetFileNameWithoutExtension(error.FileName.RemoveWhitespace()) + ".cs"].RemoveWhitespace(), error.Line, error.ErrorText));
                }
                catch
                {
                    sb.AppendLine(error.ErrorText);     // other errors, like "process in use", do not have an associated filename, so general catch-all here.
                }
            }

            // MessageBox.Show(sb.ToString(), assyFilename, MessageBoxButtons.OK, MessageBoxIcon.Error);
            ServiceManager.Get<IFlowSharpCodeOutputWindowService>().WriteLine(sb.ToString());

            return results;
        }
    }
}
