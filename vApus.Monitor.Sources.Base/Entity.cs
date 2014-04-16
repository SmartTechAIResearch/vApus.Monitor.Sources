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
    /// A device, client, outlet for whom counters are applicable.
    /// </summary>
    [Serializable]
    public class Entity {
        /// <summary>
        /// For serialization only; use the appropriate get and or set function.
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// For serialization only; use the appropriate get and or set function.
        /// </summary>
        public bool isAvailable { get; set; }
        /// <summary>
        /// For serialization only; use the appropriate get and or set function.
        /// </summary>
        public List<CounterInfo> subs { get; set; }

        /// <summary>
        /// For serialization only.
        /// </summary>
        public Entity() { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name">Should be unique. Is for instance a machine name.</param>
        /// <param name="isAvailable">Ex: Is the machine to monitor powered on.</param>
        public Entity(String name, bool isAvailable) {
            this.name = name;
            this.isAvailable = isAvailable;
        }

        /// <summary>
        /// Should be unique. Is for instance a machine name.
        /// </summary>
        public string GetName() { return this.name; }

        /// <summary>
        /// Ex: Is the machine to monitor powered on.
        /// </summary>
        public bool IsAvailable() { return this.isAvailable; }

        /// <summary>
        /// Initiates the internal List if it is null.
        /// </summary>
        public List<CounterInfo> GetSubs() {
            if (this.subs == null)
                this.subs = new List<CounterInfo>();
            return this.subs;
        }

        /// <summary>     
        /// Match the name and the subs if any with the given CounterInfos. The order
        /// of CounterInfos in both collections is not important. You can choose not
        /// to match the counters.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="matchCounters"></param>
        /// <returns></returns>
        internal bool Match(Entity entity, bool matchCounters) {
            bool match = this.name == entity.name;
            if (match) {
                if (this.subs != null && entity.subs != null && this.subs.Count == entity.subs.Count) {
                    int count = this.subs.Count;
                    var matched = new List<int>();
                    for (int i = 0; i != count; i++) {
                        CounterInfo counterInfo = this.subs[i];
                        for (int j = 0; j != count; j++)
                            if (!matched.Contains(j) && counterInfo.Match(entity.subs[i], matchCounters))
                                matched.Add(j);
                    }
                    match = count == matched.Count;
                } else if (this.subs == null && entity.subs == null) {
                } else {
                    match = false;
                }
            }
            return match;
        }

        /// <summary>     
        /// Set the counters for the CounterInfos with the same name to the
        /// CounterInfos in this. This will happen on all the levels.
        /// </summary>
        /// <param name="entity"></param>
        internal void SetCounters(Entity entity) {
            if (subs != null && entity.subs != null) 
                foreach (CounterInfo to in subs) {
                    CounterInfo from = entity.GetCounterInfo(0, to.GetName());
                    to.SetCounters(from);
                }
        }

        private CounterInfo GetCounterInfo(int level, string name) {
            List<CounterInfo> counterInfos = GetCounterInfos(level);
            foreach (CounterInfo counterInfo in counterInfos)
                if (counterInfo.GetName() == name)
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
                    if (names.Contains(sub.GetName()))
                        return true;

                    names.Add(sub.GetName());

                    if (sub.HasDuplicateNames())
                        return true;
                }
            }
            return false;
        }

        internal Entity Clone() {
            var clone = new Entity(this.name, this.isAvailable);
            if (this.subs != null) 
                foreach (CounterInfo counterInfo in this.subs)
                    clone.GetSubs().Add(counterInfo.Clone());
            return clone;
        }
    }
}
