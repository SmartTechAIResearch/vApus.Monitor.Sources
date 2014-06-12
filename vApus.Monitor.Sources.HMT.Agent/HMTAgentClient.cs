/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using vApus.Monitor.Sources.Generic.Agent;

namespace vApus.Monitor.Sources.HMT.Agent {
    /// <summary>
    /// A generic agent implementation having 5557 for a default port to connect to.
    /// </summary>
    public class HMTAgentClient : GenericAgentClient {
        public HMTAgentClient()
            : base() {
            base.GetParameter("Port").DefaultValue = 5557;
        }
    }
}
