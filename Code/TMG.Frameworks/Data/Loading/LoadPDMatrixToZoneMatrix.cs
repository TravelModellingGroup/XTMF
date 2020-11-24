﻿using Datastructure;
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
                    for (int j = 0; j < flatData[i].Length; j++)
                    {
                        if(pdMap[i] == pdO && pdMap[j] == pdD)
                        {
                            flatData[i][j] = entry.Data;
                            any = true;
                        }
                    }
                }
                if (!any)
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
