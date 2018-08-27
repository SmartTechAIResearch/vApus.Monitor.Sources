/*
 * 2016 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
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
    public class HbaseRegionServer : BasePollingClient {
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

                new CounterInfo("Exceptions"),
                new CounterInfo("Queue size"),
                new CounterInfo("Active handlers"),
                new CounterInfo("Total call time (95th percentile)"),

                new CounterInfo("Read request count"),
                new CounterInfo("Write request count"),
                new CounterInfo("Static index size"),
                new CounterInfo("Static bloom size"),
                new CounterInfo("Slow get count"),
                new CounterInfo("Slow put count")
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

        public HbaseRegionServer() {
            base._parameters = new Parameter[] {
                new Parameter() { Name = "Hostname", DefaultValue = "bdc-01" },
                new Parameter() { Name = "Port", DefaultValue = "16030" }
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
                switch ((string)obj["modelerType"]) {
                    case "JvmMetrics":
                        if (counters.ContainsKey("GC count")) counters["GC count"].SetCounter((long)obj["GcCount"]);
                        if (counters.ContainsKey("GC time millis")) counters["GC time millis"].SetCounter((long)obj["GcTimeMillis"]);

                        break;
                    case "sun.management.MemoryImpl":
                        if (counters.ContainsKey("Used heap bytes")) counters["Used heap bytes"].SetCounter((long)obj["HeapMemoryUsage"]["used"]);

                        break;
                    case "RegionServer,sub=IPC":
                        if (counters.ContainsKey("Exceptions")) counters["Exceptions"].SetCounter((long)obj["exceptions"]);
                        if (counters.ContainsKey("Queue size")) counters["Queue size"].SetCounter((long)obj["queueSize"]);
                        if (counters.ContainsKey("Active handlers")) counters["Active handlers"].SetCounter((long)obj["numActiveHandler"]);
                        if (counters.ContainsKey("Total call time (95th percentile)")) counters["Total call time (95th percentile)"].SetCounter((double)obj["TotalCallTime_95th_percentile"]);

                        break;

                    case "RegionServer,sub=Server":
                        if (counters.ContainsKey("Read request count")) counters["Read request count"].SetCounter((long)obj["readRequestCount"]);
                        if (counters.ContainsKey("Write request count")) counters["Write request count"].SetCounter((long)obj["writeRequestCount"]);
                        if (counters.ContainsKey("Static index size")) counters["Static index size"].SetCounter((long)obj["staticIndexSize"]);
                        if (counters.ContainsKey("Static bloom size")) counters["Static bloom size"].SetCounter((long)obj["staticBloomSize"]);
                        if (counters.ContainsKey("Slow get count")) counters["Slow get count"].SetCounter((long)obj["slowGetCount"]);
                        if (counters.ContainsKey("Slow put count")) counters["Slow put count"].SetCounter((long)obj["slowPutCount"]);

                        break;
                }
            }

            base._wiwWithCounters.SetTimestamp();
            return base._wiwWithCounters;
        }
    }
}
