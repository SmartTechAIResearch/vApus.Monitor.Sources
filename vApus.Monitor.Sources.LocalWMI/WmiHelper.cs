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
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.LocalWMI {
    public class WmiHelper {
        private Dictionary<string, PerformanceCounter> _performanceCounters = new Dictionary<string, PerformanceCounter>(); //key == category + "." + counter + "." + instance (__Total__ surrogate for none)

        public string GetHardwareInfo() {
            CultureInfo prevCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            var sb = new StringBuilder();

            var systemInformation = new SystemInformation();
            if (!systemInformation.Get())
                throw new Exception("Failed to get the hardware info.");

            // get all public instance properties
            PropertyInfo[] propertyInfos = typeof(SystemInformation).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo propInfo in propertyInfos) {
                sb.AppendLine(propInfo.Name);
                string value = propInfo.GetValue(systemInformation, null).ToString();
                foreach (string line in value.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)) {
                    sb.Append("  ");
                    sb.AppendLine(line);
                }
                sb.AppendLine();
            }

            Thread.CurrentThread.CurrentCulture = prevCulture;
            return sb.ToString();
        }

        public Entities GetWDYH() {
            CultureInfo prevCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = Thread.CurrentThread.CurrentUICulture = new CultureInfo("en-US");

            var wdyh = new Entities();
            var entity = new Entity("Local WMI", true);
            string defaultInstance = "__Total__";

            PerformanceCounterCategory[] categories = PerformanceCounterCategory.GetCategories();
            Array.Sort(categories, PerformanceCounterCategoryComparer.GetInstance());
            foreach (PerformanceCounterCategory category in categories) {
                string[] instances = category.GetInstanceNames();
                Array.Sort(instances);

                if (instances.Length == 0) {
                    PerformanceCounter[] counters = GetCounters(category);
                    if (counters != null)
                        foreach (PerformanceCounter counter in counters) {
                            //Cleanup invalid counter
                            if (counter.CounterName.Equals("No name", StringComparison.InvariantCultureIgnoreCase))
                                continue;
                            //try { counter.NextValue(); } catch { continue; } Too slow

                            string counterInfoName = category.CategoryName + "." + counter.CounterName;
                            var counterInfo = new CounterInfo(counterInfoName);

                            counterInfo.GetSubs().Add(new CounterInfo(defaultInstance));

                            string name = counterInfoName + "." + defaultInstance;
                            if (!_performanceCounters.ContainsKey(name))
                                _performanceCounters.Add(name, counter);

                            entity.GetSubs().Add(counterInfo);
                        }
                } else {
                    foreach (string instance in instances) {
                        PerformanceCounter[] counters = GetCounters(category, instance);
                        if (counters != null)
                            foreach (PerformanceCounter counter in counters) {
                                //Cleanup invalid counter
                                if (counter.CounterName.Equals("No name", StringComparison.InvariantCultureIgnoreCase))
                                    continue;
                                //try { counter.NextValue(); } catch { continue; } Too slow

                                string counterInfoName = category.CategoryName + "." + counter.CounterName;
                                CounterInfo counterInfo = entity.GetSubs().Find(item => item.GetName() == counterInfoName);

                                string name = counterInfoName + "." + instance;
                                if (!_performanceCounters.ContainsKey(name)) {
                                    if (counterInfo == null) {
                                        counterInfo = new CounterInfo(counterInfoName);
                                        entity.GetSubs().Add(counterInfo);
                                    }

                                    _performanceCounters.Add(name, counter);
                                    counterInfo.GetSubs().Add(new CounterInfo(instance));
                                }
                            }
                    }
                }
            }

            wdyh.Add(entity);

            Thread.CurrentThread.CurrentCulture = prevCulture;

            return wdyh;
        }
        private PerformanceCounter[] GetCounters(PerformanceCounterCategory category, string instance = null) {
            PerformanceCounter[] counters = null;
            try {
                counters = instance == null ? category.GetCounters() : category.GetCounters(instance);
                Array.Sort(counters, PerformanceCounterComparer.GetInstance());
            } catch {
                //Temp counter
            }
            return counters;
        }

        public void RefreshValues(Entities wiw) {
            Entity entity = wiw[0];
            foreach (CounterInfo counterInfo in entity.GetSubs())
                foreach (CounterInfo instance in counterInfo.GetSubs())
                    instance.SetCounter(GetNextValue(counterInfo.GetName() + "." + instance.GetName()));
        }
        private float GetNextValue(string name) {
            if (_performanceCounters.ContainsKey(name))
                try {
                    return _performanceCounters[name].NextValue();
                } catch {
                    _performanceCounters.Remove(name);
                }
            return -1f;
        }

        private class PerformanceCounterCategoryComparer : IComparer<PerformanceCounterCategory> {
            private static PerformanceCounterCategoryComparer _performanceCounterCategoryComparer;

            public static PerformanceCounterCategoryComparer GetInstance() {
                if (_performanceCounterCategoryComparer == null)
                    _performanceCounterCategoryComparer = new PerformanceCounterCategoryComparer();
                return _performanceCounterCategoryComparer;
            }

            private PerformanceCounterCategoryComparer() { }

            public int Compare(PerformanceCounterCategory x, PerformanceCounterCategory y) {
                return x.CategoryName.CompareTo(y.CategoryName);
            }
        }
        private class PerformanceCounterComparer : IComparer<PerformanceCounter> {
            private static PerformanceCounterComparer _performanceCounterComparer;

            public static PerformanceCounterComparer GetInstance() {
                if (_performanceCounterComparer == null)
                    _performanceCounterComparer = new PerformanceCounterComparer();
                return _performanceCounterComparer;
            }

            private PerformanceCounterComparer() { }

            public int Compare(PerformanceCounter x, PerformanceCounter y) {
                return x.CounterName.CompareTo(y.CounterName);
            }
        }

    }
}
