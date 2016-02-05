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
using TMG.Input;
using TMG;
using Datastructure;
using Tasha.Common;
namespace Tasha.DataExtraction
{

    public class SaveAsCSVMatrix : ISelfContainedModule
    {
        [SubModelInformation(Required = true, Description = "Zone based ODMatrix of the zone system.")]
        public IResource ODMatrix;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            if(!ODMatrix.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the ODMatrix resource is not of type SparseTwinIndex<float>!";
                return false;
            }
            return true;
        }

        [SubModelInformation(Required = true, Description = "The location to save the matrix (CSV).")]
        public FileLocation SaveLocation;

        [RunParameter("Third Normalized Form", false, "Save in third normalized form.")]
        public bool ThirdNormalized;

        public void Start()
        {
            var matrix = ODMatrix.AcquireResource<SparseTwinIndex<float>>();
            if(ThirdNormalized)
            {
                TMG.Functions.SaveData.SaveMatrixThirdNormalized(matrix, SaveLocation);
            }
            else
            {
                TMG.Functions.SaveData.SaveMatrix(matrix, SaveLocation);
            }
        }
    }

}
