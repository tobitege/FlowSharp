using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpLib;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class GroupedPasteTests
    {
        [TestMethod]
        public void GetSelectableShapeAt_PrefersGroupBoxOverGroupedChild()
        {
            BaseController controller = CreateController();
            GroupBox group = new GroupBox(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(20, 20, 120, 100)
            };
            Box child = new Box(controller.Canvas)
            {
                DisplayRectangle = new Rectangle(40, 40, 40, 30)
            };
            child.Parent = group;
            group.GroupChildren.Add(child);
            controller.AddElement(group);
            controller.AddElement(child);

            GraphicElement selectable = controller.GetSelectableShapeAt(new Point(50, 50));

            Assert.AreSame(group, selectable);
        }

        [TestMethod]
        public void PersistDeserialize_GroupedShape_CanBeSelectedAndMovedAsGroup()
        {
            BaseController sourceController = CreateController();
            GroupBox sourceGroup = new GroupBox(sourceController.Canvas)
            {
                DisplayRectangle = new Rectangle(20, 20, 120, 100)
            };
            Box sourceChild = new Box(sourceController.Canvas)
            {
                DisplayRectangle = new Rectangle(40, 40, 40, 30)
            };
            sourceChild.Parent = sourceGroup;
            sourceGroup.GroupChildren.Add(sourceChild);

            string xml = Persist.Serialize(new List<GraphicElement> { sourceGroup, sourceChild });

            BaseController targetController = CreateController();
            List<GraphicElement> pasted = Persist.Deserialize(targetController.Canvas, xml);
            targetController.AddElements(pasted);

            GroupBox pastedGroup = pasted.OfType<GroupBox>().Single();
            Box pastedChild = pasted.OfType<Box>().Single(b => b.Parent == pastedGroup);
            Point originalGroupLocation = pastedGroup.DisplayRectangle.Location;
            Point originalChildLocation = pastedChild.DisplayRectangle.Location;
            GraphicElement selectable = targetController.GetSelectableShapeAt(new Point(pastedChild.DisplayRectangle.Left + 5, pastedChild.DisplayRectangle.Top + 5));

            targetController.SelectElement(selectable);
            targetController.MoveSelectedElements(new Point(10, 5));

            Assert.AreSame(pastedGroup, selectable);
            Assert.AreEqual(new Point(originalGroupLocation.X + 10, originalGroupLocation.Y + 5), pastedGroup.DisplayRectangle.Location);
            Assert.AreEqual(new Point(originalChildLocation.X + 10, originalChildLocation.Y + 5), pastedChild.DisplayRectangle.Location);
        }

        private static BaseController CreateController()
        {
            Canvas canvas = new Canvas();
            canvas.CreateBitmap(220, 180);
            return new CanvasController(canvas);
        }
    }
}
