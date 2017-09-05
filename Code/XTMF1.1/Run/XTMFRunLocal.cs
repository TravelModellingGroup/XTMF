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

        private void Run()
        {
            string cwd = null;
            string error = null;
            IModelSystemStructure mstStructure;
            try
            {
                mstStructure = CrateModelSystem(ref error);
            }
            catch (Exception e)
            {
                SendValidationError(e.Message);
                return;
            }
            if (MST == null)
            {
                SendValidationError(error);
                return;
            }
            Exception caughtError = null;
            try
            {
                cwd = RunModelSystem(ref error, mstStructure);
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
                mstStructure = CleanupModelSystem(cwd, mstStructure, caughtError);
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
                SendRuntimeError(caughtError.Message, caughtError.StackTrace);
            }
            else
            {
                SendRunComplete();
            }
            Directory.SetCurrentDirectory(cwd);
            return mstStructure;
        }

        private void GetInnermostError(ref Exception caughtError)
        {
            while(caughtError is AggregateException agg)
            {
                caughtError = agg.InnerException;
            }
        }

        private string RunModelSystem(ref string error, IModelSystemStructure mstStructure)
        {
            string cwd;
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
            if (!RunTimeValidation("", ref error, mstStructure))
            {
                SendRuntimeValidationError(error);
            }
            else
            {
                SetStatusToRunning();
                MST.Start();
            }

            return cwd;
        }

        private IModelSystemStructure CrateModelSystem(ref string error)
        {
            IModelSystemStructure mstStructure;
            if (ModelSystemStructureModelRoot == null)
            {
                MST = Project.CreateModelSystem(ref error, ModelSystemIndex);
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

        override public List<Tuple<IModelSystemStructure, Queue<int>, string>> CollectValidationErrors()
        {
            List<Tuple<IModelSystemStructure, Queue<int>, string>> validationErrorList = new List<Tuple<IModelSystemStructure, Queue<int>, string>>();
            IModelSystemStructure mstStructure = Project.ModelSystemStructure[ModelSystemIndex];
            Queue<int> path = new Queue<int>();
            path.Enqueue(0);
            CollectValidationErrors((ModelSystemStructure)mstStructure, path, ref validationErrorList);
            return validationErrorList;
        }

        private void CollectValidationErrors(ModelSystemStructure currentPoint, Queue<int> path, ref List<Tuple<IModelSystemStructure, Queue<int>, string>> errorList)
        {
            if (currentPoint != null)
            {
                string error = "";
                if (!currentPoint.ValidateSelf(ref error))
                {
                    errorList.Add(Tuple.Create<IModelSystemStructure, Queue<int>, string>(currentPoint, path, error));
                }
            }
            // check to see if there are descendants that need to be checked
            if (currentPoint.Children != null)
            {
                for (int i = 0; i < currentPoint.Children.Count; i++)
                {
                    Queue<int> newPath = new Queue<int>(path);
                    newPath.Enqueue(i);
                    CollectValidationErrors((ModelSystemStructure)currentPoint.Children[i], newPath, ref errorList);
                }
            }
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
                return current.Children.Aggregate(false, (acc,m)=> acc | Exit(m))
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

        public override List<Tuple<IModelSystemStructure, Queue<int>, string>> CollectRuntimeValidationErrors()
        {
            void CollectRuntimeValidationErrors(IModelSystemStructure structure, Queue<int> innerPath, List<Tuple<IModelSystemStructure, Queue<int>, string>> errorList)
            {
                string internalError = "";
                if (structure.Module != null)
                {
                    if (!structure.Module.RuntimeValidation(ref internalError))
                    {
                        errorList.Add(Tuple.Create<IModelSystemStructure, Queue<int>, string>(structure, innerPath, internalError));
                    }
                }
                if (structure.Children != null)
                {
                    for (int i = 0; i < structure.Children.Count; i++)
                    {
                        Queue<int> newPath = new Queue<int>(innerPath);
                        newPath.Enqueue(i);
                        CollectRuntimeValidationErrors(structure.Children[i], newPath, errorList);
                    }
                }
            }
            List<Tuple<IModelSystemStructure, Queue<int>, string>> runtimeValidationErrorList = new List<Tuple<IModelSystemStructure, Queue<int>, string>>();

            IModelSystemStructure mstStructure;
            string error = "";
            try
            {
                if (ModelSystemStructureModelRoot == null)
                {
                    MST = Project.CreateModelSystem(ref error, ModelSystemIndex);
                    mstStructure = Project.ModelSystemStructure[ModelSystemIndex];
                }
                else
                {
                    MST = ((Project)Project).CreateModelSystem(ref error, Configuration, ModelSystemStructureModelRoot.RealModelSystemStructure);
                    mstStructure = ModelSystemStructureModelRoot.RealModelSystemStructure;
                }
            }
            catch (Exception)
            {
                return runtimeValidationErrorList;
            }
            if (MST == null)
            {
                return runtimeValidationErrorList;
            }

            Queue<int> path = new Queue<int>();
            path.Enqueue(0);
            CollectRuntimeValidationErrors(mstStructure, path, runtimeValidationErrorList);

            return runtimeValidationErrorList;
        }

        /// <summary>
        /// Do a runtime validation check for the currently running model system
        /// </summary>
        /// <param name="error">This parameter gets the error message if any is generated</param>
        /// <param name="currentPoint">The module to look at, set this to the root to begin.</param>
        /// <returns>This will be false if there is an error, true otherwise</returns>
        private static bool RunTimeValidation(string path, ref string error, IModelSystemStructure currentPoint)
        {
            if (currentPoint.Module != null)
            {
                if (!currentPoint.Module.RuntimeValidation(ref error))
                {
                    error = $"Runtime Validation Error in {path + currentPoint.Name}\r\n{error}";
                    return false;
                }
            }
            // check to see if there are descendants that need to be checked
            if (currentPoint.Children != null)
            {
                foreach (var module in currentPoint.Children)
                {
                    if (!RunTimeValidation(path + currentPoint.Name + ".", ref error, module))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
