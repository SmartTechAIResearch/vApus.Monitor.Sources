/*
 * 2014 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.IPMI {
    public class IPMIClient : BasePollingClient {
        private IPMIHelper _ipmiHelper;
        private string[] _wiwSensorIDs = { }; //For specified readings instead of always everything, reading a sensor that is faulty can cause a timeout.
        private bool _alwaysReadAllSensors; //Faster than reading just the specified onces. But can be slower if there are faulty sensors where the reading time out.

        private string HostNameOrIPAddress {
            get { return GetParameter("Host Name or IP address").Value as string; }
        }

        public override bool IsConnected { get { return _ipmiHelper != null; } }

        public override int RefreshCountersInterval { get { return 20000; } }

        public override string DecimalSeparator { get { return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator; } }

        public override Entities WDYH {
            get {
                if (base._wdyh == null) {
                    base._wdyh = new Entities();
                    DataTable sensorData = _ipmiHelper.FetchIPMISensorData();

                    var entity = new Entity(HostNameOrIPAddress, true);

                    foreach (DataRow row in sensorData.Rows)
                        if ((double)row["Reading"] != -1d) {
                            string sensor = string.Format("{0} ({1})", row["Sensor"], row["ID"]);
                            entity.GetSubs().Add(new CounterInfo(sensor));
                        }

                    base._wdyh.GetSubs().Add(entity);

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
                if (!_alwaysReadAllSensors) {
                    var sortedSet = new SortedSet<int>();
                    foreach (CounterInfo counterInfo in _wiw.GetCounterInfosAtLastLevel()) {
                        string sensor = counterInfo.GetName();
                        string[] s = sensor.Split('(');
                        string sensorId = s[s.Length - 1];
                        sensorId = sensorId.Substring(0, sensorId.Length - 1);
                        sortedSet.Add(int.Parse(sensorId, NumberStyles.HexNumber));
                    }

                    int[] arr = new int[sortedSet.Count];
                    sortedSet.CopyTo(arr);
                    if (arr.Length != 0) {
                        var wiwSensorIdRanges = new Dictionary<int, int>(); //Start, count

                        int lowerBoundary = arr[0];
                        int upperBoundary = arr[arr.Length - 1] + 1;

                        int i = lowerBoundary;
                        do {
                            int startRange = i;
                            int endRange = i;
                            while (arr.Contains(++i)) endRange = i;

                            wiwSensorIdRanges.Add(startRange, endRange);

                            while (i < upperBoundary && !arr.Contains(++i)) ;
                        } while (i < upperBoundary);

                        var wiwSensorIDs = new HashSet<string>();
                        _wiwSensorIDs = new string[wiwSensorIdRanges.Count];
                        foreach (var kvp in wiwSensorIdRanges)
                            if (kvp.Key == kvp.Value)
                                wiwSensorIDs.Add(string.Format("{0:x}", kvp.Key));
                            else
                                wiwSensorIDs.Add(string.Format("{0:x}-{1:x}", kvp.Key, kvp.Value));

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
            var alwaysReadAllSensors = new Parameter() { Name = "Always read all sensors", Description = "Can be faster than reading just the specified onces, which is multi-process. Can be slower if there are faulty sensors where reading times out. Try both.", DefaultValue = false };
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

                if (!_ipmiHelper.IsReachable) {
                    _ipmiHelper.Dispose();
                    _ipmiHelper = null;
                }

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

                Entity entity = base._wih.GetSubs()[0];
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
                Entity entity = base._wiwWithCounters.GetSubs()[0];
                foreach (DataRow row in sensorData.Rows) {
                    string sensor = string.Format("{0} ({1})", row["Sensor"], row["ID"]);
                    foreach (CounterInfo info in entity.GetSubs())
                        if (info.GetName() == sensor) {
                            info.SetCounter(row["Reading"]);
                            break;
                        }
                }
            }

            base._wiwWithCounters.SetTimestamp();
            return base._wiwWithCounters;
        }

        public override bool Disconnect() {
            if (_ipmiHelper != null) {
                _ipmiHelper.Dispose();
                _ipmiHelper = null;
                
                Stop();
            }
            return !IsConnected;
        }

        /// <summary>
        /// Function for the client tester.
        /// </summary>
        /// <param name="verboseConsoleOutput"></param>
        /// <param name="id"></param>
        /// <param name="parameterValues"></param>
        public override void Test(bool verboseConsoleOutput, int id, params object[] parameterValues) {
            base._verboseConsoleOutput = verboseConsoleOutput;
            base._id = id;
            try {
                try {
                    Console.WriteLine("Test " + base._id + " Started");
                    if (verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Setting the parameters.");
                    if (!IsConnected && parameterValues != null) SetParameterValues(parameterValues);

                    if (verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Connecting to the monitor source...");
                    Connect();

                    if (!IsConnected)
                        throw new Exception("Test " + base._id + " Failed to connect to the monitor source.");

                    string config = Config;
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Config: " + config);

                    int refreshCountersInterval = 2;
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " RefreshCountersInterval: " + refreshCountersInterval);

                    string decimalSeparator = DecimalSeparator;
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " DecimalSeparator: " + decimalSeparator);

                    WIW = WDYH;
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Random wiw determined: " + WIWRepresentation);

                    _started = true;
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Reading and parsing counters 3 times...");

                    _sleepWaitHandle = new AutoResetEvent(false);
                    for (int i = 0; i != 3; i++) {
                        ValidateCounters(PollCounters());
                        if (_sleepWaitHandle != null)
                            _sleepWaitHandle.WaitOne(refreshCountersInterval);
                    }

                    Stop();

                    Console.WriteLine("Test " + base._id + " Finished succesfully");

                } catch {
                    throw;

                } finally {
                    try { Dispose(); } catch { throw new Exception("Failed to dispose."); }
                }
            } catch (Exception ex) {
                var defaultColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Test " + base._id + " Failed: " + ex.Message + " " + ex.StackTrace);
                Console.ForegroundColor = defaultColor;
            }
            base._verboseConsoleOutput = false;
        }

    }
}
