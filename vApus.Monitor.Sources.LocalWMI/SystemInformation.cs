//This file comes from CodeProject (http://www.codeproject.com/KB/IP/remotesysinformation.aspx)
//The author is N. Smith and his website is http://www.simplyneatsoftware.com/
//This has been slighty adjusted by me, Glenn Desmadryl

using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Net;
using System.Text;

namespace vApus.Monitor.Sources.LocalWMI {
    /// <summary>
    /// Summary description for SystemInformation.
    /// </summary>
    public class SystemInformation {
        #region Enums
        public enum DriveTypes {
            Unknown = 0,
            No_Root_Directory = 1,
            Removable_Disk = 2,
            Local_Disk = 3,
            Network_Drive = 4,
            Compact_Disc = 5,
            RAM_Disk = 6
        }

        public enum Status {
            Success = 0,
            AuthenticateFailure = 1,
            UnauthorizedAccess = 2,
            RPCServicesUnavailable = 3
        }
        #endregion

        #region Structs
        public struct LogicalDrive {
            public String name;
            public DriveTypes drivetype;
            public ulong size;
            public ulong freespace;
            public String filesystem;
        }

        #endregion

        #region Fields
        private string _computer;
        private string _os;
        private string _system;
        private string _bios;
        private string _processors;
        private string _memory;
        private string _disks;
        private string _networkAdapters;
        #endregion

        #region Properties
        public string Computer { get { return _computer; } }

        public string OS { get { return _os; } }

        public string System { get { return _system; } }

        public string Bios { get { return _bios; } }

        public string Processors { get { return _processors; } }

        public string Memory { get { return _memory; } }

        public string Disks { get { return _disks; } }

        public string NetworkAdapters { get { return _networkAdapters; } }
        #endregion

        public SystemInformation() { }

        public bool Get() {
            try {
                GetSystemInformation();
            } catch {
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

            foreach (ManagementObject mo in new ManagementClass(scope, new ManagementPath("Win32_OperatingSystem"), null).GetInstances()) {
                _computer = mo["CSName"].ToString();
                _os = string.Format("{0} {1} Build {2}", mo["Name"].ToString().Split("|".ToCharArray())[0], mo["Version"], mo["BuildNumber"]);
                break;
            }

            foreach (ManagementObject mo in new ManagementClass(scope, new ManagementPath("Win32_ComputerSystem"), null).GetInstances()) {
                _system = mo["Manufacturer"].ToString() + " - " + mo["Model"].ToString();
                _computer += "." + mo["Domain"].ToString();
                break;
            }

            foreach (ManagementObject mo in new ManagementClass(scope, new ManagementPath("Win32_BIOS"), null).GetInstances()) {
                _bios = mo["Version"].ToString();
                break;
            }

            ManagementObjectCollection processors = new ManagementObjectSearcher(scope, new ObjectQuery("Select Name from Win32_Processor")).Get();
            var arr = new string[processors.Count];
            int i = 0;
            foreach (ManagementObject mo in processors)
                arr[i++] = mo["Name"].ToString().Trim();
            _processors = Combine(arr);

            ManagementObjectCollection memory = new ManagementObjectSearcher(scope, new ObjectQuery("Select Capacity, Manufacturer, Model, Speed from Win32_PhysicalMemory")).Get();
            arr = new string[memory.Count];
            i = 0;
            foreach (ManagementObject mo in memory)
                arr[i++] = string.Format("{0} GB - {1} - {2} ({3} Mhz)", ulong.Parse(mo["Capacity"].ToString()) / (1024 * 1024 * 1024), mo["Manufacturer"] ?? "unknown manufacturer", mo["Model"] ?? "unknown model", mo["Speed"] ?? "?");
            _memory = Combine(arr);

            ManagementObjectCollection disks = new ManagementObjectSearcher(scope, new ObjectQuery("Select Size, Manufacturer, Model, InterfaceType from Win32_DiskDrive")).Get();
            arr = new string[disks.Count];
            i = 0;
            foreach (ManagementObject mo in disks)
                arr[i++] = string.Format("{0} GB - {1} - {2} ({3})", ulong.Parse(mo["Size"].ToString()) / (1024 * 1024 * 1024), mo["Manufacturer"], mo["Model"], mo["InterfaceType"]);
            _disks = Combine(arr);

            scope = ConnectScope("root\\StandardCimv2");
            //Only real nics are selected.
            //ManagementObjectCollection adapters = new ManagementObjectSearcher(scope, new ObjectQuery(@"SELECT Description FROM Win32_NetworkAdapter WHERE  Manufacturer != 'Microsoft' AND NOT PNPDeviceID LIKE 'ROOT\\%'")).Get();
            ManagementObjectCollection adapters = new ManagementObjectSearcher(scope, new ObjectQuery(@"SELECT Name, DriverDescription, MediaConnectState FROM MSFT_NetAdapter")).Get();
            arr = new string[adapters.Count];
            i = 0;
            foreach (ManagementObject adapter in adapters) {
                string s = adapter["Name"] + " - " + adapter["DriverDescription"];
                uint mediaConnectState = uint.Parse(adapter["MediaConnectState"].ToString());
                if (mediaConnectState == 0)
                    s += " (unknown connection state)";
                if (mediaConnectState == 1)
                    s += " (connected)";
                else if (mediaConnectState == 2)
                    s += " (disconnected)";

                arr[i++] = s;
            }
            _networkAdapters = Combine(arr);
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

