/*
 * 2014 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System.Collections.Generic;
using Vim25Api;

namespace vApus.Monitor.Sources.ESXi {
    internal class Host {
        public ManagedObjectReference Reference { get; set; }
        public HostHardwareInfo HardwareInfo { get; set; }
        public List<VM> VMs { get; set; }
        public List<PerformanceCounter> PerformanceCounters { get; set; }
    }
}
