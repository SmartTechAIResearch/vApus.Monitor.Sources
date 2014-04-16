/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */

The configuration for the tester must be in config.xml.

Example:

 <?xml version="1.0" encoding="utf-8" ?>
 <tests verbosity="verbose">
   <!--<test client="Generic Agent Client" parameters="192.168.34.207,5556;fe80::20c:29ff:fe4c:95d8,5556;dBuntu,5556" testCounts="2,1,50" repeats="0,10,0" />-->
   <test client="Generic Agent Client" parameters="192.168.34.207,5556" testCounts="1" repeats="0" />
  </tests>

tests node: 
* verbosity
  Set the test output to verbose if you like.
  Can be ommited: in that case only errors and a bit of meta data will get outputted.
  Best practices: ommit this property the first time you run the tester. If there are errors run the tester again with verbosity=verbose.

test node:
You can add as many test nodes if you like. Tests for those tags will be executed in order.

* client
  The value of the assembly title attribute for the client.
  Best practices: define a client only once; for repeating the test you can set this in the repeats attribute.

* parameters
  , seperated values will be set as parameter values for a test. (At minimum this should be hostname or ip and port.)
  You can define multplie parameter groups ; seperated. Each group should have the same amount of parameters.
  A parameter group will be chosen sequentially for each parallel test (testCounts).

* testCounts
  Number of parallel tests.
  Can be ommited: in that case the number of tests equal to the number of parameters will run.
  Multiple values can be assigned , delimited. This number must be equal to the number of values in repeats.
  Best practices: test once with a count equals to the numbers op ips / host names.
  Test once with a large test count (50) and run the server in debug mode to see if there are no 
  infinite loops or deadlocks. Only for stability testing, normally only one or a few clients should connect to a monitor
  at the same time.

* repeats
  Repeat the parallel tests.
  Can be ommited: in that case the tests will be repeated once.
  Multiple values can be assigned , delimited. This number must be equal to the number of values in testCounts.
  Best practices: test once with a test count of one and a large repeat (10) and run the server in debug mode
  to see if connections close like they should and if there are no infinite loops or deadlocks.
