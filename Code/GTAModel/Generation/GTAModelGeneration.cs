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

using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel;

// ReSharper disable once InconsistentNaming
public sealed class GTAModelGeneration : DemographicCategoryGeneration
{
    [RunParameter( "Probability", 1.0f, "The probability for a person to make this trip." )]
    public float Probability;

    public float ComputeAttraction(float[] flatAttraction, IZone[] zones, int numberOfZones)
    {
        float totalAttractions = 0;
        var demographics = Root.Demographics;
        var flatEmploymentRates = demographics.JobOccupationRates.GetFlatData();
        var flatJobTypes = demographics.JobTypeRates.GetFlatData();

        for ( int i = 0; i < numberOfZones; i++ )
        {
            var total = 0f;
            foreach ( var empRange in EmploymentStatusCategory )
            {
                for ( int emp = empRange.Start; emp <= empRange.Stop; emp++ )
                {
                    foreach ( var occRange in EmploymentStatusCategory )
                    {
                        for ( int occ = occRange.Start; occ <= occRange.Stop; occ++ )
                        {
                            var temp = flatEmploymentRates[i][emp][occ];
                            temp *= flatJobTypes[i][emp];
                            temp *= zones[i].Employment;
                            total += temp;
                        }
                    }
                }
            }
            totalAttractions += ( flatAttraction[i] = total );
        }
        return totalAttractions;
    }

    public float ComputeProduction(float[] flatProduction, int numberOfZones)
    {
        int totalProduction = 0;
        var flatPopulation = Root.Population.Population.GetFlatData();
        Parallel.For( 0, numberOfZones, delegate(int i)
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
            Interlocked.Add( ref totalProduction, count );
        } );
        return totalProduction * Probability;
    }

    override public void Generate(SparseArray<float> production, SparseArray<float> attractions)
    {
        var flatProduction = production.GetFlatData();
        var flatAttraction = attractions.GetFlatData();

        var numberOfIndexes = flatAttraction.Length;

        // Compute the Production and Attractions
        var totalProduction = ComputeProduction( flatProduction, numberOfIndexes );
        var totalAttraction = ComputeAttraction( flatAttraction, Root.ZoneSystem.ZoneArray.GetFlatData(), numberOfIndexes );

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
}