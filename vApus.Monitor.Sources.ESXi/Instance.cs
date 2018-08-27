/*
 * 2014 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System.Collections.Generic;

namespace vApus.Monitor.Sources.ESXi {
    internal class Instance {
        public string Name { get; set; }
        public double? Value { get; set; }
    }

    /// <summary>
    /// Compare the names. Tries a numeric approach first. If that fails a default text compare is used.
    /// </summary>
    internal class InstanceComparer : IComparer<Instance> {
        private static InstanceComparer _instanceComparer;

        public static InstanceComparer GetInstance() {
            if (_instanceComparer == null) _instanceComparer = new InstanceComparer();
            return _instanceComparer;
        }

        private InstanceComparer() { }

        public int Compare(Instance x, Instance y) {
            int a, b;
            if (int.TryParse(x.Name, out a) && int.TryParse(y.Name, out b))
                return a.CompareTo(b);
            return x.Name.CompareTo(y.Name);
        }
    }
}
