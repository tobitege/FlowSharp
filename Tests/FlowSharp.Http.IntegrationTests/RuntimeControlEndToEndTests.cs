using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowSharp.Http.IntegrationTests
{
    [TestClass]
    [DoNotParallelize]
    [TestCategory("E2E")]
    public class RuntimeControlEndToEndTests
    {
        private const int StartupTimeoutSeconds = 45;
        private const int CommandTimeoutSeconds = 15;
        private const string RestPortEnvironmentVariable = "FLOWSHARP_REST_PORT";
        private const string WebSocketPortEnvironmentVariable = "FLOWSHARP_WEBSOCKET_PORT";
        private const string MacroStepDelayEnvironmentVariable = "FLOWSHARP_MACRO_STEP_DELAY_MS";

        [TestMethod]
        [Timeout(120000)]
        public async Task LiveApp_HttpRuntimeControl_CanDriveCanvasStateAndPersistence()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("FlowSharp end-to-end tests require Windows.");
            }

            using var tempDir = new TempDirectory("flowsharp-e2e-http-");
            using FlowSharpAppSession session = await FlowSharpAppSession.StartAsync();

            await session.SendHttpExpectOkAsync(("cmd", "clearcanvas"));
            using (JsonDocument initialView = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "getcanvasview"))))
            {
                Assert.AreEqual(100, initialView.RootElement.GetProperty("Zoom").GetInt32());
                Assert.AreEqual(0, initialView.RootElement.GetProperty("OffsetX").GetInt32());
                Assert.AreEqual(0, initialView.RootElement.GetProperty("OffsetY").GetInt32());
            }

            await session.SendHttpExpectOkAsync(
                ("cmd", "dropshape"),
                ("ShapeName", "GroupBox"),
                ("Name", "GroupRoot"),
                ("X", "60"),
                ("Y", "60"),
                ("Width", "240"),
                ("Height", "180"),
                ("Text", "Group"));
            await session.SendHttpExpectOkAsync(
                ("cmd", "dropshape"),
                ("ShapeName", "Box"),
                ("Name", "InsideGroup"),
                ("X", "110"),
                ("Y", "110"),
                ("Width", "70"),
                ("Height", "50"),
                ("Text", "Child"));

            using (JsonDocument inspectChild = JsonDocument.Parse(await session.SendHttpAsync(
                ("cmd", "inspectshape"),
                ("Name", "InsideGroup"),
                ("Properties", "TextAlign"))))
            {
                JsonElement child = inspectChild.RootElement[0];
                Assert.AreEqual("GroupRoot", child.GetProperty("ParentName").GetString());
            }

            await session.SendHttpExpectOkAsync(
                ("cmd", "dropshape"),
                ("ShapeName", "Box"),
                ("Name", "RootA"),
                ("X", "320"),
                ("Y", "100"),
                ("Width", "80"),
                ("Height", "50"),
                ("Text", "A"));
            await session.SendHttpExpectOkAsync(
                ("cmd", "dropshape"),
                ("ShapeName", "Box"),
                ("Name", "RootB"),
                ("X", "430"),
                ("Y", "110"),
                ("Width", "80"),
                ("Height", "50"),
                ("Text", "B"));

            await session.SendHttpExpectOkAsync(("cmd", "setzoom"), ("Zoom", "80"));
            await session.SendHttpExpectOkAsync(("cmd", "setcanvasoffset"), ("X", "15"), ("Y", "5"));

            using (JsonDocument adjustedView = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "getcanvasview"))))
            {
                Assert.AreEqual(80, adjustedView.RootElement.GetProperty("Zoom").GetInt32());
                Assert.AreEqual(15, adjustedView.RootElement.GetProperty("OffsetX").GetInt32());
                Assert.AreEqual(5, adjustedView.RootElement.GetProperty("OffsetY").GetInt32());
            }

            await session.SendHttpExpectOkAsync(("cmd", "selectshapes"), ("Name", "RootA"));
            await session.SendHttpExpectOkAsync(("cmd", "selectshapes"), ("Name", "RootB"), ("Mode", "add"));

            using (JsonDocument selection = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "getselection"))))
            {
                Assert.AreEqual(2, selection.RootElement.GetArrayLength());
            }

            await session.SendHttpExpectOkAsync(("cmd", "moveselection"), ("Dx", "25"), ("Dy", "15"));

            using (JsonDocument moved = JsonDocument.Parse(await session.SendHttpAsync(
                ("cmd", "listshapes"),
                ("Name", "RootA"),
                ("IncludeConnectors", "false"))))
            {
                JsonElement rootA = moved.RootElement[0];
                Assert.AreEqual(366, rootA.GetProperty("X").GetInt32());
                Assert.AreEqual(123, rootA.GetProperty("Y").GetInt32());
            }

            await session.SendHttpExpectOkAsync(("cmd", "newcanvas"), ("Name", "Second"));
            await session.WaitForCanvasesAsync(2);
            using (JsonDocument secondCanvasView = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "getcanvasview"))))
            {
                Assert.AreEqual(100, secondCanvasView.RootElement.GetProperty("Zoom").GetInt32());
                Assert.AreEqual(0, secondCanvasView.RootElement.GetProperty("OffsetX").GetInt32());
                Assert.AreEqual(0, secondCanvasView.RootElement.GetProperty("OffsetY").GetInt32());
            }
            await session.SendHttpExpectOkAsync(
                ("cmd", "dropshape"),
                ("ShapeName", "Ellipse"),
                ("Name", "SecondCanvasShape"),
                ("X", "150"),
                ("Y", "150"),
                ("Width", "90"),
                ("Height", "60"),
                ("Text", "Second"));

            string baseDiagramPath = Path.Combine(tempDir.Path, "runtime-e2e.fsd");
            string siblingDiagramPath = Path.Combine(tempDir.Path, "runtime-e2e-1.fsd");
            string layoutPath = Path.Combine(tempDir.Path, "runtime-e2e-layout.xml");
            string pngPath = Path.Combine(tempDir.Path, "runtime-e2e.png");

            await session.SendHttpExpectOkAsync(
                ("cmd", "saveworkspace"),
                ("Filename", baseDiagramPath),
                ("RebaseFilenames", "true"));
            await session.WaitForFileAsync(baseDiagramPath);
            await session.WaitForFileAsync(siblingDiagramPath);
            await session.WaitForFileAsync(layoutPath);

            await session.SendHttpExpectOkAsync(("cmd", "exportpng"), ("Filename", pngPath));
            await session.WaitForFileAsync(pngPath);

            await session.SendHttpExpectOkAsync(("cmd", "loaddiagram"), ("Filename", baseDiagramPath));
            await session.WaitForCanvasesAsync(2);

            await session.SendHttpExpectOkAsync(("cmd", "usecanvas"), ("Name", "Second"));
            using (JsonDocument secondCanvasShapes = JsonDocument.Parse(await session.SendHttpAsync(
                ("cmd", "listshapes"),
                ("Name", "SecondCanvasShape"),
                ("IncludeConnectors", "false"))))
            {
                Assert.AreEqual(1, secondCanvasShapes.RootElement.GetArrayLength());
            }

            await session.SendHttpExpectOkAsync(("cmd", "usecanvas"), ("Name", "Canvas"));
            using (JsonDocument firstCanvasShapes = JsonDocument.Parse(await session.SendHttpAsync(
                ("cmd", "listshapes"),
                ("Name", "GroupRoot"),
                ("IncludeConnectors", "false"))))
            {
                Assert.AreEqual(1, firstCanvasShapes.RootElement.GetArrayLength());
            }
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task LiveApp_WebSocketMacro_CanExecuteCommandsAndReturnStepResults()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("FlowSharp end-to-end tests require Windows.");
            }

            using var tempDir = new TempDirectory("flowsharp-e2e-ws-");
            using FlowSharpAppSession session = await FlowSharpAppSession.StartAsync();
            string pngPath = Path.Combine(tempDir.Path, "macro.png");

            string script = string.Join(
                "\n",
                new[]
                {
                    "clearcanvas",
                    "setzoom Zoom=80",
                    "dropshape ShapeName=Box Name=Alpha X=80 Y=80 Width=120 Height=70 Text=\"Alpha\"",
                    "setcanvasoffset X=10 Y=5",
                    "updateproperty Name=Alpha PropertyName=TextAlign Value=TopCenter",
                    "selectshapes Name=Alpha",
                    "moveselection Dx=10 Dy=15",
                    "getcanvasview",
                    "exportpng Filename=" + ToMacroPath(pngPath),
                    "listshapes Name=Alpha IncludeConnectors=false"
                });

            string macroResponse = await session.SendWebSocketAsync(
                "cmd=runmacro&continueonerror=false&script=" + Uri.EscapeDataString(script));

            using (JsonDocument macroResults = JsonDocument.Parse(macroResponse))
            {
                Assert.AreEqual(10, macroResults.RootElement.GetArrayLength());

                foreach (JsonElement step in macroResults.RootElement.EnumerateArray())
                {
                    Assert.IsTrue(step.GetProperty("Success").GetBoolean(), "Macro step failed: " + step);
                }

                using JsonDocument view = JsonDocument.Parse(macroResults.RootElement[7].GetProperty("Response").GetString());
                Assert.AreEqual(80, view.RootElement.GetProperty("Zoom").GetInt32());
                Assert.AreEqual(10, view.RootElement.GetProperty("OffsetX").GetInt32());
                Assert.AreEqual(5, view.RootElement.GetProperty("OffsetY").GetInt32());
            }

            await session.WaitForFileAsync(pngPath);

            using (JsonDocument inspect = JsonDocument.Parse(await session.SendWebSocketAsync(
                "cmd=inspectshape&Name=Alpha&Properties=TextAlign")))
            {
                JsonElement alpha = inspect.RootElement[0];
                string textAlign = alpha.GetProperty("Properties").GetProperty("TextAlign").GetString();
                Assert.AreEqual("TopCenter", textAlign);
            }

            using (JsonDocument shapes = JsonDocument.Parse(await session.SendWebSocketAsync(
                "cmd=listshapes&Name=Alpha&IncludeConnectors=false")))
            {
                JsonElement alpha = shapes.RootElement[0];
                Assert.AreEqual(102, alpha.GetProperty("X").GetInt32());
                Assert.AreEqual(103, alpha.GetProperty("Y").GetInt32());
            }
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task BugReview_TextAlignmentScript_CanBeRunThroughRuntimeControl()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("FlowSharp end-to-end tests require Windows.");
            }

            string diagramPath = GetBugReviewArtifactPath("flowsharp-bug01-text-alignment.fsd");
            string layoutPath = GetBugReviewArtifactPath("flowsharp-bug01-text-alignment-layout.xml");
            string topPngPath = GetBugReviewArtifactPath("flowsharp-bug01-text-top.png");
            string bottomPngPath = GetBugReviewArtifactPath("flowsharp-bug01-text-bottom.png");
            CleanupFiles(diagramPath, layoutPath, topPngPath, bottomPngPath);

            try
            {
                using FlowSharpAppSession session = await FlowSharpAppSession.StartAsync();
                using JsonDocument macroResults = await RunBugReviewScriptAsync(session, "01-text-alignment.flow");

                AssertMacroSucceeded(macroResults);

                using JsonDocument topInspect = JsonDocument.Parse(GetMacroStepResponse(macroResults, 4));
                using JsonDocument bottomInspect = JsonDocument.Parse(GetMacroStepResponse(macroResults, 7));
                using JsonDocument persistedInspect = JsonDocument.Parse(GetMacroStepResponse(macroResults, 11));

                Assert.AreEqual("TopCenter", topInspect.RootElement[0].GetProperty("Properties").GetProperty("TextAlign").GetString());
                Assert.AreEqual("BottomCenter", bottomInspect.RootElement[0].GetProperty("Properties").GetProperty("TextAlign").GetString());
                Assert.AreEqual("BottomCenter", persistedInspect.RootElement[0].GetProperty("Properties").GetProperty("TextAlign").GetString());

                Assert.IsTrue(File.Exists(diagramPath), "Expected diagram file was not created.");
                Assert.IsTrue(File.Exists(layoutPath), "Expected layout file was not created.");
                Assert.IsTrue(File.Exists(topPngPath), "Expected top-aligned PNG was not created.");
                Assert.IsTrue(File.Exists(bottomPngPath), "Expected bottom-aligned PNG was not created.");
                Assert.IsFalse(File.ReadAllBytes(topPngPath).SequenceEqual(File.ReadAllBytes(bottomPngPath)));
            }
            finally
            {
                CleanupFiles(diagramPath, layoutPath, topPngPath, bottomPngPath);
            }
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task BugReview_SaveUnnamedSecondCanvasScript_CanBeRunThroughRuntimeControl()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("FlowSharp end-to-end tests require Windows.");
            }

            string diagramPath = GetBugReviewArtifactPath("flowsharp-bug02-unnamed-second.fsd");
            string siblingPath = GetBugReviewArtifactPath("flowsharp-bug02-unnamed-second-1.fsd");
            string layoutPath = GetBugReviewArtifactPath("flowsharp-bug02-unnamed-second-layout.xml");
            CleanupFiles(diagramPath, siblingPath, layoutPath);

            try
            {
                using FlowSharpAppSession session = await FlowSharpAppSession.StartAsync();
                using JsonDocument macroResults = await RunBugReviewScriptAsync(session, "02-save-unnamed-second-canvas.flow");

                AssertMacroSucceeded(macroResults);
                using JsonDocument canvases = JsonDocument.Parse(GetMacroStepResponse(macroResults, 6));
                Assert.AreEqual(2, canvases.RootElement.GetArrayLength());

                Assert.IsTrue(File.Exists(diagramPath), "Expected base diagram file was not created.");
                Assert.IsTrue(File.Exists(siblingPath), "Expected sibling diagram file was not created.");
                Assert.IsTrue(File.Exists(layoutPath), "Expected layout file was not created.");

                await session.SendHttpExpectOkAsync(("cmd", "usecanvas"), ("Index", "0"));
                using JsonDocument firstShapes = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "FirstCanvasMarker"), ("IncludeConnectors", "false")));
                Assert.AreEqual(1, firstShapes.RootElement.GetArrayLength());

                await session.SendHttpExpectOkAsync(("cmd", "usecanvas"), ("Index", "1"));
                using JsonDocument secondShapes = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "SecondCanvasMarker"), ("IncludeConnectors", "false")));
                Assert.AreEqual(1, secondShapes.RootElement.GetArrayLength());
            }
            finally
            {
                CleanupFiles(diagramPath, siblingPath, layoutPath);
            }
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task BugReview_SaveMultipleCanvasesScript_CanBeRunThroughRuntimeControl()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("FlowSharp end-to-end tests require Windows.");
            }

            string diagramPath = GetBugReviewArtifactPath("flowsharp-bug03-multicanvas.fsd");
            string siblingPath = GetBugReviewArtifactPath("flowsharp-bug03-multicanvas-1.fsd");
            string layoutPath = GetBugReviewArtifactPath("flowsharp-bug03-multicanvas-layout.xml");
            CleanupFiles(diagramPath, siblingPath, layoutPath);

            try
            {
                using FlowSharpAppSession session = await FlowSharpAppSession.StartAsync();
                using JsonDocument macroResults = await RunBugReviewScriptAsync(session, "03-save-multiple-canvases.flow");

                AssertMacroSucceeded(macroResults);
                using JsonDocument canvases = JsonDocument.Parse(GetMacroStepResponse(macroResults, 7));
                Assert.AreEqual(2, canvases.RootElement.GetArrayLength());

                Assert.IsTrue(File.Exists(diagramPath), "Expected base diagram file was not created.");
                Assert.IsTrue(File.Exists(siblingPath), "Expected sibling diagram file was not created.");
                Assert.IsTrue(File.Exists(layoutPath), "Expected layout file was not created.");

                await session.SendHttpExpectOkAsync(("cmd", "usecanvas"), ("Index", "0"));
                using JsonDocument firstShapes = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "CanvasOneMarker"), ("IncludeConnectors", "false")));
                Assert.AreEqual(1, firstShapes.RootElement.GetArrayLength());

                await session.SendHttpExpectOkAsync(("cmd", "usecanvas"), ("Index", "1"));
                using JsonDocument secondShapes = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "CanvasTwoMarker"), ("IncludeConnectors", "false")));
                Assert.AreEqual(1, secondShapes.RootElement.GetArrayLength());
            }
            finally
            {
                CleanupFiles(diagramPath, siblingPath, layoutPath);
            }
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task BugReview_MultiSelectScript_CanBeRunThroughRuntimeControl()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("FlowSharp end-to-end tests require Windows.");
            }

            using FlowSharpAppSession session = await FlowSharpAppSession.StartAsync();
            using JsonDocument macroResults = await RunBugReviewScriptAsync(session, "04-multi-select.flow");

            AssertMacroSucceeded(macroResults);

            using JsonDocument additiveSelection = JsonDocument.Parse(GetMacroStepResponse(macroResults, 7));
            using JsonDocument regionSelection = JsonDocument.Parse(GetMacroStepResponse(macroResults, 9));
            Assert.AreEqual(2, additiveSelection.RootElement.GetArrayLength());
            Assert.AreEqual(2, regionSelection.RootElement.GetArrayLength());

            using JsonDocument rootA = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "RootA"), ("IncludeConnectors", "false")));
            using JsonDocument rootB = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "RootB"), ("IncludeConnectors", "false")));
            using JsonDocument rootC = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "RootC"), ("IncludeConnectors", "false")));

            Assert.AreEqual(120, rootA.RootElement[0].GetProperty("X").GetInt32());
            Assert.AreEqual(220, rootB.RootElement[0].GetProperty("X").GetInt32());
            Assert.AreEqual(320, rootC.RootElement[0].GetProperty("X").GetInt32());
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task BugReview_DuplicateConnectorAttachmentScript_CanBeRunThroughRuntimeControl()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("FlowSharp end-to-end tests require Windows.");
            }

            using FlowSharpAppSession session = await FlowSharpAppSession.StartAsync();
            using JsonDocument macroResults = await RunBugReviewScriptAsync(session, "05-duplicate-connector-attachment.flow");

            AssertMacroSucceeded(macroResults);

            using JsonDocument inspect = JsonDocument.Parse(GetMacroStepResponse(macroResults, 8));
            JsonElement serviceNode = inspect.RootElement[0];
            Assert.AreEqual("DockPanelSuiteServices", serviceNode.GetProperty("ParentName").GetString());
            Assert.AreEqual(1, serviceNode.GetProperty("ConnectionCount").GetInt32());
            Assert.AreEqual(1, serviceNode.GetProperty("DistinctConnectionCount").GetInt32());

            using JsonDocument shapes = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "ServiceNode"), ("IncludeConnectors", "false")));
            Assert.AreEqual(310, shapes.RootElement[0].GetProperty("X").GetInt32());
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task BugReview_GroupMoveAndAutoGroupScript_CanBeRunThroughRuntimeControl()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("FlowSharp end-to-end tests require Windows.");
            }

            using FlowSharpAppSession session = await FlowSharpAppSession.StartAsync();
            using JsonDocument macroResults = await RunBugReviewScriptAsync(session, "06-group-move-and-autogroup.flow");

            AssertMacroSucceeded(macroResults);

            using JsonDocument childInspect = JsonDocument.Parse(GetMacroStepResponse(macroResults, 7));
            using JsonDocument groupInspect = JsonDocument.Parse(GetMacroStepResponse(macroResults, 8));
            JsonElement child = childInspect.RootElement[0];
            JsonElement group = groupInspect.RootElement[0];

            Assert.AreEqual("GroupRoot", child.GetProperty("ParentName").GetString());
            Assert.AreEqual(1, child.GetProperty("ConnectionCount").GetInt32());
            Assert.AreEqual(1, child.GetProperty("DistinctConnectionCount").GetInt32());
            Assert.AreEqual(1, group.GetProperty("GroupChildCount").GetInt32());

            using JsonDocument shapes = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "GroupedChild"), ("IncludeConnectors", "false")));
            Assert.AreEqual(210, shapes.RootElement[0].GetProperty("X").GetInt32());
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task BugReview_GroupedCopyPasteScript_CanBeRunThroughRuntimeControl()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("FlowSharp end-to-end tests require Windows.");
            }

            using FlowSharpAppSession session = await FlowSharpAppSession.StartAsync();
            using JsonDocument macroResults = await RunBugReviewScriptAsync(session, "07-grouped-copy-paste.flow");

            AssertMacroSucceeded(macroResults);

            using JsonDocument selection = JsonDocument.Parse(GetMacroStepResponse(macroResults, 11));
            Assert.AreEqual(1, selection.RootElement.GetArrayLength());

            using JsonDocument groupRoots = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "GroupRoot"), ("IncludeConnectors", "false"), ("All", "true")));
            using JsonDocument groupedChildren = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "listshapes"), ("Name", "GroupedChild"), ("IncludeConnectors", "false"), ("All", "true")));

            CollectionAssert.AreEquivalent(new[] { 40, 300 }, groupRoots.RootElement.EnumerateArray().Select(e => e.GetProperty("X").GetInt32()).ToArray());
            CollectionAssert.AreEquivalent(new[] { 90, 350 }, groupedChildren.RootElement.EnumerateArray().Select(e => e.GetProperty("X").GetInt32()).ToArray());

            using JsonDocument finalSelection = JsonDocument.Parse(await session.SendHttpAsync(("cmd", "getselection")));
            Assert.AreEqual(1, finalSelection.RootElement.GetArrayLength());
        }

        [TestMethod]
        [Timeout(120000)]
        public async Task BugReview_RuntimeFeatureSurfacesScript_CanBeRunThroughRuntimeControl()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("FlowSharp end-to-end tests require Windows.");
            }

            string printPath = GetBugReviewArtifactPath("flowsharp-runtime-feature-surfaces-print.png");
            string diagramPath = GetBugReviewArtifactPath("flowsharp-runtime-feature-surfaces.fsd");
            string layoutPath = GetBugReviewArtifactPath("flowsharp-runtime-feature-surfaces-layout.xml");
            CleanupFiles(printPath, diagramPath, layoutPath);

            try
            {
                using FlowSharpAppSession session = await FlowSharpAppSession.StartAsync();
                using JsonDocument macroResults = await RunBugReviewScriptAsync(session, "08-runtime-feature-surfaces.flow");

                AssertMacroSucceeded(macroResults);

                using JsonDocument textInspect = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=TextBox "));
                JsonElement textProperties = textInspect.RootElement[0].GetProperty("Properties");
                Assert.AreEqual("20,15,130,70", textProperties.GetProperty("TextBounds").GetString());
                Assert.AreEqual("Justify", textProperties.GetProperty("ParagraphJustification").GetString());

                using JsonDocument flowInspect = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=Flow "));
                JsonElement flow = flowInspect.RootElement[0];
                Assert.AreEqual("DynamicConnectorLR", flow.GetProperty("Type").GetString());
                Assert.AreEqual("12,-8", flow.GetProperty("Properties").GetProperty("LabelOffset").GetString());

                using JsonDocument aligned = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "listshapes Name=AlignB "));
                Assert.AreEqual(100, aligned.RootElement[0].GetProperty("X").GetInt32());

                using JsonDocument snapped = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "listshapes Name=SnapMoving "));
                Assert.AreEqual(150, snapped.RootElement[0].GetProperty("X").GetInt32());

                using JsonDocument customPoints = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=CustomPointBox ", 1));
                JsonElement customPoint = customPoints.RootElement[0].GetProperty("ConnectionPoints").EnumerateArray().First(p => p.GetProperty("IsCustom").GetBoolean());
                Assert.AreEqual(60, customPoint.GetProperty("X").GetInt32());
                Assert.AreEqual(40, customPoint.GetProperty("Y").GetInt32());

                using JsonDocument resizedCustomPoints = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=CustomPointBox ", 2));
                JsonElement resizedCustomPoint = resizedCustomPoints.RootElement[0].GetProperty("ConnectionPoints").EnumerateArray().First(p => p.GetProperty("IsCustom").GetBoolean());
                Assert.AreEqual(110, resizedCustomPoint.GetProperty("X").GetInt32());
                Assert.AreEqual(60, resizedCustomPoint.GetProperty("Y").GetInt32());

                using JsonDocument capLine = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=CapLine "));
                Assert.AreEqual("Square", capLine.RootElement[0].GetProperty("Properties").GetProperty("StartCap").GetString());
                Assert.AreEqual("Round", capLine.RootElement[0].GetProperty("Properties").GetProperty("EndCap").GetString());

                using JsonDocument upDown = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=UpDownCandidate"));
                Assert.AreEqual("DynamicConnectorUD", upDown.RootElement[0].GetProperty("Type").GetString());

                using JsonDocument removeResult = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "removediagonalconnectors"));
                Assert.AreEqual(1, removeResult.RootElement.GetProperty("Count").GetInt32());

                using JsonDocument rotateAfter = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=RotateBox ", 1));
                Assert.AreEqual("30", rotateAfter.RootElement[0].GetProperty("Properties").GetProperty("RotationAngle").GetString());
                using JsonDocument rotateUndo = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=RotateBox ", 2));
                Assert.AreEqual("0", rotateUndo.RootElement[0].GetProperty("Properties").GetProperty("RotationAngle").GetString());
                using JsonDocument rotateRedo = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=RotateBox ", 3));
                Assert.AreEqual("30", rotateRedo.RootElement[0].GetProperty("Properties").GetProperty("RotationAngle").GetString());

                using JsonDocument regrouped = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=RegroupBox"));
                Assert.AreEqual(2, regrouped.RootElement[0].GetProperty("GroupChildCount").GetInt32());

                using JsonDocument routeSourceAfterMove = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=RouteSource", 2));
                using JsonDocument routeTargetAfterMove = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=RouteTarget", 1));
                Assert.AreEqual("LeftMiddle", routeSourceAfterMove.RootElement[0].GetProperty("Connections")[0].GetProperty("ShapeGrip").GetString());
                Assert.AreEqual("RightMiddle", routeTargetAfterMove.RootElement[0].GetProperty("Connections")[0].GetProperty("ShapeGrip").GetString());

                using JsonDocument routeSourceAfterGeometry = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=RouteSource", 3));
                using JsonDocument routeTargetAfterGeometry = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=RouteTarget", 2));
                Assert.AreEqual("BottomMiddle", routeSourceAfterGeometry.RootElement[0].GetProperty("Connections")[0].GetProperty("ShapeGrip").GetString());
                Assert.AreEqual("TopMiddle", routeTargetAfterGeometry.RootElement[0].GetProperty("Connections")[0].GetProperty("ShapeGrip").GetString());

                using JsonDocument viewBeforeFocus = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "getcanvasview", 1));
                using JsonDocument viewAfterFocus = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "getcanvasview", 2));
                Assert.IsTrue(
                    viewBeforeFocus.RootElement.GetProperty("ViewportOriginX").GetInt32() != viewAfterFocus.RootElement.GetProperty("ViewportOriginX").GetInt32() ||
                    viewBeforeFocus.RootElement.GetProperty("ViewportOriginY").GetInt32() != viewAfterFocus.RootElement.GetProperty("ViewportOriginY").GetInt32(),
                    "ShowShape did not change the viewport origin.");

                using JsonDocument persistBefore = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=PersistBox ", 1));
                using JsonDocument persistAfter = JsonDocument.Parse(GetMacroCommandResponse(macroResults, "inspectshape Name=PersistBox ", 2));
                JsonElement beforeProps = persistBefore.RootElement[0].GetProperty("Properties");
                JsonElement afterProps = persistAfter.RootElement[0].GetProperty("Properties");
                Assert.AreEqual(beforeProps.GetProperty("WordWrap").GetString(), afterProps.GetProperty("WordWrap").GetString());
                Assert.AreEqual(beforeProps.GetProperty("RotationAngle").GetString(), afterProps.GetProperty("RotationAngle").GetString());
                Assert.AreEqual(beforeProps.GetProperty("TextBounds").GetString(), afterProps.GetProperty("TextBounds").GetString());
                Assert.AreEqual(beforeProps.GetProperty("TextMargin").GetString(), afterProps.GetProperty("TextMargin").GetString());
                Assert.AreEqual(beforeProps.GetProperty("ParagraphJustification").GetString(), afterProps.GetProperty("ParagraphJustification").GetString());
                JsonElement persistedCustomPoint = persistAfter.RootElement[0].GetProperty("ConnectionPoints").EnumerateArray().First(p => p.GetProperty("IsCustom").GetBoolean());
                Assert.AreEqual(70, persistedCustomPoint.GetProperty("X").GetInt32());
                Assert.AreEqual(680, persistedCustomPoint.GetProperty("Y").GetInt32());

                Assert.IsTrue(File.Exists(printPath), "Expected print-page render file was not created.");
                Assert.IsTrue(new FileInfo(printPath).Length > 0, "Expected print-page render file to be non-empty.");
                Assert.IsTrue(File.Exists(diagramPath), "Expected persisted diagram file was not created.");
            }
            finally
            {
                CleanupFiles(printPath, diagramPath, layoutPath);
            }
        }

        private static string ToMacroPath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }

        private static async Task<JsonDocument> RunBugReviewScriptAsync(FlowSharpAppSession session, string scriptFileName)
        {
            string scriptPath = Path.Combine(FindSolutionRoot(), "tools", "repl-scripts", "bug-review", scriptFileName);
            Assert.IsTrue(File.Exists(scriptPath), "Bug review script was not found: " + scriptPath);
            string response = await session.SendWebSocketAsync("cmd=runmacro&continueonerror=false&filename=" + Uri.EscapeDataString(scriptPath));
            return JsonDocument.Parse(response);
        }

        private static void AssertMacroSucceeded(JsonDocument macroResults)
        {
            foreach (JsonElement step in macroResults.RootElement.EnumerateArray())
            {
                Assert.IsTrue(step.GetProperty("Success").GetBoolean(), "Macro step failed: " + step);
            }
        }

        private static string GetMacroStepResponse(JsonDocument macroResults, int stepNumber)
        {
            return macroResults.RootElement[stepNumber - 1].GetProperty("Response").GetString();
        }

        private static string GetMacroCommandResponse(JsonDocument macroResults, string commandPrefix, int occurrence = 1)
        {
            int seen = 0;
            foreach (JsonElement step in macroResults.RootElement.EnumerateArray())
            {
                string command = step.GetProperty("Command").GetString();
                if (command != null && command.StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    seen++;
                    if (seen == occurrence)
                    {
                        return step.GetProperty("Response").GetString();
                    }
                }
            }

            Assert.Fail("Macro command was not found: " + commandPrefix + " occurrence " + occurrence + ".");
            return null;
        }

        private static string GetBugReviewArtifactPath(string fileName)
        {
            return Path.Combine(Path.GetTempPath(), fileName);
        }

        private static void CleanupFiles(params string[] paths)
        {
            foreach (string path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch
                {
                }
            }
        }

        private sealed class FlowSharpAppSession : IDisposable
        {
            private readonly HttpClient httpClient;
            private readonly Process process;
            private readonly JobObject jobObject;
            private readonly Uri httpEndpoint;
            private readonly IReadOnlyList<Uri> websocketEndpoints;

            private FlowSharpAppSession(Process process, JobObject jobObject, HttpClient httpClient, Uri httpEndpoint, IReadOnlyList<Uri> websocketEndpoints)
            {
                this.process = process;
                this.jobObject = jobObject;
                this.httpClient = httpClient;
                this.httpEndpoint = httpEndpoint;
                this.websocketEndpoints = websocketEndpoints;
            }

            public static async Task<FlowSharpAppSession> StartAsync()
            {
                string solutionRoot = FindSolutionRoot();
                string appDirectory = Path.Combine(solutionRoot, "bin", "Debug", "net8.0-windows");
                string exePath = Path.Combine(appDirectory, "FlowSharp.exe");
                string moduleFile = Path.Combine(appDirectory, "FlowSharpRuntimeControlModules.xml");

                Assert.IsTrue(File.Exists(exePath), "FlowSharp executable was not found: " + exePath);
                Assert.IsTrue(File.Exists(moduleFile), "FlowSharp module definition was not found: " + moduleFile);

                int restPort = GetFreePort();
                int webSocketPort;

                do
                {
                    webSocketPort = GetFreePort();
                }
                while (webSocketPort == restPort);

                var startInfo = new ProcessStartInfo(exePath, "\"" + moduleFile + "\"")
                {
                    WorkingDirectory = appDirectory,
                    UseShellExecute = false
                };
                startInfo.Environment[RestPortEnvironmentVariable] = restPort.ToString();
                startInfo.Environment[WebSocketPortEnvironmentVariable] = webSocketPort.ToString();
                startInfo.Environment[MacroStepDelayEnvironmentVariable] =
                    Environment.GetEnvironmentVariable(MacroStepDelayEnvironmentVariable) ?? "0";

                Process process = Process.Start(startInfo);
                Assert.IsNotNull(process, "FlowSharp process did not start.");
                var jobObject = JobObject.CreateKillOnClose();
                jobObject.AddProcess(process);

                var session = new FlowSharpAppSession(
                    process,
                    jobObject,
                    new HttpClient { Timeout = TimeSpan.FromSeconds(CommandTimeoutSeconds) },
                    new Uri("http://localhost:" + restPort + "/flowsharp"),
                    new[]
                    {
                        new Uri("ws://localhost:" + webSocketPort + "/flowsharp/"),
                        new Uri("ws://127.0.0.1:" + webSocketPort + "/flowsharp/")
                    });

                await session.WaitForReadyAsync();
                return session;
            }

            public async Task<string> SendHttpAsync(params (string Key, string Value)[] query)
            {
                string queryString = string.Join(
                    "&",
                    query.Select(kvp => Uri.EscapeDataString(kvp.Key) + "=" + Uri.EscapeDataString(kvp.Value ?? string.Empty)));
                using HttpResponseMessage response = await httpClient.GetAsync(httpEndpoint + "?" + queryString);
                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Assert.Fail("HTTP command failed with status " + (int)response.StatusCode + ": " + body);
                }

                return body;
            }

            public async Task SendHttpExpectOkAsync(params (string Key, string Value)[] query)
            {
                string response = await SendHttpAsync(query);
                Assert.AreEqual("OK", response);
            }

            public async Task<string> SendWebSocketAsync(string payload)
            {
                Exception lastException = null;

                foreach (Uri endpoint in websocketEndpoints)
                {
                    try
                    {
                        using var socket = new ClientWebSocket();
                        await socket.ConnectAsync(endpoint, CancellationToken.None);

                        byte[] bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
                        await socket.SendAsync(
                            new ArraySegment<byte>(bytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);

                        string response = await ReceiveTextMessageAsync(socket);
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);

                        return response;
                    }
                    catch (Exception ex) when (ex is WebSocketException || ex is HttpRequestException)
                    {
                        lastException = ex;
                    }
                }

                throw new AssertFailedException("Could not connect to any FlowSharp WebSocket endpoint.", lastException);
            }

            public async Task WaitForCanvasesAsync(int expectedCount)
            {
                await WaitUntilAsync(async () =>
                {
                    using JsonDocument canvases = JsonDocument.Parse(await SendHttpAsync(("cmd", "listcanvases")));
                    return canvases.RootElement.GetArrayLength() == expectedCount;
                }, "Canvas count did not reach " + expectedCount + ".");
            }

            public async Task WaitForFileAsync(string path)
            {
                await WaitUntilAsync(
                    () => Task.FromResult(File.Exists(path) && new FileInfo(path).Length > 0),
                    "File was not created: " + path);
            }

            public void Dispose()
            {
                httpClient.Dispose();

                if (process == null)
                {
                    jobObject?.Dispose();
                    return;
                }

                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000);
                    }
                }
                catch
                {
                }
                finally
                {
                    jobObject?.Dispose();
                    process.Dispose();
                }
            }

            private async Task WaitForReadyAsync()
            {
                await WaitUntilAsync(async () =>
                {
                    if (process.HasExited)
                    {
                        Assert.Fail("FlowSharp exited during startup with code " + process.ExitCode + ".");
                    }

                    try
                    {
                        using HttpResponseMessage response = await httpClient.GetAsync(httpEndpoint + "?cmd=listcanvases");
                        if (!response.IsSuccessStatusCode)
                        {
                            return false;
                        }

                        string body = await response.Content.ReadAsStringAsync();
                        using JsonDocument json = JsonDocument.Parse(body);
                        return json.RootElement.ValueKind == JsonValueKind.Array
                            && json.RootElement.GetArrayLength() > 0
                            && json.RootElement.EnumerateArray().Any(c =>
                                c.TryGetProperty("IsActive", out JsonElement isActive) &&
                                isActive.ValueKind == JsonValueKind.True);
                    }
                    catch
                    {
                        return false;
                    }
                }, "FlowSharp runtime control endpoint did not become ready.");

                await WaitUntilAsync(async () =>
                {
                    foreach (Uri endpoint in websocketEndpoints)
                    {
                        try
                        {
                            using var socket = new ClientWebSocket();
                            await socket.ConnectAsync(endpoint, CancellationToken.None);
                            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "ready", CancellationToken.None);
                            return true;
                        }
                        catch (Exception ex) when (ex is WebSocketException || ex is HttpRequestException)
                        {
                        }
                    }

                    return false;
                }, "FlowSharp WebSocket endpoint did not become ready.");
            }

            private static async Task<string> ReceiveTextMessageAsync(ClientWebSocket socket)
            {
                var buffer = new byte[4096];
                var segment = new ArraySegment<byte>(buffer);
                var builder = new StringBuilder();

                while (true)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(segment, CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return null;
                    }

                    builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        return builder.ToString();
                    }
                }
            }

            private static async Task WaitUntilAsync(Func<Task<bool>> condition, string failureMessage)
            {
                var sw = Stopwatch.StartNew();

                while (sw.Elapsed < TimeSpan.FromSeconds(StartupTimeoutSeconds))
                {
                    if (await condition())
                    {
                        return;
                    }

                    await Task.Delay(250);
                }

                Assert.Fail(failureMessage);
            }
        }

        private sealed class TempDirectory : IDisposable
        {
            public TempDirectory(string prefix)
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
        }

        private sealed class JobObject : IDisposable
        {
            private IntPtr handle;

            private JobObject(IntPtr handle)
            {
                this.handle = handle;
            }

            public static JobObject CreateKillOnClose()
            {
                IntPtr handle = CreateJobObject(IntPtr.Zero, null);
                if (handle == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create job object.");
                }

                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
                {
                    BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                    {
                        LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                    }
                };

                int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
                IntPtr infoPtr = Marshal.AllocHGlobal(length);

                try
                {
                    Marshal.StructureToPtr(info, infoPtr, false);

                    if (!SetInformationJobObject(handle, JobObjectInfoType.ExtendedLimitInformation, infoPtr, (uint)length))
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not configure job object.");
                    }
                }
                catch
                {
                    CloseHandle(handle);
                    throw;
                }
                finally
                {
                    Marshal.FreeHGlobal(infoPtr);
                }

                return new JobObject(handle);
            }

            public void AddProcess(Process process)
            {
                if (!AssignProcessToJobObject(handle, process.Handle))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not assign process to job object.");
                }
            }

            public void Dispose()
            {
                if (handle == IntPtr.Zero)
                {
                    return;
                }

                CloseHandle(handle);
                handle = IntPtr.Zero;
            }

            private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

            private enum JobObjectInfoType
            {
                ExtendedLimitInformation = 9
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct IO_COUNTERS
            {
                public ulong ReadOperationCount;
                public ulong WriteOperationCount;
                public ulong OtherOperationCount;
                public ulong ReadTransferCount;
                public ulong WriteTransferCount;
                public ulong OtherTransferCount;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                public long PerProcessUserTimeLimit;
                public long PerJobUserTimeLimit;
                public uint LimitFlags;
                public UIntPtr MinimumWorkingSetSize;
                public UIntPtr MaximumWorkingSetSize;
                public uint ActiveProcessLimit;
                public UIntPtr Affinity;
                public uint PriorityClass;
                public uint SchedulingClass;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
                public IO_COUNTERS IoInfo;
                public UIntPtr ProcessMemoryLimit;
                public UIntPtr JobMemoryLimit;
                public UIntPtr PeakProcessMemoryUsed;
                public UIntPtr PeakJobMemoryUsed;
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType jobObjectInfoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool CloseHandle(IntPtr handle);
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
                    if (File.Exists(System.IO.Path.Combine(dirInfo.FullName, "FlowSharp.sln")))
                    {
                        return dirInfo.FullName;
                    }

                    dirInfo = dirInfo.Parent;
                }
            }

            throw new InvalidOperationException("Could not locate FlowSharp solution root.");
        }

        private static int GetFreePort()
        {
            var probe = new TcpListener(System.Net.IPAddress.Loopback, 0);
            probe.Start();
            int port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            return port;
        }
    }
}
