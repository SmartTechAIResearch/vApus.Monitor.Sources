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
using System.Threading;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.Generic.Agent {
    /// <summary>
    /// A client implementation as described at http://wiki.sizingservers.be/wiki/VApus-vApus-agent_communication_protocol.
    /// Can communicate with every agent that uses the base java packages.
    /// </summary>
    public class GenericAgentClient : BaseSocketClient<string> {
        private string _agentVersion, _agentCopyright;
        private int _refreshCountersInterval;

        private Thread _readMonitorCountersThread;

        /// <summary>
        /// Example: 0.1
        /// </summary>
        public string AgentVersion {
            get {
                if (_agentVersion == null)
                    _agentVersion = WriteRead("version");
                return _agentVersion;
            }
        }

        /// <summary>
        /// Example: Copyright 2014 (c) Sizing Servers Lab\nUniversity College of West-Flanders, Department GKG
        /// </summary>
        public string AgentCopyright {
            get {
                if (_agentCopyright == null)
                    _agentCopyright = WriteRead("copyright");
                return _agentCopyright;
            }
        }

        /// <summary>
        /// The hardware configuration of the monitored machine if applicable.
        /// </summary>
        public override string Config {
            get {
                if (base._config == null)
                    try {
                        base._config = WriteRead("config");
                    } catch (Exception ex) {
                        base._config = "<config>Not available</config>";
                        Loggers.Log(Level.Error, "Failed getting the config.", ex);
                    }
                return base._config;
            }
        }

        /// <summary>
        /// In ms.
        /// </summary>
        public override int RefreshCountersInterval {
            get {
                if (_refreshCountersInterval == 0)
                    _refreshCountersInterval = int.Parse(WriteRead("sendCountersInterval"));
                return _refreshCountersInterval;
            }
        }

        public override string DecimalSeparator {
            get {
                if (base._decimalSeparator == null)
                    base._decimalSeparator = WriteRead("decimalSeparator");
                return base._decimalSeparator;
            }
        }

        /// <summary>
        /// Example: [{"name":"entity","isAvailable":true,"subs":[{"name":"header","subs":[{"name":"subheader"},{"name":"subheader"}]},{"name":"header","subs":[{"name":"subheader"},{"name":"subheader"}]}]}]
        /// </summary>
        public override Entities WDYH {
            get {
                if (base._wdyh == null)
                    WDYHRepresentation = WriteRead("wdyh");
                return base._wdyh;
            }
        }

        /// <summary>
        /// Example set: [{"name":"entity","isAvailable":true,"subs":[{"name":"header","subs":[{"name":"subheader"},{"name":"subheader"}]},{"name":"header","subs":[{"name":"subheader"},{"name":"subheader"}]}]}]
        /// </summary>
        public override Entities WIW {
            get { return base._wiw; }
            set { base._wiw = value; }
        }

        public GenericAgentClient() : base() { }

        public override bool Start() {
            try {
                if (IsConnected && !base._started) {
                    //Reset the connecion to be sure.
                    _socket.Close();

                    if (_wiw == null || _wiw.Count == 0)
                        throw new Exception("You did not set the counters you want to monitor.");

                    Connect();
                    WriteRead(WIWRepresentation);

                    WriteRead("start");
                    base._started = true;
                    //Queue on another thread.
                    _readMonitorCountersThread = new Thread(() => {
                        while (base._started)
                            try {
                                base.InvokeOnMonitor(ParseCounters(Read("[{\"name\":\"entity\",\"isAvailable\":true,\"subs\":[{\"name\":\"header\",\"subs\":...")));
                            }catch(JsonReaderException jex){
                                Loggers.Log(Level.Error, "Communication Error. Dropping the counter.", jex);
                            } catch (Exception ex) {
                                StopOnCommunicationError();
                                Loggers.Log(Level.Error, "Communication Error. Monitor Stopped.", ex);
                            }
                    });
                    _readMonitorCountersThread.Start();
                }
            } catch (Exception ex) {
                StopOnCommunicationError();
                Loggers.Log(Level.Error, "Failed starting the monitor. Monitor stopped.", ex);
            }
            return base._started;
        }

        public override bool Stop() {
            try {
                if (base._started) {
                    base._started = false;
                    bool threadExitedNicely = true;
                    if (_readMonitorCountersThread != null && _readMonitorCountersThread.IsAlive) {
                        if (!_readMonitorCountersThread.Join(5000)) {
                            try { _readMonitorCountersThread.Abort(); } catch { }
                            threadExitedNicely = false;
                        }

                    }
                    _readMonitorCountersThread = null;

                    if (threadExitedNicely)
                        WriteRead("stop");
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
                if (_readMonitorCountersThread != null && _readMonitorCountersThread.IsAlive) {
                    try {
                        if (!_readMonitorCountersThread.Join(5000))
                            _readMonitorCountersThread.Abort();
                    } catch { }
                }
                _readMonitorCountersThread = null;
            }
            return !base._started;
        }

        protected override string WriteRead(string write) {
            if (base._verboseConsoleOutput)
                Console.WriteLine("Out: " + write);
            if (!write.EndsWith("\n")) write += '\n';
            base._socket.Send(SerializationHelper.Encode(write, SerializationHelper.TextEncoding.UTF8));
            return Read(write);
        }
        protected override string Read(string expectedResponse) {
            string read = string.Empty;
            while (base._socket.Connected && !read.EndsWith("\n")) {
                byte[] buffer = new byte[base._bufferSize];
                base._socket.Receive(buffer);
                read += SerializationHelper.Decode(buffer, SerializationHelper.TextEncoding.UTF8);
                if (read.Length == 0) read = "\n"; //Connection error.
            }
            if (base._verboseConsoleOutput) {
                Console.Write("In: ");
                Console.Write(read);
            }
            //The last char is a \n
            read = read.Substring(0, read.Length - 1);

            if (read == "404")
                throw new Exception("404: expected " + expectedResponse.Trim());
            else if (read.Length == 0)
                throw new Exception("The read message is empty. Probably the connection to the agent faulted.");

            return read;
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

                    string agentVersion = AgentVersion;
                    string agentCopyright = AgentCopyright;
                    string config = Config;
                    int refreshCountersInterval = RefreshCountersInterval;
                    string decimalSeparator = DecimalSeparator;

                    base._wiw = DetermineRandomWiwEntities();
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Random wiw determined: " + WIWRepresentation);
                    WriteRead(WIWRepresentation);

                    WriteRead("start");
                    _started = true;

                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Reading and parsing counters 3 times...");

                    for (int i = 0; i != 3; i++)
                        ParseCounters(Read("[{\"name\":\"entity\",\"isAvailable\":true,\"subs\":[{\"name\":\"header\",\"subs\":..."));

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
            Entities entities = JsonConvert.DeserializeObject<Entities>(counters, _jsonSerializerSettings);
            if (validate)
                ValidateCounters(entities);
            return entities;
        }
    }
}
