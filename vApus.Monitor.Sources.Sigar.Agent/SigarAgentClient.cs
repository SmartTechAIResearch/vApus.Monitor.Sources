/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
 using vApus.Monitor.Sources.Generic.Agent;

namespace vApus.Monitor.Sources.Sigar.Agent {
    public class SigarAgentClient : GenericAgentClient {
        public SigarAgentClient()
            : base() {
            base.GetParameter("Port").DefaultValue = 5560;
        }
    }
}
