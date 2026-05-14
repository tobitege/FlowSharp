using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpServiceInterfaces;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class SemanticTypeParserTests
    {
        [TestMethod]
        public void NormalizeCommandName_MapsClearCanvasAliasToExpectedTypeName()
        {
            string commandName = SemanticTypeParser.NormalizeCommandName("clearcanvas");

            Assert.AreEqual("CmdClearCanvas", commandName);
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("clearcanvas"), typeof(CmdClearCanvas));
        }

        [TestMethod]
        public void PopulateType_ConvertsBooleanIntegerAndDecodedTextValues()
        {
            var command = new CmdMoveShape();
            var values = new Dictionary<string, string>
            {
                ["Dx"] = "12",
                ["Dy"] = "-4",
                ["Relative"] = "false",
                ["Text"] = "Hello+FlowSharp"
            };

            SemanticTypeParser.PopulateType(command, values);

            Assert.AreEqual(12, command.Dx);
            Assert.AreEqual(-4, command.Dy);
            Assert.IsFalse(command.Relative);
            Assert.AreEqual("Hello FlowSharp", command.Text);
        }

        [TestMethod]
        public void CmdListShapesSerializeResponse_ReturnsEmptyArrayWhenUnset()
        {
            var command = new CmdListShapes();

            Assert.AreEqual("[]", command.SerializeResponse());
        }

        [TestMethod]
        public void NormalizeCommandName_AllowsDashedCanvasAlias()
        {
            string commandName = SemanticTypeParser.NormalizeCommandName("list-canvases");

            Assert.AreEqual("CmdListCanvases", commandName);
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("list_canvases"), typeof(CmdListCanvases));
        }

        [TestMethod]
        public void NormalizeCommandName_MapsShortClipboardAliases()
        {
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("copy"), typeof(CmdCopySelection));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("paste"), typeof(CmdPasteClipboard));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("export-png"), typeof(CmdExportPng));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("getviewport"), typeof(CmdGetCanvasView));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("setzoom"), typeof(CmdSetZoom));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("setviewport"), typeof(CmdSetCanvasOffset));
        }

        [TestMethod]
        public void NormalizeCommandName_MapsRuntimeFeatureSurfaceAliases()
        {
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("setproperty"), typeof(CmdSetShapeProperty));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("renderprintpage"), typeof(CmdRenderPrintPage));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("dragselection"), typeof(CmdDragSelection));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("align"), typeof(CmdAlignSelection));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("rotate"), typeof(CmdRotateSelection));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("convertconnector"), typeof(CmdConvertConnector));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("removediagonalconnectors"), typeof(CmdRemoveDiagonalConnectors));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("setcustomconnectionpoints"), typeof(CmdSetCustomConnectionPoints));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("regroupselection"), typeof(CmdRegroupSelection));
        }

        [TestMethod]
        public void NormalizeCommandName_ResolvesKnownCommandTypesWithoutExplicitAlias()
        {
            Assert.AreEqual("CmdUpdateProperty", SemanticTypeParser.NormalizeCommandName("updateproperty"));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("updateproperty"), typeof(CmdUpdateProperty));
            Assert.IsInstanceOfType(SemanticTypeParser.NewCommand("CmdUpdateproperty"), typeof(CmdUpdateProperty));
        }

        [TestMethod]
        public void SerializeResponse_NewVerificationCommands_DefaultToEmptyArray()
        {
            Assert.AreEqual("[]", new CmdListCanvases().SerializeResponse());
            Assert.AreEqual("[]", new CmdGetSelection().SerializeResponse());
            Assert.AreEqual("[]", new CmdInspectShape().SerializeResponse());
        }
    }
}
