using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;

using Newtonsoft.Json;

using Clifton.Core.Semantics;
using Clifton.Core.ServiceManagement;
using Clifton.Core.Services.SemanticProcessorService;

using FlowSharpHopeService;
using FlowSharpCodeCompilerService;
using FlowSharpToolboxService;

namespace CodeTester
{
    public class ST_SerializeToXml : ISemanticType
    {
        public object Object { get; set; }
        public Func<string, ISemanticType> Continuation { get; set; }
    }

    public class ST_XmlHttpGet : ISemanticType
    {
        public string Url { get; set; }
        public Func<string, ISemanticType> Continuation { get; set; }
    }

    public class ST_DeserializeFromXml : ISemanticType
    {
        public string Xml { get; set; }
        public Type Instance { get; set; }
    }

    public class ST_USPSAddressResponse : ISemanticType
    {
        public int ID { get; set; }
        public string FirmName { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Urbanization { get; set; }
        public string Zip5 { get; set; }
        public string Zip4 { get; set; }
        public string DeliveryPoint { get; set; }
        public string CarrierRoute { get; set; }
        public string DPVConfirmation { get; set; }
        public string DPVCMRA { get; set; }
        public string DPVFootnotes { get; set; }
        public string Business { get; set; }
        public string CentralDeliveryPoint { get; set; }
        public string Vacant { get; set; }
    }

    public class AddressValidateResponse
    {
        public ST_USPSAddressResponse Address { get; set; }
    }

    public class Address
    {
        [XmlAttribute] public int ID { get; set; }
        public string FirmName { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Urbanization { get; set; }
        public string Zip5 { get; set; }
        public string Zip4 { get; set; }

        public Address()
        {
            ID = 0;         // Only 1 address
            // These elements must be serialized even if not populated.
            Address1 = "";
            Zip4 = "";
        }
    }

    public class AddressValidateRequest
    {
        [XmlAttribute] public string USERID { get; set; }
        public int Revision { get; set; }
        public Address Address { get; set; }

        public AddressValidateRequest()
        {
            USERID = "457INTER2602";
            Revision = 1;
            Address = new Address();
        }
    }

    public class Foo
    {
        [Category("A")]
        [Description("A Text")]
        public string Text { get; set; }

        [Category("A")]
        public DateTime Date { get; set; }
        [Category("A")]
        public Bar Bar { get; set; }
    }

    public class Bar
    {
        [Category("B")]
        public int I { get; set; }

        public int J { get; set; }
    }

    public class ST_Address : ISemanticType
    {
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public ST_City City { get; set; }
        public ST_State State { get; set; }
        public ST_Zip Zip { get; set; }

        public ST_Address()
        {
            City = new ST_City();
            State = new ST_State();
            Zip = new ST_Zip();
        }
    }

    public class ST_Zip : ISemanticType
    {
        public ST_Zip5 Zip5 { get; set; }
        public ST_Zip4 Zip4 { get; set; }

        public ST_Zip()
        {
            Zip5 = new ST_Zip5();
            Zip4 = new ST_Zip4();
        }
    }

    public class ST_Zip4 : ISemanticType
    {
        public string Zip4 { get; set; }
    }

    public class ST_Zip5 : ISemanticType
    {
        public string Zip5 { get; set; }
    }

    public class ST_City : ISemanticType
    {
        public string City { get; set; }
    }

    public class ST_State : ISemanticType
    {
        public string State { get; set; }
    }

    public class PropertyContainer
    {
        public List<PropertyData> Types { get; set; }

        public PropertyContainer()
        {
            Types = new List<PropertyData>();
        }
    }

    public class PropertyData
    {
        public string Category { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public PropertyContainer ChildType { get; set; }

        public PropertyData()
        {
        }
    }

    class Program
    {
        private const string HopeAlcSmokeArg = "--hope-alc-smoke";
        private const string HopeCrossContextSmokeArg = "--hope-cross-context-smoke";
        private const string DynamicCompileSmokeArg = "--dynamic-compile-smoke";
        private const string PluginSmokeArg = "--plugin-smoke";
        private const string WebSocketSmokeArg = "--websocket-smoke";
        private const string HopeRunnerAssemblyName = "HopeRunner.dll";
        private const string WebSocketSmokeCommand = "cmd=CmdGetShapeFiles";
        private const int WebSocketPort = 1100;

        public static string Get(string uri)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("FlowSharp/1.0");

                return client.GetStringAsync(uri).GetAwaiter().GetResult();
            }
        }

        static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], WebSocketSmokeArg, StringComparison.OrdinalIgnoreCase))
            {
                return RunWebSocketSmoke();
            }

            if (args.Length > 0 && string.Equals(args[0], DynamicCompileSmokeArg, StringComparison.OrdinalIgnoreCase))
            {
                int cycles = 3;

                if (args.Length > 1 && !int.TryParse(args[1], out cycles))
                {
                    Console.Error.WriteLine("Invalid cycle count. Expected an integer.");
                    return 2;
                }

                return RunDynamicCompileSmoke(cycles);
            }

            if (args.Length > 0 && string.Equals(args[0], PluginSmokeArg, StringComparison.OrdinalIgnoreCase))
            {
                int cycles = 3;

                if (args.Length > 1 && !int.TryParse(args[1], out cycles))
                {
                    Console.Error.WriteLine("Invalid cycle count. Expected an integer.");
                    return 2;
                }

                return RunPluginSmoke(cycles);
            }

            if (args.Length > 0 && string.Equals(args[0], HopeAlcSmokeArg, StringComparison.OrdinalIgnoreCase))
            {
                int cycles = 10;

                if (args.Length > 1 && !int.TryParse(args[1], out cycles))
                {
                    Console.Error.WriteLine("Invalid cycle count. Expected an integer.");
                    return 2;
                }

                return RunHopeAlcSmoke(cycles);
            }

            if (args.Length > 0 && string.Equals(args[0], HopeCrossContextSmokeArg, StringComparison.OrdinalIgnoreCase))
            {
                int cycles = 3;

                if (args.Length > 1 && !int.TryParse(args[1], out cycles))
                {
                    Console.Error.WriteLine("Invalid cycle count. Expected an integer.");
                    return 2;
                }

                return RunHopeCrossContextSmoke(cycles);
            }

            var avr = new AddressValidateRequest
            {
                Address =
                {
                    Address2 = "565 Roxbury Rd",
                    City = "Hudson",
                    State = "NY",
                    Zip5 = "12534"
                }
            };

            // All this necessary to omit the XML declaration and remove namespaces.  Sigh.
            var xws = new XmlWriterSettings
            {
                OmitXmlDeclaration = true
            };
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            var xs = new XmlSerializer(avr.GetType());
            var sb = new StringBuilder();
            var tw = new StringWriter(sb);
            var xtw = XmlWriter.Create(tw, xws);
            xs.Serialize(xtw, avr, ns);
            var xml = sb.ToString();
            var ret = Get("https://secure.shippingapis.com/ShippingAPI.dll?API=Verify&XML=" + xml);
            // var ret = "<?xml version =\"1.0\" encoding=\"UTF-8\"?><AddressValidateResponse><Address ID=\"0\"><Address2>565 ROXBURY RD</Address2><City>HUDSON</City><State>NY</State><Zip5>12534</Zip5><Zip4>3626</Zip4><DeliveryPoint>65</DeliveryPoint><CarrierRoute>R001</CarrierRoute><DPVConfirmation>Y</DPVConfirmation><DPVCMRA>N</DPVCMRA><DPVFootnotes>AABB</DPVFootnotes><Business>N</Business><CentralDeliveryPoint>N</CentralDeliveryPoint><Vacant>N</Vacant></Address></AddressValidateResponse>";

            var xs2 = new XmlSerializer(typeof(AddressValidateResponse));
            var sr = new StringReader(ret);
            var resp = (AddressValidateResponse)xs2.Deserialize(sr);

            var t = typeof(ST_Address);
            var pis = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var pc = new PropertyContainer();
            BuildTypes(pc, pis);
            var json = JsonConvert.SerializeObject(pc);

            return 0;
        }

        private static int RunDynamicCompileSmoke(int cycles)
        {
            if (cycles < 1)
            {
                Console.Error.WriteLine("Cycle count must be >= 1.");
                return 100;
            }

            var compilerService = new FlowSharpCodeCompilerService.FlowSharpCodeCompilerService();
            var compileMethod = typeof(FlowSharpCodeCompilerService.FlowSharpCodeCompilerService).GetMethod(
                "CompileWithRoslyn",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (compileMethod == null)
            {
                Console.Error.WriteLine("Could not locate CompileWithRoslyn method.");
                return 101;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "flowsharp-dynamic-compile-smoke");
            Directory.CreateDirectory(tempDir);

            Console.WriteLine("Running dynamic compile smoke cycles: " + cycles);

            for (int i = 1; i <= cycles; i++)
            {
                string sourcePath = Path.Combine(tempDir, "dynamic-smoke-" + i + ".cs");
                string outputPath = Path.Combine(tempDir, "dynamic_smoke_" + i + ".dll");
                File.WriteAllText(sourcePath, GetDynamicCompileSmokeSource(i));

                object compileResult = compileMethod.Invoke(
                    compilerService,
                    new object[] { outputPath, new List<string> { sourcePath }, new List<string>(), false });

                if (compileResult == null)
                {
                    Console.Error.WriteLine("Cycle " + i + ": compiler returned null result.");
                    return 102 + i;
                }

                var errors = GetRoslynErrors(compileResult);

                if (errors.Count > 0)
                {
                    Console.Error.WriteLine("Cycle " + i + ": compile reported errors.");
                    errors.ForEach(error => Console.Error.WriteLine("  " + error));
                    return 120 + i;
                }

                if (!File.Exists(outputPath))
                {
                    Console.Error.WriteLine("Cycle " + i + ": output assembly was not created.");
                    return 140 + i;
                }

                Console.WriteLine("Cycle " + i + "/" + cycles + " OK");
            }

            Console.WriteLine("Dynamic compile smoke test passed.");

            return 0;
        }

        private static int RunPluginSmoke(int cycles)
        {
            if (cycles < 1)
            {
                Console.Error.WriteLine("Cycle count must be >= 1.");
                return 70;
            }

            string solutionRoot = FindSolutionRoot();

            if (string.IsNullOrEmpty(solutionRoot))
            {
                Console.Error.WriteLine("Could not locate FlowSharp solution root.");
                return 71;
            }

            string outputPath = Path.Combine(solutionRoot, "bin", "Debug", "net8.0-windows");
            string pluginListPath = Path.Combine(outputPath, "plugins.txt");

            if (!File.Exists(pluginListPath))
            {
                Console.Error.WriteLine("Missing plugin list: " + pluginListPath);
                return 72;
            }

            string[] pluginFiles = File.ReadAllLines(pluginListPath)
                .Where(p => !string.IsNullOrWhiteSpace(p) && !p.TrimStart().StartsWith("#"))
                .ToArray();

            if (pluginFiles.Length == 0)
            {
                Console.Error.WriteLine("No plugin entries found in " + pluginListPath);
                return 73;
            }

            var resolvedPluginPaths = pluginFiles
                .Select(pluginFile => ResolvePluginAssemblyPath(solutionRoot, outputPath, pluginFile.Trim()))
                .ToList();

            if (resolvedPluginPaths.Any(string.IsNullOrWhiteSpace))
            {
                Console.Error.WriteLine("Could not resolve one or more plugin assemblies from plugins.txt.");
                return 74;
            }

            string originalDirectory = Directory.GetCurrentDirectory();
            string originalPluginList = File.ReadAllText(pluginListPath);

            try
            {
                File.WriteAllLines(pluginListPath, resolvedPluginPaths);
                Directory.SetCurrentDirectory(outputPath);
                Console.WriteLine("Running plugin smoke cycles: " + cycles);

                for (int i = 1; i <= cycles; i++)
                {
                    var pluginManager = new PluginManager();
                    pluginManager.InitializePlugins();
                    var shapeTypes = pluginManager.GetShapeTypes();

                    if (shapeTypes == null || shapeTypes.Count == 0)
                    {
                        Console.Error.WriteLine("Cycle " + i + ": no plugin shapes were loaded.");
                        return 80 + i;
                    }

                    bool foundKnownPlugin = shapeTypes.Any(t =>
                        t.Namespace == "PluginExample" || t.Namespace == "FlowSharpWindowsControlShapes");

                    if (!foundKnownPlugin)
                    {
                        Console.Error.WriteLine("Cycle " + i + ": expected plugin shape namespace not found.");
                        return 90 + i;
                    }

                    Console.WriteLine("Cycle " + i + "/" + cycles + " OK (" + shapeTypes.Count + " shapes)");
                }
            }
            finally
            {
                File.WriteAllText(pluginListPath, originalPluginList);
                Directory.SetCurrentDirectory(originalDirectory);
            }

            Console.WriteLine("Plugin smoke test passed.");

            return 0;
        }

        private static int RunWebSocketSmoke()
        {
            var uriCandidates = GetWebSocketSmokeUris();

            var serviceManager = new ServiceManager();
            serviceManager.RegisterSingleton<ISemanticProcessor, SemanticProcessor>();

            var wsService = new FlowSharpWebSocketService.FlowSharpWebSocketService();
            wsService.Initialize(serviceManager);
            bool serviceStartedBySmoke = false;

            try
            {
                try
                {
                    wsService.StartServer();
                    serviceStartedBySmoke = true;
                }
                catch (HttpListenerException ex)
                {
                    Console.WriteLine("Could not start local websocket service, attempting existing listener: " + ex.Message);
                }

                using (var socket = ConnectToFirstAvailableUri(uriCandidates))
                {
                    SendWebSocketText(socket, WebSocketSmokeCommand);
                    string response = ReceiveWebSocketText(socket);

                    if (response == null)
                    {
                        Console.Error.WriteLine("Websocket smoke failed: no response payload.");
                        return 61;
                    }

                    if (!(response.StartsWith("[") && response.EndsWith("]")))
                    {
                        Console.Error.WriteLine("Websocket smoke failed: unexpected response format: " + response);
                        return 62;
                    }

                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Websocket smoke failed with exception: " + ex.Message);
                return 63;
            }
            finally
            {
                if (serviceStartedBySmoke)
                {
                    wsService.StopServer();
                }
            }

            Console.WriteLine("Websocket smoke test passed.");

            return 0;
        }

        private static int RunHopeAlcSmoke(int cycles)
        {
            if (cycles < 1)
            {
                Console.Error.WriteLine("Cycle count must be >= 1.");
                return 2;
            }

            string hopeRunnerPath = Path.Combine(AppContext.BaseDirectory, HopeRunnerAssemblyName);

            if (!File.Exists(hopeRunnerPath))
            {
                Console.Error.WriteLine("Missing HopeRunner assembly: " + hopeRunnerPath);
                return 3;
            }

            Console.WriteLine("Running HOPE AssemblyLoadContext smoke cycles: " + cycles);

            for (int i = 1; i <= cycles; i++)
            {
                var runner = new AppDomainRunner();
                runner.Load(hopeRunnerPath);

                if (!runner.Loaded)
                {
                    Console.Error.WriteLine("Cycle " + i + ": runner did not load.");
                    return 10 + i;
                }

                var descriptions = runner.DescribeReceptor("Runner");

                if (descriptions == null)
                {
                    Console.Error.WriteLine("Cycle " + i + ": DescribeReceptor returned null.");
                    return 30 + i;
                }

                runner.Unload();

                if (runner.Loaded)
                {
                    Console.Error.WriteLine("Cycle " + i + ": runner stayed loaded after unload.");
                    return 50 + i;
                }

                Console.WriteLine("Cycle " + i + "/" + cycles + " OK");
            }

            Console.WriteLine("HOPE AssemblyLoadContext smoke test passed.");

            return 0;
        }

        private static int RunHopeCrossContextSmoke(int cycles)
        {
            if (cycles < 1)
            {
                Console.Error.WriteLine("Cycle count must be >= 1.");
                return 160;
            }

            string solutionRoot = FindSolutionRoot();

            if (string.IsNullOrWhiteSpace(solutionRoot))
            {
                Console.Error.WriteLine("Could not locate FlowSharp solution root.");
                return 161;
            }

            string hopeRunnerInterfaceDll = ResolveBuiltAssemblyPath(solutionRoot, "HopeRunnerAppDomainInterface.dll");
            string cliftonCoreDll = ResolveBuiltAssemblyPath(solutionRoot, "Clifton.Core.dll");

            if (string.IsNullOrWhiteSpace(hopeRunnerInterfaceDll) || string.IsNullOrWhiteSpace(cliftonCoreDll))
            {
                Console.Error.WriteLine("Missing required dependency assemblies for cross-context smoke.");
                return 162;
            }

            var compilerService = new FlowSharpCodeCompilerService.FlowSharpCodeCompilerService();
            var compileMethod = typeof(FlowSharpCodeCompilerService.FlowSharpCodeCompilerService).GetMethod(
                "CompileWithRoslyn",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (compileMethod == null)
            {
                Console.Error.WriteLine("Could not locate CompileWithRoslyn method.");
                return 163;
            }

            string tempRoot = Path.Combine(Path.GetTempPath(), "flowsharp-hope-cross-context-smoke");
            Directory.CreateDirectory(tempRoot);

            Console.WriteLine("Running HOPE cross-context smoke cycles: " + cycles);

            for (int i = 1; i <= cycles; i++)
            {
                string cycleDir = Path.Combine(tempRoot, "cycle_" + i);
                Directory.CreateDirectory(cycleDir);
                string dependencySourcePath = Path.Combine(cycleDir, "HopeRunnerDependency.cs");
                string runnerSourcePath = Path.Combine(cycleDir, "HopeRunner.cs");
                string dependencyDllPath = Path.Combine(cycleDir, "HopeRunnerDependency.dll");
                string runnerDllPath = Path.Combine(cycleDir, "HopeRunner.dll");

                File.WriteAllText(dependencySourcePath, GetHopeDependencySmokeSource());
                File.WriteAllText(runnerSourcePath, GetHopeRunnerCrossContextSmokeSource());

                if (!CompileWithFlowSharpRoslyn(compilerService, compileMethod, dependencyDllPath, dependencySourcePath, new List<string>(), out List<string> dependencyErrors))
                {
                    Console.Error.WriteLine("Cycle " + i + ": failed to compile dependency assembly.");
                    dependencyErrors.ForEach(error => Console.Error.WriteLine("  " + error));
                    return 170 + i;
                }

                var runnerRefs = new List<string> { dependencyDllPath, hopeRunnerInterfaceDll, cliftonCoreDll };

                if (!CompileWithFlowSharpRoslyn(compilerService, compileMethod, runnerDllPath, runnerSourcePath, runnerRefs, out List<string> runnerErrors))
                {
                    Console.Error.WriteLine("Cycle " + i + ": failed to compile runner assembly.");
                    runnerErrors.ForEach(error => Console.Error.WriteLine("  " + error));
                    return 180 + i;
                }

                var runner = new AppDomainRunner();
                bool processingEventReceived = false;
                string processingSemanticType = null;
                runner.Processing += (_, args) =>
                {
                    processingEventReceived = true;
                    processingSemanticType = args.SemanticTypeTypeName;
                };

                runner.Load(runnerDllPath);

                if (!runner.Loaded)
                {
                    Console.Error.WriteLine("Cycle " + i + ": runner failed to load.");
                    return 190 + i;
                }

                object semanticTypeInstance = runner.InstantiateSemanticType("TestSemantic");

                if (!(semanticTypeInstance is ISemanticType))
                {
                    Console.Error.WriteLine("Cycle " + i + ": instantiated semantic type is not compatible with ISemanticType.");
                    return 200 + i;
                }

                runner.InstantiateReceptor("TestReceptor");
                var receptorDescriptions = runner.DescribeReceptor("TestReceptor");
                var testReceptorDescription = receptorDescriptions.FirstOrDefault();

                if (testReceptorDescription == null ||
                    testReceptorDescription.ReceivingSemanticType != "TestSemantic" ||
                    !testReceptorDescription.Publishes.Contains("PublishedSemantic"))
                {
                    Console.Error.WriteLine("Cycle " + i + ": receptor description did not match expected semantic contract.");
                    return 210 + i;
                }

                var semanticTypeDescription = runner.DescribeSemanticType("TestSemantic");
                var payloadProperty = semanticTypeDescription?.Types?.FirstOrDefault(property => property.Name == "Payload");
                bool hasPayloadNoteProperty = payloadProperty?.ChildType?.Types?.Any(property => property.Name == "Note") == true;

                if (semanticTypeDescription == null || !hasPayloadNoteProperty)
                {
                    Console.Error.WriteLine("Cycle " + i + ": semantic type description missing dependency-backed property metadata.");
                    return 220 + i;
                }

                runner.Publish("TestSemantic", "{\"Name\":\"CrossContext\",\"Payload\":{\"Note\":\"dependency\"}}");

                if (!processingEventReceived || string.IsNullOrWhiteSpace(processingSemanticType) || !processingSemanticType.EndsWith("TestSemantic", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine("Cycle " + i + ": publish path did not raise expected processing event.");
                    return 230 + i;
                }

                // Ensure no collectible-context instances are still rooted in this method before unload checks.
                semanticTypeInstance = null;
                testReceptorDescription = null;
                receptorDescriptions = null;
                payloadProperty = null;
                semanticTypeDescription = null;

                runner.Unload();
                if (runner.Loaded)
                {
                    Console.Error.WriteLine("Cycle " + i + ": runner stayed loaded after unload.");
                    return 240 + i;
                }
                runner = null;
                ForceFullGc();
                Thread.Sleep(250);

                if (!DeleteFileWithRetries(runnerDllPath) || !DeleteFileWithRetries(dependencyDllPath))
                {
                    Console.WriteLine("Cycle " + i + ": warning - generated assemblies remained locked after wait+retry cleanup.");
                }

                Console.WriteLine("Cycle " + i + "/" + cycles + " OK");
            }

            Console.WriteLine("HOPE cross-context smoke test passed.");

            return 0;
        }

        private static List<Uri> GetWebSocketSmokeUris()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var uris = host.AddressList
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => new Uri("ws://" + address + ":" + WebSocketPort + "/flowsharp"))
                .ToList();
            uris.Add(new Uri("ws://localhost:" + WebSocketPort + "/flowsharp"));
            uris.Add(new Uri("ws://127.0.0.1:" + WebSocketPort + "/flowsharp"));

            return uris
                .GroupBy(uri => uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static ClientWebSocket ConnectToFirstAvailableUri(List<Uri> uriCandidates)
        {
            Exception lastException = null;

            foreach (var uri in uriCandidates)
            {
                var socket = new ClientWebSocket();

                try
                {
                    socket.ConnectAsync(uri, CancellationToken.None).GetAwaiter().GetResult();
                    return socket;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    socket.Dispose();
                }
            }

            throw lastException ?? new InvalidOperationException("No websocket URI candidates available.");
        }

        private static void SendWebSocketText(ClientWebSocket socket, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            var segment = new ArraySegment<byte>(bytes);
            socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static string ReceiveWebSocketText(ClientWebSocket socket)
        {
            var buffer = new byte[4096];
            var segment = new ArraySegment<byte>(buffer);
            var sb = new StringBuilder();

            while (true)
            {
                var result = socket.ReceiveAsync(segment, CancellationToken.None).GetAwaiter().GetResult();

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    break;
                }
            }

            return sb.ToString();
        }

        private static string FindSolutionRoot()
        {
            var candidates = new List<string>
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory
            };

            foreach (string candidate in candidates.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                var dirInfo = new DirectoryInfo(candidate);

                while (dirInfo != null)
                {
                    string solutionFile = Path.Combine(dirInfo.FullName, "FlowSharp.sln");

                    if (File.Exists(solutionFile))
                    {
                        return dirInfo.FullName;
                    }

                    dirInfo = dirInfo.Parent;
                }
            }

            return null;
        }

        private static string ResolvePluginAssemblyPath(string solutionRoot, string outputPath, string pluginEntry)
        {
            string candidate = pluginEntry;

            if (!Path.IsPathRooted(candidate))
            {
                candidate = Path.Combine(outputPath, pluginEntry);
            }

            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }

            string pluginFileName = Path.GetFileName(pluginEntry);
            var matches = Directory.GetFiles(solutionRoot, pluginFileName, SearchOption.AllDirectories)
                .Where(path => path.IndexOf(Path.Combine("bin", "Debug", "net8.0-windows"), StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matches.Count > 0)
            {
                return Path.GetFullPath(matches[0]);
            }

            return null;
        }

        private static string ResolveBuiltAssemblyPath(string solutionRoot, string assemblyFileName)
        {
            var matches = Directory.GetFiles(solutionRoot, assemblyFileName, SearchOption.AllDirectories)
                .Where(path => path.IndexOf(Path.Combine("bin", "Debug", "net8.0-windows"), StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matches.Count == 0)
            {
                return null;
            }

            return Path.GetFullPath(matches[0]);
        }

        private static string GetDynamicCompileSmokeSource(int cycle)
        {
            return
                "using System;\r\n" +
                "\r\n" +
                "namespace App\r\n" +
                "{\r\n" +
                "    public static class DynamicCompileSmoke" + cycle + "\r\n" +
                "    {\r\n" +
                "        public static int Run()\r\n" +
                "        {\r\n" +
                "            return " + cycle + ";\r\n" +
                "        }\r\n" +
                "    }\r\n" +
                "}\r\n";
        }

        private static List<string> GetRoslynErrors(object compileResult)
        {
            var errorsText = new List<string>();
            var errorsProperty = compileResult.GetType().GetProperty("Errors", BindingFlags.Instance | BindingFlags.Public);
            var errorsObject = errorsProperty?.GetValue(compileResult);

            if (errorsObject == null)
            {
                errorsText.Add("Missing Errors payload from Roslyn compile result.");
                return errorsText;
            }

            var hasErrorsProperty = errorsObject.GetType().GetProperty("HasErrors", BindingFlags.Instance | BindingFlags.Public);
            var hasErrorsValue = hasErrorsProperty?.GetValue(errorsObject);
            bool hasErrors = hasErrorsValue is bool value && value;

            if (!hasErrors)
            {
                return errorsText;
            }

            if (errorsObject is System.Collections.IEnumerable enumerable)
            {
                foreach (var error in enumerable)
                {
                    string fileName = error?.GetType().GetProperty("FileName", BindingFlags.Instance | BindingFlags.Public)?.GetValue(error) as string;
                    object line = error?.GetType().GetProperty("Line", BindingFlags.Instance | BindingFlags.Public)?.GetValue(error);
                    string errorText = error?.GetType().GetProperty("ErrorText", BindingFlags.Instance | BindingFlags.Public)?.GetValue(error) as string;
                    errorsText.Add((fileName ?? "<unknown>") + ":" + (line?.ToString() ?? "?") + " " + (errorText ?? "<no message>"));
                }
            }

            return errorsText;
        }

        private static bool CompileWithFlowSharpRoslyn(
            FlowSharpCodeCompilerService.FlowSharpCodeCompilerService compilerService,
            MethodInfo compileMethod,
            string outputPath,
            string sourcePath,
            List<string> refs,
            out List<string> errors)
        {
            errors = new List<string>();

            object compileResult = compileMethod.Invoke(
                compilerService,
                new object[] { outputPath, new List<string> { sourcePath }, refs ?? new List<string>(), false });

            if (compileResult == null)
            {
                errors.Add("CompileWithRoslyn returned null.");
                return false;
            }

            errors = GetRoslynErrors(compileResult);

            if (errors.Count > 0)
            {
                return false;
            }

            return File.Exists(outputPath);
        }

        private static bool DeleteFileWithRetries(string path)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }

                    return !File.Exists(path);
                }
                catch
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(50);
                }
            }

            return !File.Exists(path);
        }

        private static void ForceFullGc()
        {
            for (int i = 0; i < 5; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(50);
            }
        }

        private static string GetHopeDependencySmokeSource()
        {
            return
                "namespace HopeRunnerDependency\r\n" +
                "{\r\n" +
                "    public class ExternalPayload\r\n" +
                "    {\r\n" +
                "        public string Note { get; set; }\r\n" +
                "    }\r\n" +
                "}\r\n";
        }

        private static string GetHopeRunnerCrossContextSmokeSource()
        {
            return
                "using System;\r\n" +
                "using System.Collections.Generic;\r\n" +
                "using System.Linq;\r\n" +
                "using System.Reflection;\r\n" +
                "using Clifton.Core.Semantics;\r\n" +
                "using HopeRunnerAppDomainInterface;\r\n" +
                "using HopeRunnerDependency;\r\n" +
                "\r\n" +
                "namespace HopeRunner\r\n" +
                "{\r\n" +
                "    [Serializable]\r\n" +
                "    public class TestSemantic : ISemanticType\r\n" +
                "    {\r\n" +
                "        public string Name { get; set; }\r\n" +
                "        public ExternalPayload Payload { get; set; } = new ExternalPayload();\r\n" +
                "    }\r\n" +
                "\r\n" +
                "    [Serializable]\r\n" +
                "    public class PublishedSemantic : ISemanticType\r\n" +
                "    {\r\n" +
                "        public int Count { get; set; }\r\n" +
                "    }\r\n" +
                "\r\n" +
                "    public class TestReceptor\r\n" +
                "    {\r\n" +
                "        [Publishes(typeof(PublishedSemantic))]\r\n" +
                "        public void Process(ISemanticProcessor proc, IMembrane membrane, TestSemantic semantic)\r\n" +
                "        {\r\n" +
                "        }\r\n" +
                "    }\r\n" +
                "\r\n" +
                "    [Serializable]\r\n" +
                "    public class Runner : MarshalByRefObject, IHopeRunner\r\n" +
                "    {\r\n" +
                "        private readonly List<object> receptors = new List<object>();\r\n" +
                "\r\n" +
                "        public event EventHandler<HopeRunnerAppDomainInterface.ProcessEventArgs> Processing;\r\n" +
                "\r\n" +
                "        public void InstantiateReceptor(string typeName)\r\n" +
                "        {\r\n" +
                "            var receptorType = ResolveType(typeName);\r\n" +
                "            if (receptorType != null)\r\n" +
                "            {\r\n" +
                "                receptors.Add(Activator.CreateInstance(receptorType));\r\n" +
                "            }\r\n" +
                "        }\r\n" +
                "\r\n" +
                "        public void EnableDisableReceptor(string typeName, bool state)\r\n" +
                "        {\r\n" +
                "        }\r\n" +
                "\r\n" +
                "        public ISemanticType InstantiateSemanticType(string typeName)\r\n" +
                "        {\r\n" +
                "            var semanticType = ResolveType(typeName);\r\n" +
                "            return semanticType == null ? null : (ISemanticType)Activator.CreateInstance(semanticType);\r\n" +
                "        }\r\n" +
                "\r\n" +
                "        public void Publish(ISemanticType semanticType)\r\n" +
                "        {\r\n" +
                "            Processing?.Invoke(this, new HopeRunnerAppDomainInterface.ProcessEventArgs(\"FromMembrane\", \"FromReceptor\", \"ToMembrane\", \"ToReceptor\", semanticType?.GetType()?.FullName));\r\n" +
                "        }\r\n" +
                "\r\n" +
                "        private static Type ResolveType(string typeName)\r\n" +
                "        {\r\n" +
                "            var assembly = Assembly.GetExecutingAssembly();\r\n" +
                "            return assembly.GetType(typeName) ?? assembly.GetTypes().FirstOrDefault(type => type.Name == typeName || type.FullName == typeName);\r\n" +
                "        }\r\n" +
                "    }\r\n" +
                "}\r\n";
        }

        static void BuildTypes(PropertyContainer pc, PropertyInfo[] pis)
        {
            foreach (var pi in pis)
            {
                var pd = new PropertyData
                {
                    Name = pi.Name, TypeName = pi.PropertyType.FullName,
                    Category = pi.GetCustomAttribute<CategoryAttribute>()?.Category,
                    Description = pi.GetCustomAttribute<DescriptionAttribute>()?.Description
                };
                pc.Types.Add(pd);

                if ((!pi.PropertyType.IsValueType) && (pd.TypeName != "System.String"))
                {
                    var pisChild = pi.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                    pd.ChildType = new PropertyContainer();
                    BuildTypes(pd.ChildType, pisChild);
                }
            }
        }
    }
}
