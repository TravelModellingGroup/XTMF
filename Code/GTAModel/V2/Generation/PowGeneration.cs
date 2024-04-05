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

using System.Threading.Tasks;
using Datastructure;

namespace TMG.GTAModel.V2.Generation;

public sealed class PowGeneration : DemographicCategoryGeneration
{
    internal RangeSet AllAges;

    override public void Generate(SparseArray<float> production, SparseArray<float> attractions)
    {
        // Do nothing, the distribution needs to do it all anyways
        // The only thing this generation needs is the ability to setup the mode choice properly
        var flatProduction = production.GetFlatData();
        var ageRates = Root.Demographics.AgeRates;
        var empRates = Root.Demographics.EmploymentStatusRates.GetFlatData();
        var occRates = Root.Demographics.OccupationRates.GetFlatData();
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        var age = AgeCategoryRange[0].Start;
        var occ = OccupationCategory[0].Start;
        Parallel.For( 0, flatProduction.Length, i =>
        {
            float total = 0;
            var zoneNumber = zones[i].ZoneNumber;
            var emp = empRates[i];
            var occRate = occRates[i];
            if ( emp == null | occRate == null )
            {
                flatProduction[i] = 0;
            }
            else
            {
                foreach ( var set in AllAges )
                {
                    for ( int a = set.Start; a <= set.Stop; a++ )
                    {
                        total += ageRates[zoneNumber, a] * emp[a, 1] * occRate[a, 1, occ];
                    }
                }
                // NOTE, SINCE THE DISTRIBUTION DOES TRIP RATES BASED ON  PDO->PDD WE ONLY NEED TO
                // GIVE THE TOTAL WORKERS FOR THIS GIVEN AGE
                // the rate is not just the age, but the weight of the age for valid workers
                flatProduction[i] = total > 0 ? ( ageRates[zoneNumber, age] * emp[age, 1] * occRate[age, 1, occ] ) / total : 0;
            }
        } );
    }

    public override void InitializeDemographicCategory()
    {
        // first learn what demographic category we should be in
        DemographicParameterSetIndex = GetDemographicIndex( AgeCategoryRange[0].Start, Mobility[0].Start );
        // now we can generate
        base.InitializeDemographicCategory();
    }

    private int GetDemographicIndex(int age, int mobility)
    {
        return ( age - 2 ) * 5 + mobility;
    }
}