/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System.Collections.Generic;
using VimApi;

namespace vApus.Monitor.Sources.ESXi {
    internal class Host {
        public ManagedObjectReference Reference { get; set; }
        public HostHardwareInfo HardwareInfo { get; set; }
        public List<VM> VMs { get; set; }
        public List<PerformanceCounter> PerformanceCounters { get; set; }
    }
}
