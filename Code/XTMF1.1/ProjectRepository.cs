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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace XTMF
{
    /// <summary>
    ///     This class provides access to all of the
    ///     projects currently being managed by this
    ///     installation of XTMF
    /// </summary>
    public class ProjectRepository : IProjectRepository
    {
        /// <summary>
        ///     Initiate the project repository
        /// </summary>
        public ProjectRepository(IConfiguration configuration)
        {
            Projects = new List<IProject>();
            Configuration = configuration;
            FindAndLoadProjects();
        }

        public IConfiguration Configuration { get; }

        /// <summary>
        ///     The project that this repository is currently working with
        ///     in order to run XTMF
        /// </summary>
        public IProject ActiveProject { get; private set; }

        /// <summary>
        ///     A List of projects that are currently being managed
        /// </summary>
        public IList<IProject> Projects { get; }

        public bool AddProject(IProject project)
        {
            lock (this)
            {
                if (!ValidateProjectName(project))
                {
                    return false;
                }
                // If everything is good, add it to the list of projects
                Projects.Add(project);
                ((List<IProject>) Projects).Sort(
                    delegate(IProject first, IProject second) { return first.Name.CompareTo(second.Name); });
            }
            ProjectAdded?.Invoke(project);
            return true;
        }

        public IEnumerator<IProject> GetEnumerator()
        {
            return Projects.GetEnumerator();
        }

        public bool Remove(IProject project)
        {
            if (ActiveProject == project)
            {
                ActiveProject = null;
            }
            lock (this)
            {
                var numberOfProjects = Projects.Count;
                var found = -1;
                for (var i = 0; i < numberOfProjects; i++)
                {
                    if (Projects[i] == project)
                    {
                        found = i;
                        Projects.RemoveAt(i);
                        numberOfProjects--;
                        i--;
                    }
                }
                if (found >= 0)
                {
                    try
                    {
                        // only delete the project file, losing run data is too bad to risk
                        File.Delete(Path.Combine(Configuration.ProjectDirectory, project.Name, "Project.xml"));
                    }
                    catch
                    {
                        // we made our best attempt, just let it continue
                    }
                }
                ProjectRemoved?.Invoke(project, found);
                return found >= 0;
            }
        }

        public bool RenameProject(IProject project, string newName)
        {
            string error = null;
            return RenameProject(project, newName, ref error);
        }

        public bool SetDescription(IProject project, string newDescription, ref string error)
        {
            var ourProject = project as Project;
            if (ourProject == null)
            {
                return false;
            }
            ourProject.Description = newDescription;
            return ourProject.Save(ref error);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Projects.GetEnumerator();
        }

        public bool ValidateProjectName(string name)
        {
            if (!Project.ValidateProjectName(name))
            {
                return false;
            }
            foreach (var project in Projects)
            {
                if (project.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        public event Action<IProject> ProjectAdded;

        public event Action<IProject, int> ProjectRemoved;

        private bool ValidateProjectName(IProject project)
        {
            var projects = Projects;
            // make sure there are no projects with the same name
            for (var i = 0; i < projects.Count; i++)
            {
                // we need to ignore case because of the Windows directory code since
                // they do not distinguish between case due to FAT32's structure
                if (projects[i].Name.Equals(project.Name, StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }
            }
            // Make sure that there are no invalid characters for a project name
            return Project.ValidateProjectName(project.Name);
        }

        public bool RenameProject(IProject project, string newName, ref string error)
        {
            if (project == null)
            {
                throw new ArgumentNullException(nameof(project));
            }
            // make sure that the new project name is valid
            lock (this)
            {
                if (!Project.ValidateProjectName(newName))
                {
                    error = $"The project name {newName} was invalid!";
                    return false;
                }
                if (Projects.Any(p => p.Name.Equals(newName, StringComparison.InvariantCultureIgnoreCase)))
                {
                    error = $"The project name {newName} already exists!";
                    return false;
                }
                var oldName = project.Name;
                try
                {
                    Directory.Move(Path.Combine(Configuration.ProjectDirectory, oldName),
                        Path.Combine(Configuration.ProjectDirectory, newName));
                }
                catch (IOException e)
                {
                    error = e.Message;
                    return false;
                }
                project.Name = newName;
            }
            return true;
        }

        /// <summary>
        ///     Set the given project to be the active one
        /// </summary>
        /// <param name="project">The project to become the active one</param>
        public void SetActiveProject(IProject project) => ActiveProject = project;


        private void FindAndLoadProjects()
        {
            if (!Directory.Exists(Configuration.ProjectDirectory))
            {
                return;
            }
            var subDirectories = Directory.GetDirectories(Configuration.ProjectDirectory);
            ActiveProject = null;
            Projects.Clear();
            Parallel.For(0, subDirectories.Length, i =>
            {
                var files = Directory.GetFiles(subDirectories[i], "Project.xml", SearchOption.TopDirectoryOnly);
                if (files != null && files.Length > 0)
                {
                    try
                    {
                        var p = new Project(Path.GetFileName(subDirectories[i]), Configuration);
                        lock (this)
                        {
                            if (ValidateProjectName(p))
                            {
                                // If everything is good, add it to the list of projects
                                Projects.Add(p);
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            });
            lock (this)
            {
                ((List<IProject>) Projects).Sort(
                    delegate(IProject first, IProject second) { return first.Name.CompareTo(second.Name); });
            }
        }

        internal void Reload()
        {
            FindAndLoadProjects();
        }

        /// <summary>
        ///     This should be called if an project's clone gets saved, effectively removing the old project
        ///     from existence.
        /// </summary>
        /// <param name="baseProject">The project to be replaced</param>
        /// <param name="replaceWith">The project to replace it with</param>
        internal void ReplaceProjectFromClone(Project baseProject, Project replaceWith)
        {
            lock (this)
            {
                var index = Projects.IndexOf(baseProject);
                if (index >= 0)
                {
                    Projects[index] = replaceWith;
                }
            }
        }
    }
}