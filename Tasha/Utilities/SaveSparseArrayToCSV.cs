/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;
using Datastructure;
using TMG.Input;

namespace Tasha.Utilities
{

    public class SaveSparseArrayToCSV : ISelfContainedModule
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = true, Description = "The data to save.")]
        public IResource Data;

        [SubModelInformation(Required = true, Description = "The location to save to.")]
        public FileLocation OutputTo;

        public bool RuntimeValidation(ref string error)
        {
            if(!Data.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the Data resource was not of type SparseArray<float>!";
            }
            return true;
        }

        public void Start()
        {
            SparseArray<float> data;
            data = Data.AcquireResource<SparseArray<float>>();
            TMG.Functions.SaveData.SaveVector(data, OutputTo.GetFilePath());
        }
    }

}
