/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace XTMF
{
    /// <summary>
    /// This class encapsulates the concept of a Model System run.
    /// Subclasses of this will allow for remote model system execution.
    /// </summary>
    public class XTMFRun
    {
        /// <summary>
        /// The link to XTMF's settings
        /// </summary>
        protected IConfiguration Config;

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

        public XTMFRun(IProject project, int modelSystemIndex, IConfiguration config, string runName)
        {
            this.Project = project;
            this.ModelSystemIndex = modelSystemIndex;
            this.Config = config;
            this.RunName = runName;
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
        public event Action<Exception> RuntimeError;

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
            var mst = this.MST;
            if ( mst != null )
            {
                return mst.ExitRequest();
            }
            return false;
        }

        /// <summary>
        /// Get the currently requested colour from the model system
        /// </summary>
        /// <returns>The colour requested by the model system</returns>
        public Tuple<byte, byte, byte> PollColour()
        {
            var mst = this.MST;
            if ( mst != null )
            {
                return mst.ProgressColour;
            }
            return null;
        }

        /// <summary>
        /// Get the current progress for this run
        /// </summary>
        /// <returns>The current progress between 0 and 1</returns>
        public virtual float PollProgress()
        {
            var mst = this.MST;
            if ( mst != null )
            {
                return mst.Progress;
            }
            return 1f;
        }

        /// <summary>
        /// Get the status message for this run
        /// </summary>
        /// <returns></returns>
        public virtual string PollStatusMessage()
        {
            var mst = this.MST;
            if ( mst != null )
            {
                return mst.ToString();
            }
            return null;
        }

        public virtual void Start()
        {
            new Task( () => OurRun(), TaskCreationOptions.LongRunning ).Start();
        }

        /// <summary>
        /// Do a runtime validation check for the currently running model system
        /// </summary>
        /// <param name="error">This parameter gets the error message if any is generated</param>
        /// <param name="currentPoint">The module to look at, set this to the root to begin.</param>
        /// <returns>This will be false if there is an error, true otherwise</returns>
        protected bool RunTimeValidation(ref string error, IModelSystemStructure currentPoint)
        {
            if ( currentPoint.Module != null )
            {
                if ( !currentPoint.Module.RuntimeValidation( ref error ) )
                {
                    return false;
                }
            }
            // check to see if there are descendants that need to be checked
            if ( currentPoint.Children != null )
            {
                foreach ( var module in currentPoint.Children )
                {
                    if ( !this.RunTimeValidation( ref error, module ) )
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void AlertValidationStarting()
        {
            var alert = this.ValidationStarting;
            if ( alert != null )
            {
                alert();
            }
        }

        /// <summary>
        /// This will remove all links into the model system from the current structure
        /// </summary>
        /// <param name="ms">The model system structure to clean up</param>
        private void CleanUpModelSystem(IModelSystemStructure ms)
        {
            if ( ms != null )
            {
                var disp = ms.Module as IDisposable;
                if ( disp != null )
                {
                    disp.Dispose();
                }
                ms.Module = null;
            }
            if ( ms.Children != null )
            {
                foreach ( var child in ms.Children )
                {
                    this.CleanUpModelSystem( child );
                }
            }
        }

        private void OurRun()
        {
            string cwd = null;
            string error = null;
            try
            {
                this.MST = this.Project.CreateModelSystem( ref error, this.ModelSystemIndex );
            }
            catch (Exception e)
            {
                SendValidationError( e.Message );
                return;
            }
            if ( MST == null )
            {
                SendValidationError( error );
                return;
            }
            var MSTStructure = this.Project.ModelSystemStructure[this.ModelSystemIndex];
            try
            {
                AlertValidationStarting();
                var path = Path.Combine( this.Config.ProjectDirectory, this.Project.Name, this.RunName );
                cwd = System.IO.Directory.GetCurrentDirectory();
                // check to see if the directory exists, if it doesn't create it
                DirectoryInfo info = new DirectoryInfo( path );
                if ( !info.Exists )
                {
                    info.Create();
                }
                System.IO.Directory.SetCurrentDirectory( path );
                MSTStructure.Save( Path.GetFullPath( "RunParameters.xml" ) );
                if ( !this.RunTimeValidation( ref error, MSTStructure ) )
                {
                    this.SendRuntimeValidationError( error );
                }
                else
                {
                    this.SetStatusToRunning();
                    MST.Start();
                }
            }
            catch (Exception e)
            {
                this.SendRuntimeError( e );
            }
            finally
            {
                Thread.MemoryBarrier();
                this.CleanUpModelSystem( MSTStructure );
                MSTStructure = null;
                MST = null;
                ( this.Config as Configuration ).ModelSystemExited();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Thread.MemoryBarrier();
                System.IO.Directory.SetCurrentDirectory( cwd );
                SendRunComplete();
            }
        }

        private void SendRunComplete()
        {
            var alert = this.RunComplete;
            if ( alert != null )
            {
                alert();
            }
        }

        private void SendRuntimeError(Exception errorMessage)
        {
            var alert = this.RuntimeError;
            if ( alert != null )
            {
                alert( errorMessage );
            }
        }

        private void SendRuntimeValidationError(string errorMessage)
        {
            var alert = this.RuntimeValidationError;
            if ( alert != null )
            {
                alert( errorMessage );
            }
        }

        private void SendValidationError(string errorMessage)
        {
            var alert = this.ValidationError;
            if ( alert != null )
            {
                alert( errorMessage );
            }
        }

        private void SetStatusToRunning()
        {
            var alert = this.RunStarted;
            if ( alert != null )
            {
                alert();
            }
        }
    }
}