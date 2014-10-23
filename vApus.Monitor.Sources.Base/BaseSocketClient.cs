/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using RandomUtils.Log;
using System;
using System.Net;
using System.Net.Sockets;
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
                string hostNameOrIP = GetParameter("Host Name or IP address").Value as string;
                if (hostNameOrIP.Trim().Length == 0) return false;

                IPAddress[] ipAddresses = null;

                try {
                    ipAddresses = Dns.GetHostEntry(hostNameOrIP).AddressList;
                } catch {
                    //If the entry could not be resolved (no dns).
                    IPAddress ipAddress;
                    if (IPAddress.TryParse(hostNameOrIP, out ipAddress))
                        ipAddresses = new IPAddress[] { ipAddress };
                }

                if (ipAddresses != null) {
                    int port = (int)GetParameter("Port").Value;

                    foreach (IPAddress ipAddress in ipAddresses) {
                        try {
                            System.Net.Sockets.Socket socket = new System.Net.Sockets.Socket(ipAddress.AddressFamily, _socketType, _protocolType);
                            socket.ReceiveBufferSize = socket.SendBufferSize = _bufferSize;
                            socket.ReceiveTimeout = socket.SendTimeout = 60000;

                            _connectWaitHandle.Reset();
                            socket.BeginConnect(ipAddress, port, ConnectCallback, socket);
                            _connectWaitHandle.WaitOne(CONNECTTIMEOUT);

                            if (socket.Connected) {
                                _socket = socket;
                                break;
                            }
                        } catch (Exception ex) {
                            Loggers.Log(Level.Info, "Failed to connect the socket for ip address: " + ipAddress.ToString(), ex);
                        }
                    }
                }
            }
            return IsConnected;
        }
        private void ConnectCallback(IAsyncResult ar) {
            System.Net.Sockets.Socket socket = ar.AsyncState as System.Net.Sockets.Socket;
            try {
                if (socket.Connected)
                    socket.EndConnect(ar);
            } catch (Exception ex) {
                Loggers.Log(Level.Error, "Failed to end connect the socket.", ex);
            }
            _connectWaitHandle.Set();
        }
        /// <summary>
        /// Write to / read from the socket. Serialization stuff must be done here.
        /// Also add Console.WriteLine stuff to be used when testing. Check using CanConsoleWriteLine if there should be outputted.
        /// </summary>
        /// <param name="write"></param>
        /// <returns></returns>
        protected abstract T WriteRead(T write);
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
                } catch (Exception ex) {
                    Loggers.Log(Level.Error, "Failed to close the connection to the agent.", ex);
                } finally {
                    _socket = null;
                }
            }
            return !IsConnected;
        }
        #endregion
    }
}
