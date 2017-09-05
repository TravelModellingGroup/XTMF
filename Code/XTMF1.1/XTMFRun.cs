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
using XTMF.Run;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Diagnostics;
using System.Reflection;

namespace XTMF
{
    /// <summary>
    /// This class encapsulates the concept of a Model System run.
    /// Subclasses of this will allow for remote model system execution.
    /// </summary>
    public abstract class XTMFRun : IDisposable
    {
        /// <summary>
        /// The link to XTMF's settings
        /// </summary>
        public IConfiguration Configuration { get; private set; }

        /// <summary>
        /// The name of this run
        /// </summary>
        protected string RunName;

        /// <summary>
        /// The model system root if we are using a past run
        /// </summary>
        protected ModelSystemStructureModel ModelSystemStructureModelRoot;

        public string RunDirectory { get; private set; }

        public abstract bool RunsRemotely { get; }

        protected XTMFRun(string runName, string runDirectory, IConfiguration config)
        {
            RunName = runName;
            RunDirectory = runDirectory;
            Configuration = config;
        }

        public static XTMFRun CreateLocalRun(Project project, int modelSystemIndex, ModelSystemModel root, Configuration config, string runName, bool overwrite = false)
        {
            return new XTMFRunLocal(project, modelSystemIndex, root, config, runName, overwrite);
        }

        public static XTMFRun CreateLocalRun(Project project, ModelSystemStructureModel root, Configuration configuration, string runName, bool overwrite = false)
        {
            return new XTMFRunLocal(project, root, configuration, runName, overwrite);
        }

        public static XTMFRun CreateRemoteClient(Configuration configuration, string runName, string runDirectory, string modelSystem)
        {
            return new XTMFRunRemoteClient(configuration, runName, runDirectory, modelSystem);
        }
       

        protected static void ClearFolder(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(path);
            if (!directory.Exists)
            {
                return;
            }
            directory.Delete(true);
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
        public abstract bool ExitRequest();

        /// <summary>
        /// Get the currently requested colour from the model system
        /// </summary>
        /// <returns>The colour requested by the model system</returns>
        public abstract Tuple<byte, byte, byte> PollColour();
        

        /// <summary>
        /// Get the current progress for this run
        /// </summary>
        /// <returns>The current progress between 0 and 1</returns>
        public abstract float PollProgress();


        /// <summary>
        /// Get the status message for this run
        /// </summary>
        /// <returns></returns>
        public abstract string PollStatusMessage();

        /// <summary>
        /// Attempts to notify all modules part of the current model system structure
        /// with an exit request usually generated by user interaction. Any module inheriting
        /// IModelSystemTemplate will be sent an ExitRequest.
        /// </summary>
        /// <returns></returns>
        public abstract bool DeepExitRequest();

        public abstract List<Tuple<IModelSystemStructure, Queue<int>, string>> CollectRuntimeValidationErrors();

        public abstract List<Tuple<IModelSystemStructure, Queue<int>, string>> CollectValidationErrors();

        public abstract void Start();

        /// <summary>
        /// Blocks execution until the run has completed.
        /// </summary>
        public abstract void Wait();

        public abstract void TerminateRun();

        protected void AlertValidationStarting()
        {
            ValidationStarting?.Invoke();
        }

        protected void SendRunComplete()
        {
            RunComplete?.Invoke();
        }

        protected void SendRuntimeError(string message, string stackTrace)
        {
            RuntimeError?.Invoke(message, stackTrace);
        }

        private static Exception GetTopRootException(Exception value)
        {
            if (value is AggregateException agg)
            {
                return GetTopRootException(agg.InnerException);
            }
            return value;
        }

        protected static void SaveErrorMessage(Exception errorMessage)
        {
            using (var writer = new StreamWriter("XTMF.ErrorLog.txt", true))
            {
                var realExeption = GetTopRootException(errorMessage);
                writer.WriteLine(realExeption.Message);
                writer.WriteLine();
                writer.WriteLine(realExeption.StackTrace);
            }
        }

        protected void SendRuntimeValidationError(string errorMessage)
        {
            RuntimeValidationError?.Invoke(errorMessage);
        }

        protected void SendValidationError(string errorMessage)
        {
            ValidationError?.Invoke(errorMessage);
        }

        protected void SetStatusToRunning()
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