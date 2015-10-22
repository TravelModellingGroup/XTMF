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
        @"This module is designed to divide two rates together for each zone.  The first resource is divided by the second." )]
    public class DivideRatesForZones : IDataSource<SparseArray<float>>
    {
        private SparseArray<float> Data;

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation( Required = true, Description = "The value to use for the numerator." )]
        public IResource FirstRateToApply;

        [SubModelInformation( Required = true, Description = "The value to use for the denominator." )]
        public IResource SecondRateToApply;

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
            var firstRate = this.FirstRateToApply.AquireResource<SparseArray<float>>();
            var secondRate = this.SecondRateToApply.AquireResource<SparseArray<float>>();
            SparseArray<float> data = firstRate.CreateSimilarArray<float>();
            var flatFirst = firstRate.GetFlatData();
            var flatSecond = secondRate.GetFlatData();
            var flat = data.GetFlatData();
            if(VectorHelper.IsHardwareAccelerated)
            {
                VectorHelper.Divide(flat, 0, flatFirst, 0, flatSecond, 0, flat.Length);
            }
            else
            {
                for(int i = 0; i < flat.Length; i++)
                {
                    flat[i] = flatFirst[i] / flatSecond[i];
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
            if ( !this.FirstRateToApply.CheckResourceType<SparseArray<float>>() )
            {
                error = "In '" + this.Name + "' the first rates resource is not of type SparseArray<float>!";
                return false;
            }
            if ( !this.SecondRateToApply.CheckResourceType<SparseArray<float>>() )
            {
                error = "In '" + this.Name + "' the second rate resource is not of type SparseArray<float>!";
                return false;
            }
            return true;
        }
    }
}
