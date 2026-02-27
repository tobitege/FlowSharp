extern alias RestServiceAlias;
extern alias StandAloneRunnerAlias;
extern alias CodeTesterAlias;

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Clifton.Core.Utils;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FlowSharp.Http.IntegrationTests
{
    [TestClass]
    public class HttpHelpersIntegrationTests
    {
        private const int TimeoutSeconds = 10;

        [TestMethod]
        public async Task RestCallGet_ShouldRoundTripThroughHttpListener()
        {
            using var listener = StartListener(out string prefix);
            Task<string> callTask = Task.Run(() => RestCall.Get(prefix + "restcall/get"));

            HttpListenerContext context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
            Assert.AreEqual("GET", context.Request.HttpMethod);

            await WriteResponseAsync(context.Response, "restcall-get-ok", "text/plain");

            string result = await callTask.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
            Assert.AreEqual("restcall-get-ok", result);
        }

        [TestMethod]
        public async Task RestCallPost_ShouldSendJsonAndDeserializeResponse()
        {
            using var listener = StartListener(out string prefix);
            var payload = new PostRequestPayload { Name = "Ada", Count = 3 };
            Task<PostResponsePayload> callTask = Task.Run(() => RestCall.Post<PostResponsePayload>(prefix + "restcall/post", payload));

            HttpListenerContext context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
            Assert.AreEqual("POST", context.Request.HttpMethod);
            StringAssert.Contains(context.Request.ContentType ?? string.Empty, "application/json");

            string requestJson = await ReadBodyAsync(context.Request);
            using (JsonDocument doc = JsonDocument.Parse(requestJson))
            {
                Assert.AreEqual("Ada", doc.RootElement.GetProperty("Name").GetString());
                Assert.AreEqual(3, doc.RootElement.GetProperty("Count").GetInt32());
            }

            await WriteResponseAsync(context.Response, "{\"Status\":\"accepted\",\"Code\":200}", "application/json");

            PostResponsePayload result = await callTask.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
            Assert.IsNotNull(result);
            Assert.AreEqual("accepted", result.Status);
            Assert.AreEqual(200, result.Code);
        }

        [TestMethod]
        public async Task FlowSharpRestServiceHttpGet_ShouldSendUserAgentAndReturnBody()
        {
            using var listener = StartListener(out string prefix);
            Task<string> callTask = Task.Run(() => RestServiceAlias::FlowSharpRestService.Http.Get(prefix + "flowsharp-rest/get"));

            HttpListenerContext context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
            Assert.AreEqual("GET", context.Request.HttpMethod);
            StringAssert.Contains(context.Request.UserAgent ?? string.Empty, "FlowSharp/1.0");

            await WriteResponseAsync(context.Response, "flowsharp-rest-ok", "text/plain");

            string result = await callTask.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
            Assert.AreEqual("flowsharp-rest-ok", result);
        }

        [TestMethod]
        public async Task StandAloneRunnerHttpGet_ShouldSendUserAgentAndReturnBody()
        {
            using var listener = StartListener(out string prefix);
            Task<string> callTask = Task.Run(() => StandAloneRunnerAlias::FlowSharpRestService.Http.Get(prefix + "standalone-runner/get"));

            HttpListenerContext context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
            Assert.AreEqual("GET", context.Request.HttpMethod);
            StringAssert.Contains(context.Request.UserAgent ?? string.Empty, "FlowSharp/1.0");

            await WriteResponseAsync(context.Response, "standalone-runner-ok", "text/plain");

            string result = await callTask.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
            Assert.AreEqual("standalone-runner-ok", result);
        }

        [TestMethod]
        public async Task CodeTesterProgramGet_ShouldSendUserAgentAndReturnBody()
        {
            using var listener = StartListener(out string prefix);
            Task<string> callTask = Task.Run(() => InvokeCodeTesterGet(prefix + "codetester/get"));

            HttpListenerContext context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
            Assert.AreEqual("GET", context.Request.HttpMethod);
            StringAssert.Contains(context.Request.UserAgent ?? string.Empty, "FlowSharp/1.0");

            await WriteResponseAsync(context.Response, "codetester-ok", "text/plain");

            string result = await callTask.WaitAsync(TimeSpan.FromSeconds(TimeoutSeconds));
            Assert.AreEqual("codetester-ok", result);
        }

        private static HttpListener StartListener(out string prefix)
        {
            int port = GetFreePort();
            prefix = "http://127.0.0.1:" + port + "/";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            return listener;
        }

        private static int GetFreePort()
        {
            var probe = new TcpListener(System.Net.IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            return port;
        }

        private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }

        private static async Task WriteResponseAsync(HttpListenerResponse response, string body, string contentType)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(body ?? string.Empty);
            response.StatusCode = 200;
            response.ContentType = contentType;
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

        private static string InvokeCodeTesterGet(string uri)
        {
            Assembly codeTesterAssembly = typeof(CodeTesterAlias::CodeTester.ST_Address).Assembly;
            Type programType = codeTesterAssembly.GetType("CodeTester.Program", throwOnError: true);
            MethodInfo getMethod = programType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);

            if (getMethod == null)
            {
                throw new InvalidOperationException("CodeTester.Program.Get was not found.");
            }

            return (string)getMethod.Invoke(null, new object[] { uri });
        }

        public class PostRequestPayload
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

        public class PostResponsePayload
        {
            public string Status { get; set; }
            public int Code { get; set; }
        }
    }
}
