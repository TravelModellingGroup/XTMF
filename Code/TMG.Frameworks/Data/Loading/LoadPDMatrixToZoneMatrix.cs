/*
    Copyright 2020 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Loading
{
    [ModuleInformation(Description = "Loads in records that are indexed by planning districts and creates a matrix of the results applied to their respective zones.")]
    public sealed class LoadPDMatrixToZoneMatrix : IDataSource<SparseTwinIndex<float>>
    {
        [RootModule]
        public ITravelDemandModel Root;

        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseTwinIndex<float> _data;

        [SubModelInformation(Required = true, Description = "The loader of PD data")]
        public IReadODData<float> Input;

        [RunParameter("Continue After Invalid ODPair", false, "Should the model system continue after an invalid value has been read in?")]
        public bool ContinueAfterInvalidODPair;

        public SparseTwinIndex<float> GiveData()
        {
            return _data;
        }

        public void LoadData()
        {
            var zoneSystem = GetZoneSystem();
            var zones = zoneSystem.GetFlatData();
            var data = zoneSystem.CreateSquareTwinArray<float>();
            var flatData = data.GetFlatData();
            var pdMap = zones.Select(z => z.PlanningDistrict).ToArray();
            foreach (var entry in Input.Read())
            {
                var any = false;
                var pdO = entry.O;
                var pdD = entry.D;
                for (int i = 0; i < flatData.Length; i++)
                {
                    if (pdMap[i] == pdO)
                    {
                        for (int j = 0; j < flatData[i].Length; j++)
                        {
                            if (pdMap[j] == pdD)
                            {
                                flatData[i][j] = entry.Data;
                                any = true;
                            }
                        }
                    }
                }
                if (!any && !ContinueAfterInvalidODPair)
                {
                    throw new XTMFRuntimeException(this, $"Read in a record of PD {pdO} to PD {pdD} that has no zone in our zone system!");
                }
            }
            _data = data;
            Loaded = true;
        }

        private SparseArray<IZone> GetZoneSystem()
        {
            var zoneSystem = Root.ZoneSystem;
            if (zoneSystem.Loaded == false)
            {
                zoneSystem.LoadData();
            }
            return zoneSystem.ZoneArray;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Loaded = false;
            _data = null;
        }
    }
}
