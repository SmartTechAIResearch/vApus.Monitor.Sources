using RandomUtils.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace vApus.Monitor.Sources.Racktivity {
    internal class OldRacktivityHelper {
        private WebClient _webClient;
        public string HostNameOrIPAddress { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }

        public OldRacktivityHelper(string hostNameOrIPAddress, string username, string password) {
            HostNameOrIPAddress = hostNameOrIPAddress;
            Username = username;
            Password = password;

            _webClient = new WebClient() { Credentials = new NetworkCredential(username, password) };
        }

        public bool IsReachable {
            get {
                try {
                    string urlToWatch = "http://" + HostNameOrIPAddress + "/Data.php?code=GR0008000002I";
                    _webClient.DownloadString(urlToWatch);
                    return true;
                } catch (Exception ex) {
                   // Loggers.Log(Level.Warning, "Could not reach " + HostNameOrIPAddress, ex);
                   // If this is the case, there will be connected using the new racktivity client.
                }
                return false;
            }
        }

        public OldRacktivityCounters GetCounters() {

            if (IsReachable) {
                double[] wattage = new double[8];
                for (int i = 0; i != 8; i++)
                    wattage[i] = GetWatt(i + 1);

                bool[] available = new bool[8];
                for (int i = 0; i != 8; i++)
                    available[i] = wattage[i] > 0;

                double[] current = new double[8];
                for (int i = 0; i != 8; i++)
                    current[i] = GetCurrent(i + 1);

                double voltage = GetVoltage();

                double[] powerFactor = new double[8];
                for (int i = 0; i != 8; i++)
                    powerFactor[i] = GetPowerFactor(wattage[i], current[i], voltage);

                return new OldRacktivityCounters(available, wattage, current, powerFactor);
            }

            return null;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="outlet">1-based</param>
        /// <returns>false if offline or if fails.</returns>
        public bool GetAvailable(int outlet) {
            return GetWatt(outlet) > 0;
        }

        /// <summary>
        /// In Watt.
        /// </summary>
        /// <param name="outlet">1-based</param>
        /// <returns>-1 if fails.</returns>
        public double GetWatt(int outlet) {
            return GetValue(outlet, 80, 10);
        }

        /// <summary>
        /// In Volt.
        /// </summary>
        /// <returns>-1 if fails.</returns>
        public double GetVoltage() {
            return GetValue(96, 100);
        }

        /// <summary>
        /// In Ampere.
        /// </summary>
        /// <param name="outlet">1-based</param>
        /// <returns>-1 if fails.</returns>
        public double GetCurrent(int outlet) {
            return GetValue(outlet, 64, 1000);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="wattage"></param>
        /// <param name="current"></param>
        /// <param name="voltage"></param>
        /// <returns>-1 if not available</returns>
        public double GetPowerFactor(double wattage, double current, double voltage) {
            if (wattage == -1d || current == -1d || voltage == -1d)
                return -1d;
            return wattage / (voltage * current) * 100d;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="outlet">1-based</param>
        /// <param name="basePort"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        private double GetValue(int outlet, int basePort, int scale) {
            //for each next port to question we have to add 2. So for port 2 we'll have 82, for port 3 84,...
            int port = basePort + ((outlet - 1) * 2);
            return GetValue(port, scale);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        /// <param name="scale"></param>
        /// <returns>-1 if fails.</returns>
        private double GetValue(int port, int scale) {
            try {
                //example string: http://192.168.36.37/Data.php?code=GR0009600002I
                string urlToWatch = "http://" + HostNameOrIPAddress.ToString() + "/Data.php?code=GR000" + port.ToString() + "00002I";

                string data = _webClient.DownloadString(urlToWatch);

                //answered string is of this format I = (x,y,z)

                //seperating the corresponding values
                string trimmedData = data.Split('(')[1];
                trimmedData = trimmedData.Replace(')', ' ');

                //split the values into an array
                string[] arr = trimmedData.Split(',');

                //1st value = valueArr[0] * (256^0)
                //2nde value = valueArr[1] * (256^1)

                double value = 0d;
                for (int i = 0; i < arr.Length; i++)
                    value += double.Parse(arr[i]) * Convert.ToSingle(((Math.Pow(256, i))));

                return (value / Convert.ToSingle(scale));
            } catch (Exception ex) {
                Loggers.Log(Level.Warning, "Could not reach " + HostNameOrIPAddress, ex);
            }
            return -1d;
        }
    }

    internal class OldRacktivityCounters {
        /// <summary>
        /// In A
        /// </summary>
        public double[] Current { get; private set; }
        //In W
        public double[] Wattage { get; private set; }
        /// <summary>
        /// Power is getting drawed at outlet?
        /// </summary>
        public bool[] Available { get; private set; }

        public double[] PowerFactor { get; private set; }

        public OldRacktivityCounters(bool[] available, double[] wattage, double[] current, double[] powerFactor) {
            Available = available;
            Wattage = wattage;
            Current = current;
            PowerFactor = powerFactor;
        }
    }
}
