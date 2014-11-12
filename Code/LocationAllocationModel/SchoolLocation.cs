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
using System.Linq;
using Tasha.Common;
using TMG;
using XTMF;

namespace DYL.Tasha
{
    public sealed class SchoolLocation : ICalculation<ITashaPerson, IZone>
    {
        [RunParameter( "beta_dis", 0f, "The distance factor applied for the school location choice" )]
        public float beta_dis;

        [RunParameter( "beta_occ", 0f, "The occupation number factor applied for the school location choice" )]
        public float beta_occ;

        [RunParameter( "RandomSeed", 12345, "The RandomSeed for the school location choice caculation" )]
        public int RandomSeed;

        [RootModule]
        public ITravelDemandModel Root;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Load()
        {
        }

        public IZone ProduceResult(ITashaPerson person)
        {
            IZone[] flatZones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            float[] utility = new float[flatZones.Length];
            // gather the utilities
            for ( int i = 0; i < flatZones.Length; i++ )
            {
                utility[i] = beta_dis * Root.ZoneSystem.Distances[person.Household.HomeZone.ZoneNumber, flatZones[i].ZoneNumber] + beta_occ * flatZones[i].WorkProfessional;
            }
            // calc utility
            var factor = 1f / utility.Sum();
            // convert to probability
            for ( int i = 0; i < utility.Length; i++ )
            {
                utility[i] *= factor;
            }
            // pick the zone to use for school
            Random haveMeSomewhereElse = new Random( this.RandomSeed * ( person.Id * 10 ) * ( person.Household.HouseholdId * 1000 ) );
            var getToMe = haveMeSomewhereElse.NextDouble();
            factor = 0;
            for ( int i = 0; i < utility.Length; i++ )
            {
                factor += utility[i];
                if ( factor > getToMe )
                {
                    return flatZones[i];
                }
            }
            return flatZones[flatZones.Length - 1];
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Unload()
        {
        }
    }
}