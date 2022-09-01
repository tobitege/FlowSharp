/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Clifton.Core.ExtensionMethods;
using Clifton.Core.ServiceManagement;

using FlowSharpLib;
using FlowSharpServiceInterfaces;
// ReSharper disable UnusedParameter.Local

namespace FlowSharpMenuService
{
    public class NavigateToShape : IComparable
    {
        public GraphicElement Shape { get; set; }
        public string Name { get; set; }

        public override string ToString()
        {
            return Name;
        }

        public int CompareTo(object obj)
        {
            return Name.CompareTo(((NavigateToShape)obj).Name);
        }
    }

    public partial class MenuController
    {
        private const string MRU_FILENAME = "FlowSharp.mru";

        public string Filename => filename;

        protected string filename;
        protected IServiceManager serviceManager;
        protected Form mainForm;
        protected List<string> mru;

        public MenuController(IServiceManager serviceManager)
        {
            this.serviceManager = serviceManager;
            Initialize();
        }

        public void Initialize(Form mainFrm)
        {
            this.mainForm = mainFrm;
            mru = new List<string>();
            InitializeMenuHandlers();
            PopulateMostRecentFiles();
        }

        public void Initialize(BaseController canvasController)
        {
            canvasController.ElementSelected += (snd, args) => UpdateMenu(args.Element != null);
            canvasController.UndoStack.AfterAction += (snd, args) => UpdateMenu(canvasController.SelectedElements.Any());
            UpdateMenu(false);
        }

        // Enable/disable to copy, paste, and delete menu shortcuts, as certain editors, like Scintilla,
        // do not capture these keystrokes early enough, and we need to stop the Form from intercepting them
        // and treating them as canvas operations when the canvas isn't focused.
        public void EnableCopyPasteDel(bool state)
        {
            mnuCopy.Enabled = state;
            mnuPaste.Enabled = state;
            mnuDelete.Enabled = state;
            mnuUndo.Enabled = state;
            mnuRedo.Enabled = state;
        }

        // TODO: The save/load operations might be best moved to the edit service?
        public bool SaveOrSaveAs(bool forceSaveAs = false, bool selectionOnly = false)
        {
            var ret = true;

            if (string.IsNullOrEmpty(filename) || forceSaveAs)
            {
                ret = SaveAs(selectionOnly);
            }
            else
            {
                SaveDiagram(filename);
            }

            return ret;
        }

        public void UpdateMenu(bool elementSelected)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            mnuBottommost.Enabled = elementSelected;
            mnuTopmost.Enabled = elementSelected;
            mnuMoveUp.Enabled = elementSelected;
            mnuMoveDown.Enabled = elementSelected;
            mnuCopy.Enabled = elementSelected;
            mnuDelete.Enabled = elementSelected;

            // Group disabled if any selected element is part of a group:
            mnuGroup.Enabled = elementSelected && !canvasController.SelectedElements.Any(el => el.Parent != null);

            // Ungroup disabled if the selected element is not a groupbox:
            mnuUngroup.Enabled = canvasController.SelectedElements.Count == 1 && canvasController.SelectedElements[0].GroupChildren.Any();

            mnuCollapseGroup.Enabled = canvasController.SelectedElements.Count == 1
                && (canvasController.SelectedElements[0] is FlowSharpLib.GroupBox box)
                && box.State==FlowSharpLib.GroupBox.CollapseState.Expanded;

            mnuExpandGroup.Enabled = canvasController.SelectedElements.Count == 1
                && (canvasController.SelectedElements[0] is FlowSharpLib.GroupBox box1)
                && box1.State == FlowSharpLib.GroupBox.CollapseState.Collapsed;

            mnuUndo.Enabled = canvasController.UndoStack.CanUndo;
            mnuRedo.Enabled = canvasController.UndoStack.CanRedo;
        }

        /// <summary>
        /// Adds a top-level menu tree, appended to the end of the default menu strip items.
        /// </summary>
        public void AddMenu(ToolStripMenuItem menuItem)
        {
            menuStrip.Items.Add(menuItem);
        }

        protected void InitializeMenuHandlers()
        {
            mnuClearCanvas.Click += MnuNew_Click;
            mnuOpen.Click += MnuOpen_Click;
            mnuImport.Click += MnuImport_Click;
            mnuSave.Click += MnuSave_Click;
            mnuSaveAs.Click += MnuSaveAs_Click;
            mnuSaveSelectionAs.Click += MnuSaveSelectionAs_Click;
            mnuExit.Click += MnuExit_Click;
            mnuCopy.Click += MnuCopy_Click;
            mnuPaste.Click += MnuPaste_Click;
            mnuDelete.Click += MnuDelete_Click;
            mnuTopmost.Click += MnuTopmost_Click;
            mnuBottommost.Click += MnuBottommost_Click;
            mnuMoveUp.Click += MnuMoveUp_Click;
            mnuMoveDown.Click += MnuMoveDown_Click;

            mnuGroup.Click += MnuGroup_Click;
            mnuUngroup.Click += MnuUngroup_Click;
            mnuCollapseGroup.Click += MnuCollapseGroup_Click;
            mnuExpandGroup.Click += MnuExpandGroup_Click;

            mnuUndo.Click += MnuUndo_Click;
            mnuRedo.Click += MnuRedo_Click;
            mnuEdit.Click += (sndr, args) => serviceManager.Get<IFlowSharpEditService>().EditText();
            mnuDebugWindow.Click += (sndr, args) => serviceManager.Get<IFlowSharpDebugWindowService>().ShowDebugWindow();
            mnuPlugins.Click += (sndr, args) => serviceManager.Get<IFlowSharpDebugWindowService>().EditPlugins();
            // mnuLoadLayout.Click += (sndr, args) => serviceManager.Get<IDockingFormService>().LoadLayout("layout.xml");
            // mnuSaveLayout.Click += (sndr, args) => serviceManager.Get<IDockingFormService>().SaveLayout("layout.xml");
            // TODO: Decouple dependency - see canvas controller
            // Instead, fire an event or publish on subscriber an action?
            mnuAddCanvas.Click += (sndr, args) => serviceManager.Get<IFlowSharpCanvasService>().RequestNewCanvas();

            mnuGoToShape.Click += GoToShape;
            mnuGoToBookmark.Click += GoToBookmark;
            mnuToggleBookmark.Click += ToogleBookmark;
            mnuClearBookmarks.Click += ClearBookmarks;

            mnuAlignLefts.Click += AlignLefts;
            mnuAlignRights.Click += AlignRights;
            mnuAlignTops.Click += AlignTops;
            mnuAlignBottoms.Click += AlignBottoms;
            mnuAlignCenters.Click += AlignCenters;
            mnuAlignSizes.Click += AlignSizes;

            mnuZoom100.Click += MenuZoom;
            mnuZoom90.Click += MenuZoom;
            mnuZoom80.Click += MenuZoom;
            mnuZoom70.Click += MenuZoom;
            mnuZoom60.Click += MenuZoom;
            mnuZoom50.Click += MenuZoom;
            mnuZoom40.Click += MenuZoom;
            mnuZoom30.Click += MenuZoom;
            mnuZoom20.Click += MenuZoom;
            mnuZoom10.Click += MenuZoom;
        }

        protected void MenuZoom(object sender, EventArgs e)
        {
            BaseController canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            canvasController.SetZoom((int)((ToolStripItem)sender).Tag);
        }

        private void AlignLefts(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var selectedElements = new List<GraphicElement>(canvasController.SelectedElements);   // For closure
            var shapeOffsets = new Dictionary<GraphicElement, int>();                             // For closure
            var minLeft = selectedElements.Min(el => el.DisplayRectangle.Left);
            selectedElements.ForEach(el => shapeOffsets[el] = el.DisplayRectangle.Left - minLeft);

            canvasController.UndoStack.UndoRedo("AlignLefts",
                () =>
                {
                    selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(-shapeOffsets[el], 0)));
                },
                () =>
                {
                    selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(shapeOffsets[el], 0)));
                });
        }

        private void AlignRights(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var selectedElements = new List<GraphicElement>(canvasController.SelectedElements);    // For closure
            var shapeOffsets = new Dictionary<GraphicElement, int>();                              // For closure
            var maxRight = canvasController.SelectedElements.Max(el => el.DisplayRectangle.Right);
            selectedElements.ForEach(el => shapeOffsets[el] = maxRight - el.DisplayRectangle.Right);

            canvasController.UndoStack.UndoRedo("AlignRights",
                () =>
                {
                    selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(shapeOffsets[el], 0)));
                },
                () =>
                {
                    selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(-shapeOffsets[el], 0)));
                });
        }

    private void AlignTops(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var selectedElements = new List<GraphicElement>(canvasController.SelectedElements);     // For closure
            var shapeOffsets = new Dictionary<GraphicElement, int>();                               // For closure
            var minTop = canvasController.SelectedElements.Min(el => el.DisplayRectangle.Top);
            selectedElements.ForEach(el => shapeOffsets[el] = el.DisplayRectangle.Top - minTop);

            canvasController.UndoStack.UndoRedo("AlignTops",
                () =>
                {
                    selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(0, -shapeOffsets[el])));
                },
                () =>
                {
                    selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(0, shapeOffsets[el])));
                });
        }

        private void AlignBottoms(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var selectedElements = new List<GraphicElement>(canvasController.SelectedElements);    // For closure
            var shapeOffsets = new Dictionary<GraphicElement, int>();                              // For closure
            var maxBottom = canvasController.SelectedElements.Max(el => el.DisplayRectangle.Bottom);
            selectedElements.ForEach(el => shapeOffsets[el] = maxBottom - el.DisplayRectangle.Bottom);

            canvasController.UndoStack.UndoRedo("AlignBottoms",
                () =>
                {
                    selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(0, shapeOffsets[el])));
                },
                () =>
                {
                    selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(0, -shapeOffsets[el])));
                });
        }

        private void AlignCenters(object sender, EventArgs e)
        {
            // Figure out whether we're aligning vertically or horizontally based on the positions of the shapes.

            // If the shapes are horizontally aligned (more or less), we center vertically.
            // If the shapes are vertically aligned (more or less), we center horizontally.

            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var selectedElements = new List<GraphicElement>(canvasController.SelectedElements);    // For closure
            var shapeOffsets = new Dictionary<GraphicElement, int>();                              // For closure

            var dx = 0;
            var dy = 0;

            if (selectedElements.Count < 2) return;
            for (var n = 1; n < selectedElements.Count; n++)
            {
                dx += Math.Abs(selectedElements[n - 1].DisplayRectangle.Center().X - selectedElements[n].DisplayRectangle.Center().X);
                dy += Math.Abs(selectedElements[n - 1].DisplayRectangle.Center().Y - selectedElements[n].DisplayRectangle.Center().Y);
            }

            if (dx < dy)
            {
                // Center vertically
                var avgx = (int)selectedElements.Average(el => el.DisplayRectangle.Center().X);
                selectedElements.ForEach(el => shapeOffsets[el] = el.DisplayRectangle.Center().X - avgx);

                canvasController.UndoStack.UndoRedo("AlignCenters",
                    () =>
                    {
                        selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(-shapeOffsets[el], 0)));
                    },
                    () =>
                    {
                        selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(shapeOffsets[el], 0)));
                    });
            }
            else
            {
                // Center horizontally
                var avgy = (int)selectedElements.Average(el => el.DisplayRectangle.Center().Y);
                selectedElements.ForEach(el => shapeOffsets[el] = el.DisplayRectangle.Center().Y - avgy);

                canvasController.UndoStack.UndoRedo("AlignCenters",
                    () =>
                    {
                        selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(0, -shapeOffsets[el])));
                    },
                    () =>
                    {
                        selectedElements.ForEach(el => canvasController.MoveElement(el, new Point(0, shapeOffsets[el])));
                    });
            }
        }

        /// <summary>
        /// Use the first selected element to determine the size of all the other elements.
        /// This updates only the size of each shape without affecting the location.  An alternative would be to resize
        /// around the center of each shape.
        /// Unlike the other align operations, align sizes uses the first element's size to set all the other shape sizes.
        /// </summary>
        private void AlignSizes(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var selectedElements = new List<GraphicElement>(canvasController.SelectedElements);     // For closure
            var shapeSizes = new Dictionary<GraphicElement, Size>();                                // For closure

            if (selectedElements.Count < 2) return;
            var setToSize = selectedElements[0].DisplayRectangle.Size;
            selectedElements.ForEach(el => shapeSizes[el] = el.DisplayRectangle.Size);

            canvasController.UndoStack.UndoRedo("AlignSizes",
                () =>
                {
                    // Do/redo:
                    selectedElements.Skip(1).ForEach(el =>
                    {
                        canvasController.Redraw(el, _ =>
                        {
                            el.DisplayRectangle = new Rectangle(el.DisplayRectangle.Location, setToSize);
                            el.UpdatePath();
                            canvasController.UpdateConnections(el);
                        });
                    });
                },
                () =>
                {
                    // Undo:
                    selectedElements.Skip(1).ForEach(el =>
                    {
                        canvasController.Redraw(el, _ =>
                        {
                            el.DisplayRectangle = new Rectangle(el.DisplayRectangle.Location, shapeSizes[el]);
                            el.UpdatePath();
                            canvasController.UpdateConnections(el);
                        });
                    });
                });
        }

        private void GoToShape(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var navShapes = canvasController.Elements.
                Where(el => !el.IsConnector).
                Select(el => new NavigateToShape() { Shape = el, Name = el.NavigateName }).
                OrderBy(s => s.Name).
                ToList();
            ShowNavigateDialog(canvasController, navShapes);
        }

        private void GoToBookmark(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var navShapes = canvasController.Elements.
                Where(el=>el.IsBookmarked).
                Select(el => new NavigateToShape() { Shape = el, Name = el.NavigateName }).
                OrderBy(s=>s).
                ToList();
            ShowNavigateDialog(canvasController, navShapes);
        }

        private void ToogleBookmark(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            canvasController.SelectedElements?.ForEach(el =>
            {
                el.ToggleBookmark();
                canvasController.Redraw(el);
            });
        }

        private void ClearBookmarks(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            canvasController.ClearBookmarks();
        }

        protected void ShowNavigateDialog(BaseController canvasController, List<NavigateToShape> navShapes)
        {
            new NavigateDlg(serviceManager, navShapes).ShowDialog();
        }

        protected void PopulateMostRecentFiles()
        {
            if (!File.Exists(MRU_FILENAME)) return;
            mru = File.ReadAllLines(MRU_FILENAME).ToList();
            foreach (var f in mru)
            {
                ToolStripItem tsi = new ToolStripMenuItem(f);
                tsi.Click += OnRecentFileSelected;
                mnuRecentFiles.DropDownItems.Add(tsi);
            }
        }

        protected void UpdateMru(string fname)
        {
            // Any existing MRU, remove, and regardless, insert at beginning of list.
            mru.Remove(fname);
            mru.Insert(0, fname);
            File.WriteAllLines(MRU_FILENAME, mru);
        }

        private void OnRecentFileSelected(object sender, EventArgs e)
        {
            if (CheckForChanges()) return;
            var tsi = sender as ToolStripItem;
            filename = tsi?.Text ?? "unknown";
            var canvasService = serviceManager.Get<IFlowSharpCanvasService>();
            canvasService.LoadDiagrams(filename);
            UpdateCaption();
        }

        private void MnuTopmost_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var originalZOrder = canvasController.GetZOrderOfSelectedElements();

            canvasController.UndoStack.UndoRedo("Z-Top",
                () =>
                {
                    canvasController.Topmost();
                },
                () =>
                {
                    canvasController.RestoreZOrder(originalZOrder);
                });
        }

        private void MnuBottommost_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var originalZOrder = canvasController.GetZOrderOfSelectedElements();

            canvasController.UndoStack.UndoRedo("Z-Bottom",
                () =>
                {
                    canvasController.Bottommost();
                },
                () =>
                {
                    canvasController.RestoreZOrder(originalZOrder);
                });
        }

        private void MnuMoveUp_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var originalZOrder = canvasController.GetZOrderOfSelectedElements();

            canvasController.UndoStack.UndoRedo("Z-Up",
                () =>
                {
                    canvasController.MoveSelectedElementsUp();
                },
                () =>
                {
                    canvasController.RestoreZOrder(originalZOrder);
                });
        }

        private void MnuMoveDown_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var originalZOrder = canvasController.GetZOrderOfSelectedElements();
            canvasController.UndoStack.UndoRedo("Z-Down",
                () =>
                {
                    canvasController.MoveSelectedElementsDown();
                },
                () =>
                {
                    canvasController.RestoreZOrder(originalZOrder);
                });
        }

        private void MnuCopy_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            if (canvasController.SelectedElements.Count > 0)
            {
                serviceManager.Get<IFlowSharpEditService>().Copy();
            }
        }

        private void MnuPaste_Click(object sender, EventArgs e)
        {
            serviceManager.Get<IFlowSharpEditService>().Paste();
        }

        private void MnuDelete_Click(object sender, EventArgs e)
        {
            serviceManager.Get<IFlowSharpEditService>().Delete();
        }

        private void MnuNew_Click(object sender, EventArgs e)
        {
            if (CheckForChanges()) return;
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            serviceManager.Get<IFlowSharpEditService>().ResetSavePoint();
            canvasController.Clear();
            canvasController.UndoStack.ClearStacks();
            // ElementCache.Instance.ClearCache();
            serviceManager.Get<IFlowSharpMouseControllerService>().ClearState();
            canvasController.Canvas.Invalidate();
            filename = string.Empty;
            canvasController.Filename = string.Empty;
            UpdateCaption();
        }

        private void MnuOpen_Click(object sender, EventArgs e)
        {
            if (CheckForChanges()) return;
            var ofd = new OpenFileDialog
            {
                Filter = "FlowSharp (*.fsd)|*.fsd"
            };
            var res = ofd.ShowDialog();

            if (res == DialogResult.OK)
            {
                filename = ofd.FileName;
            }
            else
            {
                return;
            }

            var canvasService = serviceManager.Get<IFlowSharpCanvasService>();
            canvasService.LoadDiagrams(filename);
            UpdateCaption();
            UpdateMru(filename);
        }

        private void MnuImport_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "FlowSharp (*.fsd)|*.fsd"
            };
            var res = ofd.ShowDialog();

            if (res != DialogResult.OK) return;
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var importFilename = ofd.FileName;
            var data = File.ReadAllText(importFilename);
            var els = Persist.Deserialize(canvasController.Canvas, data);
            var selectedElements = canvasController.SelectedElements.ToList();

            canvasController.UndoStack.UndoRedo("Import",
                () =>
                {
                    canvasController.DeselectCurrentSelectedElements();
                    canvasController.AddElements(els);
                    canvasController.Elements.ForEach(el => el.UpdatePath());
                    canvasController.SelectElements(els);
                    canvasController.Canvas.Invalidate();
                },
                () =>
                {
                    canvasController.DeselectCurrentSelectedElements();
                    els.ForEach(el => canvasController.DeleteElement(el));
                    canvasController.SelectElements(selectedElements);
                });
        }

        private void MnuSave_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;

            if (canvasController.Elements.Count > 0)
            {
                SaveOrSaveAs();
                UpdateCaption();
            }
            else
            {
                MessageBox.Show("Nothing to save.", "Empty Canvas", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void MnuSaveAs_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;

            if (canvasController.Elements.Count > 0)
            {
                SaveOrSaveAs(true);
                UpdateCaption();
            }
            else
            {
                MessageBox.Show("Nothing to save.", "Empty Canvas", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void MnuSaveSelectionAs_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;

            if (canvasController.SelectedElements.Count > 0)
            {
                SaveOrSaveAs(true, true);
                UpdateCaption();
            }
            else
            {
                MessageBox.Show("Nothing to save.", "Empty Canvas", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void MnuExit_Click(object sender, EventArgs e)
        {
            if (CheckForChanges()) return;
            mainForm.Close();
        }

        private void MnuGroup_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;

            if (!canvasController.SelectedElements.Any()) return;
            var selectedShapes = canvasController.SelectedElements.ToList();
            FlowSharpLib.GroupBox groupBox = new FlowSharpLib.GroupBox(canvasController.Canvas);

            canvasController.UndoStack.UndoRedo("Group",
                () =>
                {
                    // ElementCache.Instance.Remove(groupBox);
                    canvasController.GroupShapes(groupBox);
                    canvasController.DeselectCurrentSelectedElements();
                    canvasController.SelectElement(groupBox);
                },
                () =>
                {
                    // ElementCache.Instance.Add(groupBox);
                    canvasController.UngroupShapes(groupBox, false);
                    canvasController.DeselectCurrentSelectedElements();
                    canvasController.SelectElements(selectedShapes);
                });
        }

        private void MnuUngroup_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;

            // At this point, we can only ungroup one group.
            if (canvasController.SelectedElements.Count != 1) return;
            if (!(canvasController.SelectedElements[0] is FlowSharpLib.GroupBox groupBox)) return;
            var groupedShapes = new List<GraphicElement>(groupBox.GroupChildren);
            var collapsed = groupBox.State == FlowSharpLib.GroupBox.CollapseState.Collapsed; // For closure.

            canvasController.UndoStack.UndoRedo("Ungroup",
                () =>
                {
                    if (collapsed)
                    {
                        var children = canvasController.Elements.Where(el => el.Parent == groupBox);
                        ExpandGroupBox(canvasController, groupBox, children);
                    }

                    // ElementCache.Instance.Add(groupBox);
                    canvasController.UngroupShapes(groupBox, false);
                    canvasController.DeselectCurrentSelectedElements();
                    canvasController.SelectElements(groupedShapes);
                },
                () =>
                {
                    // ElementCache.Instance.Remove(groupBox);
                    canvasController.GroupShapes(groupBox);
                    canvasController.DeselectCurrentSelectedElements();
                    canvasController.SelectElement(groupBox);

                    if (collapsed)
                    {
                        var children = canvasController.Elements.Where(el => el.Parent == groupBox);
                        CollapseGroupBox(canvasController, groupBox, children);
                    }
                });
        }

        private void MnuCollapseGroup_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var gb = ((FlowSharpLib.GroupBox)canvasController.SelectedElements[0]);
            var children = canvasController.Elements.Where(el => el.Parent == gb);

            canvasController.UndoStack.UndoRedo("Collapse Group",
                () => CollapseGroupBox(canvasController, gb, children),
                () => ExpandGroupBox(canvasController, gb, children)
                );
        }

        private void MnuExpandGroup_Click(object sender, EventArgs e)
        {
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var gb = ((FlowSharpLib.GroupBox)canvasController.SelectedElements[0]);
            var children = canvasController.Elements.Where(el => el.Parent == gb);

            canvasController.UndoStack.UndoRedo("Expand Group",
                () => ExpandGroupBox(canvasController, gb, children),
                () => CollapseGroupBox(canvasController, gb, children)
                );
        }

        private void CollapseGroupBox(BaseController canvasController, FlowSharpLib.GroupBox gb, IEnumerable<GraphicElement> children)
        {
            canvasController.Redraw(gb, _ =>
            {
                gb.SetCollapsedState();
                gb.SaveExpandedSize();
                canvasController.Elements.Where(el => el.Parent == gb).ForEach(el => el.Visible = false);
                var r = gb.DisplayRectangle;
                gb.DisplayRectangle = new Rectangle(r.Location, new Size(r.Width, 30));
                // Update connections after display rectangle has been updated, as this adjusts the connection points.
                canvasController.UpdateConnections(gb);
            });
            // In a collapse, the children, being intersecting with the groupbox, will be redrawn.
        }

        private void ExpandGroupBox(BaseController canvasController, FlowSharpLib.GroupBox gb, IEnumerable<GraphicElement> children)
        {
            canvasController.Redraw(gb, _ =>
            {
                gb.SetExpandedState();
                var r = gb.DisplayRectangle;
                gb.DisplayRectangle = new Rectangle(r.Location, gb.ExpandedSize);
                // Update connections after display rectangle has been updated, as this adjusts the connection points.
                canvasController.UpdateConnections(gb);
            });

            // In an expand , not all the children may redraw because the collapsed groupbox does not intersect them!
            children.ForEach(el =>
            {
                el.Visible = true;
                el.Redraw();
            });
        }

        private void MnuUndo_Click(object sender, EventArgs e)
        {
            serviceManager.Get<IFlowSharpEditService>().Undo();
        }

        private void MnuRedo_Click(object sender, EventArgs e)
        {
            serviceManager.Get<IFlowSharpEditService>().Redo();
        }

        /// <summary>
        /// Return true if operation should be cancelled.
        /// </summary>
        protected bool CheckForChanges()
        {
            var ret = true;

            var state = serviceManager.Get<IFlowSharpEditService>().CheckForChanges();

            if (state == ClosingState.SaveChanges)
            {
                ret = !SaveOrSaveAs();   // override because of possible cancel in save operation.
            }
            else if (state != ClosingState.CancelClose)
            {
                var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
                if (canvasController != null)
                {
                    canvasController.UndoStack.ClearStacks();       // Prevents second "are you sure" when exiting with Ctrl+X
                }

                ret = false;
            }
            return ret;
        }

        protected bool SaveAs(bool selectionOnly = false)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "FlowSharp (*.fsd)|*.fsd|PNG (*.png)|*.png"
            };
            var res = sfd.ShowDialog();
            var ext = ".fsd";
            var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;

            if (res != DialogResult.OK) return false;
            ext = Path.GetExtension(sfd.FileName).ToLower();

            if (ext == ".png")
            {
                canvasController.SaveAsPng(sfd.FileName, selectionOnly);
            }
            else
            {
                filename = sfd.FileName;
                // TODO: What about other canvases that are open in the diagram?

                if (!selectionOnly)
                {
                    canvasController.Filename = String.Empty;       // Force blank filename so filename is saved to the new destination.  See FlowSharpCanvasService.cs, SaveDiagrams
                }

                // Let canvas controller assign filenames.
                SaveDiagram(filename, selectionOnly);

                if (!selectionOnly)
                {
                    UpdateCaption();
                    UpdateMru(filename);
                }
            }

            return ext != ".png";
        }

        protected void SaveDiagram(string fname, bool selectionOnly = false)
        {
            var canvasService = serviceManager.Get<IFlowSharpCanvasService>();
            canvasService.SaveDiagramsAndLayout(fname, selectionOnly);
            //var canvasController = serviceManager.Get<IFlowSharpCanvasService>().ActiveController;
            //var data = Persist.Serialize(canvasController.Elements);
            //File.WriteAllText(fname, data);

            if (!selectionOnly)
            {
                serviceManager.Get<IFlowSharpEditService>().SetSavePoint();
            }
        }

        protected void UpdateCaption()
        {
            mainForm.Text = "FlowSharp" + (String.IsNullOrEmpty(filename) ? "" : " - ") + filename;
        }
    }
}
