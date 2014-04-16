/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using vApus.Monitor.Sources.Generic.Agent;

namespace vApus.Monitor.Sources.WMI.Agent {
    public class WMIAgentClient : GenericAgentClient {
        public WMIAgentClient()
            : base() {
            base.GetParameter("Port").DefaultValue = 5557;
        }
    }
}