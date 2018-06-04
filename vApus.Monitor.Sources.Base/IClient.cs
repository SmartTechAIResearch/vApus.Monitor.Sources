/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;

namespace vApus.Monitor.Sources.Base {
    /// <summary>
    /// Implemented by BaseClient
    /// </summary>
    public interface IClient : IDisposable {
        /// <summary>
        /// 
        /// </summary>
        event EventHandler<OnMonitorEventArgs> OnMonitor;

        #region Properties
        /// <summary>
        /// 
        /// </summary>
        string Name { get; }
        /// <summary>
        /// 
        /// </summary>
        string Description { get; }
        /// <summary>
        /// 
        /// </summary>
        Parameter[] Parameters { get; }
        /// <summary>
        /// 
        /// </summary>
        bool IsConnected { get; }
        /// <summary>
        /// The hardware configuration of the monitored machine if applicable.
        /// Returns N/A
        /// </summary>
        string Config { get; }
        /// <summary>
        /// In ms.
        /// </summary>
        int RefreshCountersInterval { get; }
        /// <summary>
        /// Can be . or , because of for instance the locale settings. 
        /// </summary>
        string DecimalSeparator { get; }
        /// <summary>
        /// What do you have: the available counters
        /// </summary>
        Entities WDYH { get; }
        /// <summary>
        /// Get: Json serialized what do you have: the available counters. Returns null if _wdyh == null.
        /// </summary>
        string WDYHRepresentation { get; set; }
        /// <summary>
        /// What I want
        /// </summary>
        Entities WIW { get; set; }
        /// <summary>
        /// Get: Json serialized what I want. Returns null if _wiw == null.
        /// Set: Serialized the given value and stores it in _wiw.
        /// </summary>
        string WIWRepresentation { get; set; }
        #endregion

        #region Functions
        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The parameter, if found or null.</returns>
        Parameter GetParameter(string name);
        /// <summary>
        /// Set the values in the parameters.
        /// The number of values cannot be greater than the number of parameters.
        /// The type of the values must be the same as the type of the default values in the parameters.
        /// </summary>
        /// <param name="values"></param>
        void SetParameterValues(params object[] values);
        /// <summary>
        /// 
        /// </summary>
        /// <returns>If connected.</returns>
        bool Connect();
        /// <summary>
        /// Use the bool _started to determine if applicable. After started the client should listen for counters, if is is not a polling monitor. (use InvokeOnMonitor()).
        /// </summary>
        bool Start();
        /// <summary>
        /// Use the bool _started to determine if applicable.
        /// </summary>
        bool Stop();
        /// <summary>
        /// Disconnect and clean up resources here.
        /// </summary>
        /// <returns></returns>
        bool Disconnect();
        /// <summary>
        /// A test should be as complete as possible. All possible RPCs must be done; returned messages must be validated.
        /// </summary>
        /// <param name="verboseConsoleOutput"></param>
        /// <param name="id">Should be unique.</param>
        /// <param name="parameterValues"></param>
        void Test(bool verboseConsoleOutput, int id, params object[] parameterValues);
        #endregion
    }
}
