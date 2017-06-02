/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.CodeDom;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using XTMF.Editing;

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
        private readonly XTMFRuntime Runtime;
        public ProjectEditingSession(Project project, XTMFRuntime runtime)
        {
            Project = project;
            Runtime = runtime;
            EditingSessions = new SessionData[Project.ModelSystemStructure.Count];
            project.ExternallySaved += Project_ExternallySaved;
        }

        /// <summary>
        /// The project externally saved by a cloned project!
        /// </summary>
        public event EventHandler ProjectWasExternallySaved;

        internal void Project_ExternallySaved(object sender, ProjectExternallySavedEventArgs e)
        {
            if (Project == e.BaseProject)
            {
                // If our project was overwritten, dump everything and switch what the active project is.
                lock (EditingSessionsLock)
                {
                    Project.ExternallySaved -= Project_ExternallySaved;
                    Project = e.CloneProject;
                    Project.ExternallySaved += Project_ExternallySaved;
                    for (int i = 0; i < EditingSessions.Length; i++)
                    {
                        var session = EditingSessions[i].Session;
                        if (session != null)
                        {
                            session.ProjectWasExternalSaved();
                        }
                    }
                }
                var call = ProjectWasExternallySaved;
                if (call != null)
                {
                    call(this, e);
                }
            }
        }

        /// <summary>
        /// The currently open editing sessions
        /// </summary>
        private SessionData[] EditingSessions;

        private object EditingSessionsLock = new object();

        public string Name { get { lock (EditingSessionsLock) { return Project.Name; } } }

        public event EventHandler SessionClosed;

        public event PropertyChangedEventHandler NameChanged;

        public event EventHandler ModelSystemNameChanged;

        public event EventHandler ModelSystemSaved;

        /// <summary>
        /// Attempt to rename the project.  This name must be unique.
        /// </summary>
        /// <param name="newName">The name to save the project as</param>
        /// <param name="error">The reason the call failed</param>
        /// <returns>True if the rename was successful, otherwise error will describe why it failed.</returns>
        public bool ReameProject(string newName, ref string error)
        {
            lock (EditingSessionsLock)
            {
                var ret = Runtime.ProjectController.RenameProject(Project, newName, ref error);
                var e = NameChanged;
                if (ret)
                {
                    e?.Invoke(this, new PropertyChangedEventArgs(Name));
                }
                return ret;
            }
        }

        /// <summary>
        /// Create a cloned project with the given name
        /// </summary>
        /// <param name="name">The name of the cloned project</param>
        /// <param name="error">A description of the error</param>
        /// <returns>True if the clone succeeds, false otherwise</returns>
        public bool CloneProjectAs(string name, ref string error)
        {
            lock (EditingSessionsLock)
            {
                return Runtime.ProjectController.CloneProject(Project, name, ref error);
            }
        }

        /// <summary>
        /// Closes all of the model system editing sessions for this project
        /// </summary>
        private void CloseAllModelSystemEditingSessions()
        {
            lock (EditingSessions)
            {
                for (int i = 0; i < EditingSessions.Length; i++)
                {
                    if (EditingSessions[i].Session != null)
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
            if (modelSystem == null)
            {
                throw new ArgumentNullException(nameof(modelSystem));
            }
            lock (EditingSessionsLock)
            {
                if (!Project.AddModelSystem(modelSystem, modelSystem.Name, ref error))
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
        /// Create a new model system in the project with the given name
        /// </summary>
        /// <param name="modelSystemName">The name of the new model system</param>
        /// <param name="error">If an error occurs this will contain a description</param>
        /// <returns>True if successful, false otherwise with error message.</returns>
        public bool AddModelSystem(string modelSystemName, ref string error)
        {
            lock (EditingSessionsLock)
            {
                if (!Project.AddModelSystem(modelSystemName, ref error))
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
        /// Add a model system to the project
        /// </summary>
        /// <param name="modelSystem">The model system to add to the project</param>
        /// <param name="newName">The name to use for this new model system</param>
        /// <param name="error">An error message in case of failure</param>
        /// <returns>True if the model system was added successfully</returns>
        public bool AddModelSystem(ModelSystem modelSystem, string newName, ref string error)
        {
            if (modelSystem == null)
            {
                throw new ArgumentNullException(nameof(modelSystem));
            }
            lock (EditingSessionsLock)
            {
                if (!Project.AddModelSystem(modelSystem, newName, ref error))
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
        /// Import a model system into the project from a string.
        /// </summary>
        /// <param name="modelSystemAsText">The text to convert into a model system</param>
        /// <param name="name">The name of the model system</param>
        /// <param name="error">A description of the error if one occurs</param>
        /// <returns>True if the model system was imported, false with a description otherwise.</returns>
        public bool ImportModelSystemFromString(string modelSystemAsText, string name, ref string error)
        {
            ModelSystem modelSystem;
            if (!Runtime.ModelSystemController.LoadDetachedModelSystemFromString(modelSystemAsText, out modelSystem, ref error))
            {
                return false;
            }
            modelSystem.Name = name;
            return AddModelSystem(modelSystem, ref error);
        }

        /// <summary>
        /// Import a model system into the project from file.
        /// </summary>
        /// <param name="fileName">The path of the file to load.</param>
        /// <param name="modelSystemName">The name to assign to this new model system.</param>
        /// <param name="error">A description of the error if one occurs</param>
        /// <returns>True if the model system was imported, false with a description otherwise.</returns>
        public bool ImportModelSystemFromFile(string fileName, string modelSystemName, ref string error)
        {
            ModelSystem modelSystem;
            if (!Runtime.ModelSystemController.LoadDetachedModelSystemFromFile(fileName, out modelSystem, ref error))
            {
                return false;
            }
            modelSystem.Name = modelSystemName;
            return AddModelSystem(modelSystem, ref error);
        }

        /// <summary>
        /// Export the model system as a string.
        /// </summary>
        /// <param name="ms">The model system to export</param>
        /// <param name="modelSystemAsString">The string to export the model system to.</param>
        /// <param name="error">A description of the error if one occurs</param>
        /// <returns>True if successful, false with a description of the error.</returns>
        public bool ExportModelSystemAsString(ModelSystemModel ms, out string modelSystemAsString, ref string error)
        {
            lock (EditingSessionsLock)
            {
                return Runtime.ProjectController.ExportModelSystemAsString(ms, out modelSystemAsString, ref error);
            }
        }

        public bool ExportModelSystemAsString(int modelSystemIndex, out string modelSystemAsString, ref string error)
        {
            lock (EditingSessionsLock)
            {
                return Runtime.ProjectController.ExportModelSystemAsString(Project, modelSystemIndex, out modelSystemAsString, ref error);
            }
        }

        /// <summary>
        /// Add a model system to the project
        /// </summary>
        /// <param name="modelSystem">The model system to add to the project</param>
        /// <param name="error">An error message in case of failure</param>
        /// <returns>True if the model system was added successfully</returns>
        public bool AddModelSystem(ModelSystemModel modelSystem, ref string error)
        {
            if (modelSystem == null)
            {
                throw new ArgumentNullException(nameof(modelSystem));
            }
            return AddModelSystem(modelSystem.ModelSystem, ref error);
        }

        /// <summary>
        /// Add a model system to the project
        /// </summary>
        /// <param name="modelSystem">The model system to add to the project</param>
        /// <param name="newName">The name to use for this new model system</param>
        /// <param name="error">An error message in case of failure</param>
        /// <returns>True if the model system was added successfully</returns>
        public bool AddModelSystem(ModelSystemModel modelSystem, string newName, ref string error)
        {
            if (modelSystem == null)
            {
                throw new ArgumentNullException(nameof(modelSystem));
            }
            return AddModelSystem(modelSystem.ModelSystem, newName, ref error);
        }

        /// <summary>
        /// Renames the model system within the project
        /// </summary>
        /// <param name="root">The root structure of the model system to rename</param>
        /// <param name="newName">The name to set this model system to.</param>
        /// <param name="error">An error message that describes why this operation has failed.</param>
        /// <returns>True if the model system was renamed, if not the error will describe why not.</returns>
        public bool RenameModelSystem(IModelSystemStructure root, string newName, ref string error)
        {
            lock (EditingSessionsLock)
            {
                var index = Project.ModelSystemStructure.IndexOf(root);
                if (index < 0)
                {
                    error = "The model system was not found within the project!";
                    return false;
                }
                // check to see if the model system is being edited, if it is send a command to that session
                var editingSession = EditingSessions.FirstOrDefault(s => s.Session != null && s.Session.IsEditing(root));
                if (editingSession.Session != null)
                {
                    editingSession.Session.ModelSystemModel.ChangeModelSystemName(newName, ref error);
                    InvokeModelSystemNameChanged();
                }
                //In any case we need to save this change so everything updates accordingly
                Project.ModelSystemStructure[index].Name = newName;
                Project.Save(ref error);
            }
            return true;
        }

        private void InvokeModelSystemNameChanged()
        {
            ModelSystemNameChanged?.Invoke(this, new EventArgs());
        }

        /// <summary>
        /// Clone the given model system with a new name
        /// </summary>
        /// <param name="root"></param>
        /// <param name="name"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool CloneModelSystemAs(IModelSystemStructure root, string name, ref string error)
        {
            lock (EditingSessionsLock)
            {
                var index = Project.ModelSystemStructure.IndexOf(root);
                if (index < 0)
                {
                    error = "The model system was not found within the project!";
                    return false;
                }
                // If it is currently being edited, save that version
                var editingSession = EditingSessions.FirstOrDefault(s => s.Session != null && s.Session.IsEditing(root));
                if (editingSession.Session != null)
                {
                    return editingSession.Session.SaveAsModelSystem(name, ref error);
                }
                List<ILinkedParameter> lp;
                var ms = Runtime.ModelSystemController.LoadOrCreate(name);
                ms.Name = name;
                ms.Description = Project.ModelSystemDescriptions[index];
                ms.ModelSystemStructure = Project.CloneModelSystemStructure(out lp, index);
                ms.LinkedParameters = lp;
                return ms.Save(ref error);
            }
        }

        /// <summary>
        /// Clone the given model system with a new name
        /// </summary>
        /// <param name="root"></param>
        /// <param name="name"></param>
        /// <param name="error"></param>
        /// <returns></returns>
        public static bool CloneModelSystemAs(XTMFRuntime runtime, IModelSystemStructure root, List<ILinkedParameter> linkedParameters, string description, string name, ref string error)
        {
            var ms = runtime.ModelSystemController.LoadOrCreate(name);
            ms.Name = name;
            ms.Description = description;
            ms.ModelSystemStructure = root;
            ms.LinkedParameters = linkedParameters;
            var ret = ms.Save(ref error);
            // make sure we don't reuse any references
            ms.Unload();
            return ret;
        }

        /// <summary>
        /// Export a model system within a project to file.
        /// </summary>
        /// <param name="modelSystemIndex">The index of the model system to export</param>
        /// <param name="fileName">The name of the file to save it to.</param>
        /// <param name="error">A description of the error that occurred if one did.</param>
        /// <returns>False if there was an error, true otherwise.</returns>
        public bool ExportModelSystem(int modelSystemIndex, string fileName, ref string error)
        {
            lock (EditingSessions)
            {
                // If it is currently being edited, save that version
                var root = Project.ModelSystemStructure[modelSystemIndex];
                var editingSession = EditingSessions.FirstOrDefault(s => s.Session != null && s.Session.IsEditing(root));
                if (editingSession.Session != null)
                {
                    error = "You can't export a model system while editing it.";
                    return false;
                }
                return Runtime.ProjectController.ExportModelSystem(fileName, Project, modelSystemIndex, ref error);
            }
        }


        public bool AddExternalModelSystem(IModelSystem system, string name, ref string error)
        {
        

            system.ModelSystemStructure.Name = name;
            system.Name = name;
            
            if (Project.AddExternalModelSystem(system, ref error))
            {
                var temp = new SessionData[EditingSessions.Length + 1];
                Array.Copy(EditingSessions, temp, EditingSessions.Length);
                EditingSessions = temp;
                return true;
            }
            else
            {
                return false;
            }
        }

        public ModelSystem CloneModelSystem(IModelSystemStructure root, ref string error)
        {

            return Project.CloneModelSystem(root);
        }

        public bool CloneModelSystemToProjectAs(IModelSystemStructure root, string name, ref string error)
        {
            lock (EditingSessionsLock)
            {
                var index = Project.ModelSystemStructure.IndexOf(root);
                if (index < 0)
                {
                    error = "The model system was not found within the project!";
                    return false;
                    
                }
                if (Project.AddModelSystem(index, name, ref error))
                {
                    var temp = new SessionData[EditingSessions.Length + 1];
                    Array.Copy(EditingSessions, temp, EditingSessions.Length);
                    EditingSessions = temp;
                    return true;
                }
                return false;
            }
        }

        internal IModelSystemStructure CloneModelSystemStructure(out List<ILinkedParameter> lp, int modelSystemIndex)
        {
            return Project.CloneModelSystemStructure(out lp, modelSystemIndex);
        }

        /// <summary>
        /// Checks to see if a run name already exists for this project
        /// </summary>
        /// <param name="runName">The run name to check for.</param>
        /// <returns>True if the run name already exists</returns>
        internal bool RunNameExists(string runName)
        {
            return Directory.Exists(Path.Combine(GetConfiguration().ProjectDirectory, Project.Name, runName));
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
                if (index < 0 | index >= Project.ModelSystemStructure.Count)
                {
                    error = "The index is invalid.";
                    return false;
                }
                if (EditingSessions[index].Session != null)
                {
                    error = "Unable to remove the model system. It is currently being edited.";
                    return false;
                }
                if (!Project.RemoveModelSystem(index, ref error))
                {
                    return false;
                }
                var temp = new SessionData[EditingSessions.Length - 1];
                Array.Copy(EditingSessions, temp, index);
                Array.Copy(EditingSessions, index + 1, temp, index, EditingSessions.Length - index - 1);
                for (int i = index; i < EditingSessions.Length; i++)
                {
                    var session = EditingSessions[i].Session;
                    session?.SetModelSystemIndex(i);
                }
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
            var projectDir = Path.Combine(GetConfiguration().ProjectDirectory, Project.Name);
            return (from dir in Directory.EnumerateDirectories(projectDir)
                    where File.Exists(Path.Combine(projectDir, dir, "RunParameters.xml"))
                    select Path.Combine(projectDir, dir)).ToList();
        }

        /// <summary>
        /// Load a previous run from file
        /// </summary>
        /// <param name="path">The directory the previous run was in.</param>
        /// <param name="error">An error in case of failure</param>
        /// <returns>An editing session if successful, null otherwise.</returns>
        public bool LoadPreviousRun(string path, ref string error, out ModelSystemEditingSession session)
        {
            session = null;
            DirectoryInfo info = new DirectoryInfo(path);
            if (!info.Exists)
            {
                error = "There is no directory with the name '" + path + "'!";
                return false;
            }
            var runFileName = Path.Combine(path, "RunParameters.xml");
            if (!File.Exists(runFileName))
            {
                error = "There is no file containing run parameters in the directory '" + path + "'!";
                return false;
            }
            try
            {
                session = new ModelSystemEditingSession(Runtime, this, runFileName);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>
        /// Create a cloned project of the given project
        /// </summary>
        /// <param name="project">The project to clone</param>
        /// <returns>An exact copy of the project, if it is saved it will overwrite</returns>
        internal Project CreateCloneProject(Project project)
        {
            return project.CreateCloneProject();
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
                if (currentIndex < 0 | currentIndex >= EditingSessions.Length)
                {
                    error = "The model system to move is out of range!";
                    return false;
                }
                if (newIndex < 0 | newIndex >= EditingSessions.Length)
                {
                    error = "The new position is out of range!";
                    return false;
                }
                // if they are the same then success (we don't want to shortcut though if invalid data is given though)
                if (currentIndex == newIndex) return true;
                if (!Project.MoveModelSystems(currentIndex, newIndex, ref error))
                {
                    return false;
                }
                // move the indexes around
                var temp = EditingSessions[currentIndex];
                if (currentIndex < newIndex)
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
            lock (EditingSessionsLock)
            {
                // Make sure that we have a valid index. This needs to be inside of EditingSessionLock
                if (Project.ModelSystemStructure.Count < modelSystemIndex | modelSystemIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(modelSystemIndex));
                }
                // if the session doesn't exist create it
                if (EditingSessions[modelSystemIndex].Session == null)
                {
                    EditingSessions[modelSystemIndex].Session = new ModelSystemEditingSession(Runtime, this, modelSystemIndex);
                    EditingSessions[modelSystemIndex].Session.NameChanged += Session_NameChanged;
                    EditingSessions[modelSystemIndex].Session.Saved += Session_Saved; ;
                }
                // in either case add a reference to it.
                EditingSessions[modelSystemIndex].References++;
                return EditingSessions[modelSystemIndex].Session;
            }
        }

        private void Session_Saved(object sender, EventArgs e)
        {
            ModelSystemSaved?.Invoke(this, e);
        }

        private void Session_NameChanged(object sender, PropertyChangedEventArgs e)
        {
            InvokeModelSystemNameChanged();
        }

        internal void ModelSystemEditingSessionClosed(ModelSystemEditingSession modelSystemEditingSession, int modelSystemIndex)
        {
            // ensure this model system is not a past run
            if (modelSystemIndex >= 0)
            {
                lock (EditingSessionsLock)
                {
                    EditingSessions[modelSystemIndex].References--;
                    if (EditingSessions[modelSystemIndex].References <= 0)
                    {
                        EditingSessions[modelSystemIndex].References = 0;
                        EditingSessions[modelSystemIndex].Session = null;
                    }
                }
            }
        }

        internal bool WillCloseTerminate(int modelSystemIndex)
        {
            // ensure this model system is not a past run
            if (modelSystemIndex >= 0)
            {
                lock (EditingSessionsLock)
                {
                    if (EditingSessions[modelSystemIndex].References <= 1)
                    {
                        return true;
                    }
                }
            }
            return false;
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
        internal void SessionTerminated()
        {
            CloseAllModelSystemEditingSessions();
            SessionClosed?.Invoke(this, new EventArgs());
        }

        public bool DeleteProject(ref string error)
        {
            CloseAllModelSystemEditingSessions();
            Dispose();
            return Runtime.ProjectController.DeleteProject(Project, ref error);
        }

        public XTMFRuntime GetRuntime()
        {
            return Runtime;
        }

        public bool ValidateModelSystemName(string name)
        {
            return Runtime.ProjectController.ValidateProjectName(name);
        }
    }
}