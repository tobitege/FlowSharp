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
        private const int LeftDockSplitterWidth = 12;
        private const int MinLeftDockPixelMargin = 80;
        private const int MinSplitterDragPixelDelta = 2;
        private const double MinDockLeftPortion = 0.10;
        private const double MaxDockLeftPortion = 0.80;

        public event EventHandler<ContentLoadedEventArgs> ContentLoaded;
        public event EventHandler<EventArgs> ActiveDocumentChanged;
        public event EventHandler<EventArgs> DocumentClosing;

        public Panel DockPanel => dockPanel;
        public List<IDockDocument> Documents => dockPanel.DocumentsToArray().Cast<IDockDocument>().ToList();

        protected DockPanel dockPanel;
        protected Panel leftDockSplitter;
        protected bool draggingLeftDockSplitter;
        protected int lastLeftDockSplitterX = -1;
        protected int lastKnownLeftDockBoundaryX = -1;
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
            InitializeLeftDockSplitter();

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
            string resolvedFilename = ResolveLayoutPath(filename, requireExistingFile: false);
            dockPanel.SaveAsXml(resolvedFilename);      // layout.xml
        }

        public void LoadLayout(string filename)
        {
            string resolvedFilename = ResolveLayoutPath(filename, requireExistingFile: true);
            CloseAllDocuments();
            dockPanel.LoadFromXml(resolvedFilename, new DeserializeDockContent(GetContentFromPersistString));
            LoadApplicationContent(Path.GetDirectoryName(resolvedFilename));
            UpdateLeftDockSplitter();
        }

        protected virtual string ResolveLayoutPath(string filename, bool requireExistingFile)
        {
            if (Path.IsPathRooted(filename))
            {
                return filename;
            }

            if (File.Exists(filename))
            {
                return filename;
            }

            string appBasePath = AppContext.BaseDirectory;
            string appBaseFilename = Path.Combine(appBasePath, filename);

            if (!requireExistingFile || File.Exists(appBaseFilename))
            {
                return appBaseFilename;
            }

            return filename;
        }

        protected virtual void InitializeLeftDockSplitter()
        {
            leftDockSplitter = new Panel
            {
                Width = LeftDockSplitterWidth,
                Cursor = Cursors.VSplit,
                BackColor = System.Drawing.Color.Silver,
                BorderStyle = BorderStyle.FixedSingle
            };

            DockWindow leftDockWindow = dockPanel.DockWindows[WeifenLuo.WinFormsUI.Docking.DockState.DockLeft];
            leftDockSplitter.MouseDown += OnLeftDockSplitterMouseDown;
            leftDockSplitter.MouseMove += OnLeftDockSplitterMouseMove;
            leftDockSplitter.MouseUp += OnLeftDockSplitterMouseUp;
            leftDockWindow.Resize += (sndr, args) => UpdateLeftDockSplitter();
            leftDockWindow.VisibleChanged += (sndr, args) => UpdateLeftDockSplitter();
            dockPanel.Resize += (sndr, args) => UpdateLeftDockSplitter();
            dockPanel.Controls.Add(leftDockSplitter);
            UpdateLeftDockSplitter();
        }

        protected virtual void OnLeftDockSplitterMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                draggingLeftDockSplitter = true;
                lastLeftDockSplitterX = dockPanel.PointToClient(Cursor.Position).X;
            }
        }

        protected virtual void OnLeftDockSplitterMouseMove(object sender, MouseEventArgs e)
        {
            if (!draggingLeftDockSplitter)
            {
                return;
            }

            int mouseXInDockPanel = dockPanel.PointToClient(Cursor.Position).X;
            int clampedX = ClampSplitterX(dockPanel.Width, mouseXInDockPanel);

            if (lastLeftDockSplitterX >= 0 && Math.Abs(clampedX - lastLeftDockSplitterX) < MinSplitterDragPixelDelta)
            {
                return;
            }

            lastLeftDockSplitterX = clampedX;
            lastKnownLeftDockBoundaryX = clampedX;
            double dockLeftPortion = (double)clampedX / Math.Max(1, dockPanel.Width);
            dockPanel.DockLeftPortion = Math.Max(MinDockLeftPortion, Math.Min(MaxDockLeftPortion, dockLeftPortion));
            UpdateLeftDockSplitter();
        }

        protected virtual void OnLeftDockSplitterMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                draggingLeftDockSplitter = false;
                lastLeftDockSplitterX = -1;
            }
        }

        protected virtual void UpdateLeftDockSplitter()
        {
            if (leftDockSplitter == null || dockPanel == null || dockPanel.Width <= 0 || dockPanel.Height <= 0)
            {
                return;
            }

            EnsureLeftDockSplitterAttached();
            int x = CalculateLeftDockSplitterX(dockPanel.Width, GetCurrentLeftDockBoundaryX(), dockPanel.DockLeftPortion, lastKnownLeftDockBoundaryX);
            lastKnownLeftDockBoundaryX = x;
            int splitterWidth = Math.Min(LeftDockSplitterWidth, dockPanel.Width);
            int splitterLeft = Math.Max(0, Math.Min(dockPanel.Width - splitterWidth, x - (splitterWidth / 2)));

            leftDockSplitter.Bounds = new System.Drawing.Rectangle(
                splitterLeft,
                0,
                splitterWidth,
                dockPanel.Height);
            leftDockSplitter.Visible = true;
            leftDockSplitter.BringToFront();
        }

        protected virtual int GetCurrentLeftDockBoundaryX()
        {
            DockWindow leftDockWindow = dockPanel.DockWindows[WeifenLuo.WinFormsUI.Docking.DockState.DockLeft];

            if (leftDockWindow == null || !leftDockWindow.Visible || leftDockWindow.Width <= 0)
            {
                return 0;
            }

            return leftDockWindow.Right;
        }

        protected virtual int CalculateLeftDockSplitterX(int panelWidth, int currentBoundaryX, double dockLeftPortion, int previousBoundaryX)
        {
            int preferredX = currentBoundaryX;

            if (!IsValidLeftDockBoundaryX(panelWidth, preferredX))
            {
                preferredX = GetLeftBoundaryXFromDockPortion(panelWidth, dockLeftPortion);
            }

            if (!IsValidLeftDockBoundaryX(panelWidth, preferredX))
            {
                preferredX = previousBoundaryX;
            }

            if (!IsValidLeftDockBoundaryX(panelWidth, preferredX))
            {
                preferredX = (int)Math.Round(panelWidth * MinDockLeftPortion);
            }

            return ClampSplitterX(panelWidth, preferredX);
        }

        protected virtual int GetLeftBoundaryXFromDockPortion(int panelWidth, double dockLeftPortion)
        {
            if (panelWidth <= 0 || dockLeftPortion <= 0 || double.IsNaN(dockLeftPortion) || double.IsInfinity(dockLeftPortion))
            {
                return 0;
            }

            double computedBoundary = dockLeftPortion > 1
                ? dockLeftPortion
                : panelWidth * dockLeftPortion;

            if (double.IsNaN(computedBoundary) || double.IsInfinity(computedBoundary) || computedBoundary <= 0)
            {
                return 0;
            }

            return (int)Math.Round(computedBoundary);
        }

        protected virtual int ClampSplitterX(int panelWidth, int candidateX)
        {
            if (panelWidth <= 0)
            {
                return 0;
            }

            int minX;
            int maxX;

            if (panelWidth > (MinLeftDockPixelMargin * 2))
            {
                minX = MinLeftDockPixelMargin;
                maxX = panelWidth - MinLeftDockPixelMargin;
            }
            else
            {
                minX = Math.Max(1, panelWidth / 6);
                maxX = Math.Max(minX, panelWidth - minX);
            }

            return Math.Max(minX, Math.Min(maxX, candidateX));
        }

        protected virtual bool IsValidLeftDockBoundaryX(int panelWidth, int boundaryX)
        {
            return panelWidth > 1 && boundaryX > 0 && boundaryX < panelWidth;
        }

        protected virtual void EnsureLeftDockSplitterAttached()
        {
            if (leftDockSplitter == null || dockPanel == null || leftDockSplitter.Parent == dockPanel)
            {
                return;
            }

            leftDockSplitter.Parent?.Controls.Remove(leftDockSplitter);
            dockPanel.Controls.Add(leftDockSplitter);
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
