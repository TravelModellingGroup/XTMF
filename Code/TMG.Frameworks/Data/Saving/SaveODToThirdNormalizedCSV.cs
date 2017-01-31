/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Input;
using TMG.Functions;
using Datastructure;

namespace TMG.Frameworks.Data.Saving
{
    [ModuleInformation(
        Description = "This module is designed to save OD data into a csv file with the headers (Origin,Destination,Data)."
        )]
    // ReSharper disable once InconsistentNaming
    public class SaveODToThirdNormalizedCSV : ISelfContainedModule
    {
        [SubModelInformation(Required = false, Description = "The OD Data to save, this or the Raw Data Source need to be filled out.")]
        public IResource ResourceToSave;

        [SubModelInformation(Required = false, Description = "The OD Data to save, this or the Resource To Save need to be filled out.")]
        public IDataSource<SparseTwinIndex<float>> RawDataSourceToSave;

        [SubModelInformation(Required = true, Description = "The location to save the file to.")]
        public FileLocation SaveTo;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            if(!ModuleHelper.EnsureExactlyOneAndOfSameType(this, RawDataSourceToSave, ResourceToSave, ref error))
            {
                return false;
            }
            return true;
        }

        public void Start()
        {
            SaveData.SaveMatrixThirdNormalized(ModuleHelper.GetDataFromDatasourceOrResource(RawDataSourceToSave, ResourceToSave, RawDataSourceToSave != null), SaveTo);
        }
    }

}