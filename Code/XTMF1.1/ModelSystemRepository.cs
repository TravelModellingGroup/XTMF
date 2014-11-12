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
        private IConfiguration Config;

        /// <summary>
        /// Create a new model system repository for the given configuration
        /// </summary>
        /// <param name="config">The configuration in which this model system repository is built</param>
        public ModelSystemRepository(IConfiguration config)
        {
            this.Config = config;
            this.ModelSystems = new List<IModelSystem>();
            this.LoadModelSystemsFromDisk();
        }

        /// <summary>
        /// This event will fire when a model system is added
        /// </summary>
        public event Action<IModelSystem> ModelSystemAdded;

        /// <summary>
        /// This event will fire when a model system has been removed
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Design", "CA1009:DeclareEventHandlersCorrectly" )]
        public event Action<IModelSystem, int> ModelSystemRemoved;

        /// <summary>
        /// The model systems included in this repository
        /// </summary>
        public IList<IModelSystem> ModelSystems
        {
            get;
            private set;
        }

        /// <summary>
        /// Add a new model system to the repository
        /// </summary>
        /// <param name="modelSystem"></param>
        public void Add(IModelSystem modelSystem)
        {
            if ( modelSystem != null )
            {
                lock ( this )
                {
                    this.ModelSystems.Add( modelSystem );
                    ( this.ModelSystems as List<IModelSystem> ).Sort( delegate(IModelSystem first, IModelSystem second)
                    {
                        return first.Name.CompareTo( second.Name );
                    } );
                }
                var msa = this.ModelSystemAdded;
                if ( msa != null )
                {
                    msa( modelSystem );
                }
            }
        }

        /// <summary>
        /// An enumeration of all of the contained model systems
        /// </summary>
        /// <returns></returns>
        public IEnumerator<IModelSystem> GetEnumerator()
        {
            return this.ModelSystems.GetEnumerator();
        }

        /// <summary>
        /// Provides removal for a model system
        /// </summary>
        /// <param name="modelSystem"></param>
        /// <returns></returns>
        public bool Remove(IModelSystem modelSystem)
        {
            if ( modelSystem != null )
            {
                int index;
                lock ( this )
                {
                    index = this.ModelSystems.IndexOf( modelSystem );
                    if ( !this.ModelSystems.Remove( modelSystem ) )
                    {
                        return false;
                    }
                }
                var msr = this.ModelSystemRemoved;
                if ( msr != null )
                {
                    msr( modelSystem, index );
                }
                // we don't need to be locked in order to delete it
                try
                {
                    File.Delete( Path.Combine( this.Config.ModelSystemDirectory, modelSystem.Name + ".xml" ) );
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
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.ModelSystems.GetEnumerator();
        }

        /// <summary>
        /// Load all of the model systems from the disk
        /// </summary>
        private void LoadModelSystemsFromDisk()
        {
            if ( !Directory.Exists( this.Config.ModelSystemDirectory ) ) return;
            string[] files = Directory.GetFiles( this.Config.ModelSystemDirectory );
            ConcurrentQueue<IModelSystem> temp = new ConcurrentQueue<IModelSystem>();
            Parallel.For( 0, files.Length, (int i) =>
            {
                // Load the ModelSystem structure from disk
                // After we have it, then we can just go and create a new model system from it
                try
                {
                    var ms = new ModelSystem( this.Config, Path.GetFileNameWithoutExtension( files[i] ) );
                    if ( ms != null )
                    {
                        temp.Enqueue( ms );
                    }
                }
                catch
                {
                }
            } );
            IModelSystem dequeueMe;
            while ( temp.TryDequeue( out dequeueMe ) )
            {
                this.ModelSystems.Add( dequeueMe );
            }
            ( this.ModelSystems as List<IModelSystem> ).Sort( delegate(IModelSystem first, IModelSystem second)
            {
                return first.Name.CompareTo( second.Name );
            } );

        }
    }
}