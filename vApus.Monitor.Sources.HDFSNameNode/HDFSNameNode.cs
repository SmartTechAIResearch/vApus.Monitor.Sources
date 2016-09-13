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
    public class HDFSNameNode : BasePollingClient {
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

                new CounterInfo("Create file ops"),
                new CounterInfo("Get block locations"),
                new CounterInfo("Delete file ops"),

                new CounterInfo("Capacity total"),
                new CounterInfo("Files total")
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

        public HDFSNameNode() {
            base._parameters = new Parameter[] {
                new Parameter() { Name = "Hostname", DefaultValue = "192.168.32.47" },
                new Parameter() { Name = "Port", DefaultValue = "50070" }
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
                    case "NameNodeActivity":
                        if (counters.ContainsKey("Create file ops")) counters["Create file ops"].SetCounter((long)obj["CreateFileOps"]);
                        if (counters.ContainsKey("Get block locations")) counters["Get block locations"].SetCounter((long)obj["GetBlockLocations"]);
                        if (counters.ContainsKey("Delete file ops")) counters["Delete file ops"].SetCounter((long)obj["DeleteFileOps"]);
                       
                        break;

                    case "org.apache.hadoop.hdfs.server.namenode.FSNamesystem":
                        if ((string)obj["name"] == "Hadoop:service=NameNode,name=FSNamesystemState") {
                            if (counters.ContainsKey("Capacity total")) counters["Capacity total"].SetCounter((long)obj["CapacityTotal"]);
                            if (counters.ContainsKey("Files total")) counters["Files total"].SetCounter((long)obj["FilesTotal"]);
                        }
                        break;
                }
            }

            base._wiwWithCounters.SetTimestamp();
            return base._wiwWithCounters;
        }
    }
}
