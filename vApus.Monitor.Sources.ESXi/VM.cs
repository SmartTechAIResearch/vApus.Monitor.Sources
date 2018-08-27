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
    internal class VM {
        public ManagedObjectReference Reference { get; set; }
        public string Name { get; set; }
        public VirtualMachinePowerState VirtualMachinePowerState { get; set; }
        public List<PerformanceCounter> PerformanceCounters { get; set; }
    }

    /// <summary>
    /// Compare the names.
    /// </summary>
    internal class VMComparer : IComparer<VM> {
        private static VMComparer _vmComparer;

        public static VMComparer GetInstance() {
            if (_vmComparer == null) _vmComparer = new VMComparer();
            return _vmComparer;
        }

        private VMComparer() { }

        public int Compare(VM x, VM y) {
            return x.Name.CompareTo(y.Name);
        }
    }
}
