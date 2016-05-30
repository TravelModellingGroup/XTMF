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
using System.Threading.Tasks;

namespace XTMF
{
    /// <summary>
    /// This class provides access to all of the
    /// projects currently being managed by this
    /// installation of XTMF
    /// </summary>
    public class ProjectRepository : IProjectRepository
    {
        /// <summary>
        /// Initiate the project repository
        /// </summary>
        public ProjectRepository(IConfiguration configuration)
        {
            this.Projects = new List<IProject>();
            this.Configuration = configuration;
            FindAndLoadProjects();
        }

        public event Action<IProject> ProjectAdded;

        public event Action<IProject, int> ProjectRemoved;

        /// <summary>
        /// The project that this repository is currently working with
        /// in order to run XTMF
        /// </summary>
        public IProject ActiveProject { get; private set; }

        public IConfiguration Configuration { get; set; }

        /// <summary>
        /// A List of projects that are currently being managed
        /// </summary>
        public IList<IProject> Projects { get; private set; }

        public bool AddProject(IProject project)
        {
            lock (this)
            {
                if (!this.ValidateProjectName(project)) return false;
                // If everything is good, add it to the list of projects
                this.Projects.Add(project);
                (this.Projects as List<IProject>).Sort(delegate (IProject first, IProject second)
             {
                 return first.Name.CompareTo(second.Name);
             });
            }
            var pad = this.ProjectAdded;
            if (pad != null)
            {
                pad(project);
            }
            return true;
        }

        private bool ValidateProjectName(IProject project)
        {
            var projects = this.Projects;
            // make sure there are no projects with the same name
            for (int i = 0; i < projects.Count; i++)
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

        public IEnumerator<IProject> GetEnumerator()
        {
            return this.Projects.GetEnumerator();
        }

        public bool Remove(IProject project)
        {
            if (this.ActiveProject == project)
            {
                this.ActiveProject = null;
            }
            lock (this)
            {
                var numberOfProjects = this.Projects.Count;
                int found = -1;
                for (int i = 0; i < numberOfProjects; i++)
                {
                    if (this.Projects[i] == project)
                    {
                        found = i;
                        this.Projects.RemoveAt(i);
                        numberOfProjects--;
                        i--;
                    }
                }
                if (found >= 0)
                {
                    try
                    {
                        // only delete the project file, losing run data is too bad to risk
                        File.Delete(Path.Combine(this.Configuration.ProjectDirectory, project.Name, "Project.xml"));
                    }
                    catch
                    {
                        // we made our best attempt, just let it continue
                    }
                }
                var prd = this.ProjectRemoved;
                if (prd != null)
                {
                    prd(project, found);
                }
                return found >= 0;
            }
        }

        public bool RenameProject(IProject project, string newName)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }
            // make sure that the new project name is valid
            lock (this)
            {
                if (!Project.ValidateProjectName(newName) || this.Projects.Any((p) => (p.Name.Equals(newName, StringComparison.InvariantCultureIgnoreCase))))
                {
                    return false;
                }

                var oldName = project.Name;
                try
                {
                    Directory.Move(Path.Combine(this.Configuration.ProjectDirectory, oldName),
                        Path.Combine(this.Configuration.ProjectDirectory, newName));
                }
                catch
                {
                    return false;
                }
                project.Name = newName;
            }
            return true;
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

        /// <summary>
        /// Set the given project to be the active one
        /// </summary>
        /// <param name="project">The project to become the active one</param>
        public void SetActiveProject(IProject project)
        {
            this.ActiveProject = project;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.Projects.GetEnumerator();
        }

        public bool ValidateProjectName(string name)
        {
            if (!Project.ValidateProjectName(name)) return false;
            foreach (var project in this.Projects)
            {
                if (project.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    return false;
            }
            return true;
        }

        private void FindAndLoadProjects()
        {
            if (!Directory.Exists(this.Configuration.ProjectDirectory)) return;
            string[] subDirectories = Directory.GetDirectories(this.Configuration.ProjectDirectory);
            Parallel.For(0, subDirectories.Length, (int i) =>
           {
               var files = Directory.GetFiles(subDirectories[i], "Project.xml", SearchOption.TopDirectoryOnly);
               if (files != null && files.Length > 0)
               {
                   try
                   {
                       Project p = new Project(Path.GetFileName(subDirectories[i]), this.Configuration);
                       lock (this)
                       {
                           if (this.ValidateProjectName(p))
                           {
                                // If everything is good, add it to the list of projects
                                this.Projects.Add(p);
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
                (this.Projects as List<IProject>).Sort(delegate (IProject first, IProject second)
             {
                 return first.Name.CompareTo(second.Name);
             });
            }
        }

        /// <summary>
        /// This should be called if an project's clone gets saved, effectively removing the old project
        /// from existence.
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