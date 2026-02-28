using Microsoft.VisualStudio.TestTools.UnitTesting;

using Clifton.DockingFormService;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class DockingFormServiceSplitterTests
    {
        [TestMethod]
        public void CalculateLeftDockSplitterX_UsesCurrentBoundaryWhenValid()
        {
            int x = new TestDockingFormService().CalculateSplitterX(
                panelWidth: 1600,
                currentBoundaryX: 420,
                dockLeftPortion: 0.10,
                previousBoundaryX: 300);

            Assert.AreEqual(420, x);
        }

        [TestMethod]
        public void CalculateLeftDockSplitterX_FallsBackToDockLeftPortion()
        {
            int x = new TestDockingFormService().CalculateSplitterX(
                panelWidth: 1600,
                currentBoundaryX: 0,
                dockLeftPortion: 0.25,
                previousBoundaryX: 0);

            Assert.AreEqual(400, x);
        }

        [TestMethod]
        public void CalculateLeftDockSplitterX_FallsBackToLastKnownBoundaryWhenTransientValuesInvalid()
        {
            int x = new TestDockingFormService().CalculateSplitterX(
                panelWidth: 1600,
                currentBoundaryX: 0,
                dockLeftPortion: double.NaN,
                previousBoundaryX: 360);

            Assert.AreEqual(360, x);
        }

        [TestMethod]
        public void CalculateLeftDockSplitterX_UsesCenteredClampOnVerySmallPanel()
        {
            int x = new TestDockingFormService().CalculateSplitterX(
                panelWidth: 120,
                currentBoundaryX: 0,
                dockLeftPortion: 0,
                previousBoundaryX: 0);

            Assert.IsTrue(x > 0);
            Assert.IsTrue(x < 120);
        }

        [TestMethod]
        public void CalculateLeftDockSplitterX_SupportsAbsoluteDockLeftPortion()
        {
            int x = new TestDockingFormService().CalculateSplitterX(
                panelWidth: 1600,
                currentBoundaryX: 0,
                dockLeftPortion: 320,
                previousBoundaryX: 0);

            Assert.AreEqual(320, x);
        }

        private class TestDockingFormService : DockingFormService
        {
            public int CalculateSplitterX(int panelWidth, int currentBoundaryX, double dockLeftPortion, int previousBoundaryX)
            {
                return CalculateLeftDockSplitterX(panelWidth, currentBoundaryX, dockLeftPortion, previousBoundaryX);
            }
        }
    }
}
