using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace vApus.Monitor.Sources.Racktivity {

    internal static class RMCPHelper {
        public const int CURRENT_GUID = 6;
        public const int WATTAGE_GUID = 7;
        public const int VOLTAGE_GUID = 4;
        public const int RMCPCOUNTERS_GUID = 50012;

        public static bool HasUDPAccess(string hostNameOrIp) {
            try {
                if (GetWattage(hostNameOrIp, 1) == -1)
                    throw new Exception("Failed to write and read data after enabling udp requests.");
                return true;
            } catch (Exception ex) {
                Debug.WriteLine("HasUDPAccess: " + ex.Message);
            }
            return false;
        }

        /// <summary>
        /// Will return immediatly if UDP acces was aleady enabled.
        /// </summary>
        /// <param name="hostNameOrIp"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="enable"></param>
        /// <returns></returns>
        public static bool EnableDisableUDPAccess(string hostNameOrIp, string username, string password, bool enable) {
            if (HasUDPAccess(hostNameOrIp))
                return true;

            var credential = new NetworkCredential(username, password);

            var request = HttpWebRequest.Create("http://" + hostNameOrIp + "/login.cgi") as HttpWebRequest;
            request.Credentials = credential;
            request.Method = "POST";

            //create the post data
            string postData = "username=" + username + "&password=" + password;
            byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(postData);
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = byteArray.Length;
            //write the post data
            using (Stream dataStream = request.GetRequestStream())
                dataStream.Write(byteArray, 0, byteArray.Length);

            HttpWebResponse response;
            try {
                response = request.GetResponse() as HttpWebResponse;
            } catch (Exception ex) {
                Debug.WriteLine("EnableUDPAccess: " + ex.Message);
                return false;
            }

            if (response.StatusCode == HttpStatusCode.OK) {
                //logged in
                string url = "http://" + hostNameOrIp + "/API.cgi?ADDR=M1&GUID=40013&TYPE=S&LEN=1&DATA=%8" + (enable ? "2" : "3"); //82 to enable, 83 to disable

                request = HttpWebRequest.Create(url) as HttpWebRequest;
                request.Credentials = credential;
                request.Method = "GET";

                //get the auth cookie
                string auth;
                string[] values = response.Headers.GetValues("Set-Cookie");

                if (values != null && values[0].Length > 0 && values[0].StartsWith("auth")) {
                    auth = values[0];
                } else {
                    Debug.WriteLine("EnableUDPAccess: Could not find the auth cookie");
                    return false;
                }

                request.CookieContainer = new CookieContainer(1, 1, 512);
                request.CookieContainer.PerDomainCapacity = 1;
                var cookie = new Cookie("auth", auth.Substring(auth.IndexOf('=') + 1));
                cookie.Domain = hostNameOrIp;
                request.CookieContainer.Add(cookie);

                int tries = 5;
                while (tries-- != 0)
                    try {
                        response = request.GetResponse() as HttpWebResponse;

                        if (response.StatusCode == HttpStatusCode.OK)
                            break;
                    } catch (Exception ex) {
                        Debug.WriteLine("EnableUDPAccess: " + ex.Message);
                        return false;
                    }

                return HasUDPAccess(hostNameOrIp);
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hostNameOrIp"></param>
        /// <param name="message"></param>
        /// <param name="tries">cannot be smaller than 1.</param>
        /// <param name="timeout">0 to wait forever.</param>
        /// <returns></returns>
        private static RMCPMessage ReadWrite(string hostNameOrIp, RMCPMessage message, int tries = 5, int timeout = 1000) {
            if (tries < 1)
                throw new Exception("tries cannot be smaller than 1.");

            for (int i = 0; i != tries; i++) {
                try {
                    var sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = null;
                    var client = new UdpClient(hostNameOrIp, 0x4004);

                    client.Client.SendTimeout = client.Client.ReceiveTimeout = timeout;
                    using (client) {
                        data = message.GetData();
                        client.Send(data, data.Length);
                        data = client.Receive(ref sender);
                    }
                    if (data == null)
                        throw new Exception("Failed to receive data.");
                    return new RMCPMessage(data);
                } catch (Exception ex) {
                    Console.WriteLine("ReadWrite try " + i + " of " + tries + ": " + ex.Message);
                }
            }
            throw new Exception("The Racktivity PDU didn't respond after " + tries + " tries.. (each 5 seconds timeout)");
        }

        public static RMCPCounters GetCounters(string hostNameOrIp) {
            var message = new RMCPMessage('S', 'P', 1, RMCPCOUNTERS_GUID, 1, 46, 'G', null);
            RMCPMessage recvMes = ReadWrite(hostNameOrIp, message);

            return new RMCPCounters(recvMes.Data);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hostNameOrIp"></param>
        /// <param name="outlet">1 based</param>
        /// <returns></returns>
        public static float GetWattage(string hostNameOrIp, int outlet) {
            var message = new RMCPMessage('S', 'P', 1, WATTAGE_GUID, 1, 8, 'G', null);
            RMCPMessage recvMes = ReadWrite(hostNameOrIp, message);

            byte[] data = recvMes.Data;

            float wattage = ConvertArrayToFloat(data, 2 * (outlet - 1));

            return wattage;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hostNameOrIp"></param>
        /// <param name="outlet">1 based</param>
        /// <returns></returns>
        public static float GetCurrent(string hostNameOrIp, int outlet) {
            var message = new RMCPMessage('S', 'P', 1, CURRENT_GUID, 1, 8, 'G', null);
            RMCPMessage recvMes = ReadWrite(hostNameOrIp, message);

            byte[] data = recvMes.Data;

            float current = ConvertArrayToFloat(data, 2 * (outlet - 1));

            return current / 1000;
        }

        public static float GetVoltage(string hostNameOrIp) {
            var message = new RMCPMessage('S', 'P', 1, VOLTAGE_GUID, 0, 2, 'G', null);
            RMCPMessage recvMes = ReadWrite(hostNameOrIp, message);

            byte[] data = recvMes.Data;

            float voltage = ConvertArrayToFloat(data, 0);

            return voltage / 100;  //unit is in 0.01 V
        }

        #region Time Domain
        public static void SampleChannelTime(string hostNameOrIp, int outlet) {
            var message = new RMCPMessage('S', 'P', 1, 40004, 0, 1, 'S', new byte[] { (byte)((outlet + 1) * 16 + 8) }); // if number of samples = 7 --> 2^7 actual samples (128)
            ReadWrite(hostNameOrIp, message, 1, 5000);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="hostNameOrIp"></param>
        /// <returns>Current and voltage samples for the Sample Channel Time.</returns>
        public static Dictionary<string, List<float>> TimeSample(string hostNameOrIp) {
            var dic = new Dictionary<string, List<float>>();
            var values = new List<float>();

            //get current
            var message = new RMCPMessage('S', 'P', 1, 50013, 1, 2, 'G', null);
            RMCPMessage recvMes = ReadWrite(hostNameOrIp, message);

            byte[] data;
            data = recvMes.Data;

            float currentfactor = ConvertArrayToFloat(data, 0);

            //Current Samples
            message = new RMCPMessage('S', 'P', 1, 50013, 3, 255, 'G', null);

            recvMes = ReadWrite(hostNameOrIp, message);

            data = recvMes.Data;

            for (int i = 0; i < 255; i++) {
                //one sample is 8 bits --> 1 byte!!
                float value = Convert.ToSingle((int)data[i]);

                if (value > 128)
                    value -= 256;

                value *= currentfactor;
                value /= 12800; // /(128 * 100) to convert from 10 mV to V

                values.Add(value);

            }

            dic.Add("voltage", values);
            //clear values
            values.Clear();

            //Voltage samples
            message = new RMCPMessage('S', 'P', 1, 50013, 259, 2, 'G', null);

            recvMes = ReadWrite(hostNameOrIp, message);

            data = recvMes.Data;

            float voltagefactor = ConvertArrayToFloat(data, 0);

            message = new RMCPMessage('S', 'P', 1, 50013, 261, 255, 'G', null);

            recvMes = ReadWrite(hostNameOrIp, message);

            data = recvMes.Data;

            for (int i = 0; i < 255; i++) {
                float value = Convert.ToSingle((int)data[i]);

                if (value > 128)
                    value -= 256;

                value *= voltagefactor;
                value /= 128;

                values.Add(value);
            }

            dic.Add("current", values);

            return dic;
        }
        #endregion

        #region Frequency Domain

        public static void SampleChannelFFT(string hostNameOrIp, int outlet) {
            var message = new RMCPMessage('S', 'P', 1, 40005, 0, 1, 'S', new byte[] { (byte)((outlet + 1) * 16 + 7) });
            ReadWrite(hostNameOrIp, message, 1, 5000);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hostNameOrIp"></param>
        /// <returns>Amplitudes, Ampphases, Voltages and Voltphases samples for the Sample Channel FFT.</returns>
        public static Dictionary<string, List<float>> FreqSample(string hostNameOrIp) {
            var dic = new Dictionary<string, List<float>>();
            List<float> amplitudes = new List<float>(), ampphases = new List<float>();

            //get current
            var message = new RMCPMessage('S', 'P', 1, 50014, 1, 2, 'G', null);
            RMCPMessage recvMes = ReadWrite(hostNameOrIp, message);

            byte[] data;
            data = recvMes.Data;

            float currentfactor = ConvertArrayToFloat(data, 0);

            message = new RMCPMessage('S', 'P', 1, 50014, 3, 252, 'G', null);

            recvMes = ReadWrite(hostNameOrIp, message);

            data = recvMes.Data;

            for (int i = 0; i < 60; i++) {
                float value = ConvertArrayToFloat(data, i * 4);

                value *= currentfactor;
                value /= 4096;
                value /= Convert.ToSingle(Math.Pow(2, 1 / 2)); //V2 (square root of 2)

                amplitudes.Add(value);

                int phase = ConvertArrayToInt(data, i * 4 + 2);
                if (phase > 32768)
                    phase -= 65536;

                ampphases.Add(phase);

            }

            dic.Add("amplitudes", amplitudes);
            dic.Add("ampphases", ampphases);

            //get voltage

            List<float> voltages = new List<float>(), voltphases = new List<float>();

            message = new RMCPMessage('S', 'P', 1, 50014, 259, 2, 'G', null);

            recvMes = ReadWrite(hostNameOrIp, message);

            data = recvMes.Data;

            float voltagefactor = ConvertArrayToFloat(data, 0);

            message = new RMCPMessage('S', 'P', 1, 50014, 261, 252, 'G', null);

            recvMes = ReadWrite(hostNameOrIp, message);

            data = recvMes.Data;

            for (int i = 0; i < 60; i++) {
                float value = ConvertArrayToFloat(data, i * 4);

                value *= voltagefactor;
                value /= 4096;
                value /= Convert.ToSingle(Math.Pow(2, 1 / 2)); //V2 (square root of 2)

                voltages.Add(value / 100); // 10 mV to V

                int phase = ConvertArrayToInt(data, i * 4 + 2);
                if (phase > 32768) phase -= 65536;
                voltphases.Add(phase);
            }

            dic.Add("voltages", voltages);
            dic.Add("voltphases", voltphases);

            return dic;
        }

        #endregion

        private static float ConvertArrayToFloat(byte[] data, int pos) {
            return Convert.ToSingle(ConvertArrayToInt(data, pos));
        }
        private static int ConvertArrayToInt(byte[] data, int pos) {
            if (data.Length <= pos + 1)
                return -1;

            return (int)(data[pos] + data[pos + 1] * 256);

        }
    }

    /*
   From Racktivity:
   Get struct (46 bytes) over UDP using GUID: 50012
   There are new readings available every 4 periods. For a frequency of 50 Hz, a period is 20 ms (1000 / 50).    
   typedef struct
   {
       unsigned  			Irms[8];
       unsigned  			RealPower[8];
       CUR_PORT_STATE	CurPortStatus[8];
       unsigned  			Urms;
       unsigned  			Frequency;
       unsigned 			counter
   }FAST_LOG_DATA;

   The first 16 bytes are the 8 currents in mA.
   The next 16 bytes the wattages in 1/10th watt.
   The next 8 bytes is the port (outlet) state. Check the LSbit. (Bugged!)
   Then 2 bytes for the total voltage drawn.
   Then 2 for de frequency.
   Then 2 bytes for the counter (id): +1 every 4 periods.
       
   Implemented like so that -1 is used for an invalid value
*/
    internal class RMCPCounters {
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
        /// <summary>
        /// In V
        /// </summary>
        public float Voltage { get; private set; }
        /// <summary>
        /// In Hz
        /// </summary>
        public float Frequency { get; private set; }
        /// <summary>
        /// There are new readings available every 4 periods. For a frequency of 50 Hz, a period is 20 ms (1000 / 50).
        /// </summary>
        public int ID { get; private set; }

        public RMCPCounters(byte[] data) {
            if (data.Length != 46)
                throw new ArgumentException("byte[] data has to have a length of 46 bytes");

            //0.001A
            Current = new float[8];
            for (int i = 0; i != 8; i++) {
                float f = ConvertArrayToFloat(data, i * 2);
                if (f != -1) f /= 1000f;
                Current[i] = f;
            }


            //0.1W
            int offset = 16;
            Wattage = new float[8];
            for (int i = 0; i != 8; i++) {
                float f = ConvertArrayToFloat(data, offset + (i * 2));
                if (f != -1) f /= 10f;
                Wattage[i] = f;
            }

            //We should read the status from the least significant bit.
            //However, this is not well implemented in the EnergySwitch, we can get readings for outlets that he says have nothing connected to.
            //Status = new byte[8] {
            //    data[32], data[33], data[34], data[35],
            //    data[36], data[37], data[38], data[39]
            //};

            Available = new bool[8];
            for (int i = 0; i != 8; i++)
                Available[i] = Wattage[i] > 0f;

            Voltage = ConvertArrayToFloat(data, 40) / 100f; //0.01V

            Frequency = ConvertArrayToFloat(data, 42) / 100f;

            //There are new readings available every 4 periods. For a frequency of 50 Hz, a period is 20 ms (1000 / 50).
            ID = ConvertArrayToInt(data, 44);
        }

        /// <summary>
        /// Calculates and returns the power factor (power usage efficiency in %) for a given 1-based outlet.
        /// </summary>
        /// <param name="outlet">1-based</param>
        /// <returns></returns>
        public float GetPowerFactor(int outlet) {
            outlet -= 1;
            if (Available[outlet])
                return (Wattage[outlet] / (Voltage * Current[outlet])) * 100f;
            return 0f;
        }

        private float ConvertArrayToFloat(byte[] data, int pos) {
            return Convert.ToSingle(ConvertArrayToInt(data, pos));
        }
        private int ConvertArrayToInt(byte[] data, int pos) {
            if (data.Length <= pos + 1)
                return -1;
            return (int)(data[pos] + data[pos + 1] * 256);
        }
    }

    internal class RMCPMessage {
        private byte[] CRCtable = new byte[] {
                0, 49, 98, 83, 196, 245, 166, 151, 185, 136, 219, 234, 125, 76, 31, 46, 67, 114, 33, 16,
                135, 182, 229, 212, 250, 203, 152, 169, 62, 15, 92, 109, 134, 183, 228, 213, 66, 115, 32, 17,
                63, 14, 93, 108, 251, 202, 153, 168, 197, 244, 167, 150, 1, 48, 99, 82, 124, 77, 30, 47,
                184, 137, 218, 235, 61, 12, 95, 110, 249, 200, 155, 170, 132, 181, 230, 215, 64, 113, 34, 19,
                126, 79, 28, 45, 186, 139, 216, 233, 199, 246, 165, 148, 3, 50, 97, 80, 187, 138, 217, 232,
                127, 78, 29, 44, 2, 51, 96, 81, 198, 247, 164, 149, 248, 201, 154, 171, 60, 13, 94, 111,
                65, 112, 35, 18, 133, 180, 231, 214, 122, 75, 24, 41, 190, 143, 220, 237, 195, 242, 161, 144,
                7, 54, 101, 84, 57, 8, 91, 106, 253, 204, 159, 174, 128, 177, 226, 211, 68, 117, 38, 23,
                252, 205, 158, 175, 56, 9, 90, 107, 69, 116, 39, 22, 129, 176, 227, 210, 191, 142, 221, 236,
                123, 74, 25, 40, 6, 55, 100, 85, 194, 243, 160, 145, 71, 118, 37, 20, 131, 178, 225, 208,
                254, 207, 156, 173, 58, 11, 88, 105, 4, 53, 102, 87, 192, 241, 162, 147, 189, 140, 223, 238,
                121, 72, 27, 42, 193, 240, 163, 146, 5, 52, 103, 86, 120, 73, 26, 43, 188, 141, 222, 239,
                130, 179, 224, 209, 70, 119, 36, 21, 59, 10, 89, 104, 255, 206, 157, 172};

        #region properties
        public char Start { get; private set; }
        public char A1 { get; private set; }
        public byte A2 { get; private set; }
        public byte Hl { get; private set; }
        public byte Dl { get; private set; }
        public ushort Guid { get; private set; }
        public ulong Begin { get; private set; }
        public byte Len { get; private set; }
        public char Mode { get; private set; }
        public byte[] Data { get; private set; }
        public bool CRC { get; private set; }
        #endregion

        public RMCPMessage(Char start, Char a1, Byte a2, UInt16 guid, UInt64 begin, Byte len, Char mode, Byte[] data) {
            Start = start;
            A1 = a1;
            A2 = a2;
            Hl = (byte)Math.Ceiling(Math.Log(begin + 1, 256));
            Hl = (byte)((Hl == 0) ? 8 : Hl + 9);
            if (data == null) Dl = 0;
            else Dl = (byte)data.Length;
            Guid = guid;
            Begin = begin;
            Len = len;
            Mode = mode;
            Data = data;
            CRC = true;
        }

        public RMCPMessage(Byte[] data) {
            uint begin = 0;
            ushort index = 0;
            byte crc = 0;

            Start = Convert.ToChar(data[index++]);
            A1 = Convert.ToChar(data[index++]);
            A2 = data[index++];
            Hl = data[index++];
            Dl = data[index++];
            Guid = data[index++];
            Guid += (ushort)(data[index++] * 0x100);
            Begin = 1;
            for (int i = 0; i < (Hl - 9); i++) {
                Begin += begin * data[index++];
                begin *= 0x100;
            }
            if (Hl > 8) Len = data[index++];
            Mode = Convert.ToChar(data[index++]);
            Data = new byte[Dl];
            for (ushort i = 0; i < Dl; i++)
                Data[i] = data[index++];

            for (ushort i = 0; i < index; i++)
                crc = CRCtable[crc ^ data[i]];

            CRC = (data[index++] == crc);
        }

        private string ConvertToString(byte data) {
            if (data < 32) return ".";
            return Convert.ToChar(data).ToString();
        }

        public byte[] GetData() {
            byte[] data = new byte[Hl + Dl + 1];
            uint begin = (uint)Begin;
            string character;
            ushort index = 0;
            byte crc = 0;

            character = Convert.ToString(Start);
            data[index++] = (byte)Start;// Encoding.ASCII.GetBytes(character)[0];
            character = Convert.ToString(A1);
            data[index++] = Encoding.ASCII.GetBytes(character)[0];
            data[index++] = A2;
            data[index++] = Hl;
            data[index++] = Dl;
            data[index++] = (byte)(Guid % 0x100);
            data[index++] = (byte)(Guid / 0x100);
            for (int i = 0; i < Hl - 9; i++) {
                data[index++] = (byte)(begin % 0x100);
                begin = (uint)(begin / 0x100);
            }
            if (Hl > 8) data[index++] = Len;
            character = Convert.ToString(Mode);
            data[index++] = Encoding.ASCII.GetBytes(character)[0];
            for (int i = 0; i < Dl; i++)
                data[index++] = (byte)this.Data[i];


            for (int i = 0; i < index; i++)
                crc = CRCtable[crc ^ data[i]];

            data[index++] = crc;

            return data;
        }

        public string GetDataString() {
            return string.Join(",", Array.ConvertAll(Data, x => " " + ConvertToString(x) + " "));
        }

        public string GetDataHEX() {
            return string.Join(",", Array.ConvertAll(Data, x => " " + x.ToString("X2")));
        }

        public string GetDataDEC() {
            return string.Join(",", Array.ConvertAll(Data, x => x.ToString("D3")));
        }

        public string GetDataUnsigned() {
            string result = string.Empty;
            ushort value = 0;
            for (int i = 0; i < Data.Length; i++)
                if (i % 2 == 0) {
                    value = this.Data[i];
                } else {
                    value += ((ushort)(Data[i] * 0x100));
                    result += value.ToString();
                    result += ",";
                }

            return result;
        }

        public string GetStart() {
            if (Start < 0x80) return Start.ToString();
            return ((char)(Start & 0x7F)).ToString() + " Transparant";
        }
    }

}
