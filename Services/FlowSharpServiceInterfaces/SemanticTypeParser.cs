using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Clifton.Core.Semantics;

namespace FlowSharpServiceInterfaces
{
    public static class SemanticTypeParser
    {
        private static readonly Dictionary<string, string> CommandAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { CanonicalizeCommandKey("dropshape"), "CmdDropShape" },
            { CanonicalizeCommandKey("showshape"), "CmdShowShape" },
            { CanonicalizeCommandKey("getshapefiles"), "CmdGetShapeFiles" },
            { CanonicalizeCommandKey("outputmessage"), "CmdOutputMessage" },
            { CanonicalizeCommandKey("dropconnector"), "CmdDropConnector" },
            { CanonicalizeCommandKey("clearcanvas"), "CmdClearCanvas" },
            { CanonicalizeCommandKey("cleancanvas"), "CmdClearCanvas" },
            { CanonicalizeCommandKey("loaddiagram"), "CmdLoadDiagram" },
            { CanonicalizeCommandKey("newcanvas"), "CmdNewCanvas" },
            { CanonicalizeCommandKey("listcanvases"), "CmdListCanvases" },
            { CanonicalizeCommandKey("usecanvas"), "CmdUseCanvas" },
            { CanonicalizeCommandKey("saveworkspace"), "CmdSaveWorkspace" },
            { CanonicalizeCommandKey("savediagrams"), "CmdSaveWorkspace" },
            { CanonicalizeCommandKey("saveas"), "CmdSaveWorkspace" },
            { CanonicalizeCommandKey("save"), "CmdSaveWorkspace" },
            { CanonicalizeCommandKey("exportpng"), "CmdExportPng" },
            { CanonicalizeCommandKey("deleteshape"), "CmdDeleteShape" },
            { CanonicalizeCommandKey("moveshape"), "CmdMoveShape" },
            { CanonicalizeCommandKey("connectshapes"), "CmdConnectShapes" },
            { CanonicalizeCommandKey("listshapes"), "CmdListShapes" },
            { CanonicalizeCommandKey("selectshapes"), "CmdSelectShapes" },
            { CanonicalizeCommandKey("selectregion"), "CmdSelectRegion" },
            { CanonicalizeCommandKey("getselection"), "CmdGetSelection" },
            { CanonicalizeCommandKey("moveselection"), "CmdMoveSelection" },
            { CanonicalizeCommandKey("copyselection"), "CmdCopySelection" },
            { CanonicalizeCommandKey("copy"), "CmdCopySelection" },
            { CanonicalizeCommandKey("pasteclipboard"), "CmdPasteClipboard" },
            { CanonicalizeCommandKey("paste"), "CmdPasteClipboard" },
            { CanonicalizeCommandKey("deleteselection"), "CmdDeleteSelection" },
            { CanonicalizeCommandKey("groupselection"), "CmdGroupSelection" },
            { CanonicalizeCommandKey("group"), "CmdGroupSelection" },
            { CanonicalizeCommandKey("ungroupselection"), "CmdUngroupSelection" },
            { CanonicalizeCommandKey("ungroup"), "CmdUngroupSelection" },
            { CanonicalizeCommandKey("inspectshape"), "CmdInspectShape" },
            { CanonicalizeCommandKey("undo"), "CmdUndo" },
            { CanonicalizeCommandKey("redo"), "CmdRedo" },
            { CanonicalizeCommandKey("runmacro"), "CmdRunMacro" },
        };

        public static string NormalizeCommandName(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
            {
                return cmd;
            }

            var ret = cmd.Trim();

            if (ret.StartsWith("cmd=", StringComparison.OrdinalIgnoreCase))
            {
                ret = ret.Substring(4);
            }

            if (CommandAliases.TryGetValue(CanonicalizeCommandKey(ret), out var alias))
            {
                return alias;
            }

            ret = RemoveSeparators(ret);

            if (!ret.StartsWith("Cmd", StringComparison.OrdinalIgnoreCase))
            {
                ret = "Cmd" + char.ToUpperInvariant(ret[0]) + ret.Substring(1);
            }
            else if (ret.Length > 3)
            {
                ret = "Cmd" + ret.Substring(3);
            }

            return ret;
        }

        public static Type GetCommandType(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
            {
                return null;
            }

            string cmdType = NormalizeCommandName(cmd);
            return Type.GetType("FlowSharpServiceInterfaces." + cmdType + ",FlowSharpServiceInterfaces", false);
        }

        public static ISemanticType NewCommand(string cmd)
        {
            var stype = GetCommandType(cmd);

            if (stype == null)
            {
                return null;
            }

            return Activator.CreateInstance(stype) as ISemanticType;
        }

        public static ISemanticType NewCommandOrThrow(string cmd)
        {
            var stype = GetCommandType(cmd);

            if (stype == null)
            {
                throw new InvalidOperationException($"Unknown command '{cmd}'.");
            }

            return Activator.CreateInstance(stype) as ISemanticType;
        }

        public static void PopulateType(ISemanticType packet, IDictionary<string, string> data)
        {
            foreach (var key in data.Keys)
            {
                var pi = packet.GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi == null || !pi.CanWrite)
                {
                    continue;
                }

                try
                {
                    var value = DecodeData(data[key]);
                    var ptype = pi.PropertyType;
                    var converted = ConvertValue(ptype, value);
                    pi.SetValue(packet, converted);
                }
                catch
                {
                    // Ignore bad values to preserve transport behavior and avoid hard failures from user input.
                }
            }
        }

        public static IDictionary<string, string> ToDictionary(System.Collections.Specialized.NameValueCollection nvc)
        {
            return nvc.AllKeys.Where(k => !string.IsNullOrWhiteSpace(k)).ToDictionary(k => k, k => nvc[k], StringComparer.OrdinalIgnoreCase);
        }

        public static IDictionary<string, string> ToDictionary(Dictionary<string, string> values)
        {
            return values.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        private static string DecodeData(string value)
        {
            if (value == null)
            {
                return null;
            }

            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }

        private static object ConvertValue(Type targetType, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (IsNullable(targetType))
                {
                    return null;
                }

                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            var convertedType = GetConvertedType(targetType);

            if (convertedType.IsEnum)
            {
                return Enum.Parse(convertedType, value, true);
            }

            if (convertedType == typeof(Guid))
            {
                return Guid.Parse(value);
            }

            if (convertedType == typeof(bool))
            {
                return bool.Parse(value);
            }

            if (convertedType == typeof(int))
            {
                return int.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
            }

            if (convertedType == typeof(double))
            {
                return double.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
            }

            if (convertedType == typeof(decimal))
            {
                return decimal.Parse(value, NumberStyles.Any, CultureInfo.InvariantCulture);
            }

            return Convert.ChangeType(value, convertedType, CultureInfo.InvariantCulture);
        }

        private static Type GetConvertedType(Type ptype)
        {
            if (ptype.IsGenericType && ptype.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                ptype = ptype.GenericTypeArguments[0];
            }

            return ptype;
        }

        private static bool IsNullable(Type type)
        {
            if (!type.IsGenericType)
            {
                return false;
            }

            return type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        private static string CanonicalizeCommandKey(string value)
        {
            return RemoveSeparators(value).ToLowerInvariant();
        }

        private static string RemoveSeparators(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return new string(value.Where(char.IsLetterOrDigit).ToArray());
        }
    }
}
