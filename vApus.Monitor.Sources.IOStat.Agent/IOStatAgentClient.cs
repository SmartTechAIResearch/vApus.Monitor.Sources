/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using vApus.Monitor.Sources.Generic.Agent;

namespace vApus.Monitor.Sources.IOstat.Agent {
    public class IOstatAgentClient : GenericAgentClient {
        public IOstatAgentClient()
            : base() {
            base.GetParameter("Port").DefaultValue = 5558;
        }
    }
}
