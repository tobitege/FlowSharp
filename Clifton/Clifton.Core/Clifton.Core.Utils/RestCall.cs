/* The MIT License (MIT)
*
* Copyright (c) 2015 Marc Clifton
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
// ReSharper disable CheckNamespace

namespace Clifton.Core.Utils
{
    public static class RestCall
    {
        public static string Get(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";

            var ret = "";
            var resp = request.GetResponse();
            var respStream = resp?.GetResponseStream();
            if (respStream != null)
            {
                ret = new StreamReader(respStream).ReadToEnd();
            }
            resp.Close();

            return ret;
        }

        public static R Post<R>(string url, object obj)
        {
            var target = Activator.CreateInstance<R>();
            var json = JsonConvert.SerializeObject(obj);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = json.Length;

            var st = request.GetRequestStream();
            var bytes = Encoding.UTF8.GetBytes(json);
            st.Write(bytes, 0, bytes.Length);
            var resp = request.GetResponse();
            var respStream = resp?.GetResponseStream();
            if (respStream != null)
            {
                var retjson = new StreamReader(respStream).ReadToEnd();
                JObject jobj = JObject.Parse(retjson);
                JsonConvert.PopulateObject(jobj.ToString(), target);
            }
            resp.Close();
            st.Close();

            return target;
        }
    }
}