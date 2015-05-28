using RandomUtils.Log;
/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.Racktivity {
    internal class RacktivityEnergySwitchClient : BasePollingClient {
        private bool _isConnected;

        private string HostNameOrIPAddress {
            get { return GetParameter("Host Name or IP address").Value as string; }
        }

        public override bool IsConnected { get { return _isConnected; } }

        public override int RefreshCountersInterval { get { return 1000; } }

        public override string DecimalSeparator { get { return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; } }

        public override Entities WDYH {
            get {
                if (base._wdyh == null) {
                    RMCPCounters counters = RMCPHelper.GetCounters(HostNameOrIPAddress);

                    base._wdyh = new Entities();
                    for (int i = 0; i != 8; i++) {
                        int outlet = i + 1;
                        var entity = new Entity("Outlet " + outlet, counters.Available[i]);
                        entity.GetSubs().Add(new CounterInfo("Available"));
                        entity.GetSubs().Add(new CounterInfo("Wattage (Watt)"));
                        entity.GetSubs().Add(new CounterInfo("Current (A)"));
                        entity.GetSubs().Add(new CounterInfo("Power Factor (%)"));
                        base._wdyh.GetSubs().Add(entity);
                    }
                }
                return base._wdyh;
            }
        }

        public RacktivityEnergySwitchClient(Parameter[] parameters) {
            _parameters = parameters;
        }

        public override bool Connect() {
            if (!IsConnected) {
                string username = GetParameter("Username").Value as string;
                string password = GetParameter("Password").Value as string;

                try {
                    _isConnected = RMCPHelper.EnableDisableUDPAccess(HostNameOrIPAddress, username, password, true);
                } catch (Exception ex) {
                    //Failed to resolve the given host name for example.
                    //Loggers.Log(Level.Error, "Failed connecting to the racktivity energy switch.", ex);
                    //Handled in RacktivityClient.
                }
            }
            return IsConnected;
        }

        protected override Entities PollCounters() {
            RMCPCounters counters = RMCPHelper.GetCounters(HostNameOrIPAddress);

            if (base._wih == null)
                base._wih = WDYH.Clone();

            for (int i = 0; i != 8; i++) {
                int outlet = i + 1;
                var entity = base._wih.GetSubs()[i];
                entity.GetSubs()[0].SetCounter(counters.Available[i]);
                entity.GetSubs()[1].SetCounter(counters.Wattage[i]);
                entity.GetSubs()[2].SetCounter(counters.Current[i]);
                entity.GetSubs()[3].SetCounter(counters.GetPowerFactor(outlet));
            }

            if (base._wiwWithCounters == null)
                base._wiwWithCounters = base._wiw.Clone();

            base._wiwWithCounters.SetCounters(base._wih);
            base._wiwWithCounters.SetTimestamp();

            return base._wiwWithCounters;
        }

        public override bool Disconnect() {
            if (IsConnected) {
                _isConnected = false;
                Stop();
            }
            return !IsConnected;
        }
    }
}
