using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Json;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.Jolokia {
    /// <summary>
    /// A monitor for the JMX REST API Jolokia 1.5.0. Add available counters in JolokiaCountersMap.txt.
    /// </summary>
    public class JolokiaClient : BasePollingClient {
        private string _hostname, _relUrl, _username, _password;
        private int _port;
        private bool _ssl;

        private bool _connected = false;

        /// <summary>
        /// key = full path (mbean + "." + attribute + "/" + path), value = json object + "type":"read" primitives.
        /// </summary>
        private Dictionary<string, JsonObject> _wihJObjects = new Dictionary<string, JsonObject>();


        public override string DecimalSeparator { get { return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; } }

        public override bool IsConnected { get { return _connected; } }

        public override int RefreshCountersInterval { get { return 3000; } }

        public override string Config {
            get {
                return "<NotReallyAConfig></NotReallyAConfig>";
            }
        }

        public List<CounterInfo> GetCountersForEntity() {
            var infos = new List<CounterInfo>();
            string countersMapFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "JolokiaCountersMap.txt");

            //Parsing json. Keeping json in memory and mapping to counter infos.

            if (File.Exists(countersMapFile)) {
                var lines = new HashSet<string>();

                using (var sr = new StreamReader(countersMapFile)) {
                    while (sr.Peek() != -1) {
                        string line = sr.ReadLine().Trim();
                        if (line.StartsWith("{")) {
                            JsonObject joCandidate = null;
                            try {
                                joCandidate = (JsonObject)JsonValue.Parse(line);
                            }
                            catch { }

                            if (joCandidate != null && lines.Add(line)) {
                                //Construct json objects to be posted in an array to jolokia when requesting counters.
                                var jo = new JsonObject();
                                if (!jo.ContainsKey("type"))
                                    jo.Add("type", new JsonPrimitive("read"));

                                foreach (string key in joCandidate.Keys) {
                                    JsonValue value;
                                    joCandidate.TryGetValue(key, out value);
                                    jo.Add(key, value);
                                }

                                JsonValue mbean, attribute, path;
                                if (jo.TryGetValue("mbean", out mbean) && jo.TryGetValue("attribute", out attribute)) {
                                    string fullPath = mbean + "." + attribute;
                                    if (jo.TryGetValue("path", out path)) fullPath += "/" + path;
                                    infos.Add(new CounterInfo(fullPath));
                                    _wihJObjects.Add(fullPath, jo);
                                }
                            }
                        }
                    }
                }
            }

            return infos;
        }
        public override Entities WDYH {
            get {
                if (base._wdyh == null) {
                    Entities dic = new Entities();

                    var entity = new Entity(_hostname + ':' + _port, true);
                    entity.subs = GetCountersForEntity();
                    dic.GetSubs().Add(entity);

                    base._wdyh = dic;
                }
                return base._wdyh;
            }
        }

        public JolokiaClient() {
            base._parameters = new Parameter[] {
                new Parameter() { Name = "SSL", DefaultValue = false },
                new Parameter() { Name = "Hostname", DefaultValue = "" },
                new Parameter() { Name = "Port", DefaultValue = 8778 },
                new Parameter() { Name = "Relative URL", DefaultValue = "/jolokia/" },
                new Parameter() { Name = "Username", Description = "For basic authentication", DefaultValue = "", Optional = true },
                new Parameter() { Name = "Password", Description = "For basic authentication", DefaultValue = "", Encrypted = true, Optional = true }
           };
        }

        public override bool Connect() {
            _ssl = (bool)base.GetParameter("SSL").Value;
            _hostname = (string)base.GetParameter("Hostname").Value;
            _port = (int)base.GetParameter("Port").Value;
            _relUrl = (string)base.GetParameter("Relative URL").Value;

            if (!_relUrl.StartsWith("/")) {
                _relUrl = "/" + _relUrl;
            }
            if (!_relUrl.EndsWith("/")) {
                _relUrl += "/";
            }

            _username = (string)base.GetParameter("Username").Value;
            _password = (string)base.GetParameter("Password").Value;

            try {
                string url = (_ssl ? "https://" : "http://") + _hostname + ":" + _port + _relUrl;
                HttpWebRequest req = WebRequest.CreateHttp(url);
                if (_username.Length != 0) {
                    string encoded = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(_username + ":" + _password));
                    req.Headers.Add("Authorization", "Basic " + encoded);
                }
                req.Method = "GET";
                req.Timeout = 5000;

                req.Proxy = null;

                using (HttpWebResponse res = req.GetResponse() as HttpWebResponse) {
                    string encoding = res.ContentEncoding.ToLower();

                    StreamReader sr = null;
                    if (encoding.Contains("gzip")) {
                        sr = new StreamReader(new GZipStream(res.GetResponseStream(), CompressionMode.Decompress));
                    }
                    else if (encoding.Contains("deflate")) {
                        sr = new StreamReader(new DeflateStream(res.GetResponseStream(), CompressionMode.Decompress));
                    }
                    else {
                        sr = new StreamReader(res.GetResponseStream(), encoding.Length != 0 ? Encoding.GetEncoding(encoding) : Encoding.GetEncoding(1252));
                    }

                    using (sr) {
                        JsonValue.Parse(sr.ReadToEnd());
                    }
                }

                _connected = true;
            }
            catch {
                _connected = false;
            }

            return _connected;
        }

        public override bool Disconnect() {
            try {
                Stop();
                return true;
            }
            catch (Exception) {
                return false;
            }
        }

        protected override Entities PollCounters() {
            if (base._wiwWithCounters == null)
                base._wiwWithCounters = base._wiw.Clone();

            Entity e = base._wiwWithCounters.GetSubs()[0];

            //Construct and post the wiw json array.

            var arrToPost = new JsonArray();

            var counters = new Dictionary<string, CounterInfo>();
            foreach (CounterInfo ci in e.GetSubs()) {
                counters.Add(ci.name, ci);
                arrToPost.Add(_wihJObjects[ci.name]);
            }

            JsonValue jv = GetJsonValue(arrToPost);

            var responseArray = new JsonArray();

            if (jv is JsonArray) {
                responseArray = jv as JsonArray;
            }
            else {
                responseArray.Add(jv);
            }

            //Put respone values in wih.

            foreach (JsonObject jo in responseArray) {
                JsonValue req;
                if (jo.TryGetValue("request", out req)) {
                    JsonObject request = req as JsonObject;

                    JsonValue mbean, attribute, path;
                    if (request.TryGetValue("mbean", out mbean) && request.TryGetValue("attribute", out attribute)) {
                        string fullPath = mbean + "." + attribute;
                        if (request.TryGetValue("path", out path)) fullPath += "/" + path;

                        JsonValue primitive;
                        jo.TryGetValue("value", out primitive);

                        if (primitive == null) {
                            counters[fullPath].SetCounter(-1);
                        }
                        else {
                            string value = primitive.ToString();
                            if (primitive.JsonType == JsonType.Number) {
                                counters[fullPath].SetCounter(double.Parse(value));
                            }
                            else if (primitive.JsonType == JsonType.Boolean) {
                                counters[fullPath].SetCounter(bool.Parse(value));
                            }
                            else {
                                counters[fullPath].SetCounter(value);
                            }
                        }
                    }
                }
            }



            base._wiwWithCounters.SetTimestamp();
            return base._wiwWithCounters;
        }

        private JsonValue GetJsonValue(JsonArray arr) {
            string url = (_ssl ? "https://" : "http://") + _hostname + ":" + _port + _relUrl;
            HttpWebRequest req = WebRequest.CreateHttp(url);
            if (_username.Length != 0) {
                string encoded = Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes(_username + ":" + _password));
                req.Headers.Add("Authorization", "Basic " + encoded);
            }

            string postData = arr.ToString();

            req.Method = "POST";
            req.ContentLength = Encoding.UTF8.GetByteCount(postData);
            req.ContentType = "text/plain";
            req.Timeout = 5000;
            req.Proxy = null;

            using (Stream postStream = req.GetRequestStream()) {
                using (var postStreamWriter = new StreamWriter(postStream)) {
                    postStreamWriter.Write(postData);
                    postStreamWriter.Flush();
                }
            }

            using (HttpWebResponse res = req.GetResponse() as HttpWebResponse) {
                string encoding = res.ContentEncoding.ToLower();

                StreamReader sr = null;
                if (encoding.Contains("gzip")) {
                    sr = new StreamReader(new GZipStream(res.GetResponseStream(), CompressionMode.Decompress));
                }
                else if (encoding.Contains("deflate")) {
                    sr = new StreamReader(new DeflateStream(res.GetResponseStream(), CompressionMode.Decompress));
                }
                else {
                    sr = new StreamReader(res.GetResponseStream(), encoding.Length != 0 ? Encoding.GetEncoding(encoding) : Encoding.GetEncoding(1252));
                }

                using (sr) {
                    return JsonValue.Parse(sr.ReadToEnd());
                }
            }
        }
    }
}
