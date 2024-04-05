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
using Datastructure;
using TMG;
using TMG.Functions;
using XTMF;

namespace Tasha.Estimation;

public sealed class KFactors : IDataSource<SparseTwinIndex<float>>
{
    [RootModule]
    public ITravelDemandModel Root;


    public class KFactor : IModule
    {
        [RunParameter("Origin", "0", typeof(RangeSet), "The PD to start from.")]
        public RangeSet OriginPD;

        [RunParameter("Destination", "0", typeof(RangeSet), "The PD to start from.")]
        public RangeSet DestinationPD;

        [RunParameter("Factor", 1.0f, "The factor to apply to the OD pair.")]
        public float Factor;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

    [SubModelInformation(Description = "The factors to apply to PD pairs.")]
    public KFactor[] Factors;


    public bool Loaded
    {
        get; set;
    }

    public string Name
    {
        get; set;
    }

    public float Progress
    {
        get
        {
            return 0f;
        }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get
        {
            return new Tuple<byte, byte, byte>(50, 150, 50);
        }
    }

    private SparseTwinIndex<float> Data;

    public SparseTwinIndex<float> GiveData()
    {
        return Data;
    }


    public void LoadData()
    {
        var ret = Data;
        if (ret == null)
        {
            Data = ret = ZoneSystemHelper.CreatePdTwinArray<float>(Root.ZoneSystem.ZoneArray);
        }
        var data = ret.GetFlatData();
        // initialize the data
        if (data.Length > 0)
        {
            for (int i = 0; i < data.Length; i++)
            {
                var row = data[i];
                for (int j = 0; j < row.Length; j++)
                {
                    row[j] = 1.0f;
                }
            }
            // now load in our kfactors
            for (int o = 0; o < data.Length; o++)
            {
                var sparseO = ret.GetSparseIndex(o);
                for (int d = 0; d < data.Length; d++)
                {
                    var sparseD = ret.GetSparseIndex(d);
                    foreach (var factor in Factors)
                    {
                        if (factor.OriginPD.Contains(sparseO) && factor.DestinationPD.Contains(sparseD))
                        {
                            if (data[o][d] != 1.0f)
                            {
                                Console.WriteLine($"Warning: In {Name}, multiple KFactors are altering PD{sparseO} to PD{sparseD}!");
                            }
                            data[o][d] *= factor.Factor;
                        }
                    }
                }
            }
        }
        Loaded = true;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void UnloadData()
    {
        Loaded = false;
    }
}
