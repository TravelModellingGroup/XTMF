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
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using TMG.GTAModel.DataUtility;
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation(Description=
        @"Population Assignment is the fusion of a generation and a distribution algorithm.  
It has a parameter called ‘Destination Variable’ which will at runtime look at the population and 
try to find a property of the given name and use that to load the zones to assign to.
This module requires the root module of the model system to be of type ‘IDemographicsModelSystemTemplate’."
        )]
    public class PopulationAssignment : IAssignment
    {
        [SubModelInformation( Description = "The categories to assign for.", Required = false )]
        public List<IDemographicCategory> Categories;

        [RunParameter( "Destination Variable", "WorkZone", "The variable name that holds the destination." )]
        public string LookUpString;

        [RunParameter( "Probability", 1.0f, "The probability of this assignment on this population." )]
        public float Probability;

        [RootModule]
        public IDemographicsModelSystemTemplate Root;

        private IRead<IZone, IPerson> GetDest;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get;
            set;
        }

        public IEnumerable<SparseTwinIndex<float>> Assign()
        {
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var zoneSystem = zoneArray.GetFlatData();
            var population = Root.Population.Population.GetFlatData();
            var numberOfZones = zoneSystem.Length;
            foreach ( var cat in Categories )
            {
                cat.InitializeDemographicCategory();
                var ret = zoneArray.CreateSquareTwinArray<float>();
                var flatRet = ret.GetFlatData();
                Parallel.For( 0, numberOfZones,
                    delegate(int i)
                    {
                        var localPop = population[i];
                        int popLength;
                        if ( localPop != null && ( popLength = localPop.Length ) > 0 )
                        {
                            var iArray = flatRet[i];
                            EnsureGetDest( ref GetDest, localPop[0] );
                            for ( int j = 0; j < popLength; j++ )
                            {
                                IZone destZone;
                                var person = localPop[j];
                                if ( cat.IsContained( person ) )
                                {
                                    GetDest.Read( person, out destZone );
                                    if ( destZone != null )
                                    {
                                        iArray[zoneArray.GetFlatIndex( destZone.ZoneNumber )] += Probability;
                                    }
                                }
                            }
                        }
                    } );
                yield return ret;
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void EnsureGetDest(ref IRead<IZone, IPerson> getDest, IPerson reference)
        {
            if ( getDest == null )
            {
                lock ( this )
                {
                    Thread.MemoryBarrier();
                    if ( getDest == null )
                    {
                        getDest = UniversalRead<IZone>.CreateReader( reference, LookUpString );
                        Thread.MemoryBarrier();
                    }
                }
            }
        }
    }
}