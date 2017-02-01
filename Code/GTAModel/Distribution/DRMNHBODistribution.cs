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
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using TMG.GTAModel.DataUtility;
using XTMF;

namespace TMG.GTAModel.Distribution
{
    public class DRMNHBODistribution : IDemographicDistribution
    {
        [RunParameter( "Auto Network Name", "Auto", "The name of the auto network." )]
        public string AutoNetworkName;

        [RunParameter( "Region Constant Parameter", "97.80036347,0,0,35.25847232", typeof( FloatList ), "The region constant parameters." )]
        public FloatList RegionConstantParameter;

        [RunParameter( "Region Auto Time Parameter", "-0.182000,-0.178000,-0.187000,-0.165000", typeof( FloatList ), "The region parameter for the log of the employment." )]
        public FloatList RegionEmploymentGeneralParameter;

        [RunParameter( "Region Employment Parameter", "0.298000,0.320000,0.428000,0.360000", typeof( FloatList ), "The region parameter for the log of the employment." )]
        public FloatList RegionEmploymentParameter;

        [RunParameter( "Region Numbers", "1,2,3,4", typeof( NumberList ), "The space to be reading region parameters in from.\r\nThis is used as an inverse lookup for the parameters." )]
        public NumberList RegionNumbers;

        [RunParameter( "Region Population Parameter", "0.256000,0.342000,0.275000,0.213000", typeof( FloatList ), "The region parameter for the log of the employment." )]
        public FloatList RegionPopulationParameter;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        [RunParameter( "Simulation Time", "7:00AM", typeof( Time ), "The time of day this will be simulating." )]
        public Time SimulationTime;

        private INetworkData NetworkData;

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

        public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> productions, IEnumerable<SparseArray<float>> attractions, IEnumerable<IDemographicCategory> category)
        {
            var ep = productions.GetEnumerator();
            var ec = category.GetEnumerator();
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            float[] friction = null;
            while ( ep.MoveNext() && ec.MoveNext() )
            {
                friction = ComputeFriction( zones, ec.Current, friction );
                yield return SinglyConstrainedGravityModel.Process( ep.Current, friction );
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            LoadNetwork();
            if ( NetworkData == null )
            {
                error = "We were unable to find a network called '" + AutoNetworkName + "' to use in module '" + Name + "'";
                return false;
            }
            return true;
        }

        private bool CompareParameterCount(FloatList data)
        {
            return RegionNumbers.Count == data.Count;
        }

        private float[] ComputeFriction(IZone[] zones, IDemographicCategory cat, float[] friction)
        {
            var numberOfZones = zones.Length;
            float[] ret = friction == null ? new float[numberOfZones * numberOfZones] : friction;
            // let it setup the modes so we can compute friction
            cat.InitializeDemographicCategory();
            try
            {
                Parallel.For( 0, numberOfZones, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int j)
                {
                    var destination = zones[j];
                    int regionIndex;
                    if ( !InverseLookup( destination.RegionNumber, out regionIndex ) )
                    {
                        // make sure to reset the friction to zero
                        for ( int i = 0; i < numberOfZones; i++ )
                        {
                            ret[i * numberOfZones + j] = float.NegativeInfinity;
                        }
                        return;
                    }
                    // store the log of the population and the employment since we will be using this for each origin
                    var employmentLog = (float)Math.Log( ( destination.Employment - destination.ManufacturingEmployment ) + 1 );
                    var populationLog = (float)Math.Log( destination.Population + 1 );
                    for ( int i = 0; i < numberOfZones; i++ )
                    {
                        var origin = zones[i];
                        if ( origin.RegionNumber <= 0 )
                        {
                            ret[i * numberOfZones + j] = float.NegativeInfinity;
                        }
                        else
                        {
                            var autoTime = RegionEmploymentGeneralParameter[regionIndex] *
                                NetworkData.TravelTime( origin, destination, SimulationTime ).ToMinutes();
                            var destinationUtility = RegionEmploymentParameter[regionIndex] * employmentLog
                            + RegionPopulationParameter[regionIndex] * populationLog;
                            // this isn't friction, it is V where friction will be e^V
                            ret[i * numberOfZones + j] = destinationUtility + autoTime;
                        }
                    }
                } );
            }
            catch ( AggregateException e )
            {
                if ( e.InnerException is XTMFRuntimeException )
                {
                    throw new XTMFRuntimeException( e.InnerException.Message );
                }
                throw new XTMFRuntimeException( e.InnerException.Message + "\r\n" + e.InnerException.StackTrace );
            }
            // Use the Log-Sum from the V's as the impedence function
            return ret;
        }

        private bool InverseLookup(int regionNumber, out int regionIndex)
        {
            return ( regionIndex = RegionNumbers.IndexOf( regionNumber ) ) != -1;
        }

        private bool LoadNetwork()
        {
            foreach ( var data in Root.NetworkData )
            {
                if ( data.NetworkType == AutoNetworkName )
                {
                    NetworkData = data;
                    return true;
                }
            }
            return false;
        }
    }
}