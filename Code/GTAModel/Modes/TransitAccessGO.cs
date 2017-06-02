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
using Datastructure;
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.Modes
{
    public class TransitAccessGO : IStationCollectionMode, IIterationSensitive
    {
        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation(Required = true, Description = "DAS Access Utilities.")]
        public IResource AccessStationUtilities;

        [RunParameter("Constant", 0.0f, "The modal constant for this mode.")]
        public float Constant;

        [RunParameter("Correlation", 0.0f, "The correlation of the utilities for access stations.")]
        public float Correlation;

        [RunParameter("AgeConstant1", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant1;

        [RunParameter("AgeConstant2", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant2;

        [RunParameter("AgeConstant3", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant3;

        [RunParameter("AgeConstant4", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant4;

        [RunParameter("Minimum Access Station Utility", -10.0f, "The minimum utility for the logsum of the access stations.")]
        public float MinimumAccessStationUtility;

        [RunParameter("Origin Population Density", 0.0f, "The weight to use for the employment density of the destination zone.")]
        public float OriginPopulationDensity;

        [RunParameter("Destination Employment Density", 0.0f, "The weight to use for the employment density of the destination zone.")]
        public float DestinationEmploymentDensity;

        private SparseTwinIndex<Tuple<IZone[], IZone[], float[]>> AccessUtilities;

        [RunParameter("Access", "true", typeof(bool), "Should we be computing access or egress (true for access).")]
        public bool Access { get; set; }

        public Tuple<IZone[], IZone[], float[]> GetSubchoiceSplit(IZone origin, IZone destination, Time time)
        {
            return AccessUtilities[origin.ZoneNumber, destination.ZoneNumber];
        }

        public string NetworkType { get; set; }

        public bool NonPersonalVehicle
        {
            get { return false; }
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return 0f;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Time.Zero;
        }

        [Parameter("Demographic Category Feasible", 1.0f, "Is this mode currently feasible?")]
        public float CurrentlyFeasible { get; set; }

        [RunParameter("Mode Name", "TAG", "The name of the mode.  Should be unique to all other modes.")]
        public string ModeName { get; set; }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var o = zoneArray.GetFlatIndex( origin.ZoneNumber );
            var d = zoneArray.GetFlatIndex( destination.ZoneNumber );
            // build the constant for this demographic category
            var v = Constant + AgeConstant1 + AgeConstant2 + AgeConstant3 + AgeConstant4;
            var data = AccessUtilities.GetFlatData()[o][d];
            var accessStations = data.Item1;
            var accessUtils = data.Item3;
            var accessUtil = 0f;
            // if the current station doesn't exist then we should just exit since all other stations also don't exist
            for ( int i = 0; i < accessStations.Length && accessStations[i] != null; i++ )
            {
                // the utilities are stored as e^v already so we can just add them
                accessUtil += accessUtils[i];
            }
            // since the sum was already raised to the e, we can just take the natural log to get the logsum
            var logsum = (float)Math.Log( accessUtil );
            if ( logsum < MinimumAccessStationUtility )
            {
                return float.NaN;
            }
            v += Correlation * logsum;
            if ( Access )
            {
                v += OriginPopulationDensity * (float)( Math.Log( origin.Population / ( origin.InternalArea / 1000f ) + 1f ) );
                v += DestinationEmploymentDensity * (float)Math.Log( destination.Employment / ( destination.InternalArea / 1000f ) + 1f );
            }
            else
            {
                v += OriginPopulationDensity * (float)( Math.Log( destination.Population / ( destination.InternalArea / 1000f ) + 1f ) );
                v += DestinationEmploymentDensity * (float)Math.Log( origin.Employment / ( origin.InternalArea / 1000f ) + 1f );
            }
            return v;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            if ( CurrentlyFeasible <= 0 ) return false;
            var data = AccessUtilities[origin.ZoneNumber, destination.ZoneNumber];
            // make sure that the utilities have been processed
            return !( data == null || data.Item1 == null || data.Item1.Length == 0 );
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( !AccessStationUtilities.CheckResourceType<SparseTwinIndex<Tuple<IZone[], IZone[], float[]>>>() )
            {
                error = "In '" + Name + "' the resource Access Station Utilities are not of the proper type SparseTwinIndex<Tuple<IZone[],IZone[],float[]>>!";
                return false;
            }
            return true;
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {
        }

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            if ( iterationNumber > 0 )
            {
                AccessStationUtilities.ReleaseResource();
            }
            // each iteration reload the utilities
            if ( ( AccessUtilities = AccessStationUtilities.AcquireResource<SparseTwinIndex<Tuple<IZone[], IZone[], float[]>>>() ) == null )
            {
                throw new XTMFRuntimeException( "In '" + Name + "' we were unable to gather our Access Station Utilities!" );
            }
        }
    }
}