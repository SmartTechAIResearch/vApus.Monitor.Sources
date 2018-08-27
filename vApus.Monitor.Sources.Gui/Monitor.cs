/*
 * 2009 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using vApus.Monitor.Sources.Base;
using vApus.Util;

namespace vApus.Monitor {
    /// <summary>
    /// Keeps all the monitoring settings the previous "what I want" counter info if any.
    /// </summary>
    public static class Monitor {

        #region Fields
        private static string[] _filter = new string[0];

        private static MonitorSourceClient _monitorSourceClient = new MonitorSourceClient();
        private static int _monitorSourceClientIndex, _previousMonitorSourceIndexForCounters;
        private static string _monitorSourceClientName = string.Empty;
        private static List<MonitorSourceClient> _monitorSourceClients = new List<MonitorSourceClient>();

        private static Entities _wiw = new Entities();

        private static object[] _parameterValues = new object[0];
        #endregion

        static Monitor() {
            List<MonitorSourceClient> clients = InitAndGetMonitorSourceClients();
            string monitorSourceClient = Sources.Gui.Properties.Settings.Default.Client;
            if (!string.IsNullOrEmpty(monitorSourceClient)) {
                foreach (var c in clients) {
                    if (c.ToString() == monitorSourceClient) {
                        MonitorSource = c;
                        break;
                    }
                }
            }

            string parameters = Sources.Gui.Properties.Settings.Default.Parameters;
            if (!string.IsNullOrEmpty(parameters)) {
                try {
                    parameters = parameters.Decrypt("1l#kec@ndy!", new byte[] { 0x39, 0x16, 0xa9, 0x3d, 0x23, 0x5f, 0x65, 0x74, 0xb8, 0x95, 0x10, 0x01, 0x32 });
                    ParameterValues = parameters.Split(new string[] { "<@,#>" }, StringSplitOptions.None);
                }
                catch {
                    //ignore.
                }
            }
        }

        #region Properties

        /// <summary>
        ///     To check if the counters match the monitor source.
        /// </summary>
        public static int PreviousMonitorSourceIndexForCounters {
            get { return _previousMonitorSourceIndexForCounters; }
            set { _previousMonitorSourceIndexForCounters = value; }
        }

        public static int MonitorSourceIndex {
            get { return _monitorSourceClientIndex; }
            set { _monitorSourceClientIndex = value; }
        }

        public static string MonitorSourceName {
            get { return _monitorSourceClientName; }
            set { _monitorSourceClientName = value; }
        }

        public static MonitorSourceClient MonitorSource {
            get {
                return _monitorSourceClient;
            }
            set {
                _monitorSourceClient = value;
                _monitorSourceClientIndex = _monitorSourceClients.IndexOf(_monitorSourceClient);
                _monitorSourceClientName = _monitorSourceClient.ToString();

                Sources.Gui.Properties.Settings.Default.Client = _monitorSourceClientName;
                Sources.Gui.Properties.Settings.Default.Save();
            }
        }

        /// <summary>
        ///     The counters you want to monitor.
        /// </summary>
        public static Entities Wiw {
            get { return _wiw; }
            set { _wiw = value; }
        }

        /// <summary>
        ///     All the parameters, just the values, the names and types and such come from the monitor source.
        /// </summary>
        public static object[] ParameterValues {
            get { return _parameterValues; }
            set {
                _parameterValues = value;

                Sources.Gui.Properties.Settings.Default.Parameters =
                    string.Join("<@,#>", _parameterValues).Encrypt("1l#kec@ndy!", new byte[] { 0x39, 0x16, 0xa9, 0x3d, 0x23, 0x5f, 0x65, 0x74, 0xb8, 0x95, 0x10, 0x01, 0x32 });

                Sources.Gui.Properties.Settings.Default.Save();
            }
        }

        /// <summary>
        ///     To be able to load and save this.
        /// </summary>
        public static string WIWRepresentation {
            get {
                if (_wiw == null) _wiw = new Entities();
                return JsonConvert.SerializeObject(_wiw, Formatting.None, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore });
            }
            set {
                try {
                    _wiw = JsonConvert.DeserializeObject<Entities>(value);
                }
                catch {
                    //To make it 'backwards compatible' with older vass files.
                }
                if (_wiw == null) _wiw = new Entities();
            }
        }

        [Description("To filter the counters in a (large) counter collection. Wild card * can be used. Not case sensitive. All entries are in OR-relation with each other.")]
        public static string[] Filter {
            get { return _filter; }
            set { _filter = value; }
        }

        #endregion

        #region Functions

        /// <summary>
        /// Get all monitor source clients.
        /// </summary>
        /// <param name="monitorSources"></param>
        public static List<MonitorSourceClient> InitAndGetMonitorSourceClients() {
            if (_monitorSourceClients.Count == 0) {
                Dictionary<string, Type> clients = ClientFactory.Clients;
                foreach (var kvp in clients) {
                    var monitorSource = new MonitorSourceClient(kvp.Key, kvp.Value);
                    _monitorSourceClients.Add(monitorSource);
                }

                if (clients.Count == 0) {
                    _monitorSourceClientIndex = -1;
                    _monitorSourceClientName = string.Empty;
                }
                else {
                    if (_monitorSourceClientName == string.Empty) {
                        //Backwards compatible.
                        if (_monitorSourceClientIndex == -1)
                            _monitorSourceClientIndex = 0;
                        else if (_monitorSourceClientIndex >= _monitorSourceClients.Count)
                            _monitorSourceClientIndex = _monitorSourceClients.Count - 1;
                    }
                    else {

                        //Match names instead of indices #727 
                        int candidate = 0;
                        for (; candidate != _monitorSourceClients.Count; candidate++)
                            if (_monitorSourceClients[candidate].ToString() == _monitorSourceClientName)
                                break;

                        _monitorSourceClientIndex = candidate;
                    }

                    _monitorSourceClient = _monitorSourceClients[_monitorSourceClientIndex];
                    _monitorSourceClientName = _monitorSourceClient.ToString();
                }
            }
            return _monitorSourceClients;
        }

        #endregion
    }
}