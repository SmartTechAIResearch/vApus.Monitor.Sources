/*
 * Copyright 2017 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using vApus.Monitor.Sources.Generic.Agent;

namespace vApus.Monitor.Sources.Proc.Agent {
    /// <summary>
    /// A generic agent implementation having 5556 for a default port to connect to.
    /// </summary>
    public class ProcAgentClient : GenericAgentClient {
        public ProcAgentClient()
            : base() {
            base.GetParameter("Port").DefaultValue = 5556;
        }
    }
}
