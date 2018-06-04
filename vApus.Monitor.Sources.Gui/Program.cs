using System;
using System.Windows.Forms;

namespace vApus.Monitor {
    internal static class Program {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main(string[] args) {
            Application.Run(new Main());
        }
    }
}
