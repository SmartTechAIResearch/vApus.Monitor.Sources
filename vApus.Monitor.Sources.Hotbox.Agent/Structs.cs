/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
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
