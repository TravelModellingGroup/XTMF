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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace XTMF
{
    public sealed class ProjectEditingSession : IDisposable
    {
        private struct SessionData
        {
            internal ModelSystemEditingSession Session;
            internal int References;
        }

        public Project Project;
        private XTMFRuntime Runtime;
        public ProjectEditingSession(Project project, XTMFRuntime runtime)
        {
            Project = project;
            Runtime = runtime;
            EditingSessions = new SessionData[Project.ModelSystemStructure.Count];
        }

        /// <summary>
        /// The currently open editing sessions
        /// </summary>
        private SessionData[] EditingSessions;

        private object EditingSessionsLock = new object();

        public event EventHandler SessionClosed;

        /// <summary>
        /// Closes all of the model system editing sessions for this project
        /// </summary>
        private void CloseAllModelSystemEditingSessions()
        {
            lock (this.EditingSessions)
            {
                for(int i = 0; i < EditingSessions.Length; i++)
                {
                    if(EditingSessions[i].Session != null)
                    {
                        EditingSessions[i].Session.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Add a model system to the project
        /// </summary>
        /// <param name="modelSystem">The model system to add to the project</param>
        /// <param name="error">An error message in case of failure</param>
        /// <returns>True if the model system was added successfully</returns>
        public bool AddModelSystem(ModelSystem modelSystem, ref string error)
        {
            if(modelSystem == null)
            {
                throw new ArgumentNullException("modelSystem");
            }
            lock (EditingSessionsLock)
            {
                if(!this.Project.AddModelSystem(modelSystem, ref error))
                {
                    return false;
                }
                var temp = new SessionData[EditingSessions.Length + 1];
                Array.Copy(EditingSessions, temp, EditingSessions.Length);
                EditingSessions = temp;
                return true;
            }
        }



        /// <summary>
        /// Removes a model system from the project
        /// </summary>
        /// <param name="index">The index to remove</param>
        /// <param name="error">An error message in case of failure</param>
        /// <returns>True if the model system was removed successfully.</returns>
        public bool RemoveModelSystem(int index, ref string error)
        {
            lock (EditingSessionsLock)
            {
                if(index < 0 | index >= this.Project.ModelSystemStructure.Count)
                {
                    error = "The index is invalid.";
                    return false;
                }
                if(EditingSessions[index].Session != null)
                {
                    error = "Unable to remove the model system. It is currently being edited.";
                    return false;
                }
                if(!this.Project.RemoveModelSystem(index, ref error))
                {
                    return false;
                }
                var temp = new SessionData[EditingSessions.Length - 1];
                Array.Copy(EditingSessions, temp, index);
                Array.Copy(EditingSessions, index + 1, temp, index, EditingSessions.Length - index - 1);
                EditingSessions = temp;
            }
            return true;
        }

        /// <summary>
        /// Get a list of previous runs for this project.
        /// </summary>
        /// <returns>A list of paths to directories containing</returns>
        public List<string> GetPreviousRuns()
        {
            var projectDir = Path.Combine(this.GetConfiguration().ProjectDirectory, Project.Name);
            return (from dir in Directory.EnumerateDirectories(projectDir)
                    where File.Exists(Path.Combine(projectDir, dir, "RunParameters.xml"))
                    select Path.Combine(projectDir, dir)).ToList();
        }

        /// <summary>
        /// Load a previous run from file
        /// </summary>
        /// <param name="path">The directory the previous run was in.</param>
        /// <param name="error">An error in case of failure</param>
        /// <returns>An editing session if successfull, null otherwise.</returns>
        public ModelSystemEditingSession LoadPreviousRun(string path, ref string error)
        {
            DirectoryInfo info = new DirectoryInfo(path);
            if(!info.Exists)
            {
                error = "There is no directory with the name '" + path + "'!";
                return null;
            }
            var runFileName = Path.Combine(path, "RunParameters.xml");
            if(!File.Exists(runFileName))
            {
                error = "There is no file containing run parameters in the directory '" + path + "'!";
                return null;
            }
            return new ModelSystemEditingSession(Runtime, this, runFileName);
        }


        /// <summary>
        /// Move the model systems inside of a project.
        /// </summary>
        /// <param name="currentIndex">The index to move</param>
        /// <param name="newIndex">The new index for the model system</param>
        /// <param name="error">If there is a failure this will contain a message</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool MoveModelSystem(int currentIndex, int newIndex, ref string error)
        {
            lock (EditingSessionsLock)
            {
                if(currentIndex < 0 | currentIndex >= EditingSessions.Length)
                {
                    error = "The model system to move is out of range!";
                    return false;
                }
                if(newIndex < 0 | newIndex >= EditingSessions.Length)
                {
                    error = "The new position is out of range!";
                    return false;
                }
                // if they are the same then success (we don't want to shortcut though if invalid data is given though)
                if(currentIndex == newIndex) return true;
                if(!this.Project.MoveModelSystems(currentIndex, newIndex, ref error))
                {
                    return false;
                }
                // move the indexes around
                var temp = EditingSessions[currentIndex];
                if(currentIndex < newIndex)
                {
                    // move everything up
                    Array.Copy(EditingSessions, currentIndex + 1, EditingSessions, currentIndex, newIndex - currentIndex);
                    EditingSessions[newIndex] = temp;
                }
                else
                {
                    // move everything down
                    Array.Copy(EditingSessions, newIndex, EditingSessions, newIndex + 1, currentIndex - newIndex);
                    EditingSessions[newIndex] = temp;
                }
                return true;
            }
        }

        /// <summary>
        /// Gets the configuration for the XTMF runtime environment
        /// </summary>
        /// <returns></returns>
        public IConfiguration GetConfiguration()
        {
            return Runtime.Configuration;
        }


        /// <summary>
        /// Edit a model system inside of the project
        /// </summary>
        /// <param name="modelSystemIndex">The index of the model system inside of the project</param>
        public ModelSystemEditingSession EditModelSystem(int modelSystemIndex)
        {
            lock (this.EditingSessionsLock)
            {
                // Make sure that we have a valid index. This needs to be inside of EditingSessionLock
                if(this.Project.ModelSystemStructure.Count < modelSystemIndex | modelSystemIndex < 0)
                {
                    throw new ArgumentOutOfRangeException("modelSystemIndex");
                }
                // if the session doesn't exist create it
                if(this.EditingSessions[modelSystemIndex].Session == null)
                {
                    this.EditingSessions[modelSystemIndex].Session = new ModelSystemEditingSession(Runtime, this, modelSystemIndex);
                }
                // in either case add a reference to it.
                this.EditingSessions[modelSystemIndex].References++;
                return this.EditingSessions[modelSystemIndex].Session;
            }
        }

        internal void ModelSystemEditingSessionClosed(ModelSystemEditingSession modelSystemEditingSession, int modelSystemIndex)
        {
            // ensure this model system is not a past run
            if(modelSystemIndex >= 0)
            {
                lock (EditingSessionsLock)
                {
                    this.EditingSessions[modelSystemIndex].References--;
                    if(this.EditingSessions[modelSystemIndex].References <= 0)
                    {
                        this.EditingSessions[modelSystemIndex].References = 0;
                        this.EditingSessions[modelSystemIndex].Session = null;
                    }
                }
            }
        }

        /// <summary>
        /// Close the session and all model editing sessions connected to it.
        /// </summary>
        public void Dispose()
        {
            lock (EditingSessionsLock)
            {
                Runtime.ProjectController.RemoveEditingReference(this);
            }
        }

        /// <summary>
        /// Called internally when no references are left to the project editing session
        /// </summary>
        internal void SessoinTerminated()
        {
            CloseAllModelSystemEditingSessions();
            var temp = SessionClosed;
            if(temp != null)
            {
                temp(this, new EventArgs());
            }
        }
    }
}