/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System.Collections.Generic;
using System.Data;
using System.Globalization;
using vApus.Monitor.Sources.Base;
using RandomUtils;

namespace vApus.Monitor.Sources.IPMI {
    public class IPMIClient : BasePollingClient {
        private IPMIHelper _ipmiHelper;
        private string[] _wiwSensorIDs = { }; //For specified readings instead of always everything, reading a sensor that is faulty can cause a timeout.
        private bool _alwaysReadAllSensors; //Faster than reading just the specified onces. But can be slower if there are faulty sensors where the reading time out.

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
                        string sensor = string.Format("{0} ({1})", row["Sensor"], row["ID"]);
                        entity.GetSubs().Add(new CounterInfo(sensor));
                    }

                    base._wdyh.Add(entity);

                }
                return base._wdyh;
            }
        }

        public override Entities WIW {
            get {
                return base.WIW;
            }
            set {
                base.WIW = value;
                _wiwSensorIDs = new string[0];

                //Make ranges of sensor ids to be passed to ipmiutil. Sadly enough it does not accept lists, so multiple processes are needed reagardles.
                if (!_alwaysReadAllSensors && _wiw.GetCounterInfosAtLastLevel().Count != _wdyh.GetCounterInfosAtLastLevel().Count) {
                    var l = new List<int>();
                    foreach (CounterInfo counterInfo in _wiw.GetCounterInfosAtLastLevel()) {
                        string sensor = counterInfo.GetName();
                        string[] s = sensor.Split('(');
                        string sensorId = s[s.Length - 1];
                        sensorId = sensorId.Substring(0, sensorId.Length - 1);
                        l.Add(int.Parse(sensorId, NumberStyles.HexNumber));
                    }

                    if (l.Count != 0) {
                        l.Sort();
                        var wiwSensorIdRanges = new Dictionary<int, int>(); //Start, count

                        int lowerBoundary = l[0];
                        int upperBoundary = l[l.Count - 1] + 1;

                        int i = lowerBoundary;
                        do {
                            int startRange = i;
                            int endRange = i;
                            while (l.Contains(++i)) endRange = i;

                            wiwSensorIdRanges.Add(startRange, endRange);

                            while (i < upperBoundary && !l.Contains(++i)) ;
                        } while (i < upperBoundary);

                        var wiwSensorIDs = new HashSet<string>();
                        _wiwSensorIDs = new string[wiwSensorIdRanges.Count];
                        foreach (var kvp in wiwSensorIdRanges)
                            wiwSensorIDs.Add(string.Format("{0:X}-{1:X}", kvp.Key, kvp.Value));

                        _wiwSensorIDs = new string[wiwSensorIDs.Count];
                        wiwSensorIDs.CopyTo(_wiwSensorIDs);
                    }
                }
            }
        }
        public IPMIClient() {
            var hostNameOrIPAddress = new Parameter() { Name = "Host Name or IP address", Description = "Where the monitor source is bound to.", DefaultValue = string.Empty };
            var username = new Parameter() { Name = "Username", DefaultValue = "admin" };
            var password = new Parameter() { Name = "Password", DefaultValue = "1234", Encrypted = true };
            var ipmi2dot0 = new Parameter() { Name = "IPMI 2.0", DefaultValue = false };
            var alwaysReadAllSensors = new Parameter() { Name = "Always read all sensors", Description = "Can be baster than reading just the specified onces, which is multi-process. Can be slower if there are faulty sensors where reading times out. Try both.", DefaultValue = false };
            base._parameters = new Parameter[] { hostNameOrIPAddress, username, password, ipmi2dot0, alwaysReadAllSensors };
        }

        public override bool Connect() {
            bool isConnected = IsConnected;
            if (!isConnected) {
                string username = GetParameter("Username").Value as string;
                string password = GetParameter("Password").Value as string;
                bool ipmi2dot0 = (bool)GetParameter("IPMI 2.0").Value;
                _alwaysReadAllSensors = (bool)GetParameter("Always read all sensors").Value;

                _ipmiHelper = new IPMIHelper(HostNameOrIPAddress, username, password, ipmi2dot0);
                isConnected = IsConnected;
            }
            return isConnected;
        }

        protected override Entities PollCounters() {
            DataTable sensorData = _ipmiHelper.FetchIPMISensorData(_wiwSensorIDs);

            if (base._wiwWithCounters == null)
                base._wiwWithCounters = base._wiw.Clone();

            //Can be more performant by branching.
            if (_wiwSensorIDs.Length == 0) {
                if (base._wih == null)
                    base._wih = WDYH.Clone();

                Entity entity = base._wih[0];
                foreach (DataRow row in sensorData.Rows) {
                    string sensor = string.Format("{0} ({1})", row["Sensor"], row["ID"]);
                    foreach (CounterInfo info in entity.GetSubs())
                        if (info.GetName() == sensor) {
                            info.SetCounter(row["Reading"]);
                            break;
                        }
                }

                base._wiwWithCounters.SetCounters(base._wih);
            } else {
                Entity entity = base._wiwWithCounters[0];
                foreach (DataRow row in sensorData.Rows) {
                    string sensor = string.Format("{0} ({1})", row["Sensor"], row["ID"]);
                    foreach (CounterInfo info in entity.GetSubs())
                        if (info.GetName() == sensor) {
                            info.SetCounter(row["Reading"]);
                            break;
                        }
                }
            }
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
