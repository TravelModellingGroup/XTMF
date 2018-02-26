/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XTMF.Controller
{
    /// <summary>
    /// This controller is responsible for the organization of the runs currently executing or
    /// that are scheduled to run.
    /// </summary>
    public sealed class RunController
    {
        private object _Lock = new object();

        private ObservableCollection<XTMFRun> _Backlog = new ObservableCollection<XTMFRun>();

        private ObservableCollection<XTMFRun> _CurrentlyExecuting = new ObservableCollection<XTMFRun>();

        /// <summary>
        /// Get a reference to all of the runs that are waiting to execute
        /// </summary>
        public ReadOnlyObservableCollection<XTMFRun> RunPipeline
        {
            get
            {
                lock (_Lock) { return new ReadOnlyObservableCollection<XTMFRun>(_Backlog); }
            }
        }

        /// <summary>
        /// Get a reference to all of the runs that are waiting to execute
        /// </summary>
        public ReadOnlyObservableCollection<XTMFRun> CurrentlyExecuting
        {
            get
            {
                lock (_Lock) { return new ReadOnlyObservableCollection<XTMFRun>(_CurrentlyExecuting); }
            }
        }

        /// <summary>
        /// Setup a run for management by XTMF.
        /// </summary>
        /// <param name="run">The run to work with.</param>
        /// <param name="executeNow">Should the run be added to the back of the queue or executed right now?</param>
        public void ExecuteRun(XTMFRun run, bool executeNow)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }
            run.RunCompleted += () => TerminateRun(run);
            run.ValidationError += (e) => TerminateRun(run);
            run.RuntimeValidationError += (e) => TerminateRun(run);
            run.RuntimeError += (e) => TerminateRun(run);
            lock (_Lock)
            {
                if (executeNow || _CurrentlyExecuting.Count == 0)
                {
                    _CurrentlyExecuting.Add(run);
                    run.Start();
                }
                else
                {
                    _Backlog.Add(run);
                }
            }
        }

        public void CancelRun(XTMFRun run)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }
            lock (_Lock)
            {
                // If we are currently executing the run find it's index and send an exit request
                int index = _CurrentlyExecuting.IndexOf(run);
                if (index >= 0)
                {
                    _CurrentlyExecuting[0].ExitRequest();
                }
                else
                {
                    // if it is not running already, just remove it from the queue
                    _Backlog.Remove(run);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="run"></param>
        private void TerminateRun(XTMFRun run)
        {
            lock (_Lock)
            {
                _CurrentlyExecuting.Remove(run);
                if (_CurrentlyExecuting.Count == 0)
                {
                    if (_Backlog.Count > 0)
                    {
                        var _backlogRun = _Backlog[0];
                        _Backlog.RemoveAt(0);
                        _CurrentlyExecuting.Add(_backlogRun);
                        _backlogRun.Start();
                    }
                }
            }
        }
    }
}
