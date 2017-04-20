using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XTMF;

namespace TMG.Frameworks.Extensibility
{
    [ModuleInformation(
        Description = "This module is designed to allow given modules to be executed a specific amount of times."
    )]
    public class Iteration : ISelfContainedModule
    {
        [RunParameter("Execution Iterations", 1, "The number of iterations that will be performed.")]
        public int ExecutionIterations = 1;

        [RunParameter("Execute in Parellel", false, "Whether a single iteration should be executed in parallel.")]
        public bool ExecuteInParallel = false;

        [SubModelInformation(Description = "The modules to execute.")]
        public ISelfContainedModule[] IterationModules;

        public string Name { get; set; }
        public float Progress { get; }
        public Tuple<byte, byte, byte> ProgressColour { get; }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private ISelfContainedModule _currentlyExecutingModule;

        private int _currentIteration = 0;

        public override string ToString()
        {
            if (!ExecuteInParallel)
            {
                return $"Iteration {_currentIteration} of {ExecutionIterations}, Module: {_currentlyExecutingModule}";
            }
            else
            {
                return $"Iteration {_currentIteration} of {ExecutionIterations}";
            }
        }

        public void Start()
        {
            for (_currentIteration = 0; _currentIteration < ExecutionIterations; _currentIteration++)
            {
                if (!ExecuteInParallel)
                {
                    foreach (var module in IterationModules)
                    {
                        _currentlyExecutingModule = module;
                        module.Start();
                    }
                }

                else
                {
                    Thread[] threads = new Thread[IterationModules.Length];
                    ConcurrentQueue<Exception> errorList = new ConcurrentQueue<Exception>();
                    for (int i = 0; i < threads.Length; i++)
                    {
                        var avoidSharingI = i;
                        threads[i] = new Thread(() =>
                        {
                            try
                            {
                                IterationModules[avoidSharingI].Start();
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
                    if (errorList.Count > 0)
                    {
                        throw new AggregateException(errorList.ToArray());
                    }
                }
            }
        }
    }
}
