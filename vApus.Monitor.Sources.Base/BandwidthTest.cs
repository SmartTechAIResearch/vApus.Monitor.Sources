/*
 * Copyright 2015 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 */
using System;
using System.Diagnostics;
using System.Net.Sockets;

namespace vApus.Monitor.Sources.Base {
    /// <summary>
    /// Used in the socket clients.
    /// </summary>
    public static class BandwidthTest {
        private const int STOP = 1;
        private const long MESSAGESIZE = 10 * 1024 * 1024;
        private const int LOOPS = 100;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="socket"></param>
        /// <returns>Upload speed in mbps</returns>
        public static double GetUploadSpeed(Socket socket) {
            Console.WriteLine("Upload speed...");

            byte[] b = CreateMessage(MESSAGESIZE);

            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i != LOOPS; i++)
                socket.Send(b);

            stopwatch.Stop();
            double mbitsPerSec = GetMBitsPerSec(stopwatch.Elapsed.Ticks);

            Console.WriteLine(mbitsPerSec + " Mbps");

            socket.Send(new byte[1] { STOP });

            return mbitsPerSec;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="socket"></param>
        /// <returns>Download speed in mbps</returns>
        public static double GetDownloadSpeed(Socket socket) {
            Console.WriteLine("Download speed...");

            byte[] b;

            var stopwatch = Stopwatch.StartNew();
            b = new byte[socket.ReceiveBufferSize];
            do
                socket.Receive(b);
            while (b[0] != STOP);

            stopwatch.Stop();
            double mbitsPerSec = GetMBitsPerSec(stopwatch.Elapsed.Ticks);

            Console.WriteLine(mbitsPerSec + " Mbps");

            return mbitsPerSec;
        }

        private static byte[] CreateMessage(long size) {
            byte[] b = new byte[size];
            for (long l = 0; l != b.LongLength; l++)
                b[l] = 0;
            return b;
        }

        private static double GetMBitsPerSec(long elapsedTicks) {
            return Math.Round(((double)(MESSAGESIZE * LOOPS * 8) / (1000000)) / new TimeSpan(elapsedTicks).TotalSeconds, 2, MidpointRounding.AwayFromZero);
        }
    }
}
