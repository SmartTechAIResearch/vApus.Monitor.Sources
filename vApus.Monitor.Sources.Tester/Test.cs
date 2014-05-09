/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Xml;
using vApus.Monitor.Sources.Base;

namespace vApus.Monitor.Sources.Tester {
    public class Test {
        private readonly bool _verbose;
        private readonly Type _client;
        private readonly List<object[]> _parameters;
        private readonly int[] _testCounts, _repeats;

        internal Test(bool verbose, XmlNode testNode) {
            _verbose = verbose;

            IClient client = null;
            string s = testNode.Attributes["client"].Value;
            Dictionary<string, Type> clients = ClientFactory.Clients;
            foreach (string key in clients.Keys)
                if (s == key) {
                    _client = clients[key];
                    client = ClientFactory.Create(_client);
                    break;
                }

            if (client == null)
                throw new Exception("The given client " + s + " could not be found. Does it match with a client assembly title?");

            var defaultValueTypes = new Type[client.Parameters.Length];
            for (int i = 0; i != client.Parameters.Length; i++)
                defaultValueTypes[i] = client.Parameters[i].DefaultValue.GetType();

            client.Dispose();

            try {
                string parametersValue = testNode.Attributes["parameters"].Value;
                _parameters = new List<object[]>();

                if (parametersValue.Length == 0) {
                    _parameters.Add(null);
                } else {
                    string[] parameterGroups = parametersValue.Split(';');
                    for (int i = 0; i != parameterGroups.Length; i++) {
                        string[] parameters = parameterGroups[i].Split(',');
                        var l = new List<object>();

                        for (int j = 0; j != parameters.Length; j++) {
                            object value = Convert.ChangeType(parameters[j], defaultValueTypes[j]);
                            l.Add(value);
                        }

                        _parameters.Add(l.ToArray());
                    }
                }
            } catch (Exception ex) {
                throw new Exception("Failed to get the parameters from the config file: " + ex.Message);
            }

            string[] testCounts = testNode.Attributes["testCounts"].Value.Split(',');
            _testCounts = new int[testCounts.Length];
            for (int i = 0; i != testCounts.Length; i++) {
                int testCount = int.Parse(testCounts[i]);
                if (testCount < 1)
                    throw new Exception("testCounts cannot contain a value smaller than 1.");

                _testCounts[i] = testCount;
            }

            string[] repeats = testNode.Attributes["repeats"].Value.Split(',');
            _repeats = new int[repeats.Length];
            for (int i = 0; i != repeats.Length; i++) {
                int repeat = int.Parse(repeats[i]);
                if (repeat < 0)
                    throw new Exception("repeat cannot contain a value smaller than 0.");

                _repeats[i] = repeat;
            }

            if (testCounts.Length != repeats.Length)
                throw new Exception("testCounts must have as many values as repeats has.");

        }

        /// <summary>
        /// Run tests multi threaded / in parallel for the given client.
        /// </summary>
        internal void Run() {
            for (int i = 0; i != _testCounts.Length; i++) {
                int testCount = _testCounts[i];
                int repeat = _repeats[i];
                for (int k = 0; k <= repeat; k++) {
                    //Start tests in parallel.
                    StartTests(testCount, k);
                }
            }
        }

        private void StartTests(int testCount, int repeat) {
            String start = "--- Starting ";
            if (testCount == 1) {
                start += "test ";
            } else {
                start += testCount + " tests in parallel ";
            }
            if (repeat != 0) {
                start += "(repeat " + repeat + ")";
            }
            start += "---";

            Console.WriteLine(start);

            var tests = new List<Thread>(testCount);
            for (int i = 1; i <= testCount; i++) {
                //Determine a new parameters from the array.
                int parametersIndex = i;
                while (parametersIndex >= _parameters.Count)
                    parametersIndex -= _parameters.Count;

                tests.Add(StartTest(i, _parameters[parametersIndex]));
            }

            for (int i = 0; i != tests.Count; i++) {
                try {
                    tests[i].Join();
                } catch (Exception ex) {
                    Console.WriteLine("Failed joining test " + i + " thread : " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="id">Must be unique</param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private Thread StartTest(int id, object[] parameters) {
            var thread = new Thread((parameter) => {
                var arr = parameter as object[];

                IClient client = ClientFactory.Create(_client);
                client.Test(_verbose, (int)arr[0], arr[1] as object[]);
                client.Dispose();
            });
            thread.IsBackground = true;

            thread.Start(new object[] { id, parameters });
            return thread;
        }
    }
}
