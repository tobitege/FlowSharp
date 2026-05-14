using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

using Clifton.Core.ServiceManagement;
using Clifton.DockingFormService;
using Clifton.WinForm.ServiceInterfaces;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpCodeService;
using FlowSharpCodeServiceInterfaces;
using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class PanelRestoreTests
    {
        [TestMethod]
        public void DefaultLayout_RestoresToolboxAndPropertyGridPanels()
        {
            string layoutPath = FindRepoFile("defaultLayout.xml");
            XDocument layout = XDocument.Load(layoutPath);
            List<string> persistStrings = layout
                .Descendants("Content")
                .Select(content => (string)content.Attribute("PersistString"))
                .Where(value => !string.IsNullOrEmpty(value))
                .ToList();

            CollectionAssert.Contains(persistStrings, "Clifton.DockingFormService.GenericDockContent,Canvas");
            CollectionAssert.Contains(persistStrings, "Clifton.DockingFormService.GenericDockContent,Toolbox");
            CollectionAssert.Contains(persistStrings, "Clifton.DockingFormService.GenericDockContent,PropertyGrid");
        }

        [TestMethod]
        public void CodeService_WhenNoEditorPanelWasRestored_CreatesCSharpEditorPanel()
        {
            var canvasDocument = new GenericDockContent(FlowSharpServiceInterfaces.Constants.META_CANVAS);
            var dockingService = new TestDockingFormService();
            dockingService.Documents.Add(canvasDocument);
            var editorService = new TestCodeEditorService();
            var serviceManager = new ServiceManager();
            serviceManager.RegisterSingleton<IDockingFormService>(dockingService);
            serviceManager.RegisterSingleton<IFlowSharpCodeEditorService>(editorService);
            var codeService = new TestableFlowSharpCodeService();
            codeService.Initialize(serviceManager);

            codeService.InvokeOnFlowSharpInitialized();

            Assert.AreSame(canvasDocument, dockingService.LastRelativePane);
            Assert.AreEqual(DockAlignment.Bottom, dockingService.LastDockAlignment);
            Assert.AreEqual(FlowSharpCodeServiceInterfaces.Constants.META_CSHARP_EDITOR, dockingService.LastCreatedMetadata);
            Assert.IsNotNull(editorService.LastEditorParent);
            Assert.AreEqual("Clifton.Core.dll", editorService.LastAssembly);
        }

        private static string FindRepoFile(string filename)
        {
            string directory = Directory.GetCurrentDirectory();

            while (!string.IsNullOrEmpty(directory))
            {
                string candidate = Path.Combine(directory, filename);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = Directory.GetParent(directory)?.FullName;
            }

            throw new FileNotFoundException(filename);
        }

        private sealed class TestableFlowSharpCodeService : FlowSharpCodeService.FlowSharpCodeService
        {
            public void InvokeOnFlowSharpInitialized()
            {
                OnFlowSharpInitialized(this, EventArgs.Empty);
            }
        }

        private sealed class TestDockingFormService : ServiceBase, IDockingFormService
        {
            public event EventHandler<ContentLoadedEventArgs> ContentLoaded
            {
                add { }
                remove { }
            }

            public event EventHandler<EventArgs> ActiveDocumentChanged
            {
                add { }
                remove { }
            }

            public event EventHandler<DockDocumentClosingEventArgs> DocumentClosing
            {
                add { }
                remove { }
            }

            public Panel DockPanel { get; } = new Panel();
            public List<IDockDocument> Documents { get; } = new List<IDockDocument>();
            public Control LastRelativePane { get; private set; }
            public DockAlignment LastDockAlignment { get; private set; }
            public string LastCreatedMetadata { get; private set; }

            public Form CreateMainForm<T>() where T : Form, new()
            {
                throw new NotSupportedException();
            }

            public Control CreateDocument(DockState dockState, string tabText, string metadata = "")
            {
                return CreateDocument(metadata);
            }

            public Control CreateDocument(Control pane, DockAlignment dockAlignment, string tabText, string metadata = "", double portion = 0.25)
            {
                LastRelativePane = pane;
                LastDockAlignment = dockAlignment;
                return CreateDocument(metadata);
            }

            public Control CreateDocument(Control panel, DockState dockState, string tabText, string metadata = "")
            {
                return CreateDocument(metadata);
            }

            public void SetActiveDocument(IDockDocument document)
            {
                throw new NotSupportedException();
            }

            public void LoadLayout(string filename)
            {
                throw new NotSupportedException();
            }

            public void SaveLayout(string filename)
            {
                throw new NotSupportedException();
            }

            private Control CreateDocument(string metadata)
            {
                var document = new GenericDockContent(metadata);
                Documents.Add(document);
                LastCreatedMetadata = metadata;
                return document;
            }
        }

        private sealed class TestCodeEditorService : ServiceBase, IFlowSharpCodeEditorService
        {
            public event EventHandler<TextChangedEventArgs> TextChanged
            {
                add { }
                remove { }
            }

            public string Filename { get; set; }
            public Control LastEditorParent { get; private set; }
            public string LastAssembly { get; private set; }

            public void CreateEditor(Control parent)
            {
                LastEditorParent = parent;
            }

            public void AddAssembly(string filename)
            {
                LastAssembly = filename;
            }

            public void AddAssembly(Type t) { throw new NotSupportedException(); }
            public int GetPosition() { throw new NotSupportedException(); }
            public void SetPosition(int pos) { throw new NotSupportedException(); }
            public void SetText(string language, string text) { throw new NotSupportedException(); }
        }
    }
}
