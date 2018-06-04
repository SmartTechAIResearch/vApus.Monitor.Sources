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
    /// Reports counters.
    /// </summary>
    public class OnMonitorEventArgs : EventArgs {
        /// <summary>
        /// 
        /// </summary>
        public Entities Counters { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="counters"></param>
        public OnMonitorEventArgs(Entities counters) {
            Counters = counters;
        }
    }
}
