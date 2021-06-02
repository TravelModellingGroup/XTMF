/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Concurrent;
using System.Threading;
using XTMF;

namespace TMG.Frameworks.Extensibility
{

    [ModuleInformation(
        Description = "This module is designed to execute the given submodules in parallel.  This module will finish its execution when all of the sub modules have completed."
        )]
    public class ExecuteInParallel : ISelfContainedModule
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        [SubModelInformation(Description = "The modules to run in parallel.")]
        public ISelfContainedModule[] RunInParallel;

        public void Start()
        {
            /* we are going to avoid using the thread-pool here
             to make sure that all of these are running in parallel in case of deadlocks
             or IO bound work */
            Thread[] threads = new Thread[RunInParallel.Length];
            ConcurrentQueue<Exception> errorList = new ConcurrentQueue<Exception>();
            for (int i = 0; i < threads.Length; i++)
            {
                var avoidSharingI = i;
                threads[i] = new Thread(() =>
                {
                    try
                    {
                        RunInParallel[avoidSharingI].Start();
                    }
                    catch (Exception e)
                    {
                        errorList.Enqueue(e);
                    }
                });
                threads[i].IsBackground = true;
                threads[i].Start();
            }
            // after creating all of the threads wait until each one is complete before continuing
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }
            if (errorList.TryDequeue(out var firstError))
            {
                throw new XTMFRuntimeException(this, firstError);
            }
        }
    }

}
