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
    public sealed class WorkLocation : ICalculation<ITashaPerson, IZone>
    {
        [RunParameter( "beta_dis_G", 0f, "The distance factor applied for the general occupation location choice" )]
        public float beta_dis_G;

        [RunParameter( "beta_dis_M", 0f, "The distance factor applied for the manufactoring occupation location choice" )]
        public float beta_dis_M;

        [RunParameter( "beta_dis_P", 0f, "The distance factor applied for the prefessional occupation location choice" )]
        public float beta_dis_P;

        [RunParameter( "beta_dis_S", 0f, "The distance factor applied for the sale occupation location choice" )]
        public float beta_dis_S;

        [RunParameter( "beta_occ_G", 0f, "The occupation number factor applied for the general occupation location choice" )]
        public float beta_occ_G;

        [RunParameter( "beta_occ_M", 0f, "The occupation number factor applied for the manufactoring occupation location choice" )]
        public float beta_occ_M;

        [RunParameter( "beta_occ_P", 0f, "The occupation number factor applied for the prefessional occupation location choice" )]
        public float beta_occ_P;

        [RunParameter( "beta_occ_S", 0f, "The occupation number factor applied for the sale occupation location choice" )]
        public float beta_occ_S;

        [RunParameter( "RandomSeed", 12345, "The RandomSeed for the work location choice caculation" )]
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
                switch ( person.Occupation )
                {
                    case Occupation.Manufacturing:
                        utility[i] = beta_dis_M * Root.ZoneSystem.Distances[person.Household.HomeZone.ZoneNumber, flatZones[i].ZoneNumber] + beta_occ_M * flatZones[i].WorkManufacturing;
                        break;

                    case Occupation.Professional:
                        utility[i] = beta_dis_P * Root.ZoneSystem.Distances[person.Household.HomeZone.ZoneNumber, flatZones[i].ZoneNumber] + beta_occ_P * flatZones[i].WorkProfessional;
                        break;

                    case Occupation.Office:
                        utility[i] = beta_dis_G * Root.ZoneSystem.Distances[person.Household.HomeZone.ZoneNumber, flatZones[i].ZoneNumber] + beta_occ_G * flatZones[i].WorkGeneral;
                        break;

                    case Occupation.Retail:
                        utility[i] = beta_dis_S * Root.ZoneSystem.Distances[person.Household.HomeZone.ZoneNumber, flatZones[i].ZoneNumber] + beta_occ_S * flatZones[i].WorkRetail;
                        break;

                    default:
                        return person.Household.HomeZone;
                }
            }
            // calc utility
            var factor = 1f / utility.Sum();
            // convert to probability
            for ( int i = 0; i < utility.Length; i++ )
            {
                utility[i] *= factor;
            }
            // pick the zone to use for work
            Random haveMeSomewhereElse = new Random( this.RandomSeed * ( person.Id * 10 ) * ( person.Household.HouseholdId * 1000 ) );
            var getToMe = haveMeSomewhereElse.NextDouble();
            factor = 0;
            for ( int i = 0; i < utility.Length; i++ )
            {
                factor += utility[i];
                if ( factor > getToMe )
                {
                    var result = flatZones[i];
                    if ( result == null )
                    {
                        throw new XTMFRuntimeException( "TESTING: Work zone is null!" );
                    }
                    return flatZones[i];
                }
            }
            var result1 = flatZones[flatZones.Length - 1];
            if ( result1 == null )
            {
                throw new XTMFRuntimeException( "TESTING: Work zone is null!" );
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