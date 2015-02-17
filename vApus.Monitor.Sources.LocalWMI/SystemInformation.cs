//This file comes from CodeProject (http://www.codeproject.com/KB/IP/remotesysinformation.aspx)
//The author is N. Smith and his website is http://www.simplyneatsoftware.com/
//This has been slighty adjusted by me, Glenn Desmadryl

using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Management;
using System.Net;

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
        private string[] _processors;
        private string[] _memory;
        private string[] _disks;
        private string[] _networkAdapters;
        #endregion

        #region Properties
        public string Computer { get { return _computer; } }

        public string OS { get { return _os; } }

        public string System { get { return _system; } }

        public string Bios { get { return _bios; } }

        public string[] Processors { get { return _processors; } }

        public string[] Memory { get { return _memory; } }

        public string[] Disks { get { return _disks; } }

        public string[] NetworkAdapters { get { return _networkAdapters; } }
        #endregion

        public SystemInformation() { }

        public Status Get() {
            // Configure the connection settings.
            var options = new ConnectionOptions();
            options.Impersonation = ImpersonationLevel.Impersonate;
            options.EnablePrivileges = false;
            options.Username = null;
            options.Password = null;
            var path = new ManagementPath(String.Format("\\\\{0}\\root\\cimv2", Environment.MachineName));
            var scope = new ManagementScope(path, options);

            // Try and connect to the remote (or local) machine.
            try {
                scope.Connect();
            } catch (ManagementException ex) {
                // Failed to authenticate properly.
                Debug.WriteLine("SystemInformation.Get: " + ex.Message);
                return Status.AuthenticateFailure;
            } catch (System.Runtime.InteropServices.COMException ex) {
                // Unable to connect to the RPC service on the remote machine.
                Debug.WriteLine("SystemInformation.Get: " + ex.Message);
                return Status.RPCServicesUnavailable;
            } catch (System.UnauthorizedAccessException ex) {
                // User not authorized.
                Debug.WriteLine("SystemInformation.Get: " + ex.Message);
                return Status.UnauthorizedAccess;
            }

            // Populate the class.
            GetSystemInformation(scope);

            return Status.Success;
        }

        private void GetSystemInformation(ManagementScope scope) {
            foreach (ManagementObject mo in new ManagementClass(scope, new ManagementPath("Win32_OperatingSystem"), null).GetInstances()) {
                _computer = mo["CSName"].ToString();
                _os = string.Format("{0} {1} {2} Build {3}", mo["Name"].ToString().Split("|".ToCharArray())[0], mo["Version"], mo["CSDVersion"], mo["BuildNumber"]);
                break;
            }

            foreach (ManagementObject mo in new ManagementClass(scope, new ManagementPath("Win32_ComputerSystem"), null).GetInstances()) {
                _system = mo["Manufacturer"].ToString() + " " + mo["Model"].ToString();
                _computer += "." + mo["Domain"].ToString();
                break;
            }

            foreach (ManagementObject mo in new ManagementClass(scope, new ManagementPath("Win32_BIOS"), null).GetInstances()) {
                _bios = mo["Version"].ToString();
                break;
            }

            ManagementObjectCollection processors = new ManagementObjectSearcher(scope, new ObjectQuery("Select Name from Win32_Processor")).Get();
            _processors = new string[processors.Count];
            int i = 0;
            foreach (ManagementObject mo in processors)
                _processors[i++] = mo["Name"].ToString().Trim();

            ManagementObjectCollection memory = new ManagementObjectSearcher(scope, new ObjectQuery("Select Capacity, Manufacturer, Model, Speed from Win32_PhysicalMemory")).Get();
            _memory = new string[memory.Count];
            i = 0;
            foreach (ManagementObject mo in memory)
                _memory[i++] = string.Format("{0} GB {1} {2} ({3} Mhz)", ulong.Parse(mo["Capacity"].ToString()) / (1024 * 1024 * 1024), mo["Manufacturer"], mo["Model"], mo["Speed"]);


            ManagementObjectCollection disks = new ManagementObjectSearcher(scope, new ObjectQuery("Select Size, Manufacturer, Model, InterfaceType from Win32_DiskDrive")).Get();
            _disks = new string[disks.Count];
            i = 0;
            foreach (ManagementObject mo in disks)
                _disks[i++] = string.Format("{0} GB {1} {2} ({3})", ulong.Parse(mo["Size"].ToString()) / (1024 * 1024 * 1024), mo["Manufacturer"], mo["Model"], mo["InterfaceType"]);

            ManagementObjectCollection adapters = new ManagementObjectSearcher(scope, new ObjectQuery("Select Description from Win32_NetworkAdapterConfiguration")).Get();
            _networkAdapters = new string[adapters.Count];
            i = 0;
            foreach (ManagementObject adapter in adapters)
                _networkAdapters[i++] = adapter["Description"].ToString();

        }

    }
}

