/*
    Copyright 2015-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Threading;
using System.Timers;
using XTMF;
using Timer = System.Timers.Timer;
using XTMF.Logging;
using XTMF.Attributes;

namespace TMG.Frameworks.Testing
{
    /// <summary>
    ///     A simple test module that simulates a module that requires an extended period of time to finish executing.
    /// </summary>
    public class TestExecutingModule : ISelfContainedModule
    {
        private int _ticks;

        private Timer _timer;

        private readonly ILogger _logger;


        [RunParameter("Execution Time", 60, "Specficy the simulated length of execution for this module, in seconds..")]
        public float ExecutionTime { get; set; }

        public string Name { get; set; }

        public float Progress => _progress;

        public Tuple<byte, byte, byte> ProgressColour { get; } = new Tuple<byte, byte, byte>(100, 120, 200);

        public bool RuntimeValidation(ref string error)
        {

            return true;
        }

        private float _progress = 0.0f;

        public void Start()
        {
            _progress = 0.0f;
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
        /// 
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        public TestExecutingModule(IConfiguration configuration,
            ILogger logger)
        {
            this._logger = logger;
        }

        /// <summary>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _ticks++;
            _progress = (_ticks / (float) ExecutionTime);
            this._logger.Info("Timer tick from " + Name + " at " + e.SignalTime);
        }
    }
}