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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XTMF.Run
{
    sealed class XTMFRunLocal : XTMFRun
    {
        private Thread RunThread;

        /// <summary>
        /// The model system to execute
        /// </summary>
        private int ModelSystemIndex;

        /// <summary>
        /// The model system that is currently executing
        /// </summary>
        private IModelSystemTemplate MST;

        /// <summary>
        /// The project that is being executed
        /// </summary>
        private IProject Project;

        public override bool RunsRemotely => false;

        public XTMFRunLocal(Project project, int modelSystemIndex, ModelSystemModel root, Configuration config, string runName, bool overwrite = false)
            : base(runName, Path.Combine(config.ProjectDirectory, project.Name, runName), new ConfigurationProxy(config, project))
        {
            Project = project;
            ModelSystemStructureModelRoot = root.Root;
            RunName = runName;
            ModelSystemIndex = modelSystemIndex;
            Project.ModelSystemStructure[ModelSystemIndex] = root.ClonedModelSystemRoot;
            Project.LinkedParameters[ModelSystemIndex] = root.LinkedParameters.GetRealLinkedParameters();
            if (overwrite)
            {
                ClearFolder(RunDirectory);
            }
        }

        public XTMFRunLocal(Project project, ModelSystemStructureModel root, Configuration configuration, string runName, bool overwrite)
            : base(runName, Path.Combine(configuration.ProjectDirectory, project.Name, runName),
                  (project.ModelSystemStructure.IndexOf(root.RealModelSystemStructure) >= 0 ? (IConfiguration)new ConfigurationProxy(configuration, project) : configuration))
        {
            // we don't make a clone for this type of run
            Project = project;
            ModelSystemStructureModelRoot = root;
            RunName = runName;
            if (overwrite)
            {
                ClearFolder(RunDirectory);
            }
        }
        public override bool ExitRequest()
        {
            return MST?.ExitRequest() ?? false;
        }

        override public void Start()
        {
            RunThread = new Thread(() =>
            {
                Run();
            })
            {
                IsBackground = true
            };
            RunThread.Start();
        }

        private static List<ErrorWithPath> CreateFromSingleError(ErrorWithPath error)
        {
            var ret = new List<ErrorWithPath>(1)
            {
                error
            };
            return ret;
        }

        private void Run()
        {
            string originalWorkingDirectory = null;
            // create an empty error
            ErrorWithPath error = new ErrorWithPath();
            IModelSystemStructure mstStructure;
            try
            {
                mstStructure = CreateModelSystem(ref error);
            }
            catch (Exception e)
            {
                var pass = new List<ErrorWithPath>();
                if (e is XTMFRuntimeException runtimeException)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    pass.Add(new ErrorWithPath(null, e.Message));
                }
                InvokeValidationError(pass);
                return;
            }
            if (MST == null)
            {
                InvokeValidationError(CreateFromSingleError(error));
                return;
            }
            Exception caughtError = null;
            try
            {
                originalWorkingDirectory = RunModelSystem(out List<ErrorWithPath> errors, mstStructure);
            }
            catch (Exception e)
            {
                if (!(e is ThreadAbortException))
                {
                    caughtError = e;
                }
            }
            finally
            {
                mstStructure = CleanupModelSystem(originalWorkingDirectory, mstStructure, caughtError);
            }
        }

        private IModelSystemStructure CleanupModelSystem(string cwd, IModelSystemStructure mstStructure, Exception caughtError)
        {
            void CleanUpModelSystem(IModelSystemStructure ms)
            {
                if (ms.Module is IDisposable disp)
                {
                    try
                    {
                        disp.Dispose();
                    }
                    catch
                    { }
                }
                ms.Module = null;
                var children = ms.Children;
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        CleanUpModelSystem(child);
                    }
                }
            }
            Thread.MemoryBarrier();
            CleanUpModelSystem(mstStructure);
            mstStructure = null;
            MST = null;
            if (Configuration is Configuration configuration)
            {
                configuration.ModelSystemExited();
            }
            else
            {
                ((ConfigurationProxy)Configuration).ModelSystemExited();
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Thread.MemoryBarrier();
            GetInnermostError(ref caughtError);
            if (caughtError != null)
            {
                InvokeRuntimeError(new ErrorWithPath(null, caughtError.Message, caughtError.StackTrace));
            }
            else
            {
                InvokeRunCompleted();
            }
            Directory.SetCurrentDirectory(cwd);
            return mstStructure;
        }

        private void GetInnermostError(ref Exception caughtError)
        {
            while (caughtError is AggregateException agg)
            {
                caughtError = agg.InnerException;
            }
        }

        private string RunModelSystem(out List<ErrorWithPath> errors, IModelSystemStructure mstStructure)
        {
            string cwd;
            errors = new List<ErrorWithPath>(0);
            AlertValidationStarting();
            cwd = Directory.GetCurrentDirectory();
            // check to see if the directory exists, if it doesn't create it
            DirectoryInfo info = new DirectoryInfo(RunDirectory);
            if (!info.Exists)
            {
                info.Create();
            }
            Directory.SetCurrentDirectory(RunDirectory);
            mstStructure.Save(Path.GetFullPath("RunParameters.xml"));
            if (!RunTimeValidation(new List<int>(), errors, mstStructure))
            {
                InvokeRuntimeValidationError(errors);
            }
            else
            {
                SetStatusToRunning();
                MST.Start();
            }
            return cwd;
        }

        private IModelSystemStructure CreateModelSystem(ref ErrorWithPath error)
        {
            IModelSystemStructure mstStructure;
            if (ModelSystemStructureModelRoot == null)
            {
                MST = ((Project)Project).CreateModelSystem(ref error, Configuration, ModelSystemIndex);
                mstStructure = Project.ModelSystemStructure[ModelSystemIndex];
            }
            else
            {
                MST = ((Project)Project).CreateModelSystem(ref error, Configuration, ModelSystemStructureModelRoot.RealModelSystemStructure);
                mstStructure = ModelSystemStructureModelRoot.RealModelSystemStructure;
            }

            return mstStructure;
        }

        override public void TerminateRun()
        {
            void Terminate()
            {
                var thread = RunThread;
                if (thread != null)
                {
                    try
                    {
                        if (thread.ThreadState != ThreadState.Running)
                        {
                            thread.Abort();
                        }
                    }
                    catch
                    {
                    }
                }
            }
            Task.Run(() =>
            {
                Terminate();
            });
        }

        public override float PollProgress()
        {
            return MST?.Progress ?? 0f;
        }

        public override string PollStatusMessage()
        {
            return MST?.ToString() ?? String.Empty;
        }

        public override bool DeepExitRequest()
        {
            bool Exit(IModelSystemStructure current)
            {
                return current.Children.Aggregate(false, (acc, m) => acc | Exit(m))
                    | (current.Module is IModelSystemTemplate mst && mst.ExitRequest());
            }
            var root = ModelSystemStructureModelRoot;
            if (root != null)
            {
                return Exit(root.RealModelSystemStructure);
            }
            return false;
        }

        public override void Wait()
        {
            RunThread?.Join();
        }

        public override Tuple<byte, byte, byte> PollColour()
        {
            return MST?.ProgressColour;
        }

        /// <summary>
        /// Do a runtime validation check for the currently running model system
        /// </summary>
        /// <param name="error">This parameter gets the error message if any is generated</param>
        /// <param name="currentPoint">The module to look at, set this to the root to begin.</param>
        /// <returns>This will be false if there is an error, true otherwise</returns>
        private static bool RunTimeValidation(List<int> path, List<ErrorWithPath> errors, IModelSystemStructure currentPoint)
        {
            var ret = true;
            if (currentPoint.Module != null)
            {
                string error = null;
                if (!currentPoint.Module.RuntimeValidation(ref error))
                {
                    errors.Add(new ErrorWithPath(path.ToList(), $"Runtime Validation Error in {currentPoint.Name}\r\n{error}"));
                    ret = false;
                }
            }
            // check to see if there are descendants that need to be checked
            if (currentPoint.Children != null)
            {
                path.Add(0);
                foreach (var module in currentPoint.Children)
                {
                    if (!RunTimeValidation(path, errors, module))
                    {
                        ret = false;
                    }
                    path[path.Count - 1] += 1;
                }
                path.RemoveAt(path.Count - 1);
            }
            return ret;
        }
    }
}
