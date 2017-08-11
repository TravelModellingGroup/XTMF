/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using XTMF.RunProxy;
using System.Collections.Generic;
using System.IO.Pipes;
using XTMF.Bus;

namespace XTMF
{
    /// <summary>
    /// This class encapsulates the concept of a Model System run.
    /// Subclasses of this will allow for remote model system execution.
    /// </summary>
    public class XTMFRun : IDisposable
    {
        /// <summary>
        /// The link to XTMF's settings
        /// </summary>
        public IConfiguration Configuration { get; private set; }

        /// <summary>
        /// The model system to execute
        /// </summary>
        protected int ModelSystemIndex;

        /// <summary>
        /// The model system that is currently executing
        /// </summary>
        protected IModelSystemTemplate MST;

        /// <summary>
        /// The project that is being executed
        /// </summary>
        protected IProject Project;

        /// <summary>
        /// The name of this run
        /// </summary>
        protected string RunName;

        /// <summary>
        /// The model system root if we are using a past run
        /// </summary>
        private ModelSystemStructureModel ModelSystemStructureModelRoot;

        public string RunDirectory { get; private set; }

        protected NamedPipeServerStream Pipe;

        protected bool RunsRemotely => Pipe != null;

        public XTMFRun(Project project, int modelSystemIndex, ModelSystemModel root, Configuration config, string runName, bool overwrite = false)
        {
            Project = project;
            ModelSystemStructureModelRoot = root.Root;
            Configuration = new RunProxy.ConfigurationProxy(config, Project);
            RunName = runName;
            RunDirectory = Path.Combine(Configuration.ProjectDirectory, Project.Name, RunName);
            ModelSystemIndex = modelSystemIndex;
            Project.ModelSystemStructure[ModelSystemIndex] = root.ClonedModelSystemRoot;
            Project.LinkedParameters[ModelSystemIndex] = root.LinkedParameters.GetRealLinkedParameters();
            if (overwrite)
            {
                ClearFolder(RunDirectory);
            }
        }

        public XTMFRun(Project project, ModelSystemStructureModel root, Configuration configuration, string runName, bool overwrite = false)
        {
            // we don't make a clone for this type of run
            Project = project;
            ModelSystemStructureModelRoot = root;
            var index = project.ModelSystemStructure.IndexOf(root.RealModelSystemStructure);
            if (index >= 0)
            {
                Configuration = new RunProxy.ConfigurationProxy(configuration, Project);
            }
            RunName = runName;
            RunDirectory = Path.Combine(Configuration.ProjectDirectory, Project.Name, RunName);

            if (overwrite)
            {
                ClearFolder(RunDirectory);
            }
        }

        public XTMFRun(Configuration configuration, string runDirectory, string modelSystemString)
        {
            RunDirectory = runDirectory;
            throw new NotImplementedException();
        }

        public void ClearFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }
            DirectoryInfo directory = new DirectoryInfo(path);
            foreach (System.IO.FileInfo file in directory.GetFiles())
            {
                try
                {
                    file.Delete();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
            foreach (var dir in directory.GetDirectories())
            {
                ClearFolder(dir.FullName);
            }
        }

        /// <summary>
        /// An event that fires when the run completes successfully
        /// </summary>
        public event Action RunComplete;

        /// <summary>
        /// An event that fires when all of the validation has completed and the model system
        /// has started executing.
        /// </summary>
        public event Action RunStarted;

        /// <summary>
        /// An event that fires if a runtime error occurs, this includes out of memory exceptions
        /// </summary>
        public event Action<string, string> RuntimeError;

        /// <summary>
        /// An event that fires when the model ends in an error during runtime validation
        /// </summary>
        public event Action<string> RuntimeValidationError;

        /// <summary>
        /// An event that fires when the Model does not pass validation
        /// </summary>
        public event Action<string> ValidationError;

        /// <summary>
        /// An event that fires when Model Validation starts
        /// </summary>
        public event Action ValidationStarting;

        /// <summary>
        /// Attempt to ask the model system to exit.
        /// Even if this returns true it will not happen right away.
        /// </summary>
        /// <returns>If the model system accepted the exit request</returns>
        public bool ExitRequest()
        {
            if (RunsRemotely)
            {
                throw new NotImplementedException();
            }
            else
            {
                var mst = MST;
                if (mst != null)
                {
                    return mst.ExitRequest();
                }
                return false;
            }
        }

        /// <summary>
        /// Get the currently requested colour from the model system
        /// </summary>
        /// <returns>The colour requested by the model system</returns>
        public Tuple<byte, byte, byte> PollColour()
        {
            if (RunsRemotely)
            {
                throw new NotImplementedException();
            }
            else
            {
                var mst = MST;
                if (mst != null)
                {
                    return mst.ProgressColour;
                }
                return null;
            }
        }

        /// <summary>
        /// Get the current progress for this run
        /// </summary>
        /// <returns>The current progress between 0 and 1</returns>
        public virtual float PollProgress()
        {
            if (RunsRemotely)
            {
                throw new NotImplementedException();
            }
            else
            {
                var mst = MST;
                if (mst != null)
                {
                    return mst.Progress;
                }
                return 1f;
            }
        }

        /// <summary>
        /// Get the status message for this run
        /// </summary>
        /// <returns></returns>
        public virtual string PollStatusMessage()
        {
            if (RunsRemotely)
            {
                throw new NotImplementedException();
            }
            else
            {
                var mst = MST;
                if (mst != null)
                {
                    return mst.ToString();
                }
                return null;
            }
        }



        /// <summary>
        /// Attempts to notify all modules part of the current model system structure
        /// with an exit request usually generated by user interaction. Any module inheriting
        /// IModelSystemTemplate will be sent an ExitRequest.
        /// </summary>
        /// <returns></returns>
        public virtual bool DeepExitRequest()
        {
            if (RunsRemotely)
            {
                throw new NotImplementedException();
            }
            else
            {
                if (MST != null)
                {
                    ModelSystemStructure s = ModelSystemStructureModelRoot.RealModelSystemStructure;
                    ExitRecursive(s);
                    return true;
                }
                return false;
            }
        }

        private void ExitRecursive(IModelSystemStructure structure)
        {
            if (structure != null)
            {
                if (structure.Module is IModelSystemTemplate mst)
                {
                    mst.ExitRequest();
                }
                if (structure.Children != null)
                {
                    foreach (var child in structure.Children)
                    {
                        ExitRecursive(child);
                    }
                }
            }
        }

        private Thread RunThread;

        public virtual List<Tuple<IModelSystemStructure, Queue<int>, string>> CollectRuntimeValidationErrors()
        {
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
            catch (Exception e)
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

        private void CollectRuntimeValidationErrors(IModelSystemStructure structure, Queue<int> path, List<Tuple<IModelSystemStructure, Queue<int>, string>> errorList)
        {
            string error = "";
            if (structure.Module != null)
            {
                if (!structure.Module.RuntimeValidation(ref error))
                {
                    errorList.Add(Tuple.Create<IModelSystemStructure, Queue<int>, string>(structure, path, error));
                }
            }
            if (structure.Children != null)
            {
                for (int i = 0; i < structure.Children.Count; i++)
                {
                    Queue<int> newPath = new Queue<int>(path);
                    newPath.Enqueue(i);
                    CollectRuntimeValidationErrors(structure.Children[i], newPath, errorList);
                }
            }
        }


        public virtual List<Tuple<IModelSystemStructure, Queue<int>, string>> CollectValidationErrors()
        {
            List<Tuple<IModelSystemStructure, Queue<int>, string>> validationErrorList = new List<Tuple<IModelSystemStructure, Queue<int>, string>>();
            IModelSystemStructure mstStructure = Project.ModelSystemStructure[ModelSystemIndex];
            Queue<int> path = new Queue<int>();
            path.Enqueue(0);
            CollectValidationErrors((ModelSystemStructure)mstStructure, path, ref validationErrorList);
            return validationErrorList;
        }

        protected void CollectValidationErrors(ModelSystemStructure currentPoint, Queue<int> path, ref List<Tuple<IModelSystemStructure, Queue<int>, string>> errorList)
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

        public virtual void Start()
        {
            RunThread = new Thread(() =>
            {
                OurRun();
            })
            {
                IsBackground = true
            };
            RunThread.Start();
        }

        /// <summary>
        /// Blocks execution until the run has completed.
        /// </summary>
        public void Wait()
        {
            if (RunThread != null)
            {
                RunThread.Join();
            }
        }

        public void TerminateRun()
        {
            Task.Run(() =>
            {
                if (RunThread != null)
                {
                    try
                    {
                        if (RunThread.ThreadState != ThreadState.Running)
                        {
                            RunThread.Abort();
                        }
                    }
                    catch
                    {
                    }
                }
            });
        }

        /// <summary>
        /// Do a runtime validation check for the currently running model system
        /// </summary>
        /// <param name="error">This parameter gets the error message if any is generated</param>
        /// <param name="currentPoint">The module to look at, set this to the root to begin.</param>
        /// <returns>This will be false if there is an error, true otherwise</returns>
        protected bool RunTimeValidation(string path, ref string error, IModelSystemStructure currentPoint)
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

        private void AlertValidationStarting()
        {
            ValidationStarting?.Invoke();
        }

        /// <summary>
        /// This will remove all links into the model system from the current structure
        /// </summary>
        /// <param name="ms">The model system structure to clean up</param>
        private void CleanUpModelSystem(IModelSystemStructure ms)
        {
            if (ms != null)
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
            }
            if (ms.Children != null)
            {
                foreach (var child in ms.Children)
                {
                    CleanUpModelSystem(child);
                }
            }
        }

        private void OurRun()
        {
            if (RunsRemotely)
            {
                RunRemotely();
            }
            else
            {
                RunLocally();
            }
        }

        private void RunRemotely()
        {
            Task.Factory.StartNew(() =>
            {
                BinaryReader reader = new BinaryReader(Pipe, System.Text.Encoding.UTF8, true);
                while(true)
                {
                    switch((ToHost)reader.ReadInt32())
                    {
                        case ToHost.Heartbeat:
                            break;
                        case ToHost.ClientReportedProgress:
                            {
                                var progress = reader.ReadInt32();
                            }
                            break;
                    }
                }
            }, TaskCreationOptions.LongRunning);
            BinaryWriter writer = new BinaryWriter(Pipe, System.Text.Encoding.UTF8, true);
            writer.Write((Configuration as Configuration)?.ConfigurationFileName ?? "");
            //TODO: Implement this
            writer.Write((UInt32)ToClient.KillModelRun);
        }

        private void RunLocally()
        {
            string cwd = null;
            string error = null;
            IModelSystemStructure mstStructure;
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
                if (caughtError != null)
                {
                    SendRuntimeError(caughtError);
                }
                else
                {

                    SendRunComplete();
                }
                Directory.SetCurrentDirectory(cwd);
            }
        }

        private void SendRunComplete()
        {
            RunComplete?.Invoke();
        }

        private void SendRuntimeError(Exception errorMessage)
        {
            errorMessage = GetTopRootException(errorMessage);
            SaveErrorMessage(errorMessage);
            RuntimeError?.Invoke(errorMessage.Message, errorMessage.StackTrace);
        }

        private static Exception GetTopRootException(Exception value)
        {
            if (value == null) return null;
            if (value is AggregateException agg)
            {
                return GetTopRootException(agg.InnerException);
            }
            return value;
        }

        private void SaveErrorMessage(Exception errorMessage)
        {
            using (var writer = new StreamWriter("XTMF.ErrorLog.txt", true))
            {
                var realExeption = GetTopRootException(errorMessage);
                writer.WriteLine(realExeption.Message);
                writer.WriteLine();
                writer.WriteLine(realExeption.StackTrace);
            }
        }

        private void SendRuntimeValidationError(string errorMessage)
        {
            RuntimeValidationError?.Invoke(errorMessage);
        }

        private void SendValidationError(string errorMessage)
        {
            ValidationError?.Invoke(errorMessage);
        }

        private void SetStatusToRunning()
        {
            RunStarted?.Invoke();
        }

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    RunComplete = null;
                    RuntimeError = null;
                    ValidationError = null;
                    ValidationStarting = null;
                    RuntimeValidationError = null;
                    Pipe?.Dispose();
                }
                disposedValue = true;
            }
        }

        ~XTMFRun()
        {
            Dispose(false);
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}