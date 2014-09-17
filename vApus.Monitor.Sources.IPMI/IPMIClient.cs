/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System.Data;
using System.Globalization;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.IPMI {
    public class IPMIClient : BasePollingClient {
        private IPMIHelper _ipmiHelper;

        private string HostNameOrIPAddress {
            get { return GetParameter("Host Name or IP address").Value as string; }
        }

        public override bool IsConnected { get { return _ipmiHelper != null && _ipmiHelper.IsReachable; } }

        public override int RefreshCountersInterval { get { return 20000; } }

        public override string DecimalSeparator { get { return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; } }

        public override Entities WDYH {
            get {
                if (base._wdyh == null) {
                    base._wdyh = new Entities();
                    DataTable sensorData = _ipmiHelper.FetchIPMISensorData();

                    var entity = new Entity(HostNameOrIPAddress, true);

                    foreach (DataRow row in sensorData.Rows) {
                        var info = new CounterInfo(row["Sensor"] as string);
                        info.GetSubs().Add(new CounterInfo("Status"));
                        info.GetSubs().Add(new CounterInfo("Reading"));
                        entity.GetSubs().Add(info);
                    }

                    base._wdyh.Add(entity);

                }
                return base._wdyh;
            }
        }

        public IPMIClient() {
            var hostNameOrIPAddress = new Parameter() { Name = "Host Name or IP address", Description = "Where the monitor source is bound to.", DefaultValue = string.Empty };
            var username = new Parameter() { Name = "Username", DefaultValue = "admin" };
            var password = new Parameter() { Name = "Password", DefaultValue = "1234", Encrypted = true };
            base._parameters = new Parameter[] { hostNameOrIPAddress, username, password };
        }

        public override bool Connect() {
            bool isConnected = IsConnected;
            if (!isConnected) {
                string username = GetParameter("Username").Value as string;
                string password = GetParameter("Password").Value as string;

                _ipmiHelper = new IPMIHelper(HostNameOrIPAddress, username, password);
                isConnected = IsConnected;
            }
            return isConnected;
        }

        protected override Entities PollCounters() {
            DataTable sensorData = _ipmiHelper.FetchIPMISensorData();

            if (base._wih == null)
                base._wih = WDYH.Clone();

            Entity entity = _wih[0];
            foreach (DataRow row in sensorData.Rows) {
                string sensor = row["Sensor"] as string;
                foreach (CounterInfo info in entity.GetSubs())
                    if (info.GetName() == sensor) {
                        info.GetSubs()[0].SetCounter(row["Status"]);
                        info.GetSubs()[1].SetCounter(row["Reading"]);
                        break;
                    }
            }

            if (base._wiwWithCounters == null)
                base._wiwWithCounters = base._wiw.Clone();

            base._wiwWithCounters.SetCounters(base._wih);

            return base._wiwWithCounters;
        }

        public override bool Disconnect() {
            if (IsConnected) {
                _ipmiHelper = null;
                Stop();
            }
            return !IsConnected;
        }
    }
}
