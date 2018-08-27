/*
 * 2014 Sizing Servers Lab, affiliated with IT bachelor degree NMCT
 * University College of West-Flanders, Department GKG (www.sizingservers.be, www.nmct.be, www.howest.be/en)
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.Tester {
    class Program {
        static string CONFIGFILE = "config.xml";

        static void Main(string[] args) {
            try {
                string name = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title;
                string version = Assembly.GetEntryAssembly().GetName().Version.ToString();
                Console.Write(name);
                Console.Write(" ");
                Console.WriteLine(version);

                Console.WriteLine("Available monitor source clients");
                Dictionary<string, Type> clients = ClientFactory.Clients;
                foreach (string key in clients.Keys) {
                    Console.Write(" ");
                    Console.Write(key);
                    Console.Write(": ");
                    Console.WriteLine(Assembly.GetAssembly(clients[key]).GetCustomAttribute<AssemblyDescriptionAttribute>().Description);
                }
                Console.WriteLine();

                XmlDocument doc = new XmlDocument();
                XmlReaderSettings settings = new XmlReaderSettings() { IgnoreComments = true, IgnoreWhitespace = true };
                XmlReader reader = XmlReader.Create(CONFIGFILE, settings);
                doc.Load(reader);

                using (var sw = new StringWriter())
                using (var xmlWriter = XmlWriter.Create(sw)) {
                    doc.WriteTo(xmlWriter);
                    xmlWriter.Flush();
                    Console.WriteLine("Testing with config: " + sw.GetStringBuilder().ToString());
                }
                Console.WriteLine();

                XmlNode root = doc.DocumentElement;

                bool verbose = false;
                foreach (XmlAttribute attribute in root.Attributes)
                    if (attribute.Name == "verbosity") {
                        verbose = attribute.Value == "verbose";
                        break;
                    }

                var tests = new List<Test>();
                foreach (XmlNode node in root.ChildNodes)
                    if (node.Name == "test")
                        tests.Add(new Test(verbose, node));

                foreach (Test test in tests)
                    test.Run();

                Console.WriteLine("--- Done ---");
            } catch (Exception ex) {
                var defaultColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed: " + ex.Message + " " + ex.StackTrace);
                Console.ForegroundColor = defaultColor;
            }

            Console.WriteLine("Press any key to exit... ");
            Console.ReadLine();
        }
    }
}
