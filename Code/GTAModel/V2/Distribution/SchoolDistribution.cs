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
using System.IO;
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel.V2.Distribution
{
    public class SchoolDistribution : IDemographicDistribution
    {
        [ParentModel]
        public IDemographicCategoyPurpose Parent;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Save Location", "", "The location to save the data to, relative to the run directory.  Leave this blank to not save." )]
        public string SaveDistribution;

        [SubModelInformation( Required = false,
            Description = "The observed data rates used to find the destination.  Each row must add to one." )]
        public List<IDataSource<SparseTwinIndex<float>>> SchoolDestinationRates;

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

        public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> productions, IEnumerable<SparseArray<float>> attractions,
            IEnumerable<IDemographicCategory> category)
        {
            using (var prodEnum = productions.GetEnumerator())
            {
                var ret = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
                var distribution = ret.GetFlatData();
                var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                for (int i = 0; prodEnum.MoveNext(); i++)
                {
                    var production = prodEnum.Current.GetFlatData();
                    SchoolDestinationRates[i].LoadData();
                    var rates = SchoolDestinationRates[i].GiveData();
                    SchoolDestinationRates[i].UnloadData();

                    Parallel.For(0, production.Length, origin =>
                    {
                        // ignore zones that are external
                        var zoneProduction = production[origin];
                        if (zoneProduction == 0)
                        {
                            for (int destination = 0; destination < production.Length; destination++)
                            {
                                distribution[origin][destination] = 0;
                            }
                        }
                        for (int destination = 0; destination < production.Length; destination++)
                        {
                            // ignore zones that are external
                            distribution[origin][destination] =
                                rates[zones[origin].ZoneNumber, zones[destination].ZoneNumber] * zoneProduction;
                        }
                    });
                    if (!String.IsNullOrWhiteSpace(SaveDistribution))
                    {
                        SaveData.SaveMatrix(ret, Path.Combine(SaveDistribution, i + ".csv"));
                    }
                    yield return ret;
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}