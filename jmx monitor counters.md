# All
modelerType JvmMetrics

* long GcCount
* long GcTimeMillis

modelerType sun.management.MemoryImpl

* long HeapMemoryUsage : { used }

# Hbase - RegionServer
http://bdc-01:16030/jmx

modelerType RegionServer,sub=IPC

* long exceptions
* long queueSize
* long numActiveHandler
* double TotalCallTime_95th_percentile

modelerType RegionServer,sub=Server

* long readRequestCount
* long writeRequestCount
* long staticIndexSize
* long staticBloomSize
* long slowGetCount
* long slowPutCount

# HDFS - NameNode
http://192.168.32.47:50070/jmx

modelerType NameNodeActivity

* long CreateFileOps
* long GetBlockLocations
* long DeleteFileOps

modelerType org.apache.hadoop.hdfs.server.namenode.FSNamesystem

* long CapacityTotal
* long FilesTotal

# HDFS - DataNode
http://bdc-01:50075/jmx

modelerType DataNodeActivity-bdc-01-50010

* long BytesWritten
* long BytesRead
