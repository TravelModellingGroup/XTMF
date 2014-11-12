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
using TMG;
using XTMF;
using Datastructure;
using TMG.Input;
namespace Tasha.Data
{
    [ModuleInformation(Description=
        @"This module is designed to provide access to resources where reading from a file was expected. 
It can read from a resource of type SparseArray<float> where it will assume the data is for the origin and only load that. 
It can also read in SparseTwinIndex<float> where the O,D values will be infered from the data source.")]
    public class ReadOriginDestinationDataFromResource : IReadODData<float>
    {
        [SubModelInformation( Required = true, Description = "The resource to read from." )]
        public IResource DataResource;

        private bool OriginOnly;

        public IEnumerable<ODData<float>> Read()
        {
            if ( this.OriginOnly )
            {
                ODData<float> temp;
                var data = this.DataResource.AquireResource<SparseArray<float>>();
                temp.D = 0;
                var validIndexes = data.ValidIndexArray();
                var flatData = data.GetFlatData();
                for ( int i = 0; i < validIndexes.Length; i++ )
                {
                    temp.O = validIndexes[i];
                    temp.Data = flatData[i];
                    yield return temp;
                }
            }
            else
            {
                ODData<float> temp;
                var data = this.DataResource.AquireResource<SparseTwinIndex<float>>();
                var flatData = data.GetFlatData();
                for ( int i = 0; i < flatData.Length; i++ )
                {
                    temp.O = data.GetSparseIndex( i );
                    var row = flatData[i];
                    for ( int j = 0; j < row.Length; j++ )
                    {
                        temp.D = data.GetSparseIndex( i, j );
                        temp.Data = flatData[i][j];
                        yield return temp;
                    }
                }
            }
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
            if ( this.DataResource.CheckResourceType<SparseArray<float>>() )
            {
                this.OriginOnly = true;
            }
            else if ( this.DataResource.CheckResourceType<SparseTwinIndex<float>>() )
            {
                this.OriginOnly = false;
            }
            else
            {
                error = "In '" + this.Name + "' the DataResource needs to be either a SparseArray<float> for origin "
                    + "only information or SparseTwinIndex<float> for OD data!";
                return false;
            }
            return true;
        }
    }
}
