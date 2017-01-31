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
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation(Description=
        @"<b>This module is for testing purposes only!</b><p>This module is responsible for selecting a specific segment of the population based upon a range of ages, what type of work they have, employment status, and mobility category. It also provides parameters to change how the mode split will be placed upon them. You can disable particular modes with the ‘Infeasible Modes’ parameter by entering in the mode’s name. In addition you can also modify the utility of the mode by entering in its name, a colon and then entering in a number. You can chain these together by adding commas between them.
This module generates the trips based upon the population living in zones and the amount of retail employment in the destination zones.
This module requires the root module of the model system to be an ‘IDemographic4StepModelSystemTemplate’.</p>" )]
    public sealed class DemographicMarketGeneration : DemographicCategoryGeneration
    {
        [RunParameter( "Probability", 0.25f, "The probability of a person in this category making this kind of trip." )]
        public float Probability;

        override public void Generate(Datastructure.SparseArray<float> production, Datastructure.SparseArray<float> attractions)
        {
            var flatProduction = production.GetFlatData();
            var flatAttraction = attractions.GetFlatData();

            var numberOfIndexes = flatAttraction.Length;

            // Compute the Production and Attractions
            float totalProduction = 0;
            float totalAttraction = 0;
            totalProduction = ComputeProduction( flatProduction, numberOfIndexes );
            totalAttraction = ComputeAttraction( flatAttraction, Root.ZoneSystem.ZoneArray.GetFlatData(), numberOfIndexes );

            // Normalize the attractions
            float productionAttractionRatio;
            if ( totalAttraction != 0 )
            {
                productionAttractionRatio = totalProduction / totalAttraction; // inverse totalAttraction to save on divisions
            }
            else
            {
                productionAttractionRatio = totalProduction / numberOfIndexes;
            }
            for ( int i = 0; i < numberOfIndexes; i++ )
            {
                flatAttraction[i] = flatAttraction[i] * productionAttractionRatio;
            }
        }

        private float ComputeAttraction(float[] flatAttraction, IZone[] zones, int numberOfZones)
        {
            float totalAttractions = 0;
            var zoneArray = Root.ZoneSystem.ZoneArray.GetFlatData();
            for ( int i = 0; i < numberOfZones; i++ )
            {
                var temp = zoneArray[i].RetailEmployment;
                totalAttractions += ( flatAttraction[i] = temp );
            }
            return totalAttractions;
        }

        private float ComputeProduction(float[] flatProduction, int numberOfZones)
        {
            int totalProduction = 0;
            var flatPopulation = Root.Population.Population.GetFlatData();
            System.Threading.Tasks.Parallel.For( 0, numberOfZones, delegate(int i)
            {
                var zonePop = flatPopulation[i];
                if ( zonePop == null ) return;
                var count = 0;
                var popLength = zonePop.Length;
                for ( int person = 0; person < popLength; person++ )
                {
                    var p = zonePop[person];
                    if ( IsContained( p ) )
                    {
                        count++;
                    }
                }
                flatProduction[i] = count * Probability;
                System.Threading.Interlocked.Add( ref totalProduction, count );
            } );
            return totalProduction * Probability;
        }
    }
}