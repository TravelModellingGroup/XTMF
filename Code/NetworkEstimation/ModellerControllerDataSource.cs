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
using XTMF;
using TMG.Emme;
using TMG.Input;
namespace TMG.NetworkEstimation
{
    [ModuleInformation(
        Description=@"This data source provides access to emme modeller.")]
    public class ModellerControllerDataSource : IDataSource<ModellerController>, IDisposable
    {
        [SubModelInformation( Required = true, Description = "The location of the Emme project file." )]
        public FileLocation ProjectFolder;

        private ModellerController Data;

        public ModellerController GiveData()
        {
            return this.Data;
        }

        public bool Loaded
        {
            get { return this.Data != null; }
        }

        public void LoadData()
        {
            if ( this.Data == null )
            {
                lock ( this )
                {
                    if ( this.Data == null )
                    {
                        GC.ReRegisterForFinalize( this );
                        this.Data = new ModellerController( this.ProjectFolder.GetFilePath(), false );
                    }
                }
            }
        }

        public void UnloadData()
        {
            this.Dispose();
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        ~ModellerControllerDataSource()
        {
            this.Dispose( true );
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( true );
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Data != null )
            {
                this.Data.Dispose();
                this.Data = null;
            }
        }
    }
}
