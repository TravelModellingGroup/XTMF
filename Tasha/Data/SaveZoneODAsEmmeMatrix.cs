/*
    Copyright 2014-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Emme;
using XTMF;
using Datastructure;
using TMG;
using TMG.Input;
using TMG.Functions;

namespace Tasha.Data
{
    [ModuleInformation(Description = "This module is designed to save a matrix data source to file.")]
    public class SaveZoneODAsEmmeMatrix : ISelfContainedModule, IEmmeTool
    {

        [RootModule]
        public ITravelDemandModel Root;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool Execute(Controller controller)
        {
            Start();
            return true;
        }

        [SubModelInformation(Required = false, Description = "The matrix resource to save.")]
        public IResource MatrixToSave;

        [SubModelInformation(Required = false, Description = "Optionally a raw data source to use.")]
        public IDataSource<SparseTwinIndex<float>> MatrixToSaveRaw;

        [SubModelInformation(Required = true, Description = "The place to save the file.")]
        public FileLocation OutputFile;

        public bool RuntimeValidation(ref string error)
        {
            return this.EnsureExactlyOneAndOfSameType(MatrixToSaveRaw, MatrixToSave, ref error);
        }

        public void Start()
        {
            var matrix = new EmmeMatrix(Root.ZoneSystem.ZoneArray, ModuleHelper.GetDataFromDatasourceOrResource(MatrixToSaveRaw, MatrixToSave, MatrixToSaveRaw != null).GetFlatData());
            matrix.Save(OutputFile, true);
        }
    }

}
