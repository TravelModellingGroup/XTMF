using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XTMF;
using System.Timers;
using Timer = System.Timers.Timer;

namespace TMG.Frameworks.Testing
{
    /// <summary>
    /// A simple test module that simulates a module that requires an extended period of time to finish executing.
    /// </summary>
    public class TestExecutingModule : ISelfContainedModule
    {
        public string Name { get; set; }
        public float Progress { get =>  _progress; }
        public Tuple<byte, byte, byte> ProgressColour { get; }
    

        [RunParameter("Execution Time (s)", 60, "Specficy the simulated length of execution for this module, in seconds..")]
        public int ExecutionTime { get; set; }

        private Timer _timer;

        private int _ticks = 0;

        private float _progress = 0.0f;

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            _progress = 0;
            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Elapsed += _timer_Elapsed;
            _timer.AutoReset = true;
            _timer.Start();

            while (_ticks < ExecutionTime)
            {
                //hold up execution
                Thread.Sleep(100);
            }

            _timer.Stop();
           

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _ticks++;
            _progress = (_ticks / (float)ExecutionTime) * 100.0f;
            Console.WriteLine("Timer tick from " + this.Name + " at " + e.SignalTime);
           
        }



    }
}
