/*
 * 2014 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
namespace vApus.Monitor.Sources.Hotbox.Agent {
    internal struct HotboxCommand {
        /// <summary>
        /// getSensors, startSensors or stopSensors.
        /// </summary>
        public string command { get; set; }
    }
    internal struct HotboxSensors {
        /// <summary>
        /// Sensor names or readings.
        /// </summary>
        public string[] sensors{ get; set;}
    }
}
