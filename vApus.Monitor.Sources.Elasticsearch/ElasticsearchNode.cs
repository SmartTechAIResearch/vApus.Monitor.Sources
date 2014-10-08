using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vApus.Monitor.Sources.Base;
using System.Net;
using System.IO;
using System.Json;
using System.Globalization;

namespace vApus.Monitor.Sources.Elasticsearch
{
    public class ElasticsearchNode : BasePollingClient
    {
        private string _hostname;
        private string _port;

        private JsonValue GetJSONObject(string url)
        {
            url = "http://" + _hostname + ":" + _port + "/" + url;
            WebRequest req = WebRequest.CreateHttp(url);
            req.Timeout = 5000;
            req.Proxy = null;
            using (WebResponse res = req.GetResponse())
            {
                using (StreamReader reader = new StreamReader(res.GetResponseStream()))
                    return JsonObject.Parse(reader.ReadToEnd());
            }
        }

        public ElasticsearchNode()
        {
            base._parameters = new Parameter[] 
            {
                new Parameter() { Name = "Hostname", DefaultValue = "hp-g8.sslab.lan" },
                new Parameter() { Name = "Port", DefaultValue = "9200" }
            };
        }

        public override bool IsConnected { get { return true /* maybe */; } }

        public override int RefreshCountersInterval { get { return 1000; } }

        public override string DecimalSeparator { get { return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; } }

        public override string Config
        {
            get
            {
                return "<NotReallyAConfig></NotReallyAConfig>";
            }
        }

        public Entities Entities
        {
            get
            {
                Entities entities = new Entities();

                foreach (var x in GetJSONObject("/_nodes/stats")["nodes"])
                    entities.Add(new Entity(x.Value["name"].ToString() + " (" + x.Key + ")", true));
                //    entities.Add(new Entity(x.Value["name"].ToString(), vApusSMT.Base.PowerState.On));


                return entities;
            }
        }

        public override bool Connect()
        {
            //    InitConnect();
            _hostname = (string)base.GetParameter("Hostname").Value;
            _port = (string)base.GetParameter("Port").Value;


            return true;
        }


        public override bool Disconnect()
        {
            try
            {
                Stop();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public List<CounterInfo> GetCountersForEntity(string ename)
        {
            return new List<CounterInfo> 
            { 
                new CounterInfo("Document Count"),
                new CounterInfo("Store Size [byte]"),
                new CounterInfo("Index Total"),
                new CounterInfo("Index Time [ms]"),
                new CounterInfo("Search Open Contexts"),
                new CounterInfo("Search Query Total"),
                new CounterInfo("Search Query Current"),
                new CounterInfo("Search Fetch Total"),
                new CounterInfo("Search Fetch Current"),
                new CounterInfo("Merges Current"),
                new CounterInfo("Merges Current Docs"),
                new CounterInfo("Merges Current Size [byte]"),
                new CounterInfo("Merges Total"),
                new CounterInfo("Merges Time [ms]"),
                new CounterInfo("Merges Total Docs"),
                new CounterInfo("Filter Cache Size in Memory [byte]"),
                new CounterInfo("Segment Count"),
                new CounterInfo("Segment Memory in Bytes"),
                new CounterInfo("Open File Descriptors"),
                new CounterInfo("JVM Heap Used [byte]"),
                new CounterInfo("JVM Heap Used [%]"),
                new CounterInfo("JVM Heap Max [byte]"),
                new CounterInfo("JVM Thread Count"),
                new CounterInfo("JVM Thread Count Peak"),
                new CounterInfo("HTTP Open Connections"),
                new CounterInfo("HTTP Total Opened")
            };
        }

        public override Entities WDYH
        {
            get
            {

                if (base._wdyh == null)
                {

                    Entities dic/*k hurr*/ = new Entities();


                    foreach (Entity e in Entities)
                    {
                        e.subs = GetCountersForEntity("~* Ponyaaier 5000 *~");
                        dic.Add(e);
                    }

                    base._wdyh = dic;
                }

                //     dic.Add(e, GetCountersForEntity(e.GetName()));



                return base._wdyh;
            }
        }

        /* public override Parameter[] Parameters
         {
             get
             {
                 return new Parameter[]
                 {
                     new Parameter("IP", "WHERE ELASTISEARCh", typeof(string), false, "hp-g8.sslab.lan", false),
                     new Parameter("Port", "WHAT NUMBER", typeof(int), false, 9200, false)
                 };
             }
         }*/




        protected override Entities PollCounters()
        {
            if (base._wiwWithCounters == null)
                base._wiwWithCounters = base._wiw.Clone();

            JsonValue jv = GetJSONObject("_nodes/stats")["nodes"];

            foreach (Entity e in base._wiwWithCounters)
            {
                var stats = jv[e.GetName().Substring(e.GetName().IndexOf('(') - +2).Replace(")", "").Replace("(", "").Replace("\"", "").Trim()];
                var indices = stats["indices"];
                var jvm = stats["jvm"];

                foreach (CounterInfo ci in e.GetSubs())
                {

                    switch (ci.name.Trim())
                    {

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

                        case "Filter Cache Size in Memory [byte]":
                            ci.SetCounter((float)indices["filter_cache"]["memory_size_in_bytes"]);
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
                    }
                }
            }

            return base._wiwWithCounters;
        }

    }
}
