/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using RandomUtils;
using System;
using System.Reflection;

namespace vApus.Monitor.Sources.Base {
    /// <summary>
    /// <para>Use this if you want one client that supports multiple protocols.</para>
    /// <para>This should only be done if you have multiple versions of the same agent or monitor source.</para>
    /// <para>In the connect you must determine the client to use (internal class, so it is not visible to the outside.)</para>
    /// <para>Do not forget to subscribe to the clients' OnMonitor and re invoke it.</para>
    /// </summary>
    public abstract class BaseClientSwitcher : IClient {
        #region Fields
        private readonly BackgroundWorkQueue _backgroundWorkQueue = new BackgroundWorkQueue();
        public event System.EventHandler<OnMonitorEventArgs> OnMonitor;

        private string _name, _description;
        protected Parameter[] _parameters = { };

        protected IClient _client;
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

        public bool IsConnected {
            get {
                if (_client == null)
                    return false;
                return _client.IsConnected;
            }
        }

        /// <summary>
        /// The hardware configuration of the monitored machine if applicable.
        /// Fails if not connected.
        /// </summary>
        public string Config { get { return _client.Config; } }

        /// <summary>
        /// In ms. Fails if not connected.
        /// </summary>
        public int RefreshCountersInterval { get { return _client.RefreshCountersInterval; } }

        /// <summary>
        /// Can be . or , because of for instance the locale settings. Fails if not connected.
        /// </summary>
        public string DecimalSeparator { get { return _client.DecimalSeparator; } }

        /// <summary>
        /// What do you have: the available counters
        /// Fails if not connected.
        /// </summary>
        public Entities WDYH { get { return _client.WDYH; } }

        /// <summary>
        /// Get: Json serialized what do you have: the available counters. Returns null if _wdyh == null.
        /// Set: Serialized the given value and stores it in _wdyh.
        /// Fails if not connected.
        /// </summary>
        public string WDYHRepresentation {
            get { return _client.WDYHRepresentation; }
            set { _client.WDYHRepresentation = value; }
        }
        /// <summary>
        /// What I want
        /// Fails if not connected
        /// </summary>
        public Entities WIW {
            get { return _client.WIW; }
            set { _client.WIW = value; }
        }

        /// <summary>
        /// Get: Json serialized what I want. Returns null if _wiw == null.
        /// Set: Serialized the given value and stores it in _wiw.
        /// Fails if not connected.
        /// </summary>
        public string WIWRepresentation {
            get { return _client.WIWRepresentation; }
            set { _client.WIWRepresentation = value; }
        }

        public bool IsDisposed { get; private set; }
        #endregion

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
        /// <param name="value1"></param>
        /// <param name="otherValues"></param>
        public void SetParameterValues(params object[] values) {
            if (values.Length > _parameters.Length)
                throw new Exception("The number of values is greater than the number of parameters.");

            for (int i = 0; i != values.Length; i++)
                _parameters[i].Value = values[i];
        }

        /// <summary>
        /// Implement this and call TryConnectClient() for each client you want to connect.
        /// </summary>
        /// <returns></returns>
        public abstract bool Connect();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        /// <returns>The client if it connects or null (the client gets disposed.) Store this in _client.</returns>
        protected IClient TryConnect(IClient client) {
            client.Connect();
            if (client.IsConnected) {
                client.OnMonitor += client_OnMonitor;
                return client;
            }

            client.Dispose();
            client = null;
            return null;
        }

        public bool Start() { return _client.Start(); }

        private void client_OnMonitor(object sender, OnMonitorEventArgs e) { InvokeOnMonitor(e.Counters); }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="counters"></param>
        protected void InvokeOnMonitor(Entities counters) {
            if (OnMonitor != null)
                _backgroundWorkQueue.EnqueueWorkItem(new Action<Entities>(o => {
                    OnMonitor(this, new OnMonitorEventArgs(o));
                }), counters);
        }

        public bool Stop() { return _client.Stop(); }

        public bool Disconnect() {
            if (_client == null)
                return true;
            return _client.Disconnect();
        }

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

        public void Test(bool verboseConsoleOutput, int id, params object[] parameterValues) {
            SetParameterValues(parameterValues);
            Connect();
            _client.Test(verboseConsoleOutput, id, parameterValues);
        }
    }
}
