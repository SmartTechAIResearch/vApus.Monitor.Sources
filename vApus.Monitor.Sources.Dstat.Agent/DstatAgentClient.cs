/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using vApus.Monitor.Sources.Generic.Agent;

namespace vApus.Monitor.Sources.Dstat.Agent {
    /// <summary>
    /// A generic agent implementation having 5556 for a default port to connect to.
    /// </summary>
    public class DstatAgentClient : GenericAgentClient {
        public DstatAgentClient()
            : base() {
            base.GetParameter("Port").DefaultValue = 5556;
        }
    }
}
