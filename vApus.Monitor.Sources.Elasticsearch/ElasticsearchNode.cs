/*
 * Copyright 2016 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Kirth Lammens en Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Json;
using System.Net;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.Elasticsearch {
    public class ElasticsearchNode : BasePollingClient {
        private string _protocol, _hostname, _port, _ecUser, _ecPassword, _ecBase64Credentials; //ec = Elastic Cloud

        private string _jvMaster, _jvShards;

        private bool _connected = false;

        private JsonValue GetJSONObject(string url) {
            return JsonObject.Parse(GetBody(url));
        }

        private string GetBody(string url) {
            url = _protocol + "://" + _hostname + ":" + _port + "/" + url;
            WebRequest req = WebRequest.CreateHttp(url);
            req.Timeout = 5000;
            req.Proxy = null;

            if (!string.IsNullOrEmpty(_ecBase64Credentials))
                req.Headers.Add("Authorization", _ecBase64Credentials);

            using (WebResponse res = req.GetResponse()) {
                using (StreamReader reader = new StreamReader(res.GetResponseStream()))
                    return reader.ReadToEnd();
            }
        }

        public ElasticsearchNode() {
            base._parameters = new Parameter[]
            {
                new Parameter() { Name = "Protocol", DefaultValue = "http", Description = "http or https" },
                new Parameter() { Name = "Hostname", DefaultValue = "" },
                new Parameter() { Name = "Port", DefaultValue = "9200" },
                new Parameter() { Name = "Elastic Cloud user", DefaultValue = "", Optional = true, Description = "Leave blank if you are not using Elastic Cloud." },
                new Parameter() { Name = "Elastic Cloud password", DefaultValue = "", Optional = true, Description = "Leave blank if you are not using Elastic Cloud.", Encrypted = true }
            };
        }

        public override bool IsConnected { get { return _connected; } }

        public override int RefreshCountersInterval { get { return 1000; } }

        public override string DecimalSeparator { get { return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; } }

        public override string Config {
            get {
                _jvMaster = GetBody("_cat/master?v=pretty");
                _jvShards = GetBody("_cat/shards?v=pretty");

                return "Master\n" + _jvMaster + "\n\nShards\n" + _jvShards;
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
            _protocol = (string)base.GetParameter("Protocol").Value;
            _hostname = (string)base.GetParameter("Hostname").Value;
            _port = (string)base.GetParameter("Port").Value;
            _ecUser = (string)base.GetParameter("Elastic Cloud user").Value;
            _ecPassword = (string)base.GetParameter("Elastic Cloud password").Value;

            if (!string.IsNullOrEmpty(_ecUser) && !string.IsNullOrEmpty(_ecPassword))
                _ecBase64Credentials = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_ecUser + ":" + _ecPassword));

            try {
                var entities = Entities;

                _jvMaster = GetBody("_cat/master?v=pretty");
                _jvShards = GetBody("_cat/shards?v=pretty");

                _connected = true;
            }
            catch (Exception ex) {
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

            l.AddRange(JvShardsToCounterInfos(false));

            l.AddRange(IndexInfoPerShardToCountInfos(false));

            return l;
        }

        private List<CounterInfo> JvShardsToCounterInfos(bool addValue) {
            var shardCI = new List<CounterInfo>();

            var lines = _jvShards.Trim().Split('\n');
            var grid = new string[lines.Length - 1, 6];
            int rows = lines.Length - 1;

            for (int line = 1; line < lines.Length; line++) {
                string[] cells = lines[line].Trim().Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                int r = line - 1;
                grid[r, 0] = "Shard_" + cells[0] + "_" + cells[1] + "_" + cells[2];
                grid[r, 1] = cells[3];

                if (cells.Length == 8) {
                    grid[r, 2] = cells[4];
                    grid[r, 3] = cells[5];
                    grid[r, 4] = cells[6];
                    grid[r, 5] = cells[7].Trim();
                }
                else if (cells.Length == 6) {
                    grid[r, 2] = "-1";
                    grid[r, 3] = "-1";
                    grid[r, 4] = cells[4];
                    grid[r, 5] = cells[5].Trim();
                }
                else {
                    grid[r, 2] = "-1";
                    grid[r, 3] = "-1";
                    grid[r, 4] = "-1";
                    grid[r, 5] = "-1";
                }
            }

            for (int i = 0; i != rows; i++) {
                string id = grid[i, 0];
                var state = new CounterInfo(id + "_State");
                var docs = new CounterInfo(id + "_Docs");
                var store = new CounterInfo(id + "_Store");
                var ip = new CounterInfo(id + "_IP");
                var node = new CounterInfo(id + "_Node");

                if (addValue) {
                    state.SetCounter(grid[i, 1]);
                    docs.SetCounter(grid[i, 2]);
                    store.SetCounter(grid[i, 3]);
                    ip.SetCounter(grid[i, 4]);
                    node.SetCounter(grid[i, 5]);
                }

                shardCI.Add(state);
                shardCI.Add(docs);
                shardCI.Add(store);
                shardCI.Add(ip);
                shardCI.Add(node);
            }

            return shardCI;
        }

        private List<CounterInfo> IndexInfoPerShardToCountInfos(bool addValue) {
            var l = new List<CounterInfo>();

            JsonValue jvStatsForIndices = GetJSONObject("_stats")["indices"];
            foreach (var kvp in jvStatsForIndices)
                foreach (var kvp2 in kvp.Value)
                    foreach (var kvp3 in kvp2.Value)
                        foreach (var kvp4 in kvp3.Value) {
                            var ci = new CounterInfo("Stat_" + kvp.Key + "." + kvp2.Key + "." + kvp3.Key + "." + kvp4.Key);
                            if (addValue)
                                ci.SetCounter(kvp4.Value.ReadAs<string>());

                            l.Add(ci);
                        }

            return l;
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

        protected override Entities PollCounters() {
            if (base._wiwWithCounters == null)
                base._wiwWithCounters = base._wiw.Clone();

            JsonValue jvStats = GetJSONObject("_nodes/stats")["nodes"];
            string[] jvMaster = GetBody("_cat/master").Split(' ');
            _jvShards = GetBody("_cat/shards?v=pretty");

            List<CounterInfo> shardscis = null, statscis = null;

            foreach (Entity e in base._wiwWithCounters.GetSubs()) {
                var stats = jvStats[e.GetName().Substring(e.GetName().IndexOf('(') - +2).Replace(")", "").Replace("(", "").Replace("\"", "").Trim()];
                var indices = stats["indices"];
                var jvm = stats["jvm"];

                foreach (CounterInfo ci in e.GetSubs())
                    try {
                        string ciname = ci.name.Trim();
                        if (ciname.StartsWith("Shard_")) {
                            if (shardscis == null) shardscis = JvShardsToCounterInfos(true);
                            if (shardscis.Exists(x => x.GetName() == ci.name)) {
                                CounterInfo candidate = shardscis.Find(x => x.GetName() == ci.name);
                                ci.SetCounter(candidate.GetCounter());
                            }
                            else {
                                ci.SetCounter(-1);
                            }
                            continue;
                        }

                        if (ciname.StartsWith("Stat_")) {
                            if (statscis == null) statscis = IndexInfoPerShardToCountInfos(true);
                            if (statscis.Exists(x => x.GetName() == ci.name)) {
                                CounterInfo candidate = statscis.Find(x => x.GetName() == ci.name);
                                ci.SetCounter(candidate.GetCounter());
                            }
                            else {
                                ci.SetCounter(-1);
                            }
                            continue;
                        }

                        switch (ciname) {
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
