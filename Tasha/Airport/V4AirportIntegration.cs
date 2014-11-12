/*
    Copyright 2014 James Vaughan for integration into XTMF.

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
using System.Threading.Tasks;
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;
namespace Tasha.Airport
{
    [ModuleInformation(Description =
 @"This will distribute a single mode given a distribution to an Airport.  It will take the distribution (assumed to be a probability) multiply it against the mode's probability
and then apply that against the Emplanings and Deplainings for the time period."
)]
    public class V4AirportIntegration : IModeAggregationTally
    {
        [RootModule]
        public ITravelDemandModel Root;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = true, Description = "A link to the mode's probability for Origin to Pearson.")]
        public IResource ModeProbabilityArray;

        [SubModelInformation(Required = true, Description = "A link to the distribution probability for going to Pearson.")]
        public IResource DistributionProbabilityArray;

        [RunParameter("Emplanings", 0.0f, "The number of emplanings in the time period.")]
        public float Emplainings;

        [RunParameter("Deplainings", 0.0f, "The number of deplainings in the time period.")]
        public float Deplainings;

        [RunParameter("Airport Zone Number", 3709, "The zone number for the airport.")]
        public int PearsonZone;

        [RunParameter("Passengers Per Trip", 2.6f, "This is used to convert between auto trips to emplanings.")]
        public float PassengersPerTrip;

        public void IncludeTally(float[][] currentTally)
        {
            var modeProbability = ModeProbabilityArray.AquireResource<SparseArray<float>>().GetFlatData();
            var distribution = DistributionProbabilityArray.AquireResource<SparseArray<float>>().GetFlatData();
            var airportZone = Root.ZoneSystem.ZoneArray.GetFlatIndex(PearsonZone);
            var effectiveEmplainings = Emplainings * (1.0f / PassengersPerTrip);
            var effectiveDeplainings = Deplainings * (1.0f / PassengersPerTrip);
            for(int i = 0; i < distribution.Length; i++)
            {
                if(distribution[i] > 0)
                {
                    var probability = modeProbability[i] * distribution[i];
                    if(float.IsInfinity(probability) | float.IsNaN(probability))
                    {
                        throw new XTMFRuntimeException("In '" + Name + "' when trying to produce the probability of a zone being selected we found an invalid probability!\r\n"
                            + "at index = " + i + " the modeProbability was " + modeProbability[i] + " and the distribution was " + distribution[i]);

                    }
                    currentTally[i][airportZone] += probability * effectiveEmplainings;
                    currentTally[airportZone][i] += probability * effectiveDeplainings;
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            if(!ModeProbabilityArray.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the mode probability resource specified is not a SparseArray<float> resource!";
                return false;
            }
            if(!DistributionProbabilityArray.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the distribution resource specified is not a SparseArray<float> resource!";
                return false;
            }
            return true;
        }
    }

}
