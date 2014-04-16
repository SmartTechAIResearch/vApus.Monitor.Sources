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
                    Loggers.Log(Level.Warning, "Could not reach " + HostNameOrIPAddress, ex);
                }
                return false;
            }
        }

        public OldRacktivityCounters GetCounters() {

            if (IsReachable) {
                float[] wattage = new float[8];
                for (int i = 0; i != 8; i++)
                    wattage[i] = GetWatt(i + 1);

                bool[] available = new bool[8];
                for (int i = 0; i != 8; i++)
                    available[i] = wattage[i] > 0;

                float[] current = new float[8];
                for (int i = 0; i != 8; i++)
                    current[i] = GetCurrent(i + 1);

                float voltage = GetVoltage();

                float[] powerFactor = new float[8];
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
        public float GetWatt(int outlet) {
            return GetValue(outlet, 80, 10);
        }

        /// <summary>
        /// In Volt.
        /// </summary>
        /// <returns>-1 if fails.</returns>
        public float GetVoltage() {
            return GetValue(96, 100);
        }

        /// <summary>
        /// In Ampere.
        /// </summary>
        /// <param name="outlet">1-based</param>
        /// <returns>-1 if fails.</returns>
        public float GetCurrent(int outlet) {
            return GetValue(outlet, 64, 1000);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="wattage"></param>
        /// <param name="current"></param>
        /// <param name="voltage"></param>
        /// <returns>-1 if not available</returns>
        public float GetPowerFactor(float wattage, float current, float voltage) {
            if (wattage == -1 || current == -1 || voltage == -1)
                return -1f;
            return wattage / (voltage * current) * 100f;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="outlet">1-based</param>
        /// <param name="basePort"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        private float GetValue(int outlet, int basePort, int scale) {
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
        private float GetValue(int port, int scale) {
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

                float value = 0f;
                for (int i = 0; i < arr.Length; i++)
                    value += float.Parse(arr[i]) * Convert.ToSingle(((Math.Pow(256, i))));

                return (value / Convert.ToSingle(scale));
            } catch (Exception ex) {
                Loggers.Log(Level.Warning, "Could not reach " + HostNameOrIPAddress, ex);
            }
            return -1f;
        }
    }

    internal class OldRacktivityCounters {
        /// <summary>
        /// In A
        /// </summary>
        public float[] Current { get; private set; }
        //In W
        public float[] Wattage { get; private set; }
        /// <summary>
        /// Power is getting drawed at outlet?
        /// </summary>
        public bool[] Available { get; private set; }

        public float[] PowerFactor { get; private set; }

        public OldRacktivityCounters(bool[] available, float[] wattage, float[] current, float[] powerFactor) {
            Available = available;
            Wattage = wattage;
            Current = current;
            PowerFactor = powerFactor;
        }
    }
}
