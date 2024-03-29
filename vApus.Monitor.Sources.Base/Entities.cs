﻿/*
 * 2014 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace vApus.Monitor.Sources.Base {
    /// <summary>
    /// Holds all entities with their counter infos.
    /// </summary>
    [Serializable]
    public class Entities {
        private readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// For serialization only; use the appropriate get and or set function.
        /// </summary>
        public long timestamp { get; set; }

        /// <summary>
        /// For serialization only; use the appropriate get and or set function.
        /// </summary>
        public List<Entity> subs { get; set; }

        /// <summary>
        /// Do not forget to call this. Stores milliseconds since epoch (utc).
        /// </summary>
        public void SetTimestamp() {
            timestamp = (long)(DateTime.UtcNow - _epoch).TotalMilliseconds;
        }
        /// <summary>
        /// Returns the milliseconds since epoch (1970/1/1).
        /// </summary>
        /// <returns></returns>
        public long GetTimestamp() { return timestamp; }

        /// <summary>
        /// Initiates the internal List if it is null.
        /// </summary>
        public List<Entity> GetSubs() {
            if (this.subs == null)
                this.subs = new List<Entity>();
            return this.subs;
        }

        /// <summary>
        /// For serialization only.
        /// </summary>
        public Entities() { }
        /// <summary>
        /// Match the name and the Entities if any with the given Entities. The order
        /// of Entities and CounterInfos in both collections is not important. You
        /// can choose not to match the counters.
        /// </summary>
        /// <param name="entities"></param>
        /// <param name="matchCounters"></param>
        /// <returns></returns>
        public bool Match(Entities entities, bool matchCounters) {
            bool match = GetSubs().Count == entities.GetSubs().Count;
            if (match) {
                int size = subs.Count;
                var matched = new List<int>();
                for (int i = 0; i != size; i++) {
                    Entity entity = subs[i];
                    for (int j = 0; j != size; j++) {
                        if (!matched.Contains(j) && entity.Match(entities.subs[i], matchCounters)) {
                            matched.Add(j);
                        }
                    }
                }
                match = size == matched.Count;
            }
            return match;
        }

        /// <summary>
        /// Gets the counter values for the CounterInfos at a last level. Null values are
        /// included to preserver order, but should not be mixed with real values:
        /// use -1 instead if the counter becomes unavailable or invalid. (Counters
        /// should only be in leaf nodes.)
        /// </summary>
        /// <returns></returns>
        public List<String> GetCountersAtLastLevel() {
            int level = GetLevelCount() - 1;
            if (level < 1 && !GetSubs()[0].IsAvailable())
                return new List<string>();

            return GetCounters(level);
        }

        /// <summary>
        /// Gets the counter values for the CounterInfos at a given level. Null values are
        /// included to preserver order, but should not be mixed with real values:
        /// use -1 instead if the counter becomes unavailable or invalid. (Counters
        /// should only be in leaf nodes.)
        /// </summary>
        /// <param name="level">Throws an exception it the level is smaller than 1.</param>
        /// <returns></returns>
        public List<String> GetCounters(int level) {
            if (level < 1)
                throw new Exception("The given level cannot be smaller than 1.");

            var counters = new List<String>();
            List<CounterInfo> counterInfos = GetCounterInfos(level);
            foreach (CounterInfo counterInfo in counterInfos)
                counters.Add(counterInfo.GetCounter());

            return counters;
        }

        /// <summary>
        /// Set counters at the deepest level.
        /// </summary>
        /// <param name="counters"></param>
        public void SetCountersAtLastLevel(Object[] counters) {
            List<CounterInfo> counterInfos = GetCounterInfosAtLastLevel();
            for (int i = 0; i != counterInfos.Count; i++)
                counterInfos[i].SetCounter(counters[i]);
        }

        /// <summary>
        /// Set the counters for the CounterInfos with the same name to the
        /// CounterInfos in this. This will happen on all the levels.
        /// </summary>
        /// <param name="entities"></param>
        public void SetCounters(Entities entities) {
            for (int i = 0; i != GetSubs().Count; i++) {
                Entity to = subs[i];
                Entity from = entities.GetEntity(to.GetName());

                to.SetCounters(from);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns>If not fount: null.</returns>
        public Entity GetEntity(string name) {
            for (int i = 0; i != GetSubs().Count; i++) {
                Entity entity = subs[i];
                if (entity.GetName() == name)
                    return entity;
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="level">Throws an exception if the given level is smaller than 1.</param>
        /// <param name="name"></param>
        /// <returns>If not fount: null.</returns>
        public CounterInfo GetCounterInfo(int level, String name) {
            if (level < 1)
                throw new Exception("The given level cannot be smaller than 1.");

            List<CounterInfo> counterInfos = GetCounterInfos(level);
            foreach (CounterInfo counterInfo in counterInfos)
                if (counterInfo.GetName() == name)
                    return counterInfo;

            return null;
        }

        /// <summary>
        /// Throws a NullReferenceException if the entity does not be exist at the given
        /// level. Can happen if not all sub CounterInfo have the same number of
        /// levels.
        /// </summary>
        /// <returns></returns>
        public List<CounterInfo> GetCounterInfosAtLastLevel() {
            try {
                return GetCounterInfos(GetLevelCount() - 1);
            } catch (NullReferenceException ex) {
                throw ex;
            }
        }

        /// <summary>
        /// Throws a NullReferenceException if CounterInfos does not exist at the given
        /// level. Can happen if not all sub CounterInfos have the same number of
        /// levels.
        /// Throws an exception if the given level is smaller than 1.
        /// </summary>
        /// <param name="level">The minimum level == 1, since level 0 == the root level.</param>
        /// <returns></returns>
        public List<CounterInfo> GetCounterInfos(int level) {
            if (level < 1)
                throw new Exception("The given level cannot be smaller than 1.");

            int givenLevel = level;
            var counterInfos = new List<CounterInfo>();

            --level;
            for (int i = 0; i != GetSubs().Count; i++) {
                List<CounterInfo> subCounterInfos = subs[i].GetCounterInfos(level);
                if (subs[i].IsAvailable() && subCounterInfos.Count == 0)
                    throw new NullReferenceException("CounterInfos does not exist at the given level (" + givenLevel + ").");
                else
                    counterInfos.AddRange(subCounterInfos);
            }
            return counterInfos;
        }

        /// <summary>
        /// Providing that all subs have the same number of levels.
        /// </summary>
        /// <returns></returns>
        public int GetLevelCount() {
            int levelCount = 0;
            if (GetSubs().Count != 0) {
                levelCount = 1;
                levelCount += subs[0].GetLevelCount();
            }
            return levelCount;
        }

        /// <summary>
        /// The count of all CounterInfos on all levels.
        /// </summary>
        /// <returns></returns>
        public int GetDeepCount() {
            int deepCount = GetSubs().Count;
            for (int i = 0; i != subs.Count; i++)
                deepCount += subs[i].GetDeepCount();

            return deepCount;
        }

        /// <summary>
        /// Validates the counters in all CounterInfos. Returns warnings or throws an
        /// Exception if counter values are mixed with null values in the
        /// CounterInfos of the last level.
        /// </summary>
        /// <returns></returns>
        public string ValidateCounters() {
            var sb = new StringBuilder();

            int levelCount = GetLevelCount();
            int lastLevel = levelCount - 1;
            for (int i = 1; i < lastLevel; i++) {
                List<string> counters = GetCounters(i);

                for (int j = 0; j != counters.Count; j++)
                    if (counters[j] != null) {
                        sb.Append("WARNING: counters found at level ");
                        sb.Append(j);
                        sb.Append(". Counters should only reside at the last level: ");
                        sb.Append(lastLevel);
                        sb.AppendLine();
                    }
            }

            if (lastLevel > 0) {
                List<string> counters = GetCounters(lastLevel);

                bool nullFound = false, notNullFound = false;
                foreach (string counter in counters) {
                    if (counter == null)
                        nullFound = true;
                    else
                        notNullFound = true;

                    if (nullFound && notNullFound)
                        throw new Exception("Counter values are mixed with null values in the CounterInfos of the last level. If a counter value becomes unavailable or invalid, use -1 instead of null.");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Duplicate Entity names and CounterInfos names on the same level should
        /// not occur.
        /// </summary>
        /// <returns></returns>
        public bool HasDuplicateNames() {
            var names = new List<String>();
            for (int i = 0; i != GetSubs().Count; i++) {
                Entity entity = subs[i];
                if (names.Contains(entity.GetName()))
                    return true;

                names.Add(entity.GetName());

                if (entity.HasDuplicateNames())
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Entities Clone() {
            var clone = new Entities();
            foreach (Entity entity in GetSubs())
                clone.GetSubs().Add(entity.Clone());
            return clone;
        }
    }
}
