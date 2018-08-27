/*
 * 2014 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.Racktivity {
    /// <summary>
    /// A client for the Racktivity and Racktivity EnergySwitch smart PDUs.
    /// </summary>
    public class RacktivityClient : BaseClientSwitcher {
        public RacktivityClient() {
            var hostNameOrIPAddress = new Parameter() { Name = "Host Name or IP address", Description = "Where the monitor source is bound to.", DefaultValue = string.Empty };
            var username = new Parameter() { Name = "Username", DefaultValue = "admin" };
            var password = new Parameter() { Name = "Password", DefaultValue = "1234", Encrypted = true };
            base._parameters = new Parameter[] { hostNameOrIPAddress, username, password };
        }

        public override bool Connect() {
            _client = TryConnect(new OldRacktivityClient(base._parameters));
            if (_client != null)
                return true;

            _client = TryConnect(new RacktivityEnergySwitchClient(base._parameters));

            return _client != null;
        }

    }
}
