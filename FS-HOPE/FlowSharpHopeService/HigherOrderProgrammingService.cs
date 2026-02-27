using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

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
            var assy = Assembly.LoadFrom(dll);
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

        protected RoslynCompileResults Compile(string assyFilename, List<string> sources, List<string> refs, bool generateExecutable = false)
        {
            var allRefs = new List<string>
            {
                "System.dll",
                "System.Core.dll",
                "System.Data.dll",
                "System.Data.Linq.dll",
                "System.Design.dll",
                "System.Drawing.dll",
                "System.Net.dll",
                "System.Windows.Forms.dll",
                "System.Xml.dll",
                "System.Xml.Linq.dll",
                "System.Speech.dll"
            };
            allRefs.AddRange(refs);

            var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp7_3);
            var syntaxTrees = sources
                .Select(sourceFile => CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile), parseOptions, sourceFile, Encoding.UTF8))
                .ToList();
            var metadataReferences = ResolveMetadataReferences(allRefs);

            var compilationOptions = new CSharpCompilationOptions(
                generateExecutable ? OutputKind.WindowsApplication : OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                mainTypeName: generateExecutable ? "App.Program" : null);

            var compilation = CSharpCompilation.Create(
                Path.GetFileNameWithoutExtension(assyFilename),
                syntaxTrees,
                metadataReferences,
                compilationOptions);

            var outputPath = Path.GetFullPath(assyFilename);
            var outputDirectory = Path.GetDirectoryName(outputPath);

            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var results = new RoslynCompileResults();

            using (var peStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var pdbStream = new FileStream(Path.ChangeExtension(outputPath, ".pdb"), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                EmitResult emitResult = compilation.Emit(peStream, pdbStream);

                if (!emitResult.Success)
                {
                    emitResult.Diagnostics
                        .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                        .ToList()
                        .ForEach(diagnostic =>
                        {
                            var lineSpan = diagnostic.Location.GetLineSpan();
                            results.Errors.Add(new RoslynCompileError
                            {
                                FileName = lineSpan.Path ?? string.Empty,
                                Line = lineSpan.StartLinePosition.Line + 1,
                                ErrorText = diagnostic.GetMessage()
                            });
                        });
                }
            }

            if (!results.Errors.HasErrors) return results;
            var sb = new StringBuilder();

            foreach (var error in results.Errors)
            {
                try
                {
                    sb.AppendLine(string.Format("Error ({0} - {1}): {2}", tempToTextBoxMap[Path.GetFileNameWithoutExtension(error.FileName.RemoveWhitespace()) + ".cs"].RemoveWhitespace(), error.Line, error.ErrorText));
                }
                catch
                {
                    sb.AppendLine(error.ErrorText);
                }
            }

            ServiceManager.Get<IFlowSharpCodeOutputWindowService>().WriteLine(sb.ToString());

            return results;
        }

        protected List<MetadataReference> ResolveMetadataReferences(List<string> refs)
        {
            var metadataReferences = new List<MetadataReference>();
            var trustedPlatformAssemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                .Split(Path.PathSeparator)
                .ToList();

            trustedPlatformAssemblies
                .ForEach(path => metadataReferences.Add(MetadataReference.CreateFromFile(path)));

            refs
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => reference.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
                .ForEach(reference =>
                {
                    string resolvedPath = null;

                    if (File.Exists(reference))
                    {
                        resolvedPath = Path.GetFullPath(reference);
                    }
                    else
                    {
                        var trustedPath = trustedPlatformAssemblies.FirstOrDefault(path =>
                            Path.GetFileName(path).Equals(reference, StringComparison.OrdinalIgnoreCase) ||
                            path.EndsWith(reference, StringComparison.OrdinalIgnoreCase));

                        resolvedPath = trustedPath;
                    }

                    if (string.IsNullOrEmpty(resolvedPath))
                    {
                        return;
                    }

                    if (metadataReferences.Any(existingReference =>
                            string.Equals(existingReference.Display, resolvedPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        return;
                    }

                    metadataReferences.Add(MetadataReference.CreateFromFile(resolvedPath));
                });

            return metadataReferences;
        }

        protected class RoslynCompileResults
        {
            public RoslynCompileErrors Errors { get; } = new RoslynCompileErrors();
        }

        protected class RoslynCompileErrors : List<RoslynCompileError>
        {
            public bool HasErrors => Count > 0;
        }

        protected class RoslynCompileError
        {
            public string FileName { get; set; }
            public int Line { get; set; }
            public string ErrorText { get; set; }
        }
    }
}
