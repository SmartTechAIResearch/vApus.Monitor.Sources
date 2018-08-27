/*
 * 2014 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Dieter Vandroemme
 *    Based on code from Glenn Desmadryl
 */
using RandomUtils.Log;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace vApus.Monitor.Sources.Base {
    /// <summary>
    /// With this you can get the available clients and create a new one using the given type using a public parameterless constructor.
    /// Monitor source client dlls must reside in a subfolder MonitorSourceClients.
    /// </summary>
    public static class ClientFactory {
        private static Dictionary<string, Type> _clients = new Dictionary<string, Type>();
        /// <summary>
        /// 
        /// </summary>
        public static string MonitorSourceClientsFolderLocation { get; private set; }

        /// <summary>
        /// Gets the available monitor source clients. Key == name.
        /// Throws an exception if the folder MonitorSourceClients could not be scanned.
        /// </summary>
        public static Dictionary<string, Type> Clients {
            get {
                if (_clients.Count == 0)
                    _clients = GetClients();

                return _clients;
            }
        }

        static ClientFactory() {
            MonitorSourceClientsFolderLocation = Path.Combine(Application.StartupPath, "MonitorSourceClients");
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static IClient Create(Type type) { return Activator.CreateInstance(type) as IClient; }

        private static Dictionary<string, Type> GetClients() {
            Dictionary<string, Type> types = new Dictionary<string, Type>();

            string[] files = Directory.GetFiles(MonitorSourceClientsFolderLocation, "vApus.Monitor.Sources.*.dll");
            foreach (string file in files) {
                FileInfo info = new FileInfo(file);
                KeyValuePair<string, Type> kvp = ExtractClient(info.FullName);

                if (kvp.Key != null && kvp.Key.Length != 0) {
                    if (types.ContainsKey(kvp.Key)) {
                        string key = kvp.Key;
                        int i = 1;
                        while (types.ContainsKey(key))
                            key = kvp.Key + " (" + (i++) + ")";

                        kvp = new KeyValuePair<string, Type>(key, kvp.Value);
                    }

                    types.Add(kvp.Key, kvp.Value);
                }

            }
            return types;
        }

        private static KeyValuePair<string, Type> ExtractClient(string fileName) {
            Type type = typeof(IClient);
            try {
                Assembly assembly = null;
                try {
                    assembly = Assembly.LoadFile(fileName);
                } catch {
                    //If the assembly is in a share.
                    assembly = Assembly.Load(File.ReadAllBytes(fileName));
                }
                foreach (Type t in assembly.GetExportedTypes())
                    if (!t.IsAbstract && type.IsAssignableFrom(t))
                        return new KeyValuePair<string, Type>(((AssemblyTitleAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyTitleAttribute))).Title, t);

            } catch (Exception ex) {
                Loggers.Log(Level.Error, "Could not readout " + fileName + ".", ex);
                throw;
            }
            return new KeyValuePair<string, Type>();
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            try {
                var assemblyName = new AssemblyName(args.Name);

                if (!assemblyName.Name.EndsWith(".resources")) {
                    string fileName = Path.Combine(MonitorSourceClientsFolderLocation, assemblyName.Name + ".dll");
                    try {
                        return Assembly.LoadFile(fileName);
                    } catch {
                        //If the assembly is in a share.
                        return Assembly.Load(File.ReadAllBytes(fileName));
                    }
                }
            } catch (Exception ex) {
                Loggers.Log(Level.Error, "Could not resolve the current domain.", ex);
                throw;
            }
            return null;
        }
    }
}
