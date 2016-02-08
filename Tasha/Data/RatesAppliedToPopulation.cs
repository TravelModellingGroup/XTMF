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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMG;
using XTMF;
using Datastructure;
using TMG.Functions;

namespace Tasha.Data
{
    [ModuleInformation( Description =
        @"This module is designed to apply rates to the total population of a zone." )]
    public class RatesAppliedToPopulation : IDataSource<SparseArray<float>>
    {
        private SparseArray<float> Data;

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation( Required = false, Description = "The rates to use for each planning district." )]
        public IResource RatesToApply;

        [SubModelInformation(Required = false, Description = "An alternative source.")]
        public IDataSource<SparseArray<float>> RatesToApplyRaw;

        [RunParameter( "PD Rates", true, "Are the rates based on planning districts (true) or zones (false)." )]
        public bool RatesBasedOnPD;

        public SparseArray<float> GiveData()
        {
            return this.Data;
        }

        public bool Loaded
        {
            get { return this.Data != null; }
        }

        public void LoadData()
        {
            var zoneArray = this.Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            var data = zoneArray.CreateSimilarArray<float>();
            var flatData = data.GetFlatData();
            var studentRates = ModuleHelper.GetDataFromDatasourceOrResource(RatesToApplyRaw, RatesToApply, RatesToApplyRaw != null);
            if ( this.RatesBasedOnPD )
            {
                for ( int i = 0; i < zones.Length; i++ )
                {
                    var pop = zones[i].Population;
                    var pd = zones[i].PlanningDistrict;
                    flatData[i] = pop * studentRates[pd];
                }
            }
            else
            {
                for ( int i = 0; i < zones.Length; i++ )
                {
                    var pop = zones[i].Population;
                    var zoneNumber = zones[i].ZoneNumber;
                    flatData[i] = pop * studentRates[zoneNumber];
                }
            }
            this.Data = data;
        }

        public void UnloadData()
        {
            this.Data = null;
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return this.EnsureExactlyOneAndOfSameType(RatesToApplyRaw, RatesToApply, ref error);
        }
    }
}
