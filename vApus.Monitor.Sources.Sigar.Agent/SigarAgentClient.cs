/*
 * 2015 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
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
