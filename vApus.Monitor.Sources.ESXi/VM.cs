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
