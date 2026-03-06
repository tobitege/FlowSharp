using Microsoft.VisualStudio.TestTools.UnitTesting;

using FlowSharpServiceInterfaces;

namespace FlowSharp.Main.Tests
{
    [TestClass]
    public class RuntimeControlConfigurationTests
    {
        [TestMethod]
        public void GetMacroStepDelayMilliseconds_DefaultsTo500()
        {
            WithEnvironmentVariable(RuntimeControlConfiguration.MacroStepDelayEnvironmentVariable, null, () =>
            {
                Assert.AreEqual(500, RuntimeControlConfiguration.GetMacroStepDelayMilliseconds());
            });
        }

        [TestMethod]
        public void GetMacroStepDelayMilliseconds_AllowsZero()
        {
            WithEnvironmentVariable(RuntimeControlConfiguration.MacroStepDelayEnvironmentVariable, "0", () =>
            {
                Assert.AreEqual(0, RuntimeControlConfiguration.GetMacroStepDelayMilliseconds());
            });
        }

        [TestMethod]
        public void GetMacroStepDelayMilliseconds_IgnoresInvalidValues()
        {
            WithEnvironmentVariable(RuntimeControlConfiguration.MacroStepDelayEnvironmentVariable, "-1", () =>
            {
                Assert.AreEqual(500, RuntimeControlConfiguration.GetMacroStepDelayMilliseconds());
            });

            WithEnvironmentVariable(RuntimeControlConfiguration.MacroStepDelayEnvironmentVariable, "abc", () =>
            {
                Assert.AreEqual(500, RuntimeControlConfiguration.GetMacroStepDelayMilliseconds());
            });
        }

        private static void WithEnvironmentVariable(string name, string value, System.Action action)
        {
            string originalValue = System.Environment.GetEnvironmentVariable(name);

            try
            {
                System.Environment.SetEnvironmentVariable(name, value);
                action();
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(name, originalValue);
            }
        }
    }
}
