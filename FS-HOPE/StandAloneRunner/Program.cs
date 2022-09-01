using System;
using System.Drawing;
using System.Windows.Forms;

using Newtonsoft.Json;

using Clifton.Core.Semantics;
using Clifton.Core.Services.SemanticProcessorService;

using FlowSharpHopeCommon;
using FlowSharpRestService;

namespace StandAloneRunner
{
    /// <summary>
    /// For testing the FlowSharpHopeService.StandAloneRunner.
    /// Note that this project references a DLL built by FlowSharpHopeService.
    /// </summary>
    class Program
    {
        public static TextBox tbLog;
        public static Form form;
		public static SemanticProcessor sp;

        private const string url = "http://localhost:5002/";
        protected const string PROCESSING = "processing";

		[STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

			sp = new SemanticProcessor();
			sp.Processing += Processing;

            var routeHandlers = new RouteHandlers(sp);
            var webServer = new WebServer(routeHandlers);
            webServer.Start("localhost", new int[] { 5001 });

            form = new Form
            {
                Location = new Point(100, 100),
                Size = new Size(500, 200),
                Text = "Stand Alone Runner"
            };

            tbLog = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill
            };
            form.Controls.Add(tbLog);

            Application.Run(form);
        }

		private static void Processing(object sender, ProcessEventArgs args)
		{
            var json = JsonConvert.SerializeObject(new
			{
				FromMembraneTypeName = args.FromMembrane?.GetType()?.FullName,
				FromReceptorTypeName = args.FromReceptor?.GetType()?.FullName,
				ToMembraneTypeName   = args.ToMembrane.GetType().FullName,
				ToReceptorTypeName   = args.ToReceptor.GetType().FullName,
				SemanticTypeTypeName = args.SemanticType.GetType().FullName,
			});

			Http.Get(url + PROCESSING + "?proc=" + json);
		}
	}
}
