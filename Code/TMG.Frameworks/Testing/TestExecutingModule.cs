using System;
using System.Threading;
using System.Timers;
using XTMF;
using Timer = System.Timers.Timer;

namespace TMG.Frameworks.Testing
{
    /// <summary>
    ///     A simple test module that simulates a module that requires an extended period of time to finish executing.
    /// </summary>
    public class TestExecutingModule : ISelfContainedModule
    {
        private int _ticks;

        private Timer _timer;


        [RunParameter("Execution Time", 60, "Specficy the simulated length of execution for this module, in seconds..")]
        public float ExecutionTime { get; set; }

        public string Name { get; set; }
        public float Progress { get; private set; } = 50.0f;

        public Tuple<byte, byte, byte> ProgressColour { get; } = new Tuple<byte, byte, byte>(100, 120, 200);

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            Progress = 50.0f;
            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Elapsed += _timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Start();
            var _totalTime = ExecutionTime;
            while (_ticks < _totalTime)
            {
                //hold up execution
                Thread.Sleep(100);
            }

            _timer.Stop();
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _ticks++;
            //_progress = (_ticks / (float)ExecutionTime) * 100.0f;
            Console.WriteLine("Timer tick from " + Name + " at " + e.SignalTime);
        }
    }
}