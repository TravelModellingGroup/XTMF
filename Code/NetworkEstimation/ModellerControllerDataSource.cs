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
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace TMG.NetworkEstimation
{
    [ModuleInformation(
        Description = @"This data source provides access to emme modeller.")]
    public class ModellerControllerDataSource : IDataSource<ModellerController>, IDisposable
    {
        [SubModelInformation(Required = true, Description = "The location of the Emme project file.")]
        public FileLocation ProjectFolder;

        [RunParameter("Emme Databank", "", "The name of the emme databank to work with.  Leave this as empty to select the default.")]
        public string EmmeDatabank;

        [RunParameter("EmmePath", "", "Optional: The path to an EMME installation directory to use.  This will default to the one in the system's EMMEPath")]
        public string EmmePath;

        [RunParameter("Check Unconsolidated Tools", true, "Check that all unconsolidated tools exist after establishing connection.")]
        public bool CheckUnconsolidatedTools;

        private ModellerController Data;

        public ModellerController GiveData() => Data;

        public bool Loaded => Data != null;

        public void LoadData()
        {
            if (Data == null)
            {
                lock (this)
                {
                    if (Data == null)
                    {
                        GC.ReRegisterForFinalize(this);
                        Data = new ModellerController(this, ProjectFolder, EmmeDatabank, String.IsNullOrWhiteSpace(EmmePath) ? null : EmmePath);
                        if(CheckUnconsolidatedTools)
                        {
                            Data.CheckAllToolsExist(this);
                        }
                    }
                }
            }
        }

        public void UnloadData()
        {
            Dispose();
        }

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => null;

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        ~ModellerControllerDataSource()
        {
            Dispose(true);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool all)
        {
            Data?.Dispose();
            Data = null;
        }
    }
}
