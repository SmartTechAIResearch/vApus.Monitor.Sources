/*
 * 2014 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
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
