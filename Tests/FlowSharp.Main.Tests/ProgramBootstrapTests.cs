using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using System.Windows.Forms;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class ProgramBootstrapTests
    {
        [TestMethod]
        public void GetModuleList_WithXDocument_ReturnsAssemblyNamesInOrder()
        {
            XDocument xdoc = XDocument.Parse(
                "<Modules>" +
                "<Module AssemblyName='A.dll'/>" +
                "<Module AssemblyName='B.dll'/>" +
                "<Module AssemblyName='C.dll'/>" +
                "</Modules>");

            object result = GetModuleListFromXDocumentMethod().Invoke(null, new object[] { xdoc });
            var names = ExtractSemanticValues(result);

            CollectionAssert.AreEqual(new List<string> { "A.dll", "B.dll", "C.dll" }, names);
        }

        [TestMethod]
        public void GetModuleList_WithEmptyModulesNode_ReturnsEmptyList()
        {
            XDocument xdoc = XDocument.Parse("<Modules></Modules>");
            object result = GetModuleListFromXDocumentMethod().Invoke(null, new object[] { xdoc });
            var names = ExtractSemanticValues(result);

            Assert.AreEqual(0, names.Count);
        }

        [TestMethod]
        public void GetModuleList_WithXmlFileName_LoadsAndParsesFile()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "flowsharp-modules-" + Guid.NewGuid().ToString("N") + ".xml");

            try
            {
                File.WriteAllText(
                    tempFile,
                    "<?xml version='1.0' encoding='utf-8'?><Modules><Module AssemblyName='Alpha.dll'/><Module AssemblyName='Beta.dll'/></Modules>");

                object xmlFileName = CreateXmlFileName(tempFile);
                object result = GetModuleListFromXmlFileNameMethod().Invoke(null, new[] { xmlFileName });
                var names = ExtractSemanticValues(result);

                CollectionAssert.AreEqual(new List<string> { "Alpha.dll", "Beta.dll" }, names);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod]
        public void GetModuleList_WithRepositoryModulesXml_ContainsExpectedCoreModules()
        {
            string root = FindSolutionRoot();
            string modulesPath = Path.Combine(root, "modules.xml");
            object xmlFileName = CreateXmlFileName(modulesPath);
            object result = GetModuleListFromXmlFileNameMethod().Invoke(null, new[] { xmlFileName });
            var names = ExtractSemanticValues(result);

            CollectionAssert.Contains(names, "Clifton.SemanticProcessorService.dll");
            CollectionAssert.Contains(names, "FlowSharpService.dll");
            CollectionAssert.Contains(names, "FlowSharpCanvasService.dll");
            Assert.IsTrue(names.Count >= 10, "Expected at least 10 module entries.");
        }

        [TestMethod]
        public void GetModuleList_WithMissingXmlFileName_Throws()
        {
            string missingFile = Path.Combine(Path.GetTempPath(), "flowsharp-modules-missing-" + Guid.NewGuid().ToString("N") + ".xml");
            object xmlFileName = CreateXmlFileName(missingFile);

            TargetInvocationException ex = null;

            try
            {
                GetModuleListFromXmlFileNameMethod().Invoke(null, new[] { xmlFileName });
            }
            catch (TargetInvocationException caught)
            {
                ex = caught;
            }

            Assert.IsNotNull(ex, "Expected TargetInvocationException to be thrown.");
            Exception inner = ex.InnerException;
            Assert.IsNotNull(inner);
            Assert.IsTrue(
                inner is ApplicationException || inner is FileNotFoundException,
                "Expected ApplicationException or FileNotFoundException, got " + inner.GetType().FullName);
            StringAssert.Contains(inner.Message, missingFile);
        }

        [TestMethod]
        public void ShowAnyExceptions_WithEmptyList_DoesNotThrow()
        {
            MethodInfo showAnyExceptions = ProgramType.GetMethod(
                "ShowAnyExceptions",
                BindingFlags.NonPublic | BindingFlags.Static);

            showAnyExceptions.Invoke(null, new object[] { new List<Exception>() });
        }

        [TestMethod]
        public void Bootstrap_WithEmptyModuleFile_InitializesServiceManager()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "flowsharp-bootstrap-modules-" + Guid.NewGuid().ToString("N") + ".xml");
            File.WriteAllText(tempFile, "<?xml version='1.0' encoding='utf-8'?><Modules></Modules>");

            FieldInfo serviceManagerField = ProgramType.GetField(
                "ServiceManager",
                BindingFlags.Public | BindingFlags.Static);
            object originalServiceManager = serviceManagerField.GetValue(null);

            try
            {
                MethodInfo bootstrap = ProgramType.GetMethod(
                    "Bootstrap",
                    BindingFlags.NonPublic | BindingFlags.Static);
                bootstrap.Invoke(null, new object[] { tempFile });

                object serviceManager = serviceManagerField.GetValue(null);
                Assert.IsNotNull(serviceManager);
            }
            finally
            {
                serviceManagerField.SetValue(null, originalServiceManager);

                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod]
        public void Bootstrap_WhenReflectionTypeLoadExceptionOccurs_ShowsLoaderMessages()
        {
            FieldInfo bootstrapCoreField = ProgramType.GetField("BootstrapCore", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo showMessageBoxField = ProgramType.GetField("ShowMessageBox", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo serviceManagerField = ProgramType.GetField("ServiceManager", BindingFlags.Public | BindingFlags.Static);
            object originalBootstrapCore = bootstrapCoreField.GetValue(null);
            object originalShowMessageBox = showMessageBoxField.GetValue(null);
            object originalServiceManager = serviceManagerField.GetValue(null);
            string shownText = null;

            try
            {
                Action<string> throwingCore = _ => throw new ReflectionTypeLoadException(
                    new Type[0],
                    new Exception[] { new Exception("loader-a"), new Exception("loader-b") });
                Action<string, string, MessageBoxButtons, MessageBoxIcon> captureMessage =
                    (text, _, _, _) => shownText = text;

                bootstrapCoreField.SetValue(null, throwingCore);
                showMessageBoxField.SetValue(null, captureMessage);

                InvokeBootstrap("ignored.xml");
                Assert.IsNotNull(shownText);
                StringAssert.Contains(shownText, "loader-a");
                StringAssert.Contains(shownText, "loader-b");
            }
            finally
            {
                bootstrapCoreField.SetValue(null, originalBootstrapCore);
                showMessageBoxField.SetValue(null, originalShowMessageBox);
                serviceManagerField.SetValue(null, originalServiceManager);
            }
        }

        [TestMethod]
        public void Bootstrap_WhenGenericExceptionOccurs_ShowsExceptionMessage()
        {
            FieldInfo bootstrapCoreField = ProgramType.GetField("BootstrapCore", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo showMessageBoxField = ProgramType.GetField("ShowMessageBox", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo serviceManagerField = ProgramType.GetField("ServiceManager", BindingFlags.Public | BindingFlags.Static);
            object originalBootstrapCore = bootstrapCoreField.GetValue(null);
            object originalShowMessageBox = showMessageBoxField.GetValue(null);
            object originalServiceManager = serviceManagerField.GetValue(null);
            string shownText = null;

            try
            {
                Action<string> throwingCore = _ => throw new InvalidOperationException("boom-generic");
                Action<string, string, MessageBoxButtons, MessageBoxIcon> captureMessage =
                    (text, _, _, _) => shownText = text;

                bootstrapCoreField.SetValue(null, throwingCore);
                showMessageBoxField.SetValue(null, captureMessage);

                InvokeBootstrap("ignored.xml");
                Assert.IsNotNull(shownText);
                StringAssert.Contains(shownText, "boom-generic");
            }
            finally
            {
                bootstrapCoreField.SetValue(null, originalBootstrapCore);
                showMessageBoxField.SetValue(null, originalShowMessageBox);
                serviceManagerField.SetValue(null, originalServiceManager);
            }
        }

        [TestMethod]
        public void ResolveStartupArguments_WithFsdArgument_UsesDefaultModulesAndStartupDiagram()
        {
            Tuple<string, string> result = InvokeResolveStartupArguments(@"C:\temp\example.fsd");

            Assert.AreEqual("modules.xml", result.Item1);
            Assert.AreEqual(@"C:\temp\example.fsd", result.Item2);
        }

        [TestMethod]
        public void ResolveStartupArguments_WithModulesArgument_UsesCustomModulesAndNoDiagram()
        {
            Tuple<string, string> result = InvokeResolveStartupArguments(@"C:\temp\custom-modules.xml");

            Assert.AreEqual(@"C:\temp\custom-modules.xml", result.Item1);
            Assert.IsNull(result.Item2);
        }

        [TestMethod]
        public void ResolveStartupArguments_WithModulesAndFsd_UsesBothValues()
        {
            Tuple<string, string> result = InvokeResolveStartupArguments(@"C:\temp\custom-modules.xml", @"C:\temp\example.fsd");

            Assert.AreEqual(@"C:\temp\custom-modules.xml", result.Item1);
            Assert.AreEqual(@"C:\temp\example.fsd", result.Item2);
        }

        [TestMethod]
        public void ResolveModuleDefinitionPath_WithRelativeFileInAppBase_ResolvesToAppBasePath()
        {
            string moduleFileName = "flowsharp-modules-" + Guid.NewGuid().ToString("N") + ".xml";
            string modulePath = Path.Combine(AppContext.BaseDirectory, moduleFileName);
            string originalCurrentDirectory = Directory.GetCurrentDirectory();
            string isolatedCurrentDirectory = Path.Combine(Path.GetTempPath(), "flowsharp-cwd-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(isolatedCurrentDirectory);
                Directory.SetCurrentDirectory(isolatedCurrentDirectory);
                File.WriteAllText(modulePath, "<Modules></Modules>");

                string resolved = InvokeResolveModuleDefinitionPath(moduleFileName);

                Assert.AreEqual(modulePath, resolved);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalCurrentDirectory);

                if (File.Exists(modulePath))
                {
                    File.Delete(modulePath);
                }

                if (Directory.Exists(isolatedCurrentDirectory))
                {
                    Directory.Delete(isolatedCurrentDirectory, true);
                }
            }
        }

        private static MethodInfo GetModuleListFromXDocumentMethod()
        {
            return ProgramType.GetMethod(
                "GetModuleList",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(XDocument) },
                null);
        }

        private static MethodInfo GetModuleListFromXmlFileNameMethod()
        {
            return ProgramType.GetMethod(
                "GetModuleList",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { XmlFileNameType },
                null);
        }

        private static MethodInfo ResolveStartupArgumentsMethod()
        {
            return ProgramType.GetMethod(
                "ResolveStartupArguments",
                BindingFlags.NonPublic | BindingFlags.Static);
        }

        private static MethodInfo ResolveModuleDefinitionPathMethod()
        {
            return ProgramType.GetMethod(
                "ResolveModuleDefinitionPath",
                BindingFlags.NonPublic | BindingFlags.Static);
        }

        private static object CreateXmlFileName(string path)
        {
            MethodInfo create = XmlFileNameType.GetMethod(
                "Create",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            if (create == null)
            {
                throw new InvalidOperationException("Could not locate XmlFileName.Create(string).");
            }

            return create.Invoke(null, new object[] { path });
        }

        private static List<string> ExtractSemanticValues(object semanticList)
        {
            var values = new List<string>();

            foreach (object item in (IEnumerable)semanticList)
            {
                PropertyInfo valueProp = item.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                values.Add((string)valueProp.GetValue(item));
            }

            return values;
        }

        private static Type ProgramType =>
            Assembly.Load("FlowSharp").GetType("FlowSharp.Program", throwOnError: true);

        private static Type XmlFileNameType =>
            Type.GetType("Clifton.Core.Semantics.XmlFileName, Clifton.Core", throwOnError: true);

        private static string FindSolutionRoot()
        {
            string current = Directory.GetCurrentDirectory();
            DirectoryInfo dir = new DirectoryInfo(current);

            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "FlowSharp.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            throw new InvalidOperationException("Could not locate FlowSharp.sln from current directory.");
        }

        private static void InvokeBootstrap(string moduleFile)
        {
            MethodInfo bootstrap = ProgramType.GetMethod(
                "Bootstrap",
                BindingFlags.NonPublic | BindingFlags.Static);
            bootstrap.Invoke(null, new object[] { moduleFile });
        }

        private static Tuple<string, string> InvokeResolveStartupArguments(params string[] args)
        {
            object tuple = ResolveStartupArgumentsMethod().Invoke(null, new object[] { args });
            Type tupleType = tuple.GetType();
            string modules = (string)tupleType.GetProperty("Item1").GetValue(tuple);
            string startupDiagram = (string)tupleType.GetProperty("Item2").GetValue(tuple);

            return Tuple.Create(modules, startupDiagram);
        }

        private static string InvokeResolveModuleDefinitionPath(string moduleFilename)
        {
            object resolved = ResolveModuleDefinitionPathMethod().Invoke(null, new object[] { moduleFilename });
            return (string)resolved;
        }
    }
}
