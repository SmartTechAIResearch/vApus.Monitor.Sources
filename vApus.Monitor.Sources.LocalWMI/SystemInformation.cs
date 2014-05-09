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
        #region "Enum Types"
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

        #region "Structures"
        public struct TimezoneInfo {
            public String standardname;
            public int minoffset;
        }

        public struct LogicalDrive {
            public String name;
            public DriveTypes drivetype;
            public ulong size;
            public ulong freespace;
            public String filesystem;
        }

        public struct IPAddresses {
            public IPAddress address;
            public IPAddress subnet;
        }

        public struct NetworkAdapter {
            public IPAddresses[] networkaddress;
            public Boolean DHCPEnabled;
            public String name;
            public String databasePath;
        }

        public struct Processor {
            public String name;
            public uint speed;
            public String architecture;
        }

        public struct OperatingSystemVersion {
            public uint servicepackmajor;
            public uint servicepackminor;
            public uint major;
            public uint minor;
            public uint type;
            public uint build;
            public String description;
        }
        #endregion

        #region "Variable declarations"
        private NetworkAdapter[] p_adapters;

        private String p_bios;

        private String p_osname;
        private String p_osmanufacturer;
        private OperatingSystemVersion p_osversion;
        private String p_locale;
        private String p_windowsdirectory;
        private ulong p_freephysicalmemory;
        private ulong p_totalphysicalmemory;
        private ulong p_freevirtualmemory;
        private ulong p_totalvirtualmemory;
        private ulong p_pagefilesize;
        private TimezoneInfo p_timezone;
        private String p_computername;

        private String p_domain;
        private String p_systemmanufacturer;
        private String p_systemmodel;
        private String p_systemtype;
        private uint p_numberofprocessors;
        private Processor[] p_processors;

        private LogicalDrive[] p_drives;
        #endregion

        #region "Properties"
        //All these properties will be questioned through reflection so it's traversed from topdown!
        public LogicalDrive[] LogicalDrives {
            get { return p_drives; }
        }

        public String Bios {
            get { return p_bios; }
        }

        public NetworkAdapter[] Adapters {
            get { return p_adapters; }
        }

        public String OSName {
            get { return p_osname; }
        }

        public String OSManufacturer {
            get { return p_osmanufacturer; }
        }

        public string OSVersion {
            get { return p_osversion.description; }
        }

        public String Locale {
            get { return p_locale; }
        }

        public String WindowsDirectory {
            get { return p_windowsdirectory; }
        }

        public ulong FreePhysicalMemory {
            get { return p_freephysicalmemory; }
        }

        public ulong TotalPhysicalMemory {
            get { return p_totalphysicalmemory; }
        }

        public ulong FreeVirtualMemory {
            get { return p_freevirtualmemory; }
        }

        public ulong TotalVirtualMemory {
            get { return p_totalvirtualmemory; }
        }

        public ulong PageFileSize {
            get { return p_pagefilesize; }
        }

        public string LocalTime {
            get { return DateTime.Now.ToString(); }
        }

        public string Timezone {
            get { return p_timezone.standardname; }
        }

        public String ComputerName {
            get { return p_computername; }
        }

        public String Domain {
            get { return p_domain; }
        }

        public String SystemManufacturer {
            get { return p_systemmanufacturer; }
        }

        public String SystemModel {
            get { return p_systemmodel; }
        }

        public String SystemType {
            get { return p_systemtype; }
        }

        public uint NumberOfProcessors {
            get { return p_numberofprocessors; }
        }

        public Processor[] Processors {
            get { return p_processors; }
        }
        #endregion

        public SystemInformation() { }

        public Status Get() {
            return Get(Environment.MachineName);
        }
        public Status Get(String host) {
            return Get(host, null, null);
        }

        public Status Get(String host, String username, String password) {
            // No blank username's allowed.
            if (username == "") {
                username = null;
                password = null;
            }
            // Configure the connection settings.
            ConnectionOptions options = new ConnectionOptions();
            options.Impersonation = ImpersonationLevel.Impersonate;
            options.EnablePrivileges = false;
            options.Username = username; //could be in domain\user format
            options.Password = password;
            ManagementPath path = new ManagementPath(String.Format("\\\\{0}\\root\\cimv2", host));
            ManagementScope scope = new ManagementScope(path, options);

            // Try and connect to the remote (or local) machine.
            try {
                scope.Connect();
            } catch (ManagementException ex) {
                // Failed to authenticate properly.
                Debug.WriteLine("SystemInformatton.Get: " + ex.Message);
                return Status.AuthenticateFailure;
            } catch (System.Runtime.InteropServices.COMException ex) {
                // Unable to connect to the RPC service on the remote machine.
                Debug.WriteLine("SystemInformatton.Get: " + ex.Message);
                return Status.RPCServicesUnavailable;
            } catch (System.UnauthorizedAccessException ex) {
                // User not authorized.
                Debug.WriteLine("SystemInformatton.Get: " + ex.Message);
                return Status.UnauthorizedAccess;
            }

            // Populate the class.
            GetSystemInformation(scope);
            GetNetworkAddresses(scope);
            GetLogicalDrives(scope);

            return Status.Success;
        }

        string GetTimezone(int offset) {
            int hr, min;
            String search, sign;

            if (offset == 0) {
                search = "GMT";
            } else {
                hr = (int)Math.Abs(offset) / 60;
                min = (int)Math.Abs(offset) % 60;
                if (offset < 0)
                    sign = "-";
                else
                    sign = "+";
                search = String.Format("{0}{1:00}:{2:00}", sign, hr, min);
            }

            using (RegistryKey timeZones = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Time Zones", true)) {
                foreach (string subKeyName in timeZones.GetSubKeyNames()) {
                    using (RegistryKey tempKey = timeZones.OpenSubKey(subKeyName)) {
                        string standardName;

                        if (tempKey.GetValue("Display").ToString().IndexOf(search) >= 0) {
                            standardName = tempKey.GetValue("Display").ToString(); //used to be Std but i found that Display is a better name
                            tempKey.Close();
                            timeZones.Close();
                            return standardName;
                        }
                    }
                }
            }

            return "<unknown>";
        }

        private void GetLogicalDrives(ManagementScope scope) {
            ManagementObjectSearcher moSearch = new ManagementObjectSearcher(scope, new ObjectQuery("Select Name, DriveType, Size, FreeSpace, FileSystem from Win32_LogicalDisk Where DriveType = 3 Or DriveType = 6"));
            ManagementObjectCollection moReturn = moSearch.Get();

            p_drives = new LogicalDrive[moReturn.Count];
            int i = 0;
            foreach (ManagementObject mo in moReturn) {
                p_drives[i].drivetype = (DriveTypes)int.Parse(mo["DriveType"].ToString());
                p_drives[i].filesystem = mo["FileSystem"].ToString();
                p_drives[i].freespace = ulong.Parse(mo["FreeSpace"].ToString());
                p_drives[i].size = ulong.Parse(mo["Size"].ToString());
                p_drives[i].name = mo["Name"].ToString();
                i++;
            }
        }

        private void GetSystemInformation(ManagementScope scope) {
            // Only get the first BIOS in the list. Usually this is all there is.
            foreach (ManagementObject mo in new ManagementClass(scope, new ManagementPath("Win32_BIOS"), null).GetInstances()) {
                p_bios = mo["Version"].ToString();
                break;
            }

            foreach (ManagementObject mo in new ManagementClass(scope, new ManagementPath("Win32_OperatingSystem"), null).GetInstances()) {
                p_osversion.build = uint.Parse(mo["BuildNumber"].ToString());
                p_osversion.description = String.Format("{0} {1} Build {2}", mo["Version"], mo["CSDVersion"], mo["BuildNumber"]);
                p_osversion.servicepackmajor = uint.Parse(mo["ServicePackMajorVersion"].ToString());
                p_osversion.servicepackminor = uint.Parse(mo["ServicePackMinorVersion"].ToString());
                p_osversion.type = uint.Parse(mo["OSType"].ToString());
                // Get the major and minor version numbers.
                String[] numbers = mo["Version"].ToString().Split(".".ToCharArray());
                p_osversion.major = uint.Parse(numbers[0]);
                p_osversion.minor = uint.Parse(numbers[1]);
                // Get the rest of the fields.
                p_osname = mo["Name"].ToString().Split("|".ToCharArray())[0];
                p_osmanufacturer = mo["Manufacturer"].ToString();
                p_locale = mo["Locale"].ToString();
                p_windowsdirectory = mo["WindowsDirectory"].ToString();
                p_freevirtualmemory = ulong.Parse(mo["FreeVirtualMemory"].ToString());
                p_totalvirtualmemory = ulong.Parse(mo["TotalVirtualMemorySize"].ToString());
                p_freephysicalmemory = ulong.Parse(mo["FreePhysicalMemory"].ToString());
                p_totalphysicalmemory = ulong.Parse(mo["TotalVisibleMemorySize"].ToString());
                p_pagefilesize = ulong.Parse(mo["SizeStoredInPagingFiles"].ToString());
                p_computername = mo["CSName"].ToString();
                // Get the information related to the timezone.
                p_timezone.minoffset = int.Parse(mo["CurrentTimeZone"].ToString());
                p_timezone.standardname = GetTimezone(p_timezone.minoffset);
                break;
            }

            foreach (ManagementObject mo in new ManagementClass(scope, new ManagementPath("Win32_ComputerSystem"), null).GetInstances()) {
                p_systemmanufacturer = mo["Manufacturer"].ToString();
                p_systemmodel = mo["Model"].ToString();
                p_systemtype = mo["SystemType"].ToString();
                p_domain = mo["Domain"].ToString();
                p_numberofprocessors = uint.Parse(mo["NumberOfProcessors"].ToString());
                break;
            }

            ManagementObjectSearcher moSearch = new ManagementObjectSearcher(scope, new ObjectQuery("Select Name, CurrentClockSpeed, Architecture from Win32_Processor"));
            ManagementObjectCollection moReturn = moSearch.Get();

            p_processors = new Processor[moReturn.Count];
            int i = 0;
            foreach (ManagementObject mo in moReturn) {
                p_processors[i].name = mo["Name"].ToString().Trim();
                p_processors[i].architecture = mo["Architecture"].ToString();
                p_processors[i].speed = uint.Parse(mo["CurrentClockSpeed"].ToString());
                i++;
            }
        }

        private void GetNetworkAddresses(ManagementScope scope) {
            ManagementObjectCollection adapters;
            ManagementObjectSearcher search;

            search = new ManagementObjectSearcher(scope, new ObjectQuery("Select Description, DHCPEnabled, IPAddress, DatabasePath, IPSubnet from Win32_NetworkAdapterConfiguration Where IPEnabled = True"));
            adapters = search.Get();

            p_adapters = new NetworkAdapter[adapters.Count];

            int i = 0;
            foreach (ManagementObject adapter in adapters) {
                p_adapters[i].name = adapter["Description"].ToString();
                p_adapters[i].DHCPEnabled = Boolean.Parse(adapter["DHCPEnabled"].ToString());
                p_adapters[i].databasePath = adapter["DatabasePath"].ToString();

                if (adapter["IPAddress"] != null) {
                    p_adapters[i].networkaddress = new IPAddresses[((string[])adapter["IPAddress"]).Length];
                    for (int j = 0; j < ((string[])adapter["IPAddress"]).Length; j++) {
                        p_adapters[i].networkaddress[j].address = IPAddress.Parse(((string[])adapter.Properties["IPAddress"].Value)[j]);
                        p_adapters[i].networkaddress[j].subnet = IPAddress.Parse(((string[])adapter.Properties["IPSubnet"].Value)[j]);
                    }
                }
            }
        }

    }
}

