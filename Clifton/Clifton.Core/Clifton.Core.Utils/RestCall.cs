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
using System.Net.Http;
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
            using (var client = new HttpClient())
            {
                return client.GetStringAsync(url).GetAwaiter().GetResult();
            }
        }

        public static R Post<R>(string url, object obj)
        {
            var target = Activator.CreateInstance<R>();
            var json = JsonConvert.SerializeObject(obj);

            using (var client = new HttpClient())
            {
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    var response = client.PostAsync(url, content).GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    var retjson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (!string.IsNullOrWhiteSpace(retjson))
                    {
                        JObject jobj = JObject.Parse(retjson);
                        JsonConvert.PopulateObject(jobj.ToString(), target);
                    }
                }
            }

            return target;
        }
    }
}