/*
    Copyright 2017-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading;
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

        private ObservableCollection<(DateTime startTime, XTMFRun run)> _DelayedRuns = new ObservableCollection<(DateTime, XTMFRun)>();

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
        /// Get a reference to all of the runs that are being delayed to start at a given time.
        /// </summary>
        public ReadOnlyObservableCollection<(DateTime, XTMFRun)> DelayedRuns
        {
            get
            {
                lock (_Lock) { return new ReadOnlyObservableCollection<(DateTime, XTMFRun)>(_DelayedRuns); }
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
                    _CurrentlyExecuting[index].ExitRequest();
                }
                else
                {
                    // if it is not running already, just remove it from the queue
                    _Backlog.Remove(run);
                    if (_DelayedRuns.Count > 0)
                    {
                        for (int i = 0; i < _DelayedRuns.Count; i++)
                        {
                            if (_DelayedRuns[i].run == run)
                            {
                                _DelayedRuns.RemoveAt(i);
                            }
                        }
                    }
                    run.TerminateRun();
                }
            }
        }

        private Thread _timedRunThread;

        /// <summary>
        /// This method deals with managing the delayed model system runs
        /// </summary>
        private void ManageTimedThreads()
        {
            while (true)
            {
                Thread.Sleep(5000);
                XTMFRun run = null;
                lock (_Lock)
                {
                    if (_DelayedRuns.Count > 0)
                    {
                        if (_DelayedRuns[0].startTime < DateTime.Now)
                        {
                            run = _DelayedRuns[0].run;
                            _DelayedRuns.RemoveAt(0);
                        }
                    }
                }
                // execute the ready run if one exists
                if (run != null)
                {
                    ExecuteRun(run, false);
                }
            }
        }

        /// <summary>
        /// Setup a run for management by XTMF to start at a given time.
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="run"></param>
        public void ExecuteDelayedRun(XTMFRun run, DateTime startTime)
        {
            lock (_Lock)
            {
                if (_timedRunThread == null)
                {
                    // make sure that XTMF will not be kept alive by this
                    _timedRunThread = new Thread(ManageTimedThreads)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.Lowest
                    };
                    _timedRunThread.Start();
                }

                if (startTime <= DateTime.Now)
                {
                    ExecuteRun(run, false);
                    return;
                }
                // Add this in order
                int index = 0;
                for (; index < _DelayedRuns.Count; index++)
                {
                    if (_DelayedRuns[index].startTime > startTime)
                    {
                        break;
                    }
                }
                _DelayedRuns.Insert(index, (startTime, run));
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
                if(!_CurrentlyExecuting.Remove(run))
                {
                    if(!_Backlog.Remove(run))
                    {
                        for (int i = 0; i < _DelayedRuns.Count; i++)
                        {
                            if(_DelayedRuns[i].run == run)
                            {
                                _DelayedRuns.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
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
                // make sure the run was not in the backlog or the delayed runs
                _Backlog.Remove(run);
                if(_DelayedRuns.Count > 0)
                {
                    for(int i = 0; i < _DelayedRuns.Count; i++)
                    {
                        if(_DelayedRuns[i].run == run)
                        {
                            _DelayedRuns.RemoveAt(i);
                        }
                    }
                }
            }
        }
    }
}
