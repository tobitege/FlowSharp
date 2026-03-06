using System;
using System.Globalization;

namespace FlowSharpServiceInterfaces
{
    public static class RuntimeControlConfiguration
    {
        public const string RestPortEnvironmentVariable = "FLOWSHARP_REST_PORT";
        public const string WebSocketPortEnvironmentVariable = "FLOWSHARP_WEBSOCKET_PORT";
        public const string MacroStepDelayEnvironmentVariable = "FLOWSHARP_MACRO_STEP_DELAY_MS";

        public static int GetRestPort(int defaultPort = 8001)
        {
            return GetPortFromEnvironment(RestPortEnvironmentVariable, defaultPort);
        }

        public static int GetWebSocketPort(int defaultPort = 1100)
        {
            return GetPortFromEnvironment(WebSocketPortEnvironmentVariable, defaultPort);
        }

        public static int GetMacroStepDelayMilliseconds(int defaultDelayMilliseconds = 500)
        {
            return GetNonNegativeIntegerFromEnvironment(MacroStepDelayEnvironmentVariable, defaultDelayMilliseconds);
        }

        public static int GetPortFromEnvironment(string environmentVariable, int defaultPort)
        {
            string raw = Environment.GetEnvironmentVariable(environmentVariable);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultPort;
            }

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port) && port > 0
                ? port
                : defaultPort;
        }

        public static int GetNonNegativeIntegerFromEnvironment(string environmentVariable, int defaultValue)
        {
            string raw = Environment.GetEnvironmentVariable(environmentVariable);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 0
                ? value
                : defaultValue;
        }
    }
}
