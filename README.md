# Preface
A monitor source is the means to get monitor data into vApus. This can be by communicating to an API or a custom agent.

Listed below is the contents of the vApus.Monitor.Sources Visual Studio solution.

vApus.Monitor.Sources.sln is fairly well XML documented. I suggest you have the solution open when reading this readme.

The code was build and tested using Visual Studio 2015 and .Net 4.5.1.

# vApus.Monitor.Sources.Base
All client implementations use this as a base. Only most mentionable classes are described here: see the sln for more details.

## Base Classes

                 IDisposable
                     |
                 IClient -------> ClientFactory
                 |      |
    BaseClientSwitcher  |
                        |
                 BaseClient
                 |      |
    BaseSocketClient  BasePollingClient

There are two types of clients: A socket and a polling client. A socket client is listens for counters and is used as a base for all agent-based clients. A polling client implementation is used for when there is an API available that needs to be queried for counters.

**BaseClient** holds all needed shared logic and a few protected fields (determined by carefully tweaking).

**BaseClientSwitcher** is something special. This can be used as a base for a client that needs to support multiple protocols. This allows to choose between different socket or polling client implementations, you must do the checking yourself obviously.

**IClient** exists for two reasons: the definition of an interface and to easily invoke functions without having to use reflection.


All mentioned base classes hold functionality testing methods that are called by the tester (mentioned later on).


**Important: ClientFactory**

All client implementations that reside in the a folder./MonitorSourceClients can be enumerated if the binary name starts with vApus.Monitor.Sources.

Such binary must have an assembly title according to the naming conventions (see further) and an assembly description.

Also there can be <big>**only one**</big> exposed (public) client class; when you have a BaseClientSwitcher Implementation, make sure all 'sub clients' are marked protected.

Only when all these requirements are met a client implementation instance can be created.

## Entities, Entity and CounterInfo
These classes are used to represent infrastructure layout and counter data for the stuff that needs to be monitored. JSON representation of those is actively used to send back and forth data from client to agent. (More info [[VApus-vApus-agent_communication_protocol|here]])

There are 3 types of data: wdyh, wiw and wiw with counters. You will find that these are internally used by socket and polling based clients both.

### How they are used
In our agent and sources implementations an Entity can be for instance a VM or a power meter's port.

CounterInfo can stand on it's own or can have instances (which are CounterInfos as well). For instance The CounterInfo CPU has instances CPUTotal, CPU1 and CPU2. Only the leaf CounterInfos have counter values when monitoring.

Only doubles should be used for counter values (you are free in this of course). If a counter becomes invalid the value of  '-1' is used. Counters are timestamped. You must calculate a delta if you want to match server-time to client-time.

## Hardware Configuration
The config, if applicable, is XML-formatted in our sources- and agents implementations.

# Implementations
* **vApus.Monitor.Sources.Generic.Agent** A client that can communicate with every agent built on the base agent Netbeans packages. A port and ip or hostname must be given.

* **vApus.Monitor.Sources.Dstat.Agent** A wrapper around the generic agent client that has a default port of 5556 (so you can ommit it if you like when starting the tool).

* **vApus.Monitor.Sources.Elasticsearch** Monitor source for reading counters from Elastic Search.

* **vApus.Monitor.Sources.ESXi** A ESXi polling client (no vcenter) to monitor a datacenter. The VimAPI is used (VimService).

* **vApus.Monitor.Sources.HbaseRegionServer**,  **vApus.Monitor.Sources.HDFSDataNode**, **vApus.Monitor.Sources.HDFSNameNode** Hbase and HDFS monitors.

* **vApus.Monitor.Sources.HMT.Agent** This Hardware Monitor Tool client reads data from an agent that queries [http://en.wikipedia.org/wiki/Model-specific_register MSRs] for Intel and AMD CPUs. (C-states, P-states, DRAM freq, ...) It has a default port of 5557 (so you can ommit it if you like when starting the tool).

* **vApus.Monitor.Sources.Hotbox.Agent** A client for the ad hoc agent v1 (by Wannes) to read temperature sensors for the Hotbox tests. It has a default port of 9999 (so you can ommit it if you like when starting the tool). V2 will be implemented later if the need arises.

* **vApus.Monitor.Sources.IOstat.Agent** A wrapper around the generic agent client that has a default port of 5558 (so you can ommit it if you like when starting the tool).

* **vApus.Monitor.Sources.IPMI** Used for reading sensor data from a servers' [http://en.wikipedia.org/wiki/Intelligent_Platform_Management_Interface BMC].

* **vApus.Monitor.Sources.LocalWMI** A polling client to monitor the local system.

* **vApus.Monitor.Sources.Racktivity** Supports the Racktivity v1 (with firmware hack especially for our monitoring) and the Racktivity v2 (EnergySwitch). Both are essentialy power meters.

* **vApus.Monitor.Sources.Sigar.Agent** Reads monitor data from the tool used by JMeter.

* **vApus.Monitor.Sources.WMI.Agent** A wrapper around the generic agent client that has a default port of 5556 (so you can ommit it if you like when starting the tool).


* **Extra: vApus.Monitor.Sources.Tester** Functional tests can be configured using an xml file. More info can be found in the [[vApus.Monitor.Sources.Tester Readme|readme]].

# How to use

You need to reference <big>vApus.Monitor.Sources.Base.dll</big> and put the MonitorSourceClients folder in your build output folder.

* Iterate and list the monitor source clients.

        Dictionary<string, Type> clients = ClientFactory.Clients;
        foreach (string key in clients.Keys) { 
          using(IClient client = ClientFactory.Create(client[key])) {
 
           // Add client to GUI. Show IClient.Name, .Description and Parameters. Store the type, so there is only one client created when monitoring; The temporarily clients are nicely disposed.
           // You can get the default value types of the parameters if you like for e.g. vizualization in custom controls (A checkbox for a boolean). Default values can be pre-filled in.
 
           var defaultValueTypes = new Type[client.Parameters.Length];
           for (int i = 0; i != client.Parameters.Length; i++)
            defaultValueTypes[i] = client.Parameters[i].DefaultValue.GetType();
 
           // ...
 
          }
        }

* Connect to a client with the given parameter values and get the hardware config (Should be XML or N/A), refresh counters interval, decimal seperator and available counters (wdyh).

        IClient client = ClientFactory.Create(client[key]);
 
        client.GetParameter("parameter name").Value = "Foo";
        // OR
        client.SetParameterValues("Foo", "Bar", "..."); // Order is important.
  
        string config = null, decimalSeparator = null;
        int refreshCountersInterval = -1;
        Entities wdyh = null;
        // OR
        string wdyhRepresentation = null; // Json serialized available counters.
 
        if(client.Connect()) {
          config = client.Config;
          refreshCountersInterval = client.RefreshCountersInterval;
          decimalSeparator = client.DecimalSeparator;
          wdyh = client.WDYH;
          // OR
          wdyhRepresentation = client.WDYHRepresentation;
        }

* Set the counters that you want to monitor and start. Do not forget to subscribe to the OnMonitor event. 
 
        client.WIW = Foo;
        // OR
        client.WIWRepresentation = "Foo";
 
        client.OnMonitor += client_OnMonitor;
 
        client.Start();
 
        private void client_OnMonitor(object sender, OnMonitorEventArgs e) {
          // Vizualize e.Counters (Entities with filled in counter values).
        }

* Stop /  Disconnect / Dispose / Unsuscribe from OnMonitor

# Naming conventions
## Agents (Java)
* Netbeans project names: vApus-<1> No more than one project per agent, except for projects compiling to the lib folder.
* Package names: be.sizingservers.vapus.<2>.agent (.agent because classes in it extend be.sizingservers.agent classes)
* Class names: Pascal cased (no -s or spaces). The 3 manditory classes (Overview_vApus-agents#Netbeans_packages_and_bash_scripts) should be prefixed with <3>. Do not prefix other classes with <3> if any.
* Jar filenames: vApus-<1>.jar No more than one jar per agent, except in the lib subfolder

<1> = descriptive name in lower case with a - as word delimiter. Examples: dstat, linux-hmt.

<2> = descriptive name in lower case with a . as word delimiter. Examples: dstat, linux.hmt.

<3> = descriptive name in pascal case with a - as word delimiter, Letter words are in uppercase. Examples: Dstat, Linux-HMT.

## Monitor sources (C#.Net)
* All Visual Studio monitor source projects should reside in the same Solution file named vApus.Monitor.Sources.sln.
* Visual Studio project names: vApus.Monitor.Sources.<1> or vApus.Monitor.Sources.<1>.Agent  (if the source to monitor is a third-party agent or one of our own).
* Class names: <2>Client or <3>AgentClient (if the source to monitor is a third-party agent or one of our own).
* Assembly name property: <3> or <3> Agent (if the source to monitor is a third-party agent or one of our own).
* Exe filenames: vApus-<4>

<1> = descriptive name  in pascal case with a . as delimiter, Letter words are in uppercase. Examples: Dstat, Linux.HMT.

<2> = descriptive name  in pascal case, not delimited, Letter words are in uppercase. Examples: Dstat, LinuxHMT.

<3> = descriptive name  in pascal case with a space as delimiter, Letter words are in uppercase. Examples: Dstat, Linux HMT.

<4> = descriptive name in pascal case with a - as word delimiter, Letter words are in uppercase. Examples: Dstat, Linux-HMT.