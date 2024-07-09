/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.Report.Analyses;

[ModuleInformation(Description = "Produces a CSV report of the survey and Microsim school activity rates by age group.")]
public sealed class SchoolGenerationRateByAge : Analysis
{
    [SubModelInformation(Required = true, Description = "The age ranges to group by.")]
    public AgeRange[] AgeRanges = null!;

    [SubModelInformation(Required = true, Description = "The location to save the analysis to.")]
    public FileLocation SaveTo = null!;

    [RootModule]
    public ITravelDemandModel Root = null!;

    public override void Execute(TimePeriod[] timePeriods, MicrosimData microsimData, ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        float[] observedGenerationRates = GetObserved(surveyHouseholdsWithTrips);
        float[] modelledGenerationRates = GetModelled(microsimData);

        using var writer = new System.IO.StreamWriter(SaveTo);
        writer.WriteLine("AgeGroup,Observed,Modelled");
        for (int i = 0; i < AgeRanges.Length; i++)
        {
            var ageGroup = AgeRanges[i].Name;
            var observedValue = observedGenerationRates[i * 2] / observedGenerationRates[i * 2 + 1];
            var modelledValue = modelledGenerationRates[i * 2] / modelledGenerationRates[i * 2 + 1];
            writer.WriteLine($"{ageGroup},{observedValue},{modelledValue}");
        }
    }

    private float[] GetObserved(ITashaHousehold[] surveyHouseholdsWithTrips)
    {
        float[] ret = new float[AgeRanges.Length * 2];
        foreach (var household in surveyHouseholdsWithTrips)
        {
            foreach (var person in household.Persons)
            {
                int ageIndex = AgeRanges.GetIndex(person.Age);
                if (ageIndex < 0)
                {
                    continue;
                }
                int count = 0;
                foreach (var chain in person.TripChains)
                {
                    foreach (var trip in chain.Trips)
                    {
                        if (trip.Purpose == Activity.School)
                        {
                            count++;
                        }
                    }
                }
                var expFactor = person.ExpansionFactor;
                ret[ageIndex * 2] += count * expFactor;
                ret[ageIndex * 2 + 1] += expFactor;
            }
        }

        return ret;
    }

    private float[] GetModelled(MicrosimData microsimData)
    {
        float[] ret = new float[AgeRanges.Length * 2];
        foreach (var household in microsimData.Households)
        {
            foreach (var person in microsimData.Persons[household.HouseholdID])
            {
                int count = 0;
                if (!microsimData.Trips.TryGetValue((household.HouseholdID, person.PersonID), out var trips))
                {
                    continue;
                }
                foreach (var trip in trips)
                {
                    if (trip.DestinationPurpose == "School")
                    {
                        count++;
                    }
                }
                var expFactor = person.Weight;
                var ageIndex = AgeRanges.GetIndex(person.Age);
                ret[ageIndex * 2] += count * expFactor;
                ret[ageIndex * 2 + 1] += expFactor;
            }
        }
        return ret;
    }

}
