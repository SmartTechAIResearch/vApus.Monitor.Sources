/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System.Collections.Generic;

namespace vApus.Monitor.Sources.ESXi {
    internal class PerformanceCounter {
        public int Id { get; set; }
        /// <summary>
        /// (perfInfo.rollupType == PerfSummaryType.none) ? string.Concat(perfInfo.groupInfo.key, '.', perfInfo.nameInfo.key) : string.Concat(perfInfo.groupInfo.key, '.', perfInfo.nameInfo.key, '.', perfInfo.rollupType);
        /// </summary>
        public string DotNotatedName { get; set; }

        public List<string> Instances { get; set; }
    }

    internal class PerformanceCounterComparer : IComparer<PerformanceCounter> {
        private static PerformanceCounterComparer _performanceCounterComparer;

        public static PerformanceCounterComparer GetInstance() {
            if (_performanceCounterComparer == null) _performanceCounterComparer = new PerformanceCounterComparer();
            return _performanceCounterComparer;
        }

        private PerformanceCounterComparer() { }

        public int Compare(PerformanceCounter x, PerformanceCounter y) {
            return x.Id.CompareTo(y.Id);
        }
    }

    internal class PerformanceCounterInstanceComparer : IComparer<string> {
        private static PerformanceCounterInstanceComparer _performanceCounterInstanceComparer;

        public static PerformanceCounterInstanceComparer GetInstance() {
            if (_performanceCounterInstanceComparer == null) _performanceCounterInstanceComparer = new PerformanceCounterInstanceComparer();
            return _performanceCounterInstanceComparer;
        }

        private PerformanceCounterInstanceComparer() { }

        public int Compare(string x, string y) {
            int a, b;
            if (int.TryParse(x, out a) && int.TryParse(y, out b))
                return a.CompareTo(b);
            return x.CompareTo(y);
        }
    }
}
