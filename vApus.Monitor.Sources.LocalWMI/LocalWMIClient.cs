/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System.Globalization;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.LocalWMI {
    /// <summary>
    /// To monitor the local system.
    /// </summary>
    public class LocalWMIClient : BasePollingClient {
        private WmiHelper _wmiHelper;

        public override bool IsConnected { get { return _wmiHelper != null; } }

        public override int RefreshCountersInterval { get { return 3000; } }

        public override string DecimalSeparator { get { return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; } }

        public override string Config {
            get {
                if (base._config == null)
                    base._config = _wmiHelper.GetHardwareInfo();
                return base._config;
            }
        }

        public override Entities WDYH {
            get {
                if (base._wdyh == null)
                    base._wdyh = _wmiHelper.GetWDYH();
                return base._wdyh;
            }
        }

        public override bool Connect() {
            bool isConnected = IsConnected;
            if (!isConnected) {
                _wmiHelper = new WmiHelper();
                try {
                    base._config = _wmiHelper.GetHardwareInfo();
                } catch {
                    _wmiHelper = null;
                }
                isConnected = IsConnected;
            }
            return isConnected;
        }

        protected override Entities PollCounters() {
            if (base._wiwWithCounters == null)
                base._wiwWithCounters = base._wiw.Clone();

            _wmiHelper.RefreshValues(base._wiwWithCounters);

            return base._wiwWithCounters;
        }

        public override bool Disconnect() {
            if (IsConnected) {
                _wmiHelper = null;
                Stop();
            }
            return !IsConnected;
        }
    }
}
