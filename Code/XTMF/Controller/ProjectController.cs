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
using System.Collections.Generic;
using System.Linq;

namespace XTMF;

public class ProjectController
{
    private readonly XTMFRuntime Runtime;

    public ProjectController(XTMFRuntime runtime)
    {
        Runtime = runtime;
    }

    /// <summary>
    /// The sessions that are currently running
    /// </summary>
    private List<ProjectEditingSession> EditingSessions = [];

    /// <summary>
    /// The number of sessions per session that is currently running
    /// </summary>
    private List<int> ReferenceCount = [];

    /// <summary>
    /// This lock needs to be acquired before we are allowed to touch the editing sessions.
    /// </summary>
    private object EditingSessionLock = new();

    /// <summary>
    /// Get a session to edit a project.
    /// </summary>
    /// <param name="project">The project to edit</param>
    public ProjectEditingSession EditProject(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);
        lock (EditingSessionLock)
        {
            // First check to see if a session is already open
            for (int i = 0; i < EditingSessions.Count; i++)
            {
                if (EditingSessions[i].Project == project)
                {
                    ReferenceCount[i]++;
                    return EditingSessions[i];
                }
            }
            // If we didn't find one create a new reference
            var session = new ProjectEditingSession(project, Runtime);
            EditingSessions.Add(session);
            ReferenceCount.Add(1);
            return session;
        }
    }

    public bool IsEditSessionOpenForProject(Project project)
    {
        lock (EditingSessionLock)
        {
            return EditingSessions.Any(p => p.Name == project.Name);
        }
    }

    public void ClearEditingSessions()
    {
        lock (EditingSessionLock)
        {
            ReferenceCount.Clear();
            EditingSessions.Clear();
        }
    }


    /// <summary>
    /// Create a clone of the given project with the given name.
    /// </summary>
    /// <param name="toClone">The project to clone</param>
    /// <param name="name">The name to give the clone</param>
    /// <param name="error">A description of the error</param>
    /// <returns>True if the clone succeeded,</returns>
    public bool CloneProject(Project toClone, string name, ref string error)
    {
        if (!Project.ValidateProjectName(name))
        {
            error = "The project name was invalid!";
            return false;
        }
        lock (EditingSessionLock)
        {
            var repository = Runtime.Configuration.ProjectRepository;
            // Make sure the name is unique
            if ((from project in Runtime.Configuration.ProjectRepository.Projects
                 where project.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)
                 select project as Project).Any())
            {
                error = "A project with that name already existed!";
                return false;
            }
            var clonedProject = toClone.CreateCloneProject(false);
            clonedProject.Name = name;
            // if we are able to save, add it to the project repository
            if (clonedProject.Save(ref error))
            {
                repository.AddProject(clonedProject);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Rename the given project
    /// </summary>
    /// <param name="project">The project to rename</param>
    /// <param name="newName">The name to save this project as</param>
    /// <param name="error">A description of why this operation has failed</param>
    /// <returns>True if the rename succeeded, false otherwise.</returns>
    public bool RenameProject(Project project, string newName, ref string error)
    {
        lock (EditingSessionLock)
        {
            if (!((ProjectRepository)Runtime.Configuration.ProjectRepository).RenameProject(project, newName, ref error))
            {
                return false;
            }
            return true;
        }
    }

    public bool SetDescription(Project project, string newDescription, ref string error)
    {
        lock (EditingSessionLock)
        {
            if (!Runtime.Configuration.ProjectRepository.SetDescription(project, newDescription, ref error))
            {
                error = "The new description was invalid!";
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Remove a reference that a session is being edited
    /// </summary>
    /// <param name="session">The session to remove a reference to.</param>
    internal void RemoveEditingReference(ProjectEditingSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        lock (EditingSessionLock)
        {
            var index = EditingSessions.IndexOf(session);
            if (index >= 0)
            {
                ReferenceCount[index]--;
                if (ReferenceCount[index] <= 0)
                {
                    // Make sure to remove this binding since it will hold memory
                    EditingSessions[index].Project.ExternallySaved -= EditingSessions[index].Project_ExternallySaved;
                    // now remove the references from our session 
                    EditingSessions.RemoveAt(index);
                    ReferenceCount.RemoveAt(index);
                }
            }
            
        }
    }

    private bool ValidateProjectName(string name, ref string error)
    {
        if (String.IsNullOrWhiteSpace(name) || !Runtime.Configuration.ProjectRepository.ValidateProjectName(name))
        {
            error = "The name is invalid.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Loads a project by name, fails if it doesn't exist already
    /// </summary>
    /// <param name="name">The name of the project to load</param>
    /// <param name="error">Contains an error message in case of failure</param>
    /// <returns>The loaded project</returns>
    public Project Load(string name, ref string error)
    {
        lock (EditingSessionLock)
        {
            var loadedProject = (from project in Runtime.Configuration.ProjectRepository.Projects
                                 where project.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)
                                 select project as Project).FirstOrDefault();
            if (loadedProject == null)
            {
                error = "No project with that name was loaded";
            }
            return loadedProject;
        }
    }

    /// <summary>
    /// Loads a project if it exists or creates a new project with the given name.
    /// </summary>
    /// <param name="name">The name of the project</param>
    /// <param name="error">AContains an error message in case of failure</param>
    /// <returns>The loaded project</returns>
    public Project LoadOrCreate(string name, ref string error)
    {
        lock (EditingSessionLock)
        {
            Project alreadyLoaded;
            if ((alreadyLoaded = Load(name, ref error)) != null)
            {
                return alreadyLoaded;
            }
            error = null;
            if (!ValidateProjectName(name, ref error))
            {
                return null;
            }
            var newProject = new Project(name, Runtime.Configuration);
            if (!newProject.Save(ref error))
            {
                return null;
            }
            Runtime.Configuration.ProjectRepository.AddProject(newProject);
            return newProject;
        }
    }

    /// <summary>
    /// Gets an editing session for the project
    /// </summary>
    /// <param name="project">The project to create the editing session for</param>
    /// <param name="error">An error message in case of failure</param>
    /// <returns>The session to edit the project.</returns>
    public ProjectEditingSession EditProject(Project project, ref string error)
    {
        ArgumentNullException.ThrowIfNull(project);
        lock (EditingSessionLock)
        {
            // check to see if it already exists
            int index;
            if ((index = IndexOf(EditingSessions, (session) => session.Project == project)) >= 0)
            {
                ReferenceCount[index]++;
                return EditingSessions[index];
            }
            // if it doesn't exist already we need to make a new session
            var newSession = new ProjectEditingSession(project, Runtime);
            EditingSessions.Add(newSession);
            ReferenceCount.Add(1);
            return newSession;
        }
    }

    private static int IndexOf<T>(IList<T> data, Predicate<T> condition)
    {
        using var enumerator = data.GetEnumerator();
        for (int i = 0; enumerator.MoveNext(); i++)
        {
            if (condition(enumerator.Current))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Deletes a project
    /// </summary>
    /// <param name="name">The name of the project to delete</param>
    /// <param name="error">An error message in case of failure</param>
    /// <returns>True if the project was deleted</returns>
    public bool DeleteProject(string name, ref string error)
    {
        Project project;
        string ignore = null;
        if ((project = Load(name, ref ignore)) != null)
        {
            return DeleteProject(project, ref error);
        }
        error = "A project with that name was not found.";
        return false;
    }

    /// <summary>
    /// Deletes a project
    /// </summary>
    /// <param name="project">The project to delete</param>
    /// <param name="error">An error message in case of failure</param>
    /// <returns>True if the project was deleted, false if it failed.</returns>
    public bool DeleteProject(Project project, ref string error)
    {
        lock (EditingSessionLock)
        {
            if (IndexOf(EditingSessions, (session) => session.Project == project) >= 0)
            {
                error = "You can not delete a project while it is being edited.";
                return false;
            }
            return Runtime.Configuration.ProjectRepository.Remove(project);
        }
    }

    /// <summary>
    /// Get the list of projects currently active
    /// </summary>
    /// <returns>A snapshot of the projects that are currently active</returns>
    public List<Project> GetProjects()
    {
        lock (EditingSessionLock)
        {
            return (from project in Runtime.Configuration.ProjectRepository.Projects
                    select project as Project).ToList();
        }
    }

    public bool ValidateProjectName(string name)
    {
        return Project.ValidateProjectName(name);
    }

    internal bool ExportModelSystem(string fileName, Project project, int modelSystemIndex, ref string error)
    {
        var root = project.ModelSystemStructure[modelSystemIndex];
        return ModelSystem.Save(fileName, root, root.Description, project.LinkedParameters[modelSystemIndex], Runtime.Configuration, ref error);
    }

    /// <summary>
    /// Exports the model system model to a string.
    /// </summary>
    /// <param name="ms">The model system model to export</param>
    /// <param name="modelSystemAsString">The string to store the results into.</param>
    /// <param name="error">A description of the error if there is one.</param>
    /// <returns>True if successful, false otherwise with description.</returns>
    public bool ExportModelSystemAsString(ModelSystemModel ms, out string modelSystemAsString, ref string error)
    {
        var modelSystem = ms.CloneAsModelSystem(Runtime.Configuration);
        return Runtime.ModelSystemController.ExportModelSystemAsString(modelSystem, out modelSystemAsString,
            ref error);
    }

    /// <summary>
    /// Exports the model system model to a string.
    /// </summary>
    /// <param name="project">The project to export the model system from</param>
    /// <param name="modelSystemIndex">The index of the model system</param>
    /// <param name="modelSystemAsString">The string to store the results into.</param>
    /// <param name="error">A description of the error that occurred</param>
    /// <returns>The if successful, false otherwise with description.</returns>
    public bool ExportModelSystemAsString(Project project, int modelSystemIndex, out string modelSystemAsString, ref string error)
    {
        var mss = project.ModelSystemStructure[modelSystemIndex];
        // It is safe to not use a clone since this model system doesn't escape this method
        return
            Runtime.ModelSystemController.ExportModelSystemAsString(new ModelSystem(Runtime.Configuration, mss.Name)
            {
                Description = mss.Description,
                LinkedParameters = project.LinkedParameters[modelSystemIndex],
                ModelSystemStructure = mss
            }, out modelSystemAsString, ref error);
    }
}