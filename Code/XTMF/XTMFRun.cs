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
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Filter;
using log4net.Layout;
using XTMF.Logging;

namespace XTMF
{
    /// <summary>
    /// This class encapsulates the concept of a Model System run.
    /// Subclasses of this will allow for remote model system execution.
    /// </summary>
    public abstract class XTMFRun : IDisposable
    {
        public IModelSystemTemplate MST { get; protected set; }
        /// <summary>
        /// The link to XTMF's settings
        /// </summary>
        public IConfiguration Configuration { get; private set; }

        /// <summary>
        /// The name of this run
        /// </summary>
        public string RunName { get; protected set; }

        /// <summary>
        /// The model system root if we are using a past run
        /// </summary>
        public ModelSystemStructureModel ModelSystemStructureModelRoot { get; protected set; }

        public string RunDirectory { get; private set; }

        public abstract bool RunsRemotely { get; }

        private const string Pattern = "%logger***%message%newline";

        private static ILogger _globalLogger;

        public static ILogger GlobalLogger => _globalLogger;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="runName"></param>
        /// <param name="runDirectory"></param>
        /// <param name="config"></param>
        protected XTMFRun(string runName, string runDirectory, IConfiguration config)
        {
            RunName = runName;
            RunDirectory = runDirectory;
            Configuration = config;
            ConfigureLog4Net();
        }

        /// <summary>
        /// Configures log4net using a basic console appender
        /// </summary>
        private void ConfigureLog4Net()
        {
            PatternLayout layout = new(
                        Pattern);
            ConsoleAppender appender =
                new() {Layout = layout};
            //create logger (console) unrelated to global logging under XTMFRun
            LoggerMatchFilter filter = new()
            {
                AcceptOnMatch = false,
                LoggerToMatch = "XTMFRun"
            };
            appender.AddFilter(filter);
            appender.ActivateOptions();

            //create logger (file) for global logging using name XTMFRun
            filter = new LoggerMatchFilter
            {
                AcceptOnMatch = true,
                LoggerToMatch = "XTMFRun"
            };
            BasicConfigurator.Configure(appender);
            _globalLogger = new Logger(LogManager.GetLogger("XTMFRun"));
        }

        public static XTMFRun CreateLocalRun(Project project, int modelSystemIndex, ModelSystemModel root, Configuration config, string runName, bool overwrite = false)
        {
            return new XTMFRunLocal(project, modelSystemIndex, root, config, runName, overwrite);
        }

        public static XTMFRun CreateRemoteHost(Project project, int modelSystemIndex, ModelSystemModel root,
            Configuration config, string runName, bool overwrite = false)
        {
            return new XTMFRunRemoteHost(config, root.Root, project.LinkedParameters[modelSystemIndex], runName, Path.Combine(config.ProjectDirectory, project.Name, runName),
                overwrite);
        }

        public static XTMFRun CreateLocalRun(Project project, ModelSystemModel model, Configuration configuration, string runName, bool overwrite = false)
        {
            return new XTMFRunLocal(project, model, configuration, runName, overwrite);
        }

        public static XTMFRun CreateRemoteHost(Project project, ModelSystemStructureModel root, Configuration config, string runName, bool overwrite = false)
        {
            return new XTMFRunRemoteHost(config, root, [], runName, Path.Combine(config.ProjectDirectory, project.Name, runName),
                overwrite);
        }

        public static XTMFRun CreateRemoteClient(Configuration configuration, string runName, string runDirectory, string modelSystem)
        {
            return new XTMFRunRemoteClient(configuration, runName, runDirectory, modelSystem);
        }
       
        protected static void ClearFolder(string path)
        {
            DirectoryInfo directory = new(path);
            if (!directory.Exists)
            {
                return;
            }
            directory.Delete(true);
        }

        /// <summary>
        /// An event that is fired when the run sends back a message
        /// </summary>
        public event Action<string> RunMessage;

        /// <summary>
        /// An event that fires when the run completes successfully
        /// </summary>
        public event Action RunCompleted;

        /// <summary>
        /// An event that fires when all of the validation has completed and the model system
        /// has started executing.
        /// </summary>
        public event Action RunStarted;

        /// <summary>
        /// An event that fires if a runtime error occurs, this includes out of memory exceptions
        /// </summary>
        public event Action<ErrorWithPath> RuntimeError;

        /// <summary>
        /// An event that fires when the model ends in an error during runtime validation
        /// </summary>
        public event Action<List<ErrorWithPath>> RuntimeValidationError;

        /// <summary>
        /// An event that fires when the Model does not pass validation
        /// </summary>
        public event Action<List<ErrorWithPath>> ValidationError;

        /// <summary>
        /// An event that fires when Model Validation starts
        /// </summary>
        public event Action ValidationStarting;

        /// <summary>
        /// An event the fires when the running project has saved itself
        /// </summary>
        public event Action<XTMFRun, ModelSystemStructure> ProjectSavedByRun;

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

        /// <summary>
        /// Start the run on a different thread.
        /// </summary>
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

        protected void InvokeRunCompleted()
        {
            RunCompleted?.Invoke();
        }

        protected void InvokeRuntimeError(ErrorWithPath error)
        {
            SaveErrorMessage(error);
            RuntimeError?.Invoke(error);
        }

        private static Exception GetTopRootException(Exception value)
        {
            if (value is AggregateException agg)
            {
                return GetTopRootException(agg.InnerException);
            }
            return value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        protected static void SaveErrorMessage(ErrorWithPath error)
        {
            using var writer = new StreamWriter("XTMF.ErrorLog.txt", true);
            writer.WriteLine(error.Message);
            writer.WriteLine();
            writer.WriteLine(error.StackTrace);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="errorMessage"></param>
        protected void InvokeRuntimeValidationError(List<ErrorWithPath> errorMessage)
        {
            RuntimeValidationError?.Invoke(errorMessage);
        }


        protected void InvokeValidationError(List<ErrorWithPath> errorMessage)
        {
            ValidationError?.Invoke(errorMessage);
        }

        protected void SetStatusToRunning()
        {
            RunStarted?.Invoke();
        }

        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
           
            if (!_disposedValue)
            {
                if (disposing)
                {
                    RunCompleted = null;
                    RuntimeError = null;
                    ValidationError = null;
                    ValidationStarting = null;
                    RuntimeValidationError = null;
                }
                _disposedValue = true;
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

        protected static List<ErrorWithPath> CreateFromSingleError(ErrorWithPath error)
        {
            return
            [
                error
            ];
        }

        protected static void GetInnermostError(ref Exception caughtError)
        {
            while (caughtError is AggregateException agg)
            {
                caughtError = agg.InnerException;
            }
        }

        /// <summary>
        /// Do a runtime validation check for the currently running model system
        /// </summary>
        /// <param name="error">This parameter gets the error message if any is generated</param>
        /// <param name="currentPoint">The module to look at, set this to the root to begin.</param>
        /// <returns>This will be false if there is an error, true otherwise</returns>
        protected static bool RunTimeValidation(List<int> path, List<ErrorWithPath> errors, IModelSystemStructure currentPoint)
        {
            var ret = true;
            if (currentPoint.Module != null)
            {
                string error = null;
                if (!currentPoint.Module.RuntimeValidation(ref error))
                {
                    errors.Add(new ErrorWithPath(path, 
                        $"Runtime Validation Error in {currentPoint.Name}\r\n{error}",
                        null,
                        currentPoint.Module.Name));
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

        /// <summary>
        /// Report back that a message has been sent through the console.
        /// </summary>
        /// <param name="message"></param>
        protected void SendRunMessage(string message)
        {
            lock (this)
            {
                RunMessage?.Invoke(message);
            }
        }

        protected void SendProjectSaved(ModelSystemStructure mss)
        {
            ProjectSavedByRun?.Invoke(this, mss);
        }
    }
}