using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading.Tasks;
using VimApi;
using System.Web.Services.Protocols;
using System.Net;

namespace vApus.Monitor.Sources.ESXi {
    internal class ESXiHelper {
        private VimService _service;
        private ServiceContent _serviceContent; //Getting stuff through the content.
        private ManagedObjectReference _hostRef;
        private PerfProviderSummary _perfProviderSummary;

        public string HostNameOrIPAddress { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }

        /// <summary>
        /// Connects to a specified ESX host
        /// </summary>
        /// <param name="ipadress"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public bool Connect() {
            //Accept all (self-signed) ssl certificates.
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback((sender, certificate, chain, policyErrors) => { return true; });

            ManagedObjectReference siMOR = new ManagedObjectReference();
            siMOR.Value = "ServiceInstance"; //HostAgent for vCenter
            siMOR.type = "ServiceInstance";

            var seMOR = new ManagedObjectReference();
            //seMOR.Value = (vcenter) ? "SessionManager" : "ha-sessionmgr"; only supporting ESXi!
            seMOR.Value = "ha-sessionmgr";
            seMOR.type = "SessionManager";

            _service = new VimService();
            _service.Url = String.Concat("https://", HostNameOrIPAddress, "/sdk/");
            _service.PreAuthenticate = true;
            _service.CookieContainer = new System.Net.CookieContainer();

            try {
                UserSession session = _service.Login(seMOR, Username, Password, "en");
            } catch (Exception ex) {
                Debug.WriteLine("ESX.Connect:" + ex.Message);
                return false;
            }

            ServiceContent _serviceContent = _service.RetrieveServiceContent(siMOR);

            IPAddress address;
            if (IPAddress.TryParse(HostNameOrIPAddress, out address)) {
                _hostRef = _service.FindByIp(_serviceContent.searchIndex, null, HostNameOrIPAddress, false);
            } else {
                IPHostEntry entry = Dns.GetHostEntry(HostNameOrIPAddress);
                _hostRef = _service.FindByDnsName(_serviceContent.searchIndex, null, entry.HostName, false);
            }

            _perfProviderSummary = _service.QueryPerfProviderSummary(_serviceContent.perfManager, _hostRef);

            /*
            //getting all the perfcounters to cache it (and set the dictionary)
            var _perfCounterInfoArr = getAllPerfCounterInfo();

            //the available intervals on the system
            var _availableIntervals = getAllPerfInterval();

            //fill the list of the available virtual machines
            getVirtualMachines();

            var _allMetricsPerEntity = new Dictionary<ManagedObjectReference, List<PerfMetricId>>();
            //get specific countersets for all the entities
            getPerfCountersForAllEntities();

            */
            return true;
        }

        /// <summary>
        /// Returns the HostHardwareInfo
        /// </summary>
        /// <returns></returns>
        public HostHardwareInfo GetHostHardwareInfo() {
            ObjectContent[] results = GetPropertyContent("HostSystem", "hardware", _hostRef);
            return (HostHardwareInfo)results[0].propSet[0].val;
        }

        private ManagedObjectReference[] GetVMs() {
            //First ask the childentity from the rootfolder (datacenter)
            ObjectContent[] oCont = GetPropertyContent("Folder", "childEntity", _serviceContent.rootFolder);

            ManagedObjectReference datacenter = ((ManagedObjectReference[])oCont[0].propSet[0].val)[0];

            //Then ask the vm folder from the datacenter
            oCont = GetPropertyContent("Datacenter", "vmFolder", datacenter);
            ManagedObjectReference vmFolder = (ManagedObjectReference)oCont[0].propSet[0].val;

            //finally get the list of the managed object from the vms.
            oCont = GetPropertyContent("Folder", "childEntity", vmFolder);
            return (ManagedObjectReference[])oCont[0].propSet[0].val;
        }

        private string GetVMName(ManagedObjectReference vmRef) {
            ObjectContent[] oCont = GetPropertyContent("VirtualMachine", "name", vmRef);
            return oCont[0].propSet[0].val.ToString();
        }

        private VirtualMachinePowerState GetVMPowerState(ManagedObjectReference vmRef) {
            ObjectContent[] oCont = GetPropertyContent("VirtualMachine", "runtime", vmRef);
            return ((VirtualMachineRuntimeInfo)oCont[0].propSet[0].val).powerState;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityRef">Host or vm ref.</param>
        /// <returns></returns>
        private List<int> GetPerfCounterIdsForEntity(ManagedObjectReference entityRef) {
            PerfMetricId[] entityCounters = _service.QueryAvailablePerfMetric(_serviceContent.perfManager, entityRef, DateTime.Now, false, DateTime.Now, false, _perfProviderSummary.refreshRate, true);
            var counterIds = new List<int>();

            foreach (PerfMetricId perfMetric in entityCounters)
                //to avoid duplicates, there's a WARNING about this because you will loose data 
                //because the counter can be multiple for multiple instances but since we don't take this in account we can drop this.
                if (perfMetric.instance == "" && !counterIds.Contains(perfMetric.counterId))
                    counterIds.Add(perfMetric.counterId);

            counterIds.Sort();
            return counterIds;
        }

        /// <summary>
        /// Method to ask the objectContent[] for a given propertyType, path and reference to the managedobject you're polling
        /// </summary>
        /// <param name="propertyType"></param>
        /// <param name="path"></param>
        /// <param name="reference"></param>
        /// <returns></returns>
        private ObjectContent[] GetPropertyContent(string propertyType, string path, ManagedObjectReference reference) {
            var propertySpecs = new PropertySpec[] { new PropertySpec() { type = propertyType, pathSet = new string[] { path } } };
            var objectSpecs = new ObjectSpec[] { new ObjectSpec() { obj = reference } };

            // Create PropertyFilterSpec using the PropertySpec and ObjectPec created above.
            var propertyFilterSpecs = new PropertyFilterSpec[] { new PropertyFilterSpec() { propSet = propertySpecs, objectSet = objectSpecs } };

            return _service.RetrieveProperties(_serviceContent.propertyCollector, propertyFilterSpecs);
        }
    }
}
