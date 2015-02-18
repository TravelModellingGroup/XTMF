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
using System.Linq;
using System.Text;
using System.IO;

namespace XTMF
{
    public class ModelSystemController
    {
        private XTMFRuntime Runtime;

        public ModelSystemController(XTMFRuntime runtime)
        {
            Runtime = runtime;
            Repository = Runtime.Configuration.ModelSystemRepository as ModelSystemRepository;
        }
        private ModelSystemRepository Repository { get; set; }

        /// <summary>
        /// The lock for editing what is inside of the repository.  This should be grabbed before getting the editing lock.
        /// </summary>
        private object RepositoryLock = new object();
        /// <summary>
        /// The current editing sessions
        /// </summary>
        private List<ModelSystemEditingSession> EditingSessions = new List<ModelSystemEditingSession>();
        /// <summary>
        /// The number of references to each model system editing session
        /// </summary>
        private List<int> References = new List<int>();

        /// <summary>
        /// The lock to get before using the editing sessions
        /// </summary>
        private object EditingLock = new object();

        /// <summary>
        /// Create a new model system
        /// </summary>
        /// <param name="name">The name of the model system, must not be empty</param>
        /// <returns>The model system, null if it failed.</returns>
        public ModelSystem CreateModelSystem(string name)
        {
            if(String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The name of a model system can not be null, empty or just whitespace!", "name");
            }
            if(!ValidateName(name))
            {
                throw new ArgumentException("The given name '" + name + "' was an invalid name for a model system!.", "name");
            }
            lock (RepositoryLock)
            {
                // if another model system with the same name already exists
                if(this.Repository.ModelSystems.Any((other) => other.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    //then we can't make this new model system
                    return null;
                }
                // if no one else has the same name we can go and make the model system and add it to the repository
                var ms = new ModelSystem(Runtime.Configuration, name);
                this.Repository.Add(ms);
                return ms;
            }
        }

        /// <summary>
        /// Import a model system into XTMF
        /// </summary>
        /// <param name="fileLocation">The location of the file to import</param>
        /// <param name="overwrite">Should we overwrite a model system if it already exists?</param>
        /// <param name="error">A message in case of failure</param>
        /// <returns>True if it was added successfully, false otherwise</returns>
        public bool ImportModelSystem(string fileLocation, bool overwrite, ref string error)
        {
            if(String.IsNullOrWhiteSpace(fileLocation))
            {
                error = "The file location was not set!";
                return false;
            }
            lock (RepositoryLock)
            {
                FileInfo file = new FileInfo(fileLocation);
                var msName = Path.GetFileNameWithoutExtension(file.Name);
                if(!file.Exists)
                {
                    error = "The file does not exist!";
                    return false;
                }
                var oldModelSystem =
                    this.Runtime.Configuration.ModelSystemRepository.ModelSystems
                        .FirstOrDefault((ms) => ms.Name.Equals(msName, StringComparison.InvariantCultureIgnoreCase));
                if(oldModelSystem != null & !overwrite)
                {
                    error = "A model system with that name already exists!";
                    return false;
                }
                try
                {
                    if(oldModelSystem != null)
                    {
                        this.Delete((ModelSystem)oldModelSystem);
                    }
                    if((var newInfo = file.CopyTo(
                        Path.Combine(Runtime.Configuration.ModelSystemDirectory, msName + ".xml")
                        )) != null)
                    {
                        if(!newInfo.Exists)
                        {
                            error = "We were unable to copy the file.";
                            return false;
                        }
                        this.Runtime.Configuration.ModelSystemRepository.Add(new ModelSystem(Runtime.Configuration, msName));
                        var newMS = this.Load(msName);
                        if(newMS == null)
                        {
                            error = "We were unable to load the model system after copying it into XTMF.";
                            return false;
                        }
                        return newMS != null;
                    }
                }
                catch (IOException)
                {
                    error = "We were unable to copy the file.";
                    return false;
                }
                return false;
            }
        }


        /// <summary>
        /// Loads a model system given its name.  It will create a new blank model system if it doesn't exist.
        /// </summary>
        /// <param name="name">The name of the model system to get.</param>
        /// <returns>The model system with the given name.</returns>
        public ModelSystem LoadOrCreate(string name)
        {
            VetName(name);
            lock (RepositoryLock)
            {
                // if another model system with the same name already exists
                if((var alreadyMade = this.Repository.ModelSystems.FirstOrDefault((other) => other.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) != null)
                {
                    //then we can't make this new model system
                    return alreadyMade as ModelSystem;
                }
                // if no one else has the same name we can go and make the model system and add it to the repository
                var ms = new ModelSystem(Runtime.Configuration, name);
                this.Repository.Add(ms);
                return ms;
            }
        }

        /// <summary>
        /// Load a model system structure from file.
        /// </summary>
        /// <param name="runFile">The file to load from.</param>
        /// <returns>The model system structure located at that file.</returns>
        public ModelSystemStructure LoadFromRunFile(string runFile)
        {
            return ModelSystemStructure.Load(runFile, Runtime.Configuration) as ModelSystemStructure;
        }

        /// <summary>
        /// Check the name parameter
        /// </summary>
        /// <param name="name"></param>
        private static void VetName(string name)
        {
            if(string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The name of a model system can not be null, empty or just whitespace!", "name");
            }

            if(!ValidateName(name))
            {
                throw new ArgumentException("The given name '" + name + "' was an invalid name for a model system!", "name");
            }
        }


        /// <summary>
        /// Validate a name to make sure it is a valid possible model system
        /// </summary>
        /// <param name="name">The potential name of the model system to check</param>
        /// <returns>True if the name is alright</returns>
        private static bool ValidateName(string name)
        {
            return Project.ValidateProjectName(name);
        }


        /// <summary>
        /// Loads a model system given its name.  If it doesn't exist a null will be returned.
        /// </summary>
        /// <param name="name">The name of the model system to get.</param>
        /// <returns>The model system with the given name, or a null if it doesn't exist.</returns>
        public ModelSystem Load(string name)
        {
            if(String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The name of a model system can not be null, empty or just whitespace!", "name");
            }
            lock (RepositoryLock)
            {
                // if another model system with the same name already exists
                if((var alreadyMade = this.Repository.ModelSystems.FirstOrDefault((other) => other.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) != null)
                {
                    //then we can't make this new model system
                    return alreadyMade as ModelSystem;
                }
            }
            return null;
        }

        /// <summary>
        /// Delete a model system given its name
        /// </summary>
        /// <param name="name">The name of the model system to delete</param>
        /// <returns>If the delete succeeded or not.</returns>
        public bool Delete(string name)
        {
            if(String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The name of a model system can not be null, empty or just whitespace!", "name");
            }
            lock (RepositoryLock)
            {
                // if another model system with the same name already exists
                if((var alreadyMade = this.Repository.ModelSystems.FirstOrDefault((other) => other.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) != null)
                {
                    lock (EditingLock)
                    {
                        if(this.EditingSessions.Any((session) => session.IsEditing(alreadyMade as ModelSystem)))
                        {
                            // we can't delete a model system that is currently being edited
                            return false;
                        }
                    }
                    //then we can't make this new model system
                    this.Repository.Remove(alreadyMade);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelSystem"></param>
        /// <returns></returns>
        public bool Delete(ModelSystem modelSystem)
        {
            lock (RepositoryLock)
            {
                lock (EditingLock)
                {
                    // as long as it isn't being edited
                    if(this.EditingSessions.Any((session) => session.IsEditing(modelSystem as ModelSystem)))
                    {
                        // we can't delete a model system that is currently being edited
                        return false;
                    }
                }
                // just use the repositories model system remove
                return this.Repository.Remove(modelSystem);
            }
        }


        /// <summary>
        /// Start a new editing session for the given model system
        /// </summary>
        public ModelSystemEditingSession EditModelSystem(ModelSystem modelSystem)
        {
            if(modelSystem == null)
            {
                throw new ArgumentNullException("modelSystem");
            }
            lock (EditingLock)
            {
                for(int i = 0; i < EditingSessions.Count; i++)
                {
                    if(EditingSessions[i].IsEditing(modelSystem))
                    {
                        References[i]++;
                        return EditingSessions[i];
                    }
                }
                var newSession = new ModelSystemEditingSession(Runtime, modelSystem);
                References.Add(1);
                EditingSessions.Add(newSession);
                return newSession;
            }
        }

        /// <summary>
        /// Release the given editing session
        /// </summary>
        /// <param name="modelSystemEditingSession">The session to release</param>
        internal void ReleaseEditingSession(ModelSystemEditingSession modelSystemEditingSession)
        {
            if(modelSystemEditingSession == null)
            {
                throw new ArgumentNullException("modelSystemEditingSession");
            }
            bool terminate = false;
            lock (EditingLock)
            {
                for(int i = 0; i < EditingSessions.Count; i++)
                {
                    if(EditingSessions[i] == modelSystemEditingSession)
                    {
                        References[i]--;
                        // if nothing else is looking at this terminate the session
                        if(References[i] <= 0)
                        {
                            References.RemoveAt(i);
                            EditingSessions.RemoveAt(i);
                            terminate = true;
                        }
                        break;
                    }
                }
            }
            // if this was the last reference to the session terminate it
            if(terminate)
            {
                modelSystemEditingSession.SessionTerminated();
            }
        }

        /// <summary>
        /// Get a list of currently active model systems
        /// </summary>
        /// <returns></returns>
        public List<IModelSystem> GetModelSystems()
        {
            return new List<IModelSystem>(this.Repository.ModelSystems);
        }
    }
}