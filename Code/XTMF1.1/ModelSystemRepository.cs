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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace XTMF
{
    /// <summary>
    /// The XTMF implementation of the IModelSystemRepository
    /// </summary>
    public sealed class ModelSystemRepository : IModelSystemRepository
    {
        /// <summary>
        /// The configuration that this model system repository is based upon
        /// </summary>
        private IConfiguration _Config;

        /// <summary>
        /// Create a new model system repository for the given configuration
        /// </summary>
        /// <param name="config">The configuration in which this model system repository is built</param>
        public ModelSystemRepository(IConfiguration config)
        {
            _Config = config;
            ModelSystems = new List<IModelSystem>();
            LoadModelSystemsFromDisk();
        }

        /// <summary>
        /// This event will fire when a model system is added
        /// </summary>
        public event Action<IModelSystem> ModelSystemAdded;

        /// <summary>
        /// This event will fire when a model system has been removed
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly")]
        public event Action<IModelSystem, int> ModelSystemRemoved;

        /// <summary>
        /// The model systems included in this repository
        /// </summary>
        public IList<IModelSystem> ModelSystems { get; private set; }


        /// <summary>
        /// Add a new model system to the repository
        /// </summary>
        /// <param name="modelSystem"></param>
        public void Add(IModelSystem modelSystem)
        {
            if (modelSystem != null)
            {
                lock (this)
                {
                    ModelSystems.Add(modelSystem);
                    (ModelSystems as List<IModelSystem>).Sort(delegate (IModelSystem first, IModelSystem second)
                 {
                     return first.Name.CompareTo(second.Name);
                 });
                }
                ModelSystemAdded?.Invoke(modelSystem);
            }
        }

        /// <summary>
        /// An enumeration of all of the contained model systems
        /// </summary>
        /// <returns></returns>
        public IEnumerator<IModelSystem> GetEnumerator() => ModelSystems.GetEnumerator();

        /// <summary>
        /// Renames the model system if possible
        /// </summary>
        /// <param name="modelSystem">The model system to rename</param>
        /// <param name="newName">The name to save it as</param>
        /// <param name="error">An error message if the operation fails</param>
        /// <returns>True if the operation succeeds, false otherwise with a message.</returns>
        public bool Rename(ModelSystem modelSystem, string newName, ref string error)
        {
            var newNameLower = newName.ToLowerInvariant();
            if (ModelSystems.Any(ms => ms.Name.ToLowerInvariant() == newNameLower))
            {
                error = "There was already a model system with the name " + newName + "!";
                return false;
            }
            var oldName = modelSystem.Name;
            modelSystem.Name = newName;
            var success = modelSystem.Save(ref error);
            // if the rename worked we need to cleanup the old save file
            if (success)
            {
                try
                {
                    File.Delete(Path.Combine(_Config.ModelSystemDirectory, oldName + ".xml"));
                }
                catch (IOException)
                {
                    // if we were unable to delete the file that means it was already removed, so there is nothing to do
                }
            }
            return success;
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
            var newNameLower = newName.ToLowerInvariant();
            if (ModelSystems.Any(ms => ms.Name.ToLowerInvariant() == newNameLower))
            {
                error = "There was already a model system with the name " + newName + "!";
                return false;
            }
            ModelSystem clone = new ModelSystem(_Config, newName)
            {
                Description = modelSystem.Description,
                LinkedParameters = modelSystem.LinkedParameters,
                Name = newName,
                ModelSystemStructure = modelSystem.ModelSystemStructure
            };
            var saved = clone.Save(ref error);
            // unload so there are no references to the current model system
            clone.Unload();
            Add(clone);
            return saved;
        }

        /// <summary>
        /// Provides removal for a model system
        /// </summary>
        /// <param name="modelSystem"></param>
        /// <returns></returns>
        public bool Remove(IModelSystem modelSystem)
        {
            if (modelSystem != null)
            {
                int index;
                lock (this)
                {
                    index = ModelSystems.IndexOf(modelSystem);
                    if (!ModelSystems.Remove(modelSystem))
                    {
                        return false;
                    }
                }
                ModelSystemRemoved?.Invoke(modelSystem, index);
                // we don't need to be locked in order to delete it
                try
                {
                    File.Delete(Path.Combine(_Config.ModelSystemDirectory, modelSystem.Name + ".xml"));
                }
                catch
                {
                    // If the file no longer exists, or couldn't have it doesn't really matter
                }
                return true;
            }
            // it is not valid to remove a model system that does not exist!
            return false;
        }

        /// <summary>
        /// An enumeration of all of the contained model systems
        /// </summary>
        /// <returns></returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => ModelSystems.GetEnumerator();

        /// <summary>
        /// Load all of the model systems from the disk
        /// </summary>
        private void LoadModelSystemsFromDisk()
        {
            if (!Directory.Exists(_Config.ModelSystemDirectory)) return;
            string[] files = Directory.GetFiles(_Config.ModelSystemDirectory);
            ConcurrentQueue<IModelSystem> temp = new ConcurrentQueue<IModelSystem>();
            Parallel.For(0, files.Length, (int i) =>
           {
               // Load the ModelSystem structure from disk
               // After we have it, then we can just go and create a new model system from it
               try
               {
                   var ms = new ModelSystem(_Config, Path.GetFileNameWithoutExtension(files[i]));
                   if (ms != null)
                   {
                       temp.Enqueue(ms);
                   }
               }
               catch
               {
               }
           });
            ModelSystems.Clear();
            while (temp.TryDequeue(out IModelSystem dequeueMe))
            {
                ModelSystems.Add(dequeueMe);
            }
            (ModelSystems as List<IModelSystem>).Sort(delegate (IModelSystem first, IModelSystem second)
         {
             return first.Name.CompareTo(second.Name);
         });
        }

        internal void Reload()
        {
            LoadModelSystemsFromDisk();
        }
    }
}