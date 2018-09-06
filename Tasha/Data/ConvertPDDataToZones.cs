/*
    Copyright 2015-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using Datastructure;
using TMG;
using TMG.Input;
using XTMF;
namespace Tasha.Data
{
    [ModuleInformation(Description = "This module will take a reader and only processing the origin data will copy all data from planning districts to all zones contained.")]
    public class ConvertPDDataToZones : IDataSource<SparseArray<float>>
    {

        [RootModule]
        public ITravelDemandModel Root;

        public bool Loaded
        {
            get;set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseArray<float> Data;

        [SubModelInformation(Required = true, Description = "The loader of PD data")]
        public IReadODData<float> Input;

        public SparseArray<float> GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
            var zoneSystem = GetZoneSystem();
            var zones = zoneSystem.GetFlatData();
            var data = zoneSystem.CreateSimilarArray<float>();
            var flatData = data.GetFlatData();
            var pdMap = zones.Select(z => z.PlanningDistrict).ToArray();
            foreach(var entry in Input.Read())
            {
                var any = false;
                var pd = entry.O;
                for(int i = 0; i < pdMap.Length; i++)
                {
                    if(pdMap[i] == pd)
                    {
                        flatData[i] = entry.Data;
                        any = true;
                    }
                }
                if(!any)
                {
                    throw new XTMFRuntimeException(this, $"Read in a PD of {pd} that has no zone in our zone system!");
                }
            }
            Data = data;
            Loaded = true;
        }

        private SparseArray<IZone> GetZoneSystem()
        {
            var zoneSystem = Root.ZoneSystem;
            if(zoneSystem.Loaded == false)
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
            Data = null;
        }
    }

}
