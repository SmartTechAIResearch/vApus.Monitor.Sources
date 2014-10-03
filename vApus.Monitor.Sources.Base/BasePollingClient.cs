/*
 * Copyright 2014 (c) Sizing Servers Lab
 * University College of West-Flanders, Department GKG
 * 
 * Author(s):
 *    Dieter Vandroemme
 *    Based on code from Glenn Desmadryl
 */
using System;
using System.Threading;

namespace vApus.Monitor.Sources.Base {
    /// <summary>
    /// Handles start and stop for you (you can override this for the slim chance you have to send something to the device first).
    /// Has a timer that reports on another thread.
    /// </summary>
    public abstract class BasePollingClient : BaseClient {
        //For functionality testing.
        private AutoResetEvent _sleepWaitHandle = new AutoResetEvent(false);

        /// <summary>
        /// Store what you received from the polled source in here.
        /// </summary>
        protected Entities _wih;
        /// <summary>
        /// (Hint) Basically a clone of wiw with the counters in wih set to this object (_wiwWithCounters.SetCounters(_wih);). 
        /// </summary>
        protected Entities _wiwWithCounters;

        private Multimedia.Timer _timer;

        /// <summary>
        /// Start polling.
        /// </summary>
        /// <returns>True if started.</returns>
        public override bool Start() {
            if (IsConnected && !base._started) {
                base._started = true;
                _timer = new Multimedia.Timer() { Mode = Multimedia.TimerMode.Periodic, Period = RefreshCountersInterval };
                _timer.Tick += _timer_Tick;
                _timer.Start();
            }
            return base._started;
        }

        private void _timer_Tick(object sender, EventArgs e) { if (IsConnected) InvokeOnMonitor(PollCounters()); }

        /// <summary>
        /// <para>Poll and transform counters in here. The timer will call this fx periodically.</para> 
        /// <para>Example:</para>
        /// <para>RMCPCounters counters = RMCPHelper.GetCounters(HostNameOrIPAddress);</para>
        /// <para>protected override Entities PollCounters() {</para>
        /// <para>if (_wih == null) _wih = WDYH.Clone();</para>
        /// <para>for (int i = 0; i != 8; i++) {</para>
        /// <para>int outlet = i + 1;</para>
        /// <para>var entity = _wih[i];</para>
        /// <para>entity.GetSubs()[0].SetCounter(counters.Available[i]);</para>
        /// <para>}</para>
        /// <para>if (_wiwWithCounters == null) _wiwWithCounters = base._wiw.Clone();</para>
        /// <para>_wiwWithCounters.SetCounters(_wih);</para>
        /// <para> return _wiwWithCounters;</para>
        /// <para>}</para>
        /// </summary>
        /// <returns>_wihWithCounters</returns>
        protected abstract Entities PollCounters();

        /// <summary>
        /// Stop polling.
        /// </summary>
        /// <returns>True if stopped.</returns>
        public override bool Stop() {
            if (IsConnected && base._started) {
                base._started = false;
                if (_timer != null) {
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                }

                if (_sleepWaitHandle != null) {
                    _sleepWaitHandle.Set();
                    _sleepWaitHandle.Dispose();
                    _sleepWaitHandle = null;
                }

                _wih = _wiwWithCounters = null;
            }
            return !base._started;
        }

        /// <summary>
        /// Function for the client tester.
        /// </summary>
        /// <param name="verboseConsoleOutput"></param>
        /// <param name="id"></param>
        /// <param name="parameterValues"></param>
        public override void Test(bool verboseConsoleOutput, int id, params object[] parameterValues) {
            base._verboseConsoleOutput = verboseConsoleOutput;
            base._id = id;
            try {
                try {
                    Console.WriteLine("Test " + base._id + " Started");
                    if (verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Setting the parameters.");
                    if (!IsConnected && parameterValues != null) SetParameterValues(parameterValues);

                    if (verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Connecting to the monitor source...");
                    Connect();

                    if (!IsConnected)
                        throw new Exception("Test " + base._id + " Failed to connect to the monitor source.");

                    string config = Config;
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Config: " + config);

                    int refreshCountersInterval = RefreshCountersInterval;
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " RefreshCountersInterval: " + refreshCountersInterval);

                    string decimalSeparator = DecimalSeparator;
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " DecimalSeparator: " + decimalSeparator);

                    base._wiw = DetermineRandomWiwEntities();
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Random wiw determined: " + WIWRepresentation);

                    _started = true;
                    if (_verboseConsoleOutput) Console.WriteLine("Test " + base._id + " Reading and parsing counters 3 times...");

                    _sleepWaitHandle = new AutoResetEvent(false);
                    for (int i = 0; i != 3; i++) {
                        ValidateCounters(PollCounters());
                        if (_sleepWaitHandle != null)
                            _sleepWaitHandle.WaitOne(refreshCountersInterval);
                    }

                    Stop();

                    Console.WriteLine("Test " + base._id + " Finished succesfully");

                } catch {
                    throw;

                } finally {
                    try { Dispose(); } catch { throw new Exception("Failed to dispose."); }
                }
            } catch (Exception ex) {
                var defaultColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Test " + base._id + " Failed: " + ex.Message + " " + ex.StackTrace);
                Console.ForegroundColor = defaultColor;
            }
            base._verboseConsoleOutput = false;
        }
    }
}
