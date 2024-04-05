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
using System.Collections.Generic;

namespace XTMF;

public interface IProjectRepository : IEnumerable<IProject>
{
    /// <summary>
    /// The project that is currently
    /// being used in XTMF
    /// </summary>
    IProject ActiveProject { get; }

    /// <summary>
    /// Provides access to all of the projects
    /// in this XTMF installation
    /// </summary>
    IList<IProject> Projects { get; }

    /// <summary>
    /// Add a new project to the repository
    /// </summary>
    /// <param name="project">The project you wish to add</param>
    /// <returns>Returns true if the project was added, false otherwise</returns>
    /// <remarks>All project names need to be unique and contain no invalid path characters (will return false otherwise)</remarks>
    bool AddProject(IProject project);

    /// <summary>
    /// Remove the selected project
    /// </summary>
    bool Remove(IProject project);

    /// <summary>
    /// Rename a project, moving all of the run data as well
    /// </summary>
    /// <param name="project">The project to work with</param>
    /// <param name="newName">The new name of the project</param>
    /// <returns>If the project was renamed properly</returns>
    bool RenameProject(IProject project, string newName);

    /// <summary>
    /// Make sure that the project's name is both unique
    /// and a valid name for a project
    /// </summary>
    /// <param name="name">The name that you wish to test</param>
    /// <returns>If the name is valid, true, otherwise false.</returns>
    bool ValidateProjectName(string name);

    /// <summary>
    /// Set the description of a project
    /// </summary>
    /// <param name="project">The project to change</param>
    /// <param name="newDescription">The description to set it to.</param>
    /// <param name="error"></param>
    /// <returns>True if the operation completes successfully, false otherwise.</returns>
    bool SetDescription(IProject project, string newDescription, ref string error);
}