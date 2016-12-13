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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datastructure;
using XTMF;

namespace TMG.Frameworks.Data.Loading
{
    [ModuleInformation(Description = "This module is designed to quickly create a vector in the shape of the zone system filled with a selectable spatial aggregation.")]
    public class ZoneSystemVector : IDataSource<SparseArray<float>>
    {
        public enum FillData
        {
            ZoneNumber,
            PlanningDistrict,
            Region,
            FlatIndex
        }

        [RunParameter("Fill With", nameof(FillData.PlanningDistrict), typeof(FillData), "The type of data to fill into the zone system vector.")]
        public FillData FillWith;

        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseArray<float> Data;

        public SparseArray<float> GiveData()
        {
            return Data;
        }

        [RootModule]
        public ITravelDemandModel root;

        public void LoadData()
        {
            if (!root.ZoneSystem.Loaded)
            {
                root.ZoneSystem.LoadData();
            }
            var data = root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
            var flatData = data.GetFlatData();
            var flatZones = root.ZoneSystem.ZoneArray.GetFlatData();
            switch (FillWith)
            {
                case FillData.PlanningDistrict:
                    for (int i = 0; i < flatData.Length; i++)
                    {
                        flatData[i] = flatZones[i].PlanningDistrict;
                    }
                    break;
                case FillData.Region:
                    for (int i = 0; i < flatData.Length; i++)
                    {
                        flatData[i] = flatZones[i].RegionNumber;
                    }
                    break;
                case FillData.ZoneNumber:
                    for (int i = 0; i < flatData.Length; i++)
                    {
                        flatData[i] = flatZones[i].ZoneNumber;
                    }
                    break;
                case FillData.FlatIndex:
                    for (int i = 0; i < flatData.Length; i++)
                    {
                        flatData[i] = i;
                    }
                    break;
            }
            Data = data;
            Loaded = true;
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
