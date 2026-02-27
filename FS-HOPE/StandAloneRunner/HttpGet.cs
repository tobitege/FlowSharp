/* 
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.Net.Http;

namespace FlowSharpRestService
{
    public static class Http
    {
        public static string Get(string uri)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("FlowSharp/1.0");

                return client.GetStringAsync(uri).GetAwaiter().GetResult();
            }
        }
    }
}