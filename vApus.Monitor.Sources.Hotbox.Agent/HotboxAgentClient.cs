/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using Newtonsoft.Json;
using RandomUtils;
using RandomUtils.Log;
using System;
using System.Collections.Generic;
using System.Threading;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.Hotbox.Agent {
    /// <summary>
    /// A client for the Hotbox agent. Currently supports only v1 not v2.
    /// With a bit of luck the agent gets rewritten using the vApus-agent netbeans packages.
    /// </summary>
    public class HotboxAgentClient : BaseSocketClient<string> {
        private Entities _wih, _wiwWithCounters;
        private string _getSensors, _startSensors, _stopSensors;

        private Thread _readMonitorCountersThread;

        public override int RefreshCountersInterval { get { return 1000; } }

        public override string DecimalSeparator { get { return "."; } }

        public override Entities WDYH {
            get {
                if (base._wdyh == null) {
                    string message = WriteRead(_getSensors);

                    var entities = new Entities();
                    var entity = new Entity(GetParameter("Host Name or IP address").Value as string, true);

                    HotboxSensors sensors = JsonConvert.DeserializeObject<HotboxSensors>(message);
                    foreach (string sensor in sensors.sensors)
                        entity.GetSubs().Add(new CounterInfo(sensor));

                    entities.GetSubs().Add(entity);
                    base._wdyh = entities;
                }
                return base._wdyh;
            }
        }

        public HotboxAgentClient()
            : base() {
            base.GetParameter("Port").DefaultValue = 9999;

            _getSensors = JsonConvert.SerializeObject(new HotboxCommand() { command = "getSensors" });
            _startSensors = JsonConvert.SerializeObject(new HotboxCommand() { command = "startSensors" });
            _stopSensors = JsonConvert.SerializeObject(new HotboxCommand() { command = "stopSensors" });
        }

        public override bool Start() {
            try {
                if (IsConnected && !base._started) {
                    Write(_startSensors);
                    base._started = true;
                    _readMonitorCountersThread = new Thread(() => {
                        while (base._started) {
                            try {
                                string counters = Read("{\"sensors\":[...]}");
                                foreach (string line in counters.Split('\n'))
                                    base.InvokeOnMonitor(ParseCounters(line));
                            } catch (Exception exc) {
                                StopOnCommunicationError();
                                Loggers.Log(Level.Error, "Communication Error. Monitor Stopped.", exc);
                            }
                        }
                    });
                    _readMonitorCountersThread.Start();
                }
            } catch (Exception ex) {
                StopOnCommunicationError();
                Loggers.Log(Level.Error, "Failed starting the monitor.", ex);
            }
            return base._started;
        }

        public override bool Stop() {
            try {
                if (base._started) {
                    base._started = false;
                    if (_readMonitorCountersThread != null) {
                        try { _readMonitorCountersThread.Join(); } catch { }
                        _readMonitorCountersThread = null;
                    }
                    Write(_stopSensors);
                }
            } catch (Exception ex) {
                base._started = true;
                Loggers.Log(Level.Error, "Failed stopping the monitor.", ex);
            }
            return !base._started;
        }

        private bool StopOnCommunicationError() {
            if (base._started) {
                base._started = false;
                if (_readMonitorCountersThread != null) {
                    try { _readMonitorCountersThread.Join(); } catch { }
                    _readMonitorCountersThread = null;
                }
                try {
                    Write(_stopSensors);
                } catch {
                    //Ignore.
                }
            }
            return !base._started;
        }

        protected override void Write(string write) {
            if (base._verboseConsoleOutput)
                Console.WriteLine("Out: " + write);
            if (!write.EndsWith("\n")) write += '\n';
            _socket.Send(SerializationHelper.Encode(write, SerializationHelper.TextEncoding.UTF8));
        }
        protected override string Read(string expectedResponse) {
            string read = string.Empty;
            while (base._socket.Connected && !read.EndsWith("\n")) {
                byte[] buffer = new byte[base._bufferSize];
                base._socket.Receive(buffer);
                read += SerializationHelper.Decode(buffer, SerializationHelper.TextEncoding.UTF8);
            }
            if (base._verboseConsoleOutput) {
                Console.Write("In: ");
                Console.Write(read);
            }

            read = RemoveChars(read, ' ').TrimEnd('\0', '\n');

            if (read.Length == 0)
                throw new Exception("The read message is empty.");

            return read;
        }
        private string RemoveChars(string s, char c) {
            string newS = string.Empty;
            foreach (char cc in s)
                if (cc != c) newS += cc;
            return newS;
        }

        public override void Test(bool verboseConsoleOutput, int id, params object[] parameterValues) {
            base._verboseConsoleOutput = verboseConsoleOutput;
            base._id = id;
            try {
                try {
                    Console.WriteLine("Test " + base._id + " Started");
                    if (verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Setting the parameters.");
                    if (!IsConnected) SetParameterValues(parameterValues);

                    if (verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Connecting to the monitor source...");
                    Connect();

                    if (!IsConnected)
                        throw new Exception("Test " + base._id + " Failed to connect to the monitor source.");

                    //string agentName = AgentName;
                    //string agentVersion = AgentVersion;
                    //string agentCopyright = AgentCopyright;
                    string config = Config;
                    int refreshCountersInterval = RefreshCountersInterval;
                    string decimalSeparator = DecimalSeparator;

                    base._wiw = DetermineRandomWiwEntities();
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Random wiw determined: " + WIWRepresentation);
                    //WriteRead(WIWRepresentation);

                    Write(_startSensors);
                    _started = true;

                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Reading and parsing counters 3 times...");

                    for (int i = 0; i != 3; i++)
                        ParseCounters(Read("{\"sensors\":[...]}"));

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

        /// <summary>
        /// Checks if the counters are valid against wiw. Throws an exception if not.
        /// In case of warnings, thos are written to the console if _verboseConsoleOutput == true.
        /// </summary>
        /// <param name="counters"></param>
        /// <returns></returns>
        private Entities ParseCounters(string counters, bool validate = true) {
            var values = new List<object>();

            HotboxSensors sensors = JsonConvert.DeserializeObject<HotboxSensors>(counters);
            foreach (string ctr in sensors.sensors) {
                string[] unparsedReading = ctr.Split('.');
                double reading = double.Parse(unparsedReading[0]);
                if (unparsedReading.Length == 2) { //Add decimal places.
                    double unparsedReading1 = double.Parse(unparsedReading[1]);
                    if (unparsedReading1 != 0)
                        reading += (unparsedReading1 / (int)(Math.Pow(10, unparsedReading1.ToString().Length)));
                }
                values.Add(reading);
            }

            if (_wih == null)
                _wih = WDYH.Clone();
            _wih.SetCountersAtLastLevel(values.ToArray());

            if (_wiwWithCounters == null)
                _wiwWithCounters = _wiw.Clone();

            _wiwWithCounters.SetCounters(_wih);

            if (validate)
                ValidateCounters(_wiwWithCounters);

            return _wiwWithCounters;
        }
        /// <summary>
        /// Not implemented.
        /// </summary>
        /// <param name="downloadSpeedInMbps"></param>
        /// <param name="uploadSpeedInMbps"></param>
        public override void TestBandwidth(out double downloadSpeedInMbps, out double uploadSpeedInMbps) {
            downloadSpeedInMbps = -1;
            uploadSpeedInMbps = -1;
        }
    }
}
