/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using RandomUtils.Log;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;

namespace vApus.Monitor.Sources.Base {
    /// <summary>
    /// Holds a socket. No polling logic implemented, since data can also be pushed from the server.
    /// </summary>
    /// <typeparam name="T">The type of the messages send between agent / device and client. For instance: string or custom type.
    /// Such a message must be parsed to an Entities object in case of wdyh, wiw and received counters.</typeparam>
    public abstract class BaseSocketClient<T> : BaseClient {

        #region Fields
        /// <summary>
        /// In ms.
        /// </summary>
        public const int CONNECTTIMEOUT = 2000;

        private const string REGEX_IPV4 = "^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
        private const string REGEX_IPV6 = "^(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))$";

        private readonly ManualResetEvent _connectWaitHandle = new ManualResetEvent(true);

        /// <summary>
        /// 
        /// </summary>
        protected Socket _socket;

        /// <summary>
        /// Only set on connect to the socket.
        /// </summary>
        protected SocketType _socketType = SocketType.Stream;
        /// <summary>
        /// Only set on connect to the socket.
        /// </summary>
        protected ProtocolType _protocolType = ProtocolType.Tcp;
        /// <summary>
        /// Only set on connect to the socket.
        /// </summary>
        protected int _bufferSize = 8192;
        #endregion

        /// <summary>
        /// Reverse lookups the hostname if possible.
        /// </summary>
        public IPAddress[] IPAddresses {
            get {

                string hostNameOrIP = GetParameter("Host Name or IP address").Value as string;
                if (hostNameOrIP.Trim().Length == 0) throw new Exception("No IP address or hostname was given.");

                var ipAddresses = new List<IPAddress>();
                IPAddress originalIpAddress; //If the entry could not be resolved (no dns).

                if (Regex.IsMatch(hostNameOrIP, REGEX_IPV4) || Regex.IsMatch(hostNameOrIP, REGEX_IPV6)) {
                    if (IPAddress.TryParse(hostNameOrIP, out originalIpAddress))
                        ipAddresses.Add(originalIpAddress);

                    return ipAddresses.ToArray();
                }

                try {
                    ipAddresses.AddRange(Dns.GetHostEntry(hostNameOrIP).AddressList);
                    return ipAddresses.ToArray();
                }
                catch {
                    //If the entry could not be resolved (no dns).
                }

                if (IPAddress.TryParse(hostNameOrIP, out originalIpAddress))
                    ipAddresses.Add(originalIpAddress);

                return ipAddresses.ToArray();
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public int Port {
            get { return (int)GetParameter("Port").Value; }
        }
        /// <summary>
        /// 
        /// </summary>
        public override bool IsConnected { get { return _socket != null && _socket.Connected; } }

        /// <summary>
        /// Call this in your implementations' constructor.
        /// </summary>
        protected BaseSocketClient() {
            var hostNameOrIPAddress = new Parameter() { Name = "Host Name or IP address", Description = "Where the monitor source is bound to.", DefaultValue = string.Empty };
            var port = new Parameter() { Name = "Port", Description = "The port where the monitor source is listening at.", DefaultValue = 0 };

            base._parameters = new Parameter[] { hostNameOrIPAddress, port };
        }

        #region Functions
        /// <summary>
        /// Tries to connect to the host name or ip address in parameter "Host Name or IP address" with the port in parameter "Port".
        /// There is a connect timeout of 2 seconds for each host entry found in dns.
        /// The send- and receive timeout is  60 seconds.
        /// </summary>
        /// <returns></returns>
        public override bool Connect() {
            if (!IsConnected) {
                int port = Port;
                foreach (IPAddress ipAddress in IPAddresses) {
                    try {
                        System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(ipAddress.AddressFamily, _socketType, _protocolType);
                        socket.ReceiveBufferSize = socket.SendBufferSize = _bufferSize;
                        socket.ReceiveTimeout = socket.SendTimeout = 60000;

                        Connect(socket, ipAddress, port, CONNECTTIMEOUT, 1);

                        if (socket.Connected) {
                            _socket = socket;
                            break;
                        }
                    }
                    catch (Exception ex) {
                        Loggers.Log(Level.Info, "Failed to connect the socket for ip address: " + ipAddress.ToString(), ex);
                    }
                }
            }
            return IsConnected;
        }

        /// <summary>
        ///     Connects to a socket.
        ///     Throws an exception if it is not able too.
        ///     You must check the connected property first before calling this.
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="ipAddress"></param>
        /// <param name="port"></param>
        /// <param name="connectTimeout">In ms. If smaller then or equals 0, the timeout is infinite. If a timeout is given, connecting will happen async</param>
        /// <param name="retries">If the timeout isn't sufficient you can set a retry count.</param>
        protected void Connect(Socket socket, IPAddress ipAddress, int port, int connectTimeout, int retries = 0) {
            Exception exception = null;
            var _remoteEP = new IPEndPoint(ipAddress, port);
            for (int i = 0; i != retries + 1; i++)
                try {
                    _connectWaitHandle.Reset();
                    exception = null;
                    if (connectTimeout < 1) {
                        socket.Connect(_remoteEP);
                        _connectWaitHandle.Set();
                    }
                    else {
                        //Connect async to the remote endpoint.
                        socket.BeginConnect(_remoteEP, ConnectCallback, socket);
                        //Use a timeout to connect.
                        _connectWaitHandle.WaitOne(connectTimeout, false);
                        if (!socket.Connected)
                            throw new Exception("Connecting to the agent timed out.");
                    }
                    break;
                }
                catch (Exception ex) {
                    //Reuse the socket for re-trying to connect.
                    try {
                        if (socket.Connected)
                            socket.Disconnect(true);
                    }
                    catch {
                        //Ignore.
                    }
                    socket = new Socket(socket.AddressFamily, socket.SocketType, socket.ProtocolType);


                    exception = ex;
                }

            _connectWaitHandle.Set();

            if (exception != null) throw exception;
        }

        /// <summary>
        /// </summary>
        /// <param name="ar"></param>
        private void ConnectCallback(IAsyncResult ar) {
            try {
                var socket = ar.AsyncState as Socket;
                if (socket.Connected)
                    socket.EndConnect(ar);
            }
            catch (Exception ex) {
                Loggers.Log(Level.Error, "Failed to end connect the socket.", ex);
            }
            _connectWaitHandle.Set();
        }

        /// <summary>
        /// Write to / read from the socket. Calls Write(write) and then returns Read(write as expected response).
        /// </summary>
        /// <param name="write"></param>
        /// <returns></returns>
        protected T WriteRead(T write) {
            Write(write);
            return Read(write);
        }
        /// <summary>
        /// Write to a socket. Used in WriteRead(write).  Serialization stuff must be done here.
        /// Also add Console.WriteLine stuff to be used when testing. Check using CanConsoleWriteLine if there should be outputted.
        /// </summary>
        /// <param name="write"></param>
        protected abstract void Write(T write);
        /// <summary>
        /// Read from the socket, use this in WriteRead(T) or in Start() for getting the counters back.
        /// </summary>
        /// <param name="expectedResponse">When called in WriteRead(T) this should be the value of write.</param>
        /// <returns></returns>
        protected abstract T Read(T expectedResponse);

        /// <summary>
        /// Stops and disconnects, do all other cleanup stuff in Stop().
        /// </summary>
        /// <returns></returns>
        public override bool Disconnect() {
            if (IsConnected) {
                try {
                    Stop();
                    _socket.Close();
                    base._wiw = null;
                    return true;
                }
                catch (Exception ex) {
                    Loggers.Log(Level.Error, "Failed to close the connection to the agent.", ex);
                }
                finally {
                    _socket = null;
                }
            }
            return !IsConnected;
        }
        #endregion
    }
}
