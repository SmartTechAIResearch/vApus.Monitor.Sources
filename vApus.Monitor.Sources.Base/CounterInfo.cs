/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;

namespace vApus.Monitor.Sources.Base {
    /// <summary>
    /// A wdyh (what do you have) counterInfo to serialize from and to (used lib:
    /// Newtonsoft.Json). Sub CounterInfos
    /// can be added. If a CounterInfo is a 'leafnode', it can have a counter value (type
    /// String to be able to deserialize).
    /// </summary>
    [Serializable]
    public class CounterInfo {
        /// <summary>
        /// For serialization only; use the appropriate get and or set function.
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// For serialization only; use the appropriate get and or set function.
        /// </summary>
        public string counter { get; set; }
        /// <summary>
        /// For serialization only; use the appropriate get and or set function.
        /// </summary>
        public List<CounterInfo> subs { get; set; }

        /// <summary>
        /// For serialization only.
        /// </summary>
        public CounterInfo() { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">Should be unique for the level this CounterInfo is at. The name should have the unit in it, if applicable, like this: name + " (" + unit + ")".</param>
        public CounterInfo(string name) { this.name = name; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">Should be unique for the level this CounterInfo is at. The name should have the unit in it, if applicable, like this: name + " (" + unit + ")".</param>
        /// <param name="counter">The ToString() is stored (ToString() for easy / lazy serialization).</param>
        public CounterInfo(string name, object counter)
            : this(name) {
            if (counter != null)
                this.counter = counter.ToString();
        }

        /// <summary>
        /// Should be unique for the level this CounterInfo is at. The name should have the unit in it, if applicable, like this: name + " (" + unit + ")".
        /// </summary>
        public string GetName() { return this.name; }

        /// <summary>
        /// This is the ToString() of counter value (ToString() for easy / lazy serialization).
        /// </summary>
        public string GetCounter() { return this.counter; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="counter">The ToString() is used</param>
        public void SetCounter(object counter) { this.counter = counter == null ? null : counter.ToString(); }

        /// <summary>
        /// Initiates the internal List if it is null.
        /// </summary>
        public List<CounterInfo> GetSubs() {
            if (this.subs == null)
                this.subs = new List<CounterInfo>();
            return this.subs;
        }

        /// <summary>
        /// Match the name and the subs if any with the given CounterInfo. The order of
        /// CounterInfos in both collections is not important. You can choose not to match
        /// the counter.
        /// </summary>
        /// <param name="counterInfo"></param>
        /// <param name="matchCounter"></param>
        /// <returns></returns>
        internal bool Match(CounterInfo counterInfo, bool matchCounter) {
            bool match = this.name == counterInfo.name;
            if (match && matchCounter) {
                match = this.counter == null && counterInfo.counter == null;
                if (!match)
                    match = this.counter == counterInfo.counter;
            }

            if (match) {
                if (this.subs != null && counterInfo.subs != null && this.subs.Count == counterInfo.subs.Count) {
                    int count = this.subs.Count;
                    List<int> matched = new List<int>();
                    for (int i = 0; i != count; i++) {
                        CounterInfo sub = this.subs[i];
                        for (int j = 0; j != count; j++)
                            if (!matched.Contains(j) && sub.Match(counterInfo.subs[i], matchCounter))
                                matched.Add(j);
                    }
                    match = count == matched.Count;
                } else if (this.subs == null && counterInfo.subs == null) {
                } else {
                    match = false;
                }
            }

            return match;
        }
        /// <summary>
        /// Set the counters for the CounterInfo with the same name to this.
        /// </summary>
        /// <param name="counterInfo"></param>
        internal void SetCounters(CounterInfo counterInfo) {
            if (subs != null && counterInfo.subs != null) {
                foreach (CounterInfo to in subs) {
                    CounterInfo from = counterInfo.GetCounterInfo(0, to.name);
                    to.SetCounters(from);
                }
            } else {
                this.counter = counterInfo.counter;
            }
        }

        private CounterInfo GetCounterInfo(int level, string name) {
            List<CounterInfo> counterInfos = GetCounterInfos(level);
            foreach (CounterInfo counterInfo in counterInfos)
                if (counterInfo.name == name)
                    return counterInfo;

            return null;
        }

        /// <summary>
        /// Throws a NullReferenceException if CounterInfos does not exist at the given level.
        /// Can happen if not all subheaders have the same number of levels.
        /// </summary>
        /// <param name="level">Throws an exception if the given level is smaller than 0.</param>
        /// <returns></returns>
        internal List<CounterInfo> GetCounterInfos(int level) {
            if (level < 0)
                throw new Exception("The given level cannot be smaller than 0.");

            var counterInfos = new List<CounterInfo>();
            if (this.subs != null) {
                if (level == 0) {
                    counterInfos.AddRange(this.subs);
                } else {
                    --level;
                    foreach (CounterInfo sub in this.subs) {
                        List<CounterInfo> subCounterInfos = sub.GetCounterInfos(level);
                        if (subCounterInfos.Count == 0)
                            throw new NullReferenceException("CounterInfos does not exist at the given level.");
                        else
                            counterInfos.AddRange(subCounterInfos);
                    }
                }
            }
            return counterInfos;
        }

        /// <summary>
        /// Providing that all sub CounterInfos have the same number of levels.
        /// </summary>
        /// <returns>0 if no subs.</returns>
        internal int GetLevelCount() {
            int levelCount = 0;
            if (this.subs != null && this.subs.Count != 0) {
                levelCount = 1;
                levelCount += this.subs[0].GetLevelCount();
            }
            return levelCount;
        }

        /// <summary>
        /// The count of all CounterInfos on all levels.
        /// </summary>
        /// <returns></returns>
        internal int GetDeepCount() {
            int deepCount = 0;
            if (this.subs != null) {
                deepCount = this.subs.Count;
                foreach (CounterInfo sub in this.subs)
                    deepCount += sub.GetDeepCount();
            }
            return deepCount;
        }

        /// <summary>
        /// Duplicate CounterInfo names on the same level should not occur.
        /// </summary>
        /// <returns></returns>
        internal bool HasDuplicateNames() {
            if (this.subs != null) {
                var names = new List<String>();
                foreach (CounterInfo sub in this.subs) {
                    if (names.Contains(sub.name))
                        return true;

                    names.Add(sub.name);

                    if (sub.HasDuplicateNames())
                        return true;
                }
            }
            return false;
        }

        internal CounterInfo Clone() {
            var clone = new CounterInfo(this.name, this.counter);
            if (this.subs != null)
                foreach (CounterInfo counterInfo in this.subs)
                    clone.GetSubs().Add(counterInfo.Clone());
            return clone;
        }
    }
}
