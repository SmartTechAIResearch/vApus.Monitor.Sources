/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System.Globalization;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.Racktivity {
    internal class OldRacktivityClient : BasePollingClient {
        private OldRacktivityHelper _oldRacktivityHelper;
        private string _hostNameOrIPAddress;

        private string HostNameOrIPAddress {
            get {
                if (_hostNameOrIPAddress == null)
                    _hostNameOrIPAddress = GetParameter("Host Name or IP address").Value as string;
                return _hostNameOrIPAddress;
            }
        }

        public override bool IsConnected { get { return _oldRacktivityHelper != null && _oldRacktivityHelper.IsReachable; } }

        public override int RefreshCountersInterval { get { return 1000; } }

        public override string DecimalSeparator { get { return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; } }

        public override Entities WDYH {
            get {
                if (base._wdyh == null) {

                    base._wdyh = new Entities();
                    for (int i = 0; i != 8; i++) {
                        int outlet = i + 1;
                        var entity = new Entity("Outlet " + outlet, _oldRacktivityHelper.GetAvailable(outlet));
                        entity.GetSubs().Add(new CounterInfo("Available"));
                        entity.GetSubs().Add(new CounterInfo("Wattage (Watt)"));
                        entity.GetSubs().Add(new CounterInfo("Current (A)"));
                        entity.GetSubs().Add(new CounterInfo("Power Factor (%)"));
                        base._wdyh.Add(entity);
                    }
                }
                return base._wdyh;
            }
        }

        public OldRacktivityClient(Parameter[] parameters) {
            _parameters = parameters;
        }

        public override bool Connect() {
            if (!IsConnected) {
                string username = GetParameter("Username").Value as string;
                string password = GetParameter("Password").Value as string;

                _oldRacktivityHelper = new OldRacktivityHelper(HostNameOrIPAddress, username, password);
            }
            return IsConnected;
        }

        protected override Entities PollCounters() {
            OldRacktivityCounters counters = _oldRacktivityHelper.GetCounters();

            if (base._wih == null)
                base._wih = base._wdyh.Clone();

            for (int i = 0; i != 8; i++) {
                var entity = base._wih[i];
                entity.GetSubs()[0].SetCounter(counters.Available[i]);
                entity.GetSubs()[1].SetCounter(counters.Wattage[i]);
                entity.GetSubs()[2].SetCounter(counters.Current[i]);
                entity.GetSubs()[3].SetCounter(counters.PowerFactor[i]);
            }

            if (base._wiwWithCounters == null)
                base._wiwWithCounters = base._wiw.Clone();

            base._wiwWithCounters.SetCounters(base._wih);

            return base._wiwWithCounters;
        }

        public override bool Disconnect() {
            if (IsConnected) {
                _oldRacktivityHelper = null;
                Stop();
            }
            return !IsConnected;
        }
    }
}
