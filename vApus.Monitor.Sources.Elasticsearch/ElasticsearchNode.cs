using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Json;
using System.Net;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.Elasticsearch {
    public class ElasticsearchNode : BasePollingClient {
        private string _hostname;
        private string _port;

        private string _jvMaster;
        private string _jvShards;


        private bool _connected = false;

        private JsonValue GetJSONObject(string url) {
            return JsonObject.Parse(GetBody(url));
        }

        private string GetBody(string url) {
            url = "http://" + _hostname + ":" + _port + "/" + url;
            WebRequest req = WebRequest.CreateHttp(url);
            req.Timeout = 5000;
            req.Proxy = null;
            using (WebResponse res = req.GetResponse()) {
                using (StreamReader reader = new StreamReader(res.GetResponseStream()))
                    return reader.ReadToEnd();
            }
        }

        public ElasticsearchNode() {
            base._parameters = new Parameter[]
            {
                new Parameter() { Name = "Hostname", DefaultValue = "" },
                new Parameter() { Name = "Port", DefaultValue = "9200" }
            };
        }

        public override bool IsConnected { get { return _connected; } }

        public override int RefreshCountersInterval { get { return 1000; } }

        public override string DecimalSeparator { get { return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; } }

        public override string Config {
            get {
                return "<List>Master\n" + _jvMaster + "\n\nShards\n" + _jvShards + "</List>";
            }
        }

        public Entities Entities {
            get {
                Entities entities = new Entities();

                foreach (var x in GetJSONObject("_nodes/stats")["nodes"])
                    entities.GetSubs().Add(new Entity(x.Value["name"].ToString() + " (" + x.Key + ")", true));

                return entities;
            }
        }
        public override bool Connect() {
            //    InitConnect();
            _hostname = (string)base.GetParameter("Hostname").Value;
            _port = (string)base.GetParameter("Port").Value;

            try {
                var entities = Entities;

                _jvMaster = GetBody("_cat/master?v=pretty");
                _jvShards = GetBody("_cat/shards?v=pretty");

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
        /*
                         new CounterInfo("Shard ID"),
                new CounterInfo("Shard State"),
                new CounterInfo("Shard Docs"),
                new CounterInfo("Shard Store"),
                new CounterInfo("Shard IP"),
                new CounterInfo("Shard Nodename"),
             */

        private List<CounterInfo> GetCountersForEntity() {
            var l = new List<CounterInfo>
            {
                new CounterInfo("Master ID"),
                new CounterInfo("Master Hostname"),
                new CounterInfo("Master IP"),
                new CounterInfo("Master Nodename"),
                new CounterInfo("Document Count"),
                new CounterInfo("GC Collectors Old Collection Count"),
                new CounterInfo("GC Collectors Old Collection Time in Millis"),
                new CounterInfo("GC Collectors Young Collection Count"),
                new CounterInfo("GC Collectors Young Collection Time in Millis"),
                new CounterInfo("HTTP Open Connections"),
                new CounterInfo("HTTP Total Opened"),
                new CounterInfo("Open File Descriptors"),
                new CounterInfo("Index Total"),
                new CounterInfo("Index Time [ms]"),
                new CounterInfo("JVM Heap Used [byte]"),
                new CounterInfo("JVM Heap Used [%]"),
                new CounterInfo("JVM Heap Max [byte]"),
                new CounterInfo("JVM Thread Count"),
                new CounterInfo("JVM Thread Count Peak"),
                new CounterInfo("Merges Current"),
                new CounterInfo("Merges Current Docs"),
                new CounterInfo("Merges Current Size [byte]"),
                new CounterInfo("Merges Time [ms]"),
                new CounterInfo("Merges Total"),
                new CounterInfo("Merges Total Docs"),
                new CounterInfo("Query Cache Size in Memory [byte]"),
                new CounterInfo("Search Fetch Total"),
                new CounterInfo("Search Fetch Current"),
                new CounterInfo("Search Open Contexts"),
                new CounterInfo("Search Query Current"),
                new CounterInfo("Search Query Total"),
                new CounterInfo("Segment Count"),
                new CounterInfo("Segment Memory in Bytes"),
                new CounterInfo("Store Size [byte]")
            };




            var shardCI = new CounterInfo("Shards");
            //foreach (string line in lines) {

                //string shardId = 
            //}

            return l;
        }

        //            _jvShards = GetBody("_cat/shards?v=pretty");

        private CounterInfo JvShardsToDatatable(bool addValue) {
            var shardCI = new CounterInfo("Shards");

            var columns = _jvShards.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var columnsWithCells = new List<string[]>();
            int rows = 0;

            for (int i = 0; i != columns.Length; i++) {
                string[] cells = columns[i].Split('\n');
                columnsWithCells.Add(cells);
                rows = cells.Length;
            }

            for (int i = 1; i != rows; i++) {

                string id = columnsWithCells[0][i] + "_" + columnsWithCells[1][i] + "_" + columnsWithCells[2][i];
                var state = new CounterInfo(id + "_State");
                var docs = new CounterInfo(id + "_Docs");
                var store = new CounterInfo(id + "_Store");
                var ip = new CounterInfo(id + "_IP");
                var node = new CounterInfo(id + "_Node");
                
                shardCI.GetSubs().Add(state);
                shardCI.GetSubs().Add(docs);
                shardCI.GetSubs().Add(store);
                shardCI.GetSubs().Add(ip);
                shardCI.GetSubs().Add(node);

                if (addValue) {
                    state.SetCounter(columnsWithCells[3][i]);
                    string d = columnsWithCells[4][i];
                    if (string.IsNullOrWhiteSpace(d)) {
                        d = "-1";
                    }
                    docs.SetCounter(double.Parse(d));

                    string s = columnsWithCells[5][i];
                    if (string.IsNullOrWhiteSpace(s)) {
                        s = "-1";
                    }
                    store.SetCounter(s);
                    ip.SetCounter(columnsWithCells[6][i]);
                    node.SetCounter(columnsWithCells[7][i]);
                }
            }

            for (int i = 0; i != columnsWithCells.Count; i++) {

            }

            return shardCI;
        }

        public override Entities WDYH {
            get {

                if (base._wdyh == null) {

                    Entities dic = new Entities();

                    foreach (Entity e in Entities.GetSubs()) {
                        e.subs = GetCountersForEntity();
                        dic.GetSubs().Add(e);
                    }

                    base._wdyh = dic;
                }


                return base._wdyh;
            }
        }

        /*
                         new CounterInfo("Shard ID"),
                new CounterInfo("Shard State"),
                new CounterInfo("Shard Docs"),
                new CounterInfo("Shard Store"),
                new CounterInfo("Shard IP"),
                new CounterInfo("Shard Nodename"),
             */

        protected override Entities PollCounters() {
            if (base._wiwWithCounters == null)
                base._wiwWithCounters = base._wiw.Clone();

            JsonValue jvStats = GetJSONObject("_nodes/stats")["nodes"];
            string[] jvMaster = GetBody("_cat/master").Split(' ');
            string[] jvShards = GetBody("_cat/shards").Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (Entity e in base._wiwWithCounters.GetSubs()) {
                var stats = jvStats[e.GetName().Substring(e.GetName().IndexOf('(') - +2).Replace(")", "").Replace("(", "").Replace("\"", "").Trim()];
                var indices = stats["indices"];
                var jvm = stats["jvm"];

                foreach (CounterInfo ci in e.GetSubs())
                    try {
                        switch (ci.name.Trim()) {
                            //case "Shard ID":
                            //    ci.SetCounter(jvShards);
                            //    break;
                            case "Master ID":
                                ci.SetCounter(jvMaster[0]);
                                break;
                            case "Master Hostname":
                                ci.SetCounter(jvMaster[1]);
                                break;
                            case "Master IP":
                                ci.SetCounter(jvMaster[2]);
                                break;
                            case "Master Nodename":
                                ci.SetCounter(jvMaster[3]);
                                break;
                            case "Document Count":
                                ci.SetCounter((float)indices["docs"]["count"]);
                                break;

                            case "Store Size [byte]":
                                ci.SetCounter((float)indices["store"]["size_in_bytes"]);
                                break;

                            case "Index Total":
                                ci.SetCounter((float)indices["indexing"]["index_total"]);
                                break;

                            case "Index Time [ms]":
                                ci.SetCounter((float)indices["indexing"]["index_time_in_millis"]);
                                break;

                            case "Search Open Contexts":
                                ci.SetCounter((float)indices["search"]["open_contexts"]);
                                break;

                            case "Search Query Total":
                                ci.SetCounter((float)indices["search"]["query_total"]);
                                break;

                            case "Search Query Current":
                                ci.SetCounter((float)indices["search"]["query_current"]);
                                break;

                            case "Search Fetch Total":
                                ci.SetCounter((float)indices["search"]["fetch_total"]);
                                break;

                            case "Search Fetch Current":
                                ci.SetCounter((float)indices["search"]["fetch_current"]);
                                break;

                            case "Merges Current":
                                ci.SetCounter((float)indices["merges"]["current"]);
                                break;

                            case "Merges Current Docs":
                                ci.SetCounter((float)indices["merges"]["current_docs"]);
                                break;

                            case "Merges Current Size [byte]":
                                ci.SetCounter((float)indices["merges"]["current_size_in_bytes"]);
                                break;

                            case "Merges Total":
                                ci.SetCounter((float)indices["merges"]["total"]);
                                break;

                            case "Merges Time [ms]":
                                ci.SetCounter((float)indices["merges"]["total_time_in_millis"]);
                                break;

                            case "Merges Total Docs":
                                ci.SetCounter((float)indices["merges"]["total_docs"]);
                                break;

                            case "Query Cache Size in Memory [byte]":
                                ci.SetCounter((float)indices["query_cache"]["memory_size_in_bytes"]);
                                break;

                            case "Segment Count":
                                ci.SetCounter((float)indices["segments"]["count"]);
                                break;

                            case "Segment Memory in Bytes":
                                ci.SetCounter((float)indices["segments"]["memory_in_bytes"]);
                                break;

                            case "Open File Descriptors":
                                ci.SetCounter((float)stats["process"]["open_file_descriptors"]);
                                break;

                            case "JVM Heap Used [byte]":
                                ci.SetCounter((float)jvm["mem"]["heap_used_in_bytes"]);
                                break;

                            case "JVM Heap Used [%]":
                                ci.SetCounter((float)jvm["mem"]["heap_used_percent"]);
                                break;

                            case "JVM Heap Max [byte]":
                                ci.SetCounter((float)jvm["mem"]["heap_max_in_bytes"]);
                                break;

                            case "JVM Thread Count":
                                ci.SetCounter((float)jvm["threads"]["count"]);
                                break;

                            case "JVM Thread Count Peak":
                                ci.SetCounter((float)jvm["threads"]["peak_count"]);
                                break;

                            case "HTTP Open Connections":
                                ci.SetCounter((float)stats["http"]["current_open"]);
                                break;

                            case "HTTP Total Opened":
                                ci.SetCounter((float)stats["http"]["total_opened"]);
                                break;
                            case "GC Collectors Old Collection Count":
                                ci.SetCounter((float)jvm["gc"]["collectors"]["old"]["collection_count"]);
                                break;
                            case "GC Collectors Old Collection Time in Millis":
                                ci.SetCounter((float)jvm["gc"]["collectors"]["old"]["collection_time_in_millis"]);
                                break;
                            case "GC Collectors Young Collection Count":
                                ci.SetCounter((float)jvm["gc"]["collectors"]["young"]["collection_count"]);
                                break;
                            case "GC Collectors Young Collection Time in Millis":
                                ci.SetCounter((float)jvm["gc"]["collectors"]["young"]["collection_time_in_millis"]);
                                break;
                            default:
                                ci.SetCounter(-1f);
                                break;
                        }
                    }
                    catch {
                        ci.SetCounter(-1f);
                    }
            }

            base._wiwWithCounters.SetTimestamp();
            return base._wiwWithCounters;
        }

    }
}
