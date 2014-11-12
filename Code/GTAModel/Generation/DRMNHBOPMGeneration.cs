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
using TMG.GTAModel.DataUtility;
using XTMF;

namespace TMG.GTAModel.Generation
{
    public class DRMNHBOPMGeneration : IDemographicCategoryGeneration
    {
        [RunParameter( "Demographic Parameter Set Index", 0, "The 0 indexed index of parameters to use when calculating utility" )]
        public int DemographicParameterSetIndex;

        [RunParameter( "ModeChoice Parameter Set Index", 0, "The 0 indexed index of parameters to use when calculating utility" )]
        public int ModeChoiceParameterSetIndex;

        [RunParameter( "Region Constant Parameters", "97.80036347,0,0,35.25847232", typeof( FloatList ), "The region parameters for Auto Times." )]
        public FloatList RegionConstantsParameter;

        [RunParameter( "Region Employment Parameter", "0.123741227,0.158434606,0.158219114,0.1190458", typeof( FloatList ), "The region parameter for the log of the employment." )]
        public FloatList RegionEmploymentParameter;

        [RunParameter( "Region Numbers", "1,2,3,4", typeof( NumberList ), "The space to be reading region parameters in from.\r\nThis is used as an inverse lookup for the parameters." )]
        public NumberList RegionNumbers;

        [RunParameter( "Region Population Parameter", "0.015253473,0.026917103,0.017423103,0.013863011", typeof( FloatList ), "The region parameter for the log of the employment." )]
        public FloatList RegionPopulationParameter;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Generate(SparseArray<float> production, SparseArray<float> attractions)
        {
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfzones = zones.Length;
            var flatProduction = production.GetFlatData();
            for ( int i = 0; i < numberOfzones; i++ )
            {
                int regionIndex;
                if ( !this.InverseLookup( zones[i].RegionNumber, out regionIndex ) )
                {
                    // if this region is not included just continue
                    flatProduction[i] = 0;
                    continue;
                }
                flatProduction[i] = zones[i].Population * this.RegionPopulationParameter[regionIndex]
                    + zones[i].Employment * this.RegionEmploymentParameter[regionIndex]
                    + this.RegionConstantsParameter[regionIndex];
            }
        }

        public void InitializeDemographicCategory()
        {
            this.Root.ModeParameterDatabase.ApplyParameterSet( this.ModeChoiceParameterSetIndex, this.DemographicParameterSetIndex );
        }

        public bool IsContained(IPerson person)
        {
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private bool InverseLookup(int regionNumber, out int regionIndex)
        {
            return ( regionIndex = this.RegionNumbers.IndexOf( regionNumber ) ) != -1;
        }
    }
}