/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 *    Based on code from Glenn Desmadryl
 */
using Newtonsoft.Json;
using RandomUtils;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace vApus.Monitor.Sources.Base {
    /// <summary>
    /// The base to all vApus monitor source clients implementations. A monitor source can be a in house or a third party agent or a device (like a power meter).
    /// The socket communicates over TCP, but this can be changed if you want to.
    /// </summary>
    public abstract class BaseClient : IClient {
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<OnMonitorEventArgs> OnMonitor;

        #region Fields
        private readonly BackgroundWorkQueue _backgroundWorkQueue = new BackgroundWorkQueue();
        /// <summary>
        /// 
        /// </summary>
        protected readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Ignore };

        private string _name, _description;
        /// <summary>
        /// 
        /// </summary>
        protected Parameter[] _parameters = { };
        /// <summary>
        /// 
        /// </summary>
        protected string _config, _decimalSeparator;
        /// <summary>
        /// 
        /// </summary>
        protected Entities _wdyh, _wiw;
        /// <summary>
        /// 
        /// </summary>
        protected int _id = -1; //for tests
        /// <summary>
        /// 
        /// </summary>
        protected bool _started, _verboseConsoleOutput; //verbose = main reason is for tests
        #endregion

        #region Properties
        /// <summary>
        /// This is actualy the Title from the assembly properties.
        /// </summary>
        public string Name {
            get {
                if (_name == null)
                    _name = Assembly.GetAssembly(this.GetType()).GetCustomAttribute<AssemblyTitleAttribute>().Title;
                return _name;
            }
        }
        /// <summary>
        /// This is actualy the Description from the assembly properties.
        /// </summary>
        public string Description {
            get {
                if (_description == null)
                    _description = Assembly.GetAssembly(this.GetType()).GetCustomAttribute<AssemblyDescriptionAttribute>().Description;
                return _description;
            }
        }

        /// <summary>
        /// </summary>
        public Parameter[] Parameters { get { return _parameters; } }
        /// <summary>
        /// 
        /// </summary>
        public abstract bool IsConnected { get; }

        /// <summary>
        /// The hardware configuration of the monitored machine if applicable.
        /// Returns N/A.
        /// </summary>
        public virtual string Config { get { return "N/A"; } }

        /// <summary>
        /// In ms.
        /// </summary>
        public abstract int RefreshCountersInterval { get; }

        /// <summary>
        /// Can be . or , because of for instance the locale settings. 
        /// </summary>
        public abstract string DecimalSeparator { get; }

        /// <summary>
        /// What do you have: the available counters, override to determine the value first in the getter.
        /// </summary>
        public abstract Entities WDYH { get; }

        /// <summary>
        /// Get: Json serialized what do you have: the available counters. Returns null if _wdyh == null.
        /// Set: Serialized the given value and stores it in _wdyh.
        /// </summary>
        public string WDYHRepresentation {
            get {
                if (_wdyh == null) return null;
                return JsonConvert.SerializeObject(_wdyh, Formatting.None, _jsonSerializerSettings);
            }
            set { _wdyh = JsonConvert.DeserializeObject<Entities>(value, _jsonSerializerSettings); }
        }

        /// <summary>
        /// What I want, override if you want to determine the value first in the getter or set the value to an agent in the setter.
        /// For non-agent clients this should not be overriden.
        /// </summary>
        public virtual Entities WIW {
            get { return _wiw; }
            set { _wiw = value; }
        }

        /// <summary>
        /// Get: Json serialized what I want. Returns null if _wiw == null.
        /// Set: Serialized the given value and stores it in _wiw.
        /// </summary>
        public string WIWRepresentation {
            get {
                if (_wiw == null) return null;
                return JsonConvert.SerializeObject(_wiw, Formatting.None, _jsonSerializerSettings);
            }
            set { _wiw = JsonConvert.DeserializeObject<Entities>(value, _jsonSerializerSettings); }
        }
        /// <summary>
        /// 
        /// </summary>
        public bool IsDisposed { get; private set; }
        #endregion

        #region Functions
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The parameter, if found or null.</returns>
        public Parameter GetParameter(string name) {
            foreach (Parameter parameter in Parameters)
                if (parameter.Name == name)
                    return parameter;
            return null;
        }
        /// <summary>
        /// 
        /// Set the values in the parameters.
        /// The number of values cannot be greater than the number of parameters.
        /// The type of the values must be the same as the type of the default values in the parameters.
        /// </summary>
        /// <param name="values"></param>
        public void SetParameterValues(params object[] values) {
            if (values.Length > _parameters.Length)
                throw new Exception("The number of values is greater than the number of parameters.");

            for (int i = 0; i != values.Length; i++)
                _parameters[i].Value = values[i];
        }
        /// <summary>
        /// </summary>
        /// <returns></returns>
        public abstract bool Connect();
        /// <summary>
        /// Use the bool _started to determine if applicable. After started the client should listen for counters, if is is not a polling monitor. (use InvokeOnMonitor()).
        /// </summary>
        public abstract bool Start();
        /// <summary>
        /// 
        /// </summary>
        /// <param name="counters"></param>
        protected void InvokeOnMonitor(Entities counters) {
            if (OnMonitor != null)
                foreach (EventHandler<OnMonitorEventArgs> del in OnMonitor.GetInvocationList())
                    del.BeginInvoke(this, new OnMonitorEventArgs(counters), null, null);
                //_backgroundWorkQueue.EnqueueWorkItem(new Action<Entities>(o => {
                //    OnMonitor(this, new OnMonitorEventArgs(o));
                //}), counters);
        }
        /// <summary>
        /// Use the bool _started to determine if applicable.
        /// </summary>
        public abstract bool Stop();
        /// <summary>
        /// Stops and disconnects, do all other cleanup stuff in Stop().
        /// </summary>
        /// <returns></returns>
        public abstract bool Disconnect();
        /// <summary>
        /// Stops and disconnects, do all other cleanup stuff in Stop().
        /// </summary>
        public void Dispose() {
            if (!IsDisposed) {
                try {
                    IsDisposed = true;
                    Disconnect();
                } catch { }
            }
        }
/// <summary>
/// 
/// </summary>
/// <returns></returns>
        public override string ToString() { return Name; }

        #region Functionality Testing
        /// <summary>
        /// A test should be as complete as possible. All possible RPCs must be done; returned messages must be validated.
        /// </summary>
        /// <param name="verboseConsoleOutput"></param>
        /// <param name="id">Should be unique.</param>
        /// <param name="parameterValues"></param>
        public abstract void Test(bool verboseConsoleOutput, int id, params object[] parameterValues);

        /// <summary>
        /// Use this in a test, throws an exception if fails.
        /// Random entities are determined based on WDYH.
        /// Only available antities are chosen if any.
        /// </summary>
        /// <returns></returns>
        protected Entities DetermineRandomWiwEntities() {
            try {
                Entities wdyh = WDYH;

                var wiw = new Entities();

                bool hasAvailableEntities = false;
                foreach (Entity entity in wdyh.GetSubs())
                    if (entity.IsAvailable()) {
                        hasAvailableEntities = true;
                        break;
                    }

                if (hasAvailableEntities) {
                    bool addedOne = false;

                    // Add minimum one, otherwise parsing the values can go wrong if headers are missing at the last level.
                    while (!addedOne)
                        for (int i = 0; i != wdyh.GetSubs().Count; i++) {
                            //Random seed, otherwise System.currentTimeMillis() is used and I do not want to let the thread sleep.
                            var random = new Random(Guid.NewGuid().GetHashCode());
                            if (random.NextDouble() > 0.5d) {
                                Entity entity = wdyh.GetSubs()[i];
                                if (entity.IsAvailable()) {
                                    addedOne = true;

                                    var newEntity = new Entity(entity.GetName(), entity.IsAvailable());
                                    wiw.GetSubs().Add(newEntity);

                                    ChanceCopySubs(entity, newEntity);
                                }
                            }
                        }
                }
                return wiw;
            } catch (Exception ex) {
                throw new Exception("Could not determine a random wiw, because the given wdyh is malformed: " + ex.Message);
            }
        }
        private void ChanceCopySubs(Entity from, Entity to) {
            List<CounterInfo> subs = from.GetSubs();
            if (subs.Count != 0) {
                bool addedOne = false;
                // Add minimum one, otherwise parsing the values can go wrong if headers are missing at the last level.
                while (!addedOne)
                    for (int i = 0; i != subs.Count; i++) {
                        var random = new Random(Guid.NewGuid().GetHashCode());
                        if (random.NextDouble() > 0.5d) {
                            addedOne = true;
                            CounterInfo counterInfo = subs[i];
                            var newCounterInfo = new CounterInfo(counterInfo.GetName(), counterInfo.GetCounter());
                            to.GetSubs().Add(newCounterInfo);

                            ChanceCopySubs(counterInfo, newCounterInfo);
                        }
                    }
            }
        }
        private void ChanceCopySubs(CounterInfo from, CounterInfo to) {
            List<CounterInfo> subs = from.GetSubs();
            if (subs.Count != 0) {
                bool addedOne = false;

                // Add minimum one, otherwise parsing the values can go wrong if headers are missing at the last level.
                while (!addedOne)
                    for (int i = 0; i != subs.Count; i++) {
                        var random = new Random(Guid.NewGuid().GetHashCode());
                        if (random.NextDouble() > 0.5d) {
                            addedOne = true;
                            CounterInfo counterInfo = subs[i];
                            var newCounterInfo = new CounterInfo(counterInfo.GetName(), counterInfo.GetCounter());
                            to.GetSubs().Add(newCounterInfo);

                            ChanceCopySubs(counterInfo, newCounterInfo);
                        }
                    }
            }
        }

        /// <summary>
        /// Checks if the counters are valid against wiw. Throws an exception if not.
        /// In case of warnings, thos are written to the console if _verboseConsoleOutput == true.
        /// </summary>
        /// <param name="counters"></param>
        protected void ValidateCounters(Entities counters) {
            string warning = counters.ValidateCounters();
            if (_verboseConsoleOutput && warning.Length != 0)
                Console.WriteLine(warning);

            if (counters.HasDuplicateNames())
                throw new Exception("The Entities contain duplicate Entity or CounterInfo names. CounterInfo names must be unique for the level the CounterInfos are on.");


            if (counters.GetDeepCount() != _wiw.GetDeepCount())
                throw new Exception("The number of counters (" + counters.GetSubs().Count + ") is not equal to the number of CounterInfos (" + _wiw.GetSubs().Count + ").");

            var parsedCounters = new List<string>();

            if (!_wiw.Match(counters, false))
                throw new Exception("The counter Entities do not match the wiw Entities.");

            parsedCounters.AddRange(counters.GetCountersAtLastLevel());

            if (_verboseConsoleOutput)
                if (_id == -1)
                    Console.WriteLine("Parsed (" + DateTime.Now.ToString("HH:mm:ss") + "):" + Combine(parsedCounters, " "));
                else
                    Console.WriteLine("Test " + _id + " Parsed (" + DateTime.Now.ToString("HH:mm:ss") + "):" + Combine(parsedCounters, " "));
        }
        private string Combine(List<string> l, string delimiter) {
            var sb = new StringBuilder();
            if (l.Count != 0) {
                for (int i = 0; i != l.Count - 1; i++) {
                    sb.Append(l[i]);
                    sb.Append(delimiter);
                }
                sb.Append(l[l.Count - 1]);
            }
            return sb.ToString();
        }
        #endregion

        #endregion
    }
}
