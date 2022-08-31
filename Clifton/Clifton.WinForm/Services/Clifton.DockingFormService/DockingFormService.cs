using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using WeifenLuo.WinFormsUI.Docking;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.ModuleManagement;
using Clifton.Core.ServiceManagement;
using Clifton.WinForm.ServiceInterfaces;

namespace Clifton.DockingFormService
{
    public class DockingFormModule : IModule
    {
        public void InitializeServices(IServiceManager serviceManager)
        {
            serviceManager.RegisterSingleton<IDockingFormService, DockingFormService>();
        }
    }

    public class DockingFormService : ServiceBase, IDockingFormService
    {
        public event EventHandler<ContentLoadedEventArgs> ContentLoaded;
        public event EventHandler<EventArgs> ActiveDocumentChanged;
        public event EventHandler<EventArgs> DocumentClosing;

        public Panel DockPanel => dockPanel;
        public List<IDockDocument> Documents => dockPanel.DocumentsToArray().Cast<IDockDocument>().ToList();

        protected DockPanel dockPanel;
        //protected VS2015LightTheme theme = new VS2015LightTheme();

        public Form CreateMainForm<T>() where T : Form, new()
        {
            var form = new T();
            dockPanel = new DockPanel
            {
                Dock = DockStyle.Fill
            };
            //dockPanel.Theme = theme;
            form.Controls.Add(dockPanel);

            dockPanel.ActiveDocumentChanged += (sndr, args) => ActiveDocumentChanged.Fire(dockPanel.ActiveDocument);

            return form;
        }

        /// <summary>
        /// Create a document in the active document panel.
        /// </summary>
        public Control CreateDocument(WinForm.ServiceInterfaces.DockState dockState, string tabText, string metadata = "")
        {
            var dockContent = new GenericDockContent(metadata)
            {
                DockAreas = DockAreas.Float | DockAreas.DockBottom | DockAreas.DockLeft | DockAreas.DockRight | DockAreas.DockTop | DockAreas.Document,
                TabText = tabText
            };
            dockContent.Show(dockPanel, (WeifenLuo.WinFormsUI.Docking.DockState)dockState);
            dockContent.FormClosing += (sndr, args) => DocumentClosing.Fire(dockContent, EventArgs.Empty);

            return dockContent;
        }

        /// <summary>
        /// Creates a document in the specified document panel.
        /// </summary>
        /// <param name="panel">Must be a DockContent object.</param>
        /// <param name="dockState"></param>
        /// <param name="tabText"></param>
        /// <param name="metadata"></param>
        /// <returns></returns>
        public Control CreateDocument(Control panel, WinForm.ServiceInterfaces.DockState dockState, string tabText, string metadata = "")
        {
            var dockPnl = ((DockContent)panel).DockPanel;
            var dockContent = new GenericDockContent(metadata)
            {
                DockAreas = DockAreas.Float | DockAreas.DockBottom | DockAreas.DockLeft | DockAreas.DockRight | DockAreas.DockTop | DockAreas.Document,
                TabText = tabText
            };
            dockContent.Show(dockPnl, (WeifenLuo.WinFormsUI.Docking.DockState)dockState);
            dockContent.FormClosing += (sndr, args) => DocumentClosing.Fire(dockContent, EventArgs.Empty);

            return dockContent;
        }

        /// <summary>
        /// Create a document relative to specified pane, in a new container.
        /// </summary>
        public Control CreateDocument(Control pane, WinForm.ServiceInterfaces.DockAlignment dockAlignment, string tabText, string metadata = "", double portion = 0.25)
        {
            var dockContent = new GenericDockContent(metadata)
            {
                DockAreas = DockAreas.Float | DockAreas.DockBottom | DockAreas.DockLeft | DockAreas.DockRight | DockAreas.DockTop | DockAreas.Document,
                TabText = tabText
            };
            dockContent.Show(((DockContent)pane).Pane, (WeifenLuo.WinFormsUI.Docking.DockAlignment)dockAlignment, portion);
            dockContent.FormClosing += (sndr, args) => DocumentClosing.Fire(dockContent, EventArgs.Empty);

            return dockContent;
        }

        public void SetActiveDocument(IDockDocument document)
        {
            ((DockContent)document).Activate();
        }

        public void SaveLayout(string filename)
        {
            dockPanel.SaveAsXml(filename);      // layout.xml
        }

        public void LoadLayout(string filename)
        {
            CloseAllDocuments();
            dockPanel.LoadFromXml(filename, new DeserializeDockContent(GetContentFromPersistString));
            LoadApplicationContent(Path.GetDirectoryName(filename));
        }

        protected IDockContent GetContentFromPersistString(string persistString)
        {
            return new GenericDockContent
            {
                Metadata = persistString.RightOf(',').Trim()
            };
        }

        protected void LoadApplicationContent(string originalPath = "")
        {
            // ToList(), in case contents are modified while iterating.
            foreach (DockContent document in dockPanel.Contents.ToList())
            {
                // For content loaded from a layout, we need to rewire FormClosing, since we didn't actually create the DockContent instance.
                document.FormClosing += (sndr, args) => DocumentClosing.Fire(document, EventArgs.Empty);
                ContentLoaded.Fire(this, new ContentLoadedEventArgs()
                {
                    DockContent = document,
                    Metadata = ((GenericDockContent)document).Metadata,
                    OriginalPath = originalPath
                });
            }

            //foreach (var window in dockPanel.FloatWindows.ToList())
            //{
            //    window.Dispose();
            //}

            //foreach (DockContent doc in dockPanel.Contents.ToList())
            //{
            //    doc.DockHandler.DockPanel = null;
            //    doc.Close();
            //}
        }

        protected void CloseAllDocuments()
        {
            // Close all documents
            foreach (IDockContent document in dockPanel.DocumentsToArray())
            {
                // IMPORANT: dispose all panes.
                document.DockHandler.DockPanel = null;
                document.DockHandler.Close();
            }

            // IMPORTANT: dispose all float windows.
            foreach (var window in dockPanel.FloatWindows.ToList())
            {
                window.Dispose();
            }

            foreach (DockContent doc in dockPanel.Contents.ToList())
            {
                doc.DockHandler.DockPanel = null;
                doc.Close();
            }
        }
    }
}
