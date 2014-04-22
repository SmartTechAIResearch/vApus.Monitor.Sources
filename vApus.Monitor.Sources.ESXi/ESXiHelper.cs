/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using VimApi;

namespace vApus.Monitor.Sources.ESXi {
    internal class ESXiHelper : IDisposable {
        private VimService _service;
        private ServiceContent _serviceContent; //Getting stuff through the content.
        private Host _host;
        private PerfProviderSummary _perfProviderSummary;

        public string HostNameOrIPAddress { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }

        public bool IsReachable { get { return Connect(); } }

        private ServiceContent ServiceContent {
            get {
                if (_serviceContent == null) {
                    //Service content to retreive host and vm info.
                    var siMOR = new ManagedObjectReference();
                    siMOR.Value = "ServiceInstance"; //HostAgent for vCenter
                    siMOR.type = "ServiceInstance";
                    _serviceContent = _service.RetrieveServiceContent(siMOR);
                }
                return _serviceContent;
            }
        }
        public Host Host {
            get {
                if (_host == null)
                    _host = MakeHost();

                return _host;
            }
        }

        private PerfProviderSummary PerfProviderSummary {
            get {
                if (_perfProviderSummary == null)
                    _perfProviderSummary = _service.QueryPerfProviderSummary(ServiceContent.perfManager, Host.Reference);
                return _perfProviderSummary;
            }
        }

        public ESXiHelper(string hostNameOrIPAddress, string username, string password) {
            HostNameOrIPAddress = hostNameOrIPAddress;
            Username = username;
            Password = password;

            //Accept all (self-signed) ssl certificates.
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback((sender, certificate, chain, policyErrors) => { return true; });
        }

        /// <summary>
        /// Connects to a specified ESX host
        /// </summary>
        /// <param name="ipadress"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public bool Connect() {
            if (_service == null)
                try {
                    _service = new VimService();
                    _service.Url = String.Concat("https://", HostNameOrIPAddress, "/sdk/");
                    _service.PreAuthenticate = true;
                    _service.CookieContainer = new System.Net.CookieContainer();

                    var seMOR = new ManagedObjectReference();
                    //seMOR.Value = (vcenter) ? "SessionManager" : "ha-sessionmgr"; only supporting ESXi!
                    seMOR.Value = "ha-sessionmgr";
                    seMOR.type = "SessionManager";

                    //Finally connect, we do not need the user session later on.
                    UserSession session = _service.Login(seMOR, Username, Password, "en");
                } catch (Exception ex) {
                    Dispose();
                    Debug.WriteLine("ESX.Connect:" + ex.Message);
                }
            return _service != null;
        }

        private Host MakeHost() {
            var host = new Host();
            //Get the host ref by IP or by host name.
            IPAddress address;
            if (IPAddress.TryParse(HostNameOrIPAddress, out address))
                host.Reference = IPAddress.TryParse(HostNameOrIPAddress, out address) ?
                    _service.FindByIp(ServiceContent.searchIndex, null, HostNameOrIPAddress, false) :
                    _service.FindByDnsName(ServiceContent.searchIndex, null, Dns.GetHostEntry(HostNameOrIPAddress).HostName, false);

            host.HardwareInfo = GetPropertyContent("HostSystem", "hardware", Host.Reference)[0].propSet[0].val as HostHardwareInfo;

            host.VMs = new List<VM>();
            foreach (ManagedObjectReference vmRef in GetVMRefs())
                host.VMs.Add(MakeVM(vmRef));
            host.VMs.Sort(VMComparer.GetInstance());

            host.PerformanceCounters = MakePerformanceCounters(host.Reference);

            return host;
        }
        private ManagedObjectReference[] GetVMRefs() {
            //First ask the childentity from the rootfolder (datacenter)
            ObjectContent[] oCont = GetPropertyContent("Folder", "childEntity", ServiceContent.rootFolder);

            ManagedObjectReference datacenter = (oCont[0].propSet[0].val as ManagedObjectReference[])[0];

            //Then ask the vm folder from the datacenter
            oCont = GetPropertyContent("Datacenter", "vmFolder", datacenter);
            var vmFolder = oCont[0].propSet[0].val as ManagedObjectReference;

            //finally get the list of the managed object from the vms.
            oCont = GetPropertyContent("Folder", "childEntity", vmFolder);
            return oCont[0].propSet[0].val as ManagedObjectReference[];
        }

        private VM MakeVM(ManagedObjectReference reference) {
            var vm = new VM();
            vm.Reference = reference;
            vm.Name = GetPropertyContent("VirtualMachine", "name", vm.Reference)[0].propSet[0].val.ToString();
            vm.VirtualMachinePowerState = ((VirtualMachineRuntimeInfo)GetPropertyContent("VirtualMachine", "runtime", vm.Reference)[0].propSet[0].val).powerState;

            vm.PerformanceCounters = MakePerformanceCounters(vm.Reference);

            return vm;
        }

        private List<PerformanceCounter> MakePerformanceCounters(ManagedObjectReference reference) {
            var performanceCounters = new List<PerformanceCounter>();

            var dict = new Dictionary<int, PerformanceCounter>();
            PerfMetricId[] perfMetricIds = _service.QueryAvailablePerfMetric(ServiceContent.perfManager, reference, DateTime.Now, false, DateTime.Now, false, PerfProviderSummary.refreshRate, true);
            foreach (PerfMetricId id in perfMetricIds)
                if (dict.ContainsKey(id.counterId)) {
                    var perfCounter = dict[id.counterId];
                    if (id.instance != string.Empty && !perfCounter.Instances.Contains(id.instance)) //Sadly enough, this is possible.
                        perfCounter.Instances.Add(id.instance);
                } else {
                    dict.Add(id.counterId, MakePerformanceCounter(id));
                }

            foreach (PerformanceCounter perfCounter in dict.Values) {
                perfCounter.Instances.Sort(PerformanceCounterInstanceComparer.GetInstance());
                performanceCounters.Add(perfCounter);
            }

            dict = null;

            performanceCounters.Sort(PerformanceCounterComparer.GetInstance());

            return performanceCounters;
        }

        private PerformanceCounter MakePerformanceCounter(PerfMetricId id) {
            var performanceCounter = new PerformanceCounter();
            performanceCounter.Id = id.counterId;
            performanceCounter.Instances = new List<string>();
            performanceCounter.Instances.Add(id.instance);

            return performanceCounter;
        }


        public string GetPerfCounterName(int perfCounterID) {
            string name = string.Empty;

            //Array perfCounterKeyDicArray = _perfCounterKeyDic.Values.ToArray();

            //for (int i = 0; i < _perfCounterKeyDic.Values.Count; i++) {
            //    if (perfCounterKeyDicArray.GetValue(i).Equals(id))
            //        return PerfCounterKeyDic.Keys.ToArray()[i];
            //}

            return name;
        }

        private PerfCounterInfo[] getAllPerfCounterInfo() {
            PerfCounterInfo[] counterInfoArr = null;
            Dictionary<int, string> _perfCounterKeyDic = new Dictionary<int, string>();

            ObjectContent[] oCont = GetPropertyContent("PerformanceManager", "perfCounter", _serviceContent.perfManager);

            counterInfoArr = (PerfCounterInfo[])oCont[0].propSet[0].val;

            foreach (PerfCounterInfo perfInfo in counterInfoArr) {
                string dotted_string_notation = (perfInfo.rollupType == PerfSummaryType.none) ?
                    string.Concat(perfInfo.groupInfo.key, '.', perfInfo.nameInfo.key) :
                    string.Concat(perfInfo.groupInfo.key, '.', perfInfo.nameInfo.key, '.', perfInfo.rollupType);

                _perfCounterKeyDic.Add(perfInfo.key, dotted_string_notation);
            }


            return counterInfoArr;
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

            return _service.RetrieveProperties(ServiceContent.propertyCollector, propertyFilterSpecs);
        }

        public void Dispose() {
            if (_service != null) {
                try { _service.Dispose(); } catch { }
                _service = null;
            }
        }
    }
}
