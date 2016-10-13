/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

using System;
using System.Collections.Generic;
using System.Management;
using System.Text;

namespace vApus.Monitor.Sources.LocalWMI {
    /// <summary>
    /// Summary description for SystemInformation.
    /// </summary>
    public class SystemInformation {

        #region Fields
        private string _computer;
        private string _os;
        private string _system;
        private string _baseboard;
        private string _bios;
        private string _processors;
        private uint _totalCores;
        private string _memory;
        private ulong _totalMemoryInMegabytes;
        private string _disks;
        private string _nics;
        #endregion

        #region Properties
        public string Computer { get { return _computer; } }
        public string OS { get { return _os; } }
        public string System { get { return _system; } }
        public string Baseboard { get { return _baseboard; } }
        public string Bios { get { return _bios; } }
        public string Processors { get { return _processors; } }
        public uint Total_cores { get { return _totalCores; } }
        public string Memory { get { return _memory; } }
        public ulong Total_memory_in_megabytes { get { return _totalMemoryInMegabytes; } }
        public string Disks { get { return _disks; } }
        public string NICs { get { return _nics; } }
        #endregion

        public SystemInformation() { }

        public bool Get() {
            try {
                GetSystemInformation();
            }
            catch {
                return false;
            }

            return true;
        }

        private ManagementScope ConnectScope(string nameSpace = "root\\cimv2") {
            var options = new ConnectionOptions();
            options.Impersonation = ImpersonationLevel.Impersonate;
            options.EnablePrivileges = false;
            options.Username = null;
            options.Password = null;
            var mpath = new ManagementPath(String.Format("\\\\{0}\\{1}", Environment.MachineName, nameSpace));
            var scope = new ManagementScope(mpath, options);

            scope.Connect();

            return scope;
        }

        private void GetSystemInformation() {
            ManagementScope scope = ConnectScope();

            ManagementObjectCollection col = new ManagementObjectSearcher(scope, new ObjectQuery("Select CSName, Version, Name, BuildNumber from Win32_OperatingSystem")).Get();
            foreach (ManagementObject mo in col) {
                _computer = mo["CSName"].ToString().Trim();
                _os = string.Format("{0} {1} Build {2}", mo["Name"].ToString().Split("|".ToCharArray())[0].Trim(), mo["Version"].ToString().Trim(), mo["BuildNumber"].ToString().Trim());
                break;
            }

            col = new ManagementObjectSearcher(scope, new ObjectQuery("Select Manufacturer, Model, Domain from Win32_ComputerSystem")).Get();
            foreach (ManagementObject mo in col) {
                _system = mo["Manufacturer"].ToString().Trim() + " - " + mo["Model"].ToString().Trim();
                _computer += "." + mo["Domain"].ToString().Trim();
                break;
            }

            col = new ManagementObjectSearcher(scope, new ObjectQuery("Select Name from Win32_BIOS WHERE PrimaryBIOS='True'")).Get();
            foreach (ManagementObject mo in col) {
                _bios = mo["Name"].ToString().Trim();
                break;
            }

            col = new ManagementObjectSearcher(scope, new ObjectQuery("Select * from Win32_BaseBoard")).Get();
            foreach (ManagementObject mo in col) {
                _baseboard = string.Empty;
                if (mo["Manufacturer"] != null) _baseboard += (mo["Manufacturer"] ?? "Unknown manufacturer").ToString().Trim();
                if (mo["Model"] != null) _baseboard += " - model: " + mo["Model"].ToString().Trim();
                if (mo["Product"] != null) _baseboard += " - product: " + mo["Product"].ToString().Trim();
                if (mo["PartNumber"] != null) _baseboard += " - part number: " + mo["PartNumber"].ToString().Trim();
            }

            col = new ManagementObjectSearcher(scope, new ObjectQuery("Select Name, NumberOfEnabledCore from Win32_Processor")).Get();
            _totalCores = 0;
            var arr = new string[col.Count];
            int i = 0;
            foreach (ManagementObject mo in col) {
                arr[i++] = mo["Name"].ToString().Trim();
                _totalCores += (uint)mo["NumberOfEnabledCore"];
            }
            _processors = Combine(arr);


            col = new ManagementObjectSearcher(scope, new ObjectQuery("Select Capacity, Manufacturer, Model, PartNumber, SerialNumber, Speed from Win32_PhysicalMemory")).Get();
            arr = new string[col.Count];
            i = 0;
            foreach (ManagementObject mo in col) {
                string ram = ulong.Parse(mo["Capacity"].ToString().Trim()) / (1024 * 1024 * 1024) + " GB";
                if (mo["Manufacturer"] != null) ram += " - manufacturer: " + mo["Manufacturer"].ToString().Trim();
                if (mo["Model"] != null) ram += " - model: " + mo["Model"].ToString().Trim();
                if (mo["PartNumber"] != null) ram += " - part number: " + mo["PartNumber"].ToString().Trim();

                if (mo["Manufacturer"] == null && mo["Model"] == null)
                    ram += " - unknown manufacturer and model";

                ram += " (" + (mo["Speed"] ?? "?").ToString().Trim() + " Mhz)";

                arr[i++] = ram;
            }
            _memory = Combine(arr);

            col = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem")).Get();
            foreach (ManagementObject mo in col)
                _totalMemoryInMegabytes = (ulong)mo["TotalVisibleMemorySize"] / 1024;

            col = new ManagementObjectSearcher(scope, new ObjectQuery("Select Size, Model from Win32_DiskDrive where InterfaceType != 'USB'")).Get();
            arr = new string[col.Count];
            i = 0;
            foreach (ManagementObject mo in col)
                arr[i++] = string.Format("{0} GB - {1}", ulong.Parse(mo["Size"].ToString().Trim()) / (1024 * 1024 * 1024), mo["Model"].ToString().Trim());

            _disks = Combine(arr);

            scope = ConnectScope("root\\StandardCimv2");

            col = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT Name, DriverDescription, MediaConnectState FROM MSFT_NetAdapter WHERE HardwareInterface = 'True' AND EndpointInterface = 'False'")).Get();
            var d = new SortedDictionary<uint, SortedSet<string>>();
            foreach (ManagementObject mo in col) {
                string s = mo["Name"] + " - " + mo["DriverDescription"].ToString().Trim();
                uint mediaConnectState = uint.Parse(mo["MediaConnectState"].ToString().Trim());

                uint sortedState = mediaConnectState;
                if (mediaConnectState == 0) {
                    s += " (unknown status)";
                    sortedState = 3;
                }
                else if (mediaConnectState == 1) {
                    s += " (connected)";
                }
                else if (mediaConnectState == 2) {
                    s += " (disconnected)";
                }

                if (!d.ContainsKey(sortedState)) d.Add(sortedState, new SortedSet<string>());
                d[sortedState].Add(s);
            }
            var l = new List<string>(col.Count);
            for (uint j = 1; j != 4; j++)
                if (d.ContainsKey(j))
                    l.AddRange(d[j]);

            _nics = Combine(l.ToArray());
        }

        /// <summary>
        /// Duplicates are ommitted. A multiplier is added in that case.
        /// </summary>
        /// <param name="arr"></param>
        /// <returns></returns>
        private string Combine(string[] arr) {
            if (arr.Length == 0)
                return string.Empty;

            var d = new Dictionary<string, int>();
            var sb = new StringBuilder();
            foreach (string s in arr) {
                if (d.ContainsKey(s)) ++d[s];
                else d.Add(s, 1);
            }

            foreach (var kvp in d) {
                if (kvp.Value != 1) {
                    sb.Append(kvp.Value);
                    sb.Append(" x ");
                }

                sb.AppendLine(kvp.Key);
            }

            return sb.ToString().Trim();
        }

    }
}

