﻿/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.ModuleManagement;
using Clifton.Core.ServiceManagement;
using Clifton.WinForm.ServiceInterfaces;

using FlowSharpLib;
using FlowSharpServiceInterfaces;

namespace FlowSharpService
{
    public class FlowSharpForm : BaseForm, IFlowSharpForm
    {
        public IServiceManager ServiceManager { get; set; }
    }

    public class FlowSharpServiceModule : IModule
    {
        public void InitializeServices(IServiceManager serviceManager)
        {
            serviceManager.RegisterSingleton<IFlowSharpService, FlowSharpService>();
        }
    }

    public class FlowSharpService : ServiceBase, IFlowSharpService
    {
        public event EventHandler<ContentLoadedEventArgs> ContentResolver;
        public event EventHandler<EventArgs> FlowSharpInitialized;
        public event EventHandler<NewCanvasEventArgs> NewCanvas;

        private Form form;
        private IDockingFormService dockingService;
        private Panel pnlToolbox;
        private Panel pnlFlowSharp;
        private PropertyGrid propGrid;
        private bool loading;

        public Form CreateDockingForm(Icon icon)
        {
            dockingService = ServiceManager.Get<IDockingFormService>();
            dockingService.ContentLoaded += OnContentLoaded;
            dockingService.ActiveDocumentChanged += (sndr, args) => OnActiveDocumentChanged(sndr);
            dockingService.DocumentClosing += (sndr, args) => OnDocumentClosing(sndr);
            form = dockingService.CreateMainForm<FlowSharpForm>();
            ((FlowSharpForm)form).ServiceManager = ServiceManager;
            form.Text = "FlowSharp";
            form.Icon = icon;
            form.Size = new Size(1200, 800);
            form.Shown += OnShown;
            form.FormClosing += OnFormClosing;
            ((IBaseForm)form).ProcessCmdKeyEvent += OnProcessCmdKeyEvent;

            return form;
        }

        protected void OnContentLoaded(object sender, ContentLoadedEventArgs e)
        {
            switch (e.Metadata.LeftOf(","))
            {
                case Constants.META_CANVAS:
                    pnlFlowSharp = new Panel() { Dock = DockStyle.Fill, Tag = Constants.META_CANVAS };
                    e.DockContent.Controls.Add(pnlFlowSharp);
                    e.DockContent.Text = "Canvas";
                    var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
                    canvasService.CreateCanvas(pnlFlowSharp);
                    var baseController = ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;

                    if (e.Metadata.Contains(","))
                    {
                        var filename = e.Metadata.Between(",", ",");
                        var canvasName = e.Metadata.RightOfRightmostOf(",");
                        canvasName = string.IsNullOrWhiteSpace(canvasName) ? "Canvas" : canvasName;
                        e.DockContent.Text = canvasName;
                        // If metadata file does not exist, assume it is in the same
                        // folder as the xml file
                        if (!File.Exists(filename) && !string.IsNullOrEmpty(e.OriginalPath))
                        {
                            filename = Path.Combine(e.OriginalPath, Path.GetFileName(filename));
                        }
                        LoadFileIntoCanvas(filename, canvasName, baseController);
                    }
                    // ServiceManager.Get<IFlowSharpMouseControllerService>().Initialize(baseController);
                    break;

                case Constants.META_TOOLBOX:
                    pnlToolbox = new Panel() { Dock = DockStyle.Fill, Tag = Constants.META_TOOLBOX };
                    e.DockContent.Controls.Add(pnlToolbox);
                    e.DockContent.Text = "Toolbox";
                    break;

                case Constants.META_PROPERTYGRID:
                    propGrid = new PropertyGrid() { Dock = DockStyle.Fill, Tag = Constants.META_PROPERTYGRID };
                    e.DockContent.Controls.Add(propGrid);
                    e.DockContent.Text = "Property Grid";
                    ServiceManager.Get<IFlowSharpPropertyGridService>().Initialize(propGrid);
                    break;

                default:
                    ContentResolver.Fire(this, e);
                    break;
            }

            // Associate the toolbox with a canvas controller after both canvas and toolbox panels are created.
            // !!! This handles the defaultLayout configuration. !!!
            if ((e.Metadata == Constants.META_CANVAS || e.Metadata == Constants.META_TOOLBOX) && (pnlFlowSharp != null && pnlToolbox != null))
            {
                var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
                var canvasController = canvasService.ActiveController;
                var toolboxService = ServiceManager.Get<IFlowSharpToolboxService>();
                toolboxService.CreateToolbox(pnlToolbox);
                toolboxService.InitializeToolbox();
                toolboxService.InitializePluginsInToolbox();
                toolboxService.UpdateToolboxPaths();
            }

            //if ((e.Metadata == Constants.META_CANVAS || e.Metadata == Constants.META_PROPERTYGRID) && (pnlFlowSharp != null && propGrid != null))
            //{
            //    ServiceManager.Get<IFlowSharpPropertyGridService>().Initialize(propGrid);
            //}
        }

        protected void OnProcessCmdKeyEvent(object sender, ProcessCmdKeyEventArgs e)
        {
            e.Handled = ServiceManager.Get<IFlowSharpEditService>().ProcessCmdKey(e.KeyData);
        }

        protected void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            var state = ServiceManager.Get<IFlowSharpEditService>().CheckForChanges();

            if (state == ClosingState.SaveChanges)
            {
                if (!ServiceManager.Get<IFlowSharpMenuService>().SaveOrSaveAs())
                {
                    e.Cancel = true;
                }
            }
            else
            {
                e.Cancel = state == ClosingState.CancelClose;
            }

            if (!e.Cancel)
            {
                dockingService.SaveLayout("layout.xml");
            }
        }

        protected void OnShown(object sender, EventArgs e)
        {
            dockingService.LoadLayout("defaultLayout.xml");
            Initialize();
            FlowSharpInitialized.Fire(this);
            EnableCanvasPaint();
        }

        protected void EnableCanvasPaint()
        {
            // Enable canvas paint after all initialization has completed,
            // because adding certain controls, like TextboxShape, causes a panel canvas OnPaint to be called
            // when the TextboxShape is being added to the toolbox canvas, and this results in all shapes
            // attempting to draw, and they are not fully initialized at this point!
            var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
            canvasService.Controllers.ForEach(c => c.Canvas.EndInit());
            canvasService.Controllers.ForEach(c => c.Canvas.Invalidate());
            var toolboxService = ServiceManager.Get<IFlowSharpToolboxService>();

            // Will be null if canvas was not created when app starts.  Sanity check here
            // mainly for when we debug a form with no panels initialized by default.
            if (toolboxService.Controller == null) return;
            toolboxService.Controller.Canvas.EndInit();
            toolboxService.Controller.Canvas.Invalidate();
        }

        protected void Initialize()
        {
            var menuService = ServiceManager.Get<IFlowSharpMenuService>();
            var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
            var mouseService = ServiceManager.Get<IFlowSharpMouseControllerService>();
            menuService.Initialize(form);
            canvasService.AddCanvas += (sndr, args) => CreateCanvas();
            canvasService.LoadLayout += OnLoadLayout;
            canvasService.SaveLayout += OnSaveLayout;
            // mouseService.Initialize(canvasService.ActiveController);

            // Will be null if canvas was not created when app starts.  Sanity check here
            // mainly for when we debug a form with no panels initialized by default.
            if (canvasService.ActiveController == null) return;
            menuService.Initialize(canvasService.ActiveController);
            InformServicesOfNewCanvas(canvasService.ActiveController);
        }

        protected void OnLoadLayout(object sender, FileEventArgs e)
        {
            var layoutFilename = Path.Combine(Path.GetDirectoryName(e.Filename), Path.GetFileNameWithoutExtension(e.Filename) + "-layout.xml");

            if (File.Exists(layoutFilename))
            {
                // Use the layout file to determine the canvas files.
                ServiceManager.Get<IFlowSharpEditService>().ClearSavePoints();
                var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
                var pgService = ServiceManager.Get<IFlowSharpPropertyGridService>();
                canvasService.Controllers.ForEach(c => pgService.Terminate(c));
                canvasService.ClearControllers();
                loading = true;
                ServiceManager.Get<IDockingFormService>().LoadLayout(layoutFilename);
                loading = false;

                // Update all services with new controllers.
                canvasService.Controllers.ForEach(c => InformServicesOfNewCanvas(c));
                EnableCanvasPaint();
                SelectFirstDocument();
            }
            else
            {
                // Just open the diagram the currently selected canvas.
                var canvasController = ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
                ServiceManager.Get<IFlowSharpEditService>().ClearSavePoints();
                LoadFileIntoCanvas(e.Filename, Constants.META_CANVAS, canvasController);
            }
        }

        protected void LoadFileIntoCanvas(string filename, string canvasName, BaseController canvasController)
        {
            canvasController.Filename = filename;       // set now, in case of relative image files, etc...
            canvasController.CanvasName = canvasName;
            List<GraphicElement> els = null;
            try
            {
                if (File.Exists(filename))
                {
                    var data = File.ReadAllText(filename);
                    els = Persist.Deserialize(canvasController.Canvas, data);
                }
            }
            catch (Exception e)
            {
                // nothing
                Debug.WriteLine(e.Message);
            }
            canvasController.Clear();
            canvasController.UndoStack.ClearStacks();
            // ElementCache.Instance.ClearCache();
            ServiceManager.Get<IFlowSharpMouseControllerService>().ClearState();
            if (els?.Any() != true) return;
            canvasController.AddElements(els);
            canvasController.Elements.ForEach(el => el.UpdatePath());
            canvasController.Canvas.Invalidate();
        }

        protected void OnSaveLayout(object sender, FileEventArgs e)
        {
            // Save the layout, which, on an open, will check for a layout file and load the documents from the layout metadata.
            var layoutFilename = Path.Combine(Path.GetDirectoryName(e.Filename), Path.GetFileNameWithoutExtension(e.Filename) + "-layout.xml");
            ServiceManager.Get<IDockingFormService>().SaveLayout(layoutFilename);
        }

        protected void CreateCanvas()
        {
            // Create canvas.
            var panel = new Panel() { Dock = DockStyle.Fill, Tag = Constants.META_CANVAS };
            var dockPanel = ServiceManager.Get<IDockingFormService>().CreateDocument(DockState.Document, "Canvas", Constants.META_CANVAS);
            dockPanel.Controls.Add(panel);
            var canvasService = ServiceManager.Get<IFlowSharpCanvasService>();
            canvasService.CreateCanvas(panel);
            canvasService.ActiveController.Canvas.EndInit();
            InformServicesOfNewCanvas(canvasService.ActiveController);
        }

        protected void InformServicesOfNewCanvas(BaseController controller)
        {
            // Wire up menu for this canvas controller.
            var menuService = ServiceManager.Get<IFlowSharpMenuService>();
            menuService.Initialize(controller);

            // Wire up mouse for this canvas controller.
            var mouseService = ServiceManager.Get<IFlowSharpMouseControllerService>();
            mouseService.Initialize(controller);

            // Debug window needs to know too.
            ServiceManager.Get<IFlowSharpDebugWindowService>().Initialize(controller);

            // PropertyGrid service needs to hook controller events.
            ServiceManager.Get<IFlowSharpPropertyGridService>().Initialize(controller);

            // Update document tab when canvas name changes.
            controller.CanvasNameChanged += (sndr, args) =>
            {
                var doc = ((IDockDocument)((BaseController)sndr).Canvas.Parent.Parent);
                doc.TabText = controller.CanvasName;

                // Update the metadata for the controller document so the layout contains this info on save.
                doc.Metadata = Constants.META_CANVAS + "," + controller.Filename + "," + doc.TabText;
            };

            // Update the metadata for the controller document so the layout contains this info on save.
            controller.FilenameChanged += (sndr, args) =>
            {
                var doc = ((IDockDocument)((BaseController)sndr).Canvas.Parent.Parent);
                doc.Metadata = Constants.META_CANVAS + "," + controller.Filename + "," + doc.TabText;
            };

            // Inform debug window, so it can select the shape in the shape list when a shape is selected on the canvas.
            controller.ElementSelected += (sndr, args) =>
            {
                ServiceManager.Get<IFlowSharpDebugWindowService>().FindShape(args.Element);
            };

            ServiceManager.Get<IFlowSharpEditService>().NewCanvas(controller);

            // Update any other services needing to know about the new canvas.  These are additional services that are not
            // part of the core FlowSharp application (for example, the FlowSharpCode services.)
            NewCanvas.Fire(this, new NewCanvasEventArgs() { Controller = controller });
        }

        protected void OnActiveDocumentChanged(object document)
        {
            if (loading) return;
            if (!(document is Control ctrl && ctrl.Controls?.Count > 0))
                return;

            if (((IDockDocument)document).Metadata.LeftOf(",") == Constants.META_CANVAS)
            {
                // System.Diagnostics.Trace.WriteLine("*** Document Changed");
                var child = ctrl.Controls[0];
                ServiceManager.Get<IFlowSharpMouseControllerService>().ClearState();
                ServiceManager.Get<IFlowSharpCanvasService>().SetActiveController(child);
                ServiceManager.Get<IFlowSharpDebugWindowService>().UpdateDebugWindow();
                ServiceManager.Get<IFlowSharpMenuService>().UpdateMenu();
            }
        }

        protected void OnDocumentClosing(object document)
        {
            if (!(document is Control ctrl && ctrl.Controls?.Count > 0))
                return;

            switch(((IDockDocument)document).Metadata.LeftOf(","))
            {
                case Constants.META_CANVAS:
                    var child = ctrl.Controls[0];
                    ServiceManager.Get<IFlowSharpCanvasService>().DeleteCanvas(child);
                    break;

                case Constants.META_PROPERTYGRID:
                    // TODO
                    break;

                case Constants.META_TOOLBOX:
                    // TODO:
                    break;
            }
        }

        protected void SelectFirstDocument()
        {
            var dockService = ServiceManager.Get<IDockingFormService>();
            var docs = dockService.Documents;

            if (docs.Count > 0)
            {
                dockService.SetActiveDocument(docs[0]);
            }
        }
    }
}
