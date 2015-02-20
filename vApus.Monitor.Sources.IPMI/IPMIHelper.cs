/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 *    
 * TODO: P/Invoke ipmiutil.dll instead of reading stdout of a ipmiutil process.
 * ipmiutil is not (.Net) dev-friendly, that's is why I implemented it like this (atm).
 */
using RandomUtils.Log;
using System;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace vApus.Monitor.Sources.IPMI {
    internal class IPMIHelper {
        private Process _process;
        private string _commandText;
        private StringBuilder _output = new StringBuilder(), _error = new StringBuilder();
        private DataTable _sensorData;

        public string HostNameOrIPAddress { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }
        public bool IPMI2dot0 { get; private set; }

        public bool IsReachable {
            get {
                try {
                    DataTable sensorData = FetchIPMISensorData();
                    return sensorData != null && sensorData.Rows.Count != 0;
                } catch (Exception ex) {
                    Loggers.Log(Level.Warning, "Could not reach " + HostNameOrIPAddress, ex);
                }
                return false;
            }
        }

        public IPMIHelper(string hostNameOrIPAddress, string username, string password, bool ipmi2dot0) {
            HostNameOrIPAddress = hostNameOrIPAddress;
            Username = username;
            Password = password;
            IPMI2dot0 = ipmi2dot0;

            _commandText = string.Format(" sensor -c -U {0} -P {1} -N {2}", username, password, hostNameOrIPAddress);
            if (IPMI2dot0) _commandText += " -J 3";

            _process = new Process();
            _process.StartInfo = new ProcessStartInfo(Path.Combine(Application.StartupPath, "MonitorSourceClients\\ipmiutil"));
            _process.StartInfo.Arguments = _commandText;
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.CreateNoWindow = true;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.RedirectStandardError = true;

            _process.OutputDataReceived += _process_OutputDataReceived;
            _process.ErrorDataReceived += _process_ErrorDataReceived;

            _sensorData = new DataTable("IPMI");
            _sensorData.Columns.Add("ID", typeof(string));
            _sensorData.Columns.Add("Sensor", typeof(string)); //name (unit)
            _sensorData.Columns.Add("Status", typeof(string));
            _sensorData.Columns.Add("Reading", typeof(double));
        }


        private void _process_OutputDataReceived(object sender, DataReceivedEventArgs e) {
            _output.AppendLine(e.Data);
            Debug.WriteLine(e.Data);
        }
        private void _process_ErrorDataReceived(object sender, DataReceivedEventArgs e) {
            _error.AppendLine(e.Data);
            Debug.WriteLine(e.Data);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sensorIds">None given, all returned.</param>
        /// <returns>
        /// <para>A datatable with columns: ID, Sensor, Status, Reading</para>
        /// <para>of types: string, string, string, double</para>
        /// <para>Status can be OK or something else if not OK.</para>
        /// </returns>
        public DataTable FetchIPMISensorData(params string[] sensorIds) {
            _sensorData.Rows.Clear();

            string output = null;
                        
            if (sensorIds.Length == 0) {
                _process.StartInfo.Arguments = _commandText; 
                output = GetOutput();
            } else {
                var sb = new StringBuilder();
                foreach (string sensorId in sensorIds) {
                    _process.StartInfo.Arguments = _commandText + " -i " + sensorId;
                    sb.AppendLine(GetOutput());
                }
                output = sb.ToString().Trim();
            }

            AddOutputToSensorData(output);

            return _sensorData;
        }

        private string GetOutput() {
            _output.Clear();
            _error.Clear();

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _process.WaitForExit();

            _process.CancelOutputRead();
            _process.CancelErrorRead();

            string error = _error.ToString();
            if (error != null && error.Contains("error")) throw new Exception(error);

            return _output.ToString();
        }
        private void AddOutputToSensorData(string output) {
            if (output.Contains("|")) {
                var arr = output.Split('\r', '\n');
                foreach (string row in arr)
                    if (row.Contains("|")) {
                        var cells = row.Split('|');
                        if (!cells[0].Contains("ID")) {
                            string id = cells[0].Trim();
                            string name = cells[4].Trim();
                            string status = cells[5].Trim();
                            double reading = -1f;

                            if (cells.Length == 7) {
                                string[] readingArr = cells[6].Trim().Split(' ');
                                if (readingArr.Length != 0) {
                                    if (readingArr.Length > 1)
                                        name += " (" + readingArr[1] + ")";

                                    var unparsedReading = readingArr[0].Split('.');
                                    if (double.TryParse(unparsedReading[0], out reading)) {
                                        if (unparsedReading.Length == 2) { //Add decimal places.
                                            double unparsedReading1;
                                            if (double.TryParse(unparsedReading[1], out unparsedReading1)) {
                                                if (unparsedReading1 != 0)
                                                    reading += (unparsedReading1 / (int)(Math.Pow(10, unparsedReading1.ToString().Length)));
                                            } else {
                                                reading = -1f;
                                            }
                                        }
                                    } else {
                                        reading = -1f;
                                    }
                                }
                            }
                            _sensorData.Rows.Add(id, name, status, reading);
                        }
                    }
            }
        }
    }
}
