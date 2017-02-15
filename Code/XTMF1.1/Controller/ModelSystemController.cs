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
using System.Text;
using System.IO;

namespace XTMF
{
    public class ModelSystemController
    {
        private readonly XTMFRuntime Runtime;

        public ModelSystemController(XTMFRuntime runtime)
        {
            Runtime = runtime;
            Repository = Runtime.Configuration.ModelSystemRepository as ModelSystemRepository;
        }
        private ModelSystemRepository Repository { get; }

        /// <summary>
        /// The lock for editing what is inside of the repository.  This should be grabbed before getting the editing lock.
        /// </summary>
        private readonly object RepositoryLock = new object();
        /// <summary>
        /// The current editing sessions
        /// </summary>
        private readonly List<ModelSystemEditingSession> EditingSessions = new List<ModelSystemEditingSession>();
        /// <summary>
        /// The number of references to each model system editing session
        /// </summary>
        private readonly List<int> References = new List<int>();

        /// <summary>
        /// The lock to get before using the editing sessions
        /// </summary>
        private readonly object EditingLock = new object();

        /// <summary>
        /// Create a new model system
        /// </summary>
        /// <param name="name">The name of the model system, must not be empty</param>
        /// <returns>The model system, null if it failed.</returns>
        public ModelSystem CreateModelSystem(string name)
        {
            if(String.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("The name of a model system can not be null, empty or just whitespace!", nameof(name));
            }
            if(!ValidateName(name))
            {
                throw new ArgumentException("The given name '" + name + "' was an invalid name for a model system!.", nameof(name));
            }
            lock (RepositoryLock)
            {
                // if another model system with the same name already exists
                if(Repository.ModelSystems.Any((other) => other.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    //then we can't make this new model system
                    return null;
                }
                // if no one else has the same name we can go and make the model system and add it to the repository
                var ms = new ModelSystem(Runtime.Configuration, name);
                Repository.Add(ms);
                return ms;
            }
        }

        /// <summary>
        /// Load a model system that has been saved into a string
        /// </summary>
        /// <param name="modelSystemAsText">The model system stored as a string.</param>
        /// <param name="ms">The model system loaded</param>
        /// <param name="error">A description of the error if the operation fails.</param>
        /// <returns>True if the model system was loaded, false otherwise with a description of the failure in error.</returns>
        public bool LoadDetachedModelSystem(string modelSystemAsText, out ModelSystem ms, ref string error)
        {
            using (MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(modelSystemAsText)))
            {
                stream.Seek(0, SeekOrigin.Begin);
                ms = ModelSystem.LoadDetachedModelSystem(stream, Runtime.Configuration, ref error);
                return ms != null;
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
                    Runtime.Configuration.ModelSystemRepository.ModelSystems
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
                        if(!Delete((ModelSystem)oldModelSystem, ref error))
                        {
                            return false;
                        }
                    }
                    FileInfo newInfo;
                    if((newInfo = file.CopyTo(
                        Path.Combine(Runtime.Configuration.ModelSystemDirectory, msName + ".xml")
                        )) != null)
                    {
                        if(!newInfo.Exists)
                        {
                            error = "We were unable to copy the file.";
                            return false;
                        }
                        Runtime.Configuration.ModelSystemRepository.Add(new ModelSystem(Runtime.Configuration, msName));
                        var newMS = Load(msName);
                        if(newMS == null)
                        {
                            error = "We were unable to load the model system after copying it into XTMF.";
                            return false;
                        }
                        return true;
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
                IModelSystem alreadyMade;
                if((alreadyMade = Repository.ModelSystems.FirstOrDefault((other) => other.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) != null)
                {
                    //then we can't make this new model system
                    return alreadyMade as ModelSystem;
                }
                // if no one else has the same name we can go and make the model system and add it to the repository
                var ms = new ModelSystem(Runtime.Configuration, name);
                Repository.Add(ms);
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
                throw new ArgumentException("The name of a model system can not be null, empty or just whitespace!", nameof(name));
            }

            if(!ValidateName(name))
            {
                throw new ArgumentException("The given name '" + name + "' was an invalid name for a model system!", nameof(name));
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

        public bool ValidateModelSystemName(string newName, ref string error)
        {
            var ret = ValidateName(newName);
            if(!ret)
            {
                error = "A model system may not contain any characters not allowed in file names!";
            }
            return ret;
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
                throw new ArgumentException("The name of a model system can not be null, empty or just whitespace!", nameof(name));
            }
            lock (RepositoryLock)
            {
                IModelSystem alreadyMade;
                // if another model system with the same name already exists
                if((alreadyMade = Repository.ModelSystems.FirstOrDefault((other) => other.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) != null)
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
                throw new ArgumentException("The name of a model system can not be null, empty or just whitespace!", nameof(name));
            }
            lock (RepositoryLock)
            {
                // if another model system with the same name already exists
                IModelSystem alreadyMade;
                if((alreadyMade = Repository.ModelSystems.FirstOrDefault((other) => other.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) != null)
                {
                    lock (EditingLock)
                    {
                        if(EditingSessions.Any((session) => session.IsEditing(alreadyMade as ModelSystem)))
                        {
                            // we can't delete a model system that is currently being edited
                            return false;
                        }
                    }
                    //then we can't make this new model system
                    Repository.Remove(alreadyMade);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Makes a copy of the model system with the given new name
        /// </summary>
        /// <param name="modelSystem">The model system to create a clone of</param>
        /// <param name="newName">The name to give the model system</param>
        /// <param name="error">An error message if the operation fails</param>
        /// <returns>True if successful, false otherwise with an error message.</returns>
        public bool CloneModelSystem(ModelSystem modelSystem, string newName, ref string error)
        {
            if (!Project.ValidateProjectName(newName))
            {
                error = "The new name contained characters that are not valid!";
                return false;
            }
            lock(RepositoryLock)
            {
                lock(EditingLock)
                {
                    // as long as it isn't being edited
                    if (EditingSessions.Any((session) => session.IsEditing(modelSystem as ModelSystem)))
                    {
                        // we can't delete a model system that is currently being edited
                        error = "A model system can not be cloned while being edited!";
                        return false;
                    }
                    return Repository.CloneModelSystem(modelSystem, newName, ref error);
                }
            }
        }

        /// <summary>
        /// Renames the model system if possible
        /// </summary>
        /// <param name="modelSystem">The model system to rename</param>
        /// <param name="newName">The name to save it as</param>
        /// <param name="error">An error message if the operation fails</param>
        /// <returns>True if the operation succeeds, false otherwise with a message.</returns>
        public bool Rename(ModelSystem modelSystem, string newName, ref string error)
        {
            if(!Project.ValidateProjectName(newName))
            {
                error = "The new name contained characters that are not valid!";
                return false;
            }
            lock(RepositoryLock)
            {
                lock (EditingLock)
                {
                    // as long as it isn't being edited
                    if (EditingSessions.Any((session) => session.IsEditing(modelSystem as ModelSystem)))
                    {
                        // we can't delete a model system that is currently being edited
                        error = "A model system can not be renamed while being edited!";
                        return false;
                    }
                }
                // just use the repositories model system remove
                return Repository.Rename(modelSystem, newName, ref error);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelSystem"></param>
        /// <returns></returns>
        public bool Delete(ModelSystem modelSystem, ref string error)
        {
            lock (RepositoryLock)
            {
                lock (EditingLock)
                {
                    // as long as it isn't being edited
                    if(EditingSessions.Any((session) => session.IsEditing(modelSystem as ModelSystem)))
                    {
                        error = "We can't delete a model system that is currently being edited";
                        return false;
                    }
                }
                // just use the repositories model system remove
                return Repository.Remove(modelSystem);
            }
        }

        /// <summary>
        /// Export the model system to the given file path
        /// </summary>
        /// <param name="modelSystem">The model system to export</param>
        /// <param name="filePath">The path to save to.</param>
        /// <param name="error">A description of the error if one occurs.</param>
        /// <returns>True if successful, false otherwise.  An error will be reported if it fails.</returns>
        public bool ExportModelSystem(IModelSystem modelSystem, string filePath, ref string error)
        {
            try
            {
                return modelSystem.Save(filePath, ref error);
            }
            catch(IOException e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>
        /// Export a model system to a string
        /// </summary>
        /// <param name="ms">The model system to export</param>
        /// <param name="modelSystemAsString">The string to save the model system into</param>
        /// <param name="error">A description of the error if one occurs</param>
        /// <returns>True if the export was successful, false with description otherwise</returns>
        public bool ExportModelSystemAsString(ModelSystem ms, out string modelSystemAsString, ref string error)
        {
            using (var stream = new MemoryStream())
            {
                if (!ms.Save(stream, ref error))
                {
                    modelSystemAsString = null;
                    return false;
                }
                var buffer = stream.ToArray();
                modelSystemAsString = new string(Encoding.Unicode.GetChars(buffer, 0, buffer.Length));
                return true;
            }
        }


        /// <summary>
        /// Start a new editing session for the given model system
        /// </summary>
        public ModelSystemEditingSession EditModelSystem(ModelSystem modelSystem)
        {
            if(modelSystem == null)
            {
                throw new ArgumentNullException(nameof(modelSystem));
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
        /// 
        /// </summary>
        /// <param name="modelSystemEditingSession"></param>
        /// <returns></returns>
        internal bool WillCloseTerminate(ModelSystemEditingSession modelSystemEditingSession)
        {
            if (modelSystemEditingSession == null)
            {
                throw new ArgumentNullException(nameof(modelSystemEditingSession));
            }
            bool terminate = false;
            lock (EditingLock)
            {
                for (int i = 0; i < EditingSessions.Count; i++)
                {
                    if (EditingSessions[i] == modelSystemEditingSession)
                    {
                        if (References[i] <= 1)
                        {
                            terminate = true;
                        }
                        break;
                    }
                }
            }
            return terminate;
        }

        /// <summary>
        /// Release the given editing session
        /// </summary>
        /// <param name="modelSystemEditingSession">The session to release</param>
        internal void ReleaseEditingSession(ModelSystemEditingSession modelSystemEditingSession)
        {
            if(modelSystemEditingSession == null)
            {
                throw new ArgumentNullException(nameof(modelSystemEditingSession));
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
            return new List<IModelSystem>(Repository.ModelSystems);
        }
    }
}