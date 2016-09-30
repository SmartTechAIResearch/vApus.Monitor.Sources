/*
 * Copyright 2016 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Json;
using System.Net;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.HbaseRegionServer {
    public class HDFSDataNode : BasePollingClient {
        private string _hostname;
        private string _port;

        private JsonValue GetJSONObject() {
            string url = "http://" + _hostname + ":" + _port + "/jmx";
            WebRequest req = WebRequest.CreateHttp(url);
            req.Timeout = 5000;
            req.Proxy = null;
            using (WebResponse res = req.GetResponse()) {
                using (var reader = new StreamReader(res.GetResponseStream()))
                    return JsonObject.Parse(reader.ReadToEnd());
            }
        }

        public override bool IsConnected { get { return true; } }

        public override int RefreshCountersInterval { get { return 5000; } }

        public override string DecimalSeparator { get { return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; } }

        public override string Config {
            get {
                return "<NotReallyAConfig></NotReallyAConfig>";
            }
        }

        public List<CounterInfo> GetCountersForEntity() {
            return new List<CounterInfo> {
                new CounterInfo("GC count"),
                new CounterInfo("GC time millis"),
                new CounterInfo("Used heap bytes"),

                new CounterInfo("Bytes written"),
                new CounterInfo("Bytes read")
            };
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

        public HDFSDataNode() {
            base._parameters = new Parameter[] {
                new Parameter() { Name = "Hostname", DefaultValue = "bdc-01" },
                new Parameter() { Name = "Port", DefaultValue = "50075" }
           };
        }

        public override bool Connect() {
            _hostname = (string)base.GetParameter("Hostname").Value;
            _port = (string)base.GetParameter("Port").Value;


            return true;
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

            var counters = new Dictionary<string, CounterInfo>();
            foreach (CounterInfo ci in e.GetSubs())
                counters.Add(ci.name, ci);


            JsonValue j = GetJSONObject()["beans"];
            for (int i = 0; i != j.Count; i++) {
                var obj = j[i];
                string modelerType = (string)obj["modelerType"];
                switch (modelerType) {
                    case "JvmMetrics":
                        if (counters.ContainsKey("GC count")) counters["GC count"].SetCounter((long)obj["GcCount"]);
                        if (counters.ContainsKey("GC time millis")) counters["GC time millis"].SetCounter((long)obj["GcTimeMillis"]);

                        break;
                    case "sun.management.MemoryImpl":
                        if (counters.ContainsKey("Used heap bytes")) counters["Used heap bytes"].SetCounter((long)obj["HeapMemoryUsage"]["used"]);

                        break;
                }

                if (modelerType.StartsWith("DataNodeActivity-")) {
                    if (counters.ContainsKey("Bytes written")) counters["Bytes written"].SetCounter((long)obj["BytesWritten"]);
                    if (counters.ContainsKey("Bytes read")) counters["Bytes read"].SetCounter((long)obj["BytesRead"]);
                }
            }

            base._wiwWithCounters.SetTimestamp();
            return base._wiwWithCounters;
        }
    }
}
