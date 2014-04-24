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
using System.Linq;
using System.Net;
using System.Net.Security;
using VimApi;

namespace vApus.Monitor.Sources.ESXi {
    /// <summary>
    /// <para>This is a simple helper class to get information back from ESXi for a single host and it's VMs.</para>
    /// <para>That information consists of hardware info for the host and available counters for the host and VMs in a structured / usable manner.</para>
    /// <para>After calling Connect() you can get the Host. Counter values are added to the same Host object calling RefreshCounterValues().</para>
    /// <para>Do not forget to dispose this.</para>
    /// </summary>
    internal class ESXiHelper : IDisposable {
        private VimService _service;
        private ServiceContent _serviceContent; //Getting stuff through the content.
        private Host _host;
        private const int REFRESHRATEINSECONDS = 20;
        private Dictionary<int, PerfCounterInfo> _perfCounterInfos; //key == perf counter info key / perf metric counter id.

        public string HostNameOrIPAddress { get; private set; }
        public string Username { get; private set; }
        public string Password { get; private set; }

        /// <summary>
        /// Calls Connect().
        /// </summary>
        public bool IsReachable { get { return Connect(); } }

        public Host Host {
            get {
                if (_host == null)
                    MakeHost();

                return _host;
            }
        }

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
        private Dictionary<int, PerfCounterInfo> PerfCounterInfos {
            get {
                if (_perfCounterInfos == null) {
                    _perfCounterInfos = new Dictionary<int, PerfCounterInfo>();

                    var perfCounterInfoArr = GetPropertyContent("PerformanceManager", "perfCounter", _serviceContent.perfManager)[0].propSet[0].val as PerfCounterInfo[];

                    foreach (var info in perfCounterInfoArr)
                        _perfCounterInfos.Add(info.key, info);
                }
                return _perfCounterInfos;
            }
        }

        /// <summary>
        /// <para>This is a simple helper class to get information back from ESXi for a single host and it's VMs.</para>
        /// <para>That information consists of hardware info for the host and available counters for the host and VMs in a structured manner.</para>
        /// <para>After calling Connect() you can get the Host. Counter values are added to the same Host object calling RefreshCounterValues().</para>
        /// <para>Do not forget to dispose this.</para>
        /// </summary>
        /// <param name="hostNameOrIPAddress"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public ESXiHelper(string hostNameOrIPAddress, string username, string password) {
            HostNameOrIPAddress = hostNameOrIPAddress;
            Username = username;
            Password = password;

            //Accept all (self-signed) ssl certificates.
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback((sender, certificate, chain, policyErrors) => { return true; });
        }

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

        private void MakeHost() {
            _host = new Host();
            //Get the host ref by IP or by host name.
            IPAddress address;
            if (IPAddress.TryParse(HostNameOrIPAddress, out address))
                _host.Reference = IPAddress.TryParse(HostNameOrIPAddress, out address) ?
                    _service.FindByIp(ServiceContent.searchIndex, null, HostNameOrIPAddress, false) :
                    _service.FindByDnsName(ServiceContent.searchIndex, null, Dns.GetHostEntry(HostNameOrIPAddress).HostName, false);

            _host.HardwareInfo = GetPropertyContent("HostSystem", "hardware", _host.Reference)[0].propSet[0].val as HostHardwareInfo;

            ManagedObjectReference[] vmRefs = GetVMRefs();
            if (vmRefs != null && vmRefs.Length != 0) {
                _host.VMs = new List<VM>();
                foreach (ManagedObjectReference vmRef in vmRefs)
                    _host.VMs.Add(MakeVM(vmRef));
                _host.VMs.Sort(VMComparer.GetInstance());
            }

            _host.PerformanceCounters = MakePerformanceCounters(_host.Reference);
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
            List<PerformanceCounter> performanceCounters = null;

            PerfMetricId[] perfMetricIds = _service.QueryAvailablePerfMetric(ServiceContent.perfManager, reference, DateTime.Now, false, DateTime.Now, false, REFRESHRATEINSECONDS, true);
            if (perfMetricIds != null && perfMetricIds.Length != 0) {
                performanceCounters = new List<PerformanceCounter>();

                foreach (PerfMetricId id in perfMetricIds) {
                    PerformanceCounter perfCounter = performanceCounters.Find(item => item.Id == id.counterId);
                    if (perfCounter == null) {
                        performanceCounters.Add(MakePerformanceCounter(id));
                    } else {
                        if (id.instance != string.Empty) {
                            var instance = new Instance() { Name = id.instance };
                            if (perfCounter.Instances == null) {
                                perfCounter.Instances = new List<Instance>();
                                perfCounter.Instances.Add(instance);
                            } else if (!perfCounter.Instances.Any(item => item.Name == instance.Name)) { //Sadly enough, this is possible.
                                perfCounter.Instances.Add(instance);
                            }
                        }
                    }
                }

                foreach (PerformanceCounter perfCounter in performanceCounters)
                    if (perfCounter.Instances != null)
                        perfCounter.Instances.Sort(InstanceComparer.GetInstance());

                performanceCounters.Sort(PerformanceCounterComparer.GetInstance());
            }


            return performanceCounters;
        }

        private PerformanceCounter MakePerformanceCounter(PerfMetricId id) {
            var performanceCounter = new PerformanceCounter();
            performanceCounter.Id = id.counterId;

            PerfCounterInfo info = PerfCounterInfos[id.counterId];

            performanceCounter.DotNotatedName = info.groupInfo.key + "." + info.nameInfo.key;
            if (info.rollupType != PerfSummaryType.none)
                performanceCounter.DotNotatedName += "." + info.rollupType;

            performanceCounter.Unit = info.unitInfo.key;

            if (id.instance != string.Empty) {
                performanceCounter.Instances = new List<Instance>();
                performanceCounter.Instances.Add(new Instance() { Name = id.instance });
            }

            return performanceCounter;
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

        /// <summary>
        /// Adds values to the host and VMs performance counters.
        /// New values are available every 20 seconds.
        /// </summary>
        public void RefreshCounterValues() {
            //Specifiy what to query.
            var perfQuerySpecs = new List<PerfQuerySpec>();
            perfQuerySpecs.Add(MakePerfQuerySpec(Host.Reference, _host.PerformanceCounters));
            if (_host.VMs != null)
                foreach (VM vm in _host.VMs)
                    perfQuerySpecs.Add(MakePerfQuerySpec(vm.Reference, vm.PerformanceCounters));

            PerfEntityMetricBase[] perfEntityMetrics = _service.QueryPerf(_serviceContent.perfManager, perfQuerySpecs.ToArray());

            //Parse the returned stuff.
            foreach (PerfEntityMetricCSV perfEntityMetricCSV in perfEntityMetrics)
                if (perfEntityMetricCSV.entity.Value == _host.Reference.Value) {
                    AddCounterValuesToEntity(perfEntityMetricCSV, _host.PerformanceCounters);
                } else if (_host.VMs != null) {
                    VM vm = _host.VMs.Find(item => item.Reference.Value == perfEntityMetricCSV.entity.Value);
                    AddCounterValuesToEntity(perfEntityMetricCSV, vm.PerformanceCounters);
                }
        }

        private PerfQuerySpec MakePerfQuerySpec(ManagedObjectReference reference, List<PerformanceCounter> performanceCounters) {
            var pqSpec = new PerfQuerySpec() { entity = reference };
            pqSpec.intervalId = REFRESHRATEINSECONDS; //refresh for realtime aka every 20 seconds.
            pqSpec.intervalIdSpecified = true;

            pqSpec.maxSample = 1; //Since we poll at the smallest possible resolution there can only be one valid sample.
            pqSpec.maxSampleSpecified = true;

            pqSpec.format = "csv"; //A compact format.

            var perfMetricIds = new List<PerfMetricId>();
            if (performanceCounters != null)
                foreach (PerformanceCounter perfCounter in performanceCounters)
                    if (perfCounter.Instances == null)
                        perfMetricIds.Add(new PerfMetricId() { counterId = perfCounter.Id, instance = string.Empty });
                    else
                        foreach (Instance instance in perfCounter.Instances)
                            perfMetricIds.Add(new PerfMetricId() { counterId = perfCounter.Id, instance = instance.Name });

            pqSpec.metricId = perfMetricIds.ToArray();

            return pqSpec;
        }

        private void AddCounterValuesToEntity(PerfEntityMetricCSV perfEntityMetricCSV, List<PerformanceCounter> performanceCounters) {
            if (performanceCounters != null)
                foreach (PerfMetricSeriesCSV csvValue in perfEntityMetricCSV.value) {
                    PerformanceCounter counter = performanceCounters.Find(item => item.Id == csvValue.id.counterId);

                    float value = float.Parse(csvValue.value);
                    //counters in procent are always multiplied times 100 by Vim.
                    if (counter.Unit.Equals("percent", StringComparison.CurrentCultureIgnoreCase))
                        value /= 100f;

                    if (string.IsNullOrEmpty(csvValue.id.instance))
                        counter.Value = value;
                    else
                        counter.Instances.Find(item => item.Name == csvValue.id.instance).Value = value;
                }
        }

        public void Dispose() {
            if (_service != null) {
                try { _service.Dispose(); } catch { }
                _service = null;
            }
        }
    }
}
