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
using System.Text;
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Validation
{
    [ModuleInformation(
        Description = "This module is used to make Origin-Destination Matrices (.311 files) which " +
                        "can then be used with EMME to get travel times and costs. As an input, the module " +
                        "takes in household data and a zone system, and then creates the .311 file. For each mode " +
                        "(transit and auto), the module calculates an AM, PM and FF (off peak) O-D Matrix. The different " +
                        "matrices are based on the rush-hour start and end parameters inputteted. Note that the module " +
                        "adds together expansion factors and not just number of observations for te OD pairs."
        )]
    public class ODMatrixBuilder : ITashaRuntime
    {
        private string Status = "Initializing!";

        [SubModelInformation( Description = "All Modes", Required = false )]
        public List<ITashaMode> AllModes
        {
            get;
            set;
        }

        [RunParameter( "AM Rush Hour End Time", "9:00", typeof( Time ), "The rush hour end time in the morning" )]
        public Time AMRushEnd { get; set; }

        [RunParameter( "AM Rush Hour Start Time", "6:00", typeof( Time ), "The rush hour start time in the morning" )]
        public Time AMRushStart { get; set; }

        [DoNotAutomate]
        public ITashaMode AutoMode { get; set; }

        [SubModelInformation( Description = "The type of vehicle used for auto trips", Required = true )]
        public IVehicleType AutoType { get; set; }

        public Time EndOfDay
        {
            get;
            set;
        }

        [SubModelInformation( Description = "The model that will load our household", Required = true )]
        public IDataLoader<ITashaHousehold> HouseholdLoader { get; set; }

        [RunParameter( "Input Directory", "../../TashaInput", "The Input Directory" )]
        public string InputBaseDirectory
        {
            get;
            set;
        }

        public int TotalIterations
        {
            get;
            set;
        }

        [RunParameter( "Matrix Number", 10, "The EMME Matrix Number" )]
        public int matrixNumber { get; set; }

        [DoNotAutomate]
        public ITashaModeChoice ModeChoice { get; set; }

        public string Name
        {
            get;
            set;
        }

        [SubModelInformation( Description = "Network data", Required = false )]
        public IList<INetworkData> NetworkData
        {
            get;
            set;
        }

        [DoNotAutomate]
        public List<ITashaMode> NonSharedModes { get; set; }

        [RunParameter( "Observed Mode Attachment Name", "ObservedMode", "The name of the attachment for the observed mode." )]
        public string ObservedModeAttachment { get; set; }

        [DoNotAutomate]
        public List<ITashaMode> OtherModes { get; set; }

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        public bool Parallel
        {
            get;
            set;
        }

        [RunParameter( "PM Rush Hour End Time", "18:30", typeof( Time ), "The rush hour end time in the afternoon" )]
        public Time PMRushEnd { get; set; }

        [RunParameter( "PM Rush Hour Start Time", "15:30", typeof( Time ), "The rush hour start time in the afternoon" )]
        public Time PMRushStart { get; set; }

        [DoNotAutomate]
        public List<IPostHousehold> PostHousehold { get; set; }

        [DoNotAutomate]
        public List<IPostIteration> PostIteration { get; set; }

        [DoNotAutomate]
        public List<ISelfContainedModule> PostRun { get; set; }

        [DoNotAutomate]
        public List<IPostScheduler> PostScheduler { get; set; }

        [DoNotAutomate]
        public List<IPreIteration> PreIteration { get; set; }

        [DoNotAutomate]
        public List<ISelfContainedModule> PreRun { get; set; }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 100, 50 ); }
        }

        public int RandomSeed
        {
            get;
            set;
        }

        [SubModelInformation( Description = "The available resources for this model system.", Required = false )]
        public List<IResource> Resources { get; set; }

        [DoNotAutomate]
        public List<ISharedMode> SharedModes { get; set; }

        public Time StartOfDay
        {
            get;
            set;
        }

        [DoNotAutomate]
        public List<IVehicleType> VehicleTypes { get; set; }

        [SubModelInformation( Description = "Zone System", Required = true )]
        public TMG.IZoneSystem ZoneSystem { get; set; }

        public ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime)
        {
            throw new NotImplementedException();
        }

        public bool ExitRequest()
        {
            return false;
        }

        public int GetIndexOfMode(ITashaMode mode)
        {
            throw new NotImplementedException();
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            this.ZoneSystem.LoadData();

            this.HouseholdLoader.LoadData();
            var hhlds = this.HouseholdLoader.ToArray();

            System.Threading.Tasks.Parallel.ForEach( AllModes, delegate(ITashaMode mode)
            {
                var AM = ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
                var PM = ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
                var FF = ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();

                this.Status = "Calculating All Modes";
                CreateData( hhlds, mode, AM, PM, FF );

                this.Status = "Writing to Files...";
                this.Progress = 0;
                WriteData( AM, matrixNumber, mode.ModeName + "AM.311" );
                this.Progress = (float)0.33;
                WriteData( PM, matrixNumber, mode.ModeName + "PM.311" );
                this.Progress = (float)0.66;
                WriteData( FF, matrixNumber, mode.ModeName + "FF.311" );
                this.Progress = (float)1;
            } );

            this.ZoneSystem.UnloadData();
        }

        public override string ToString()
        {
            return this.Status;
        }

        private void CreateData(ITashaHousehold[] hhlds, ITashaMode mode, SparseTwinIndex<float> AM, SparseTwinIndex<float> PM, SparseTwinIndex<float> FF)
        {
            var length = (float)hhlds.Length;
            this.Progress = 0;
            int count = 0;
            foreach ( var household in hhlds )
            {
                foreach ( var person in household.Persons )
                {
                    foreach ( var TripChain in person.TripChains )
                    {
                        foreach ( var Trip in TripChain.Trips )
                        {
                            if ( Trip[ObservedModeAttachment] as IMode == mode )
                            {
                                var Origin = Trip.OriginalZone.ZoneNumber;
                                var Destination = Trip.DestinationZone.ZoneNumber;

                                if ( Trip.TripStartTime < AMRushEnd && Trip.TripStartTime > AMRushStart )
                                {
                                    AM[Origin, Destination] += household.ExpansionFactor;
                                }
                                else if ( Trip.TripStartTime < PMRushEnd && Trip.TripStartTime > PMRushStart )
                                {
                                    PM[Origin, Destination] += household.ExpansionFactor;
                                }
                                else
                                {
                                    FF[Origin, Destination] += household.ExpansionFactor;
                                }
                            }
                        }
                    }
                }
                this.Progress = count++ / length;
            }
        }

        private void ToEmmeFloat(float p, StringBuilder builder)
        {
            builder.Clear();
            builder.Append( (int)p );
            p = p - (int)p;
            if ( p > 0 )
            {
                var integerSize = builder.Length;
                builder.Append( '.' );
                for ( int i = integerSize; i < 4; i++ )
                {
                    p = p * 10;
                    builder.Append( (int)p );
                    p = p - (int)p;
                    if ( p == 0 )
                    {
                        break;
                    }
                }
            }
        }

        private void WriteData(SparseTwinIndex<float> data, int matrixNumber, string fileName)
        {
            var zoneNumbers = data.ValidIndexArray();
            var flatData = data.GetFlatData();
            var numberOfZones = zoneNumbers.Length;
            using ( StreamWriter writer = new StreamWriter( fileName ) )
            {
                // We need to know what the head should look like.
                writer.WriteLine( "t matrices\r\nd matrix=mf{0}\r\na matrix=mf{0} name=drvtot default=incr descr=generated", matrixNumber );
                // Now that the header is in place we can start to generate all of the instructions
                StringBuilder[] builders = new StringBuilder[numberOfZones];
                System.Threading.Tasks.Parallel.For( 0, numberOfZones, delegate(int o)
                {
                    var build = builders[o] = new StringBuilder();
                    var strBuilder = new StringBuilder( 10 );
                    var convertedO = zoneNumbers[o];
                    for ( int d = 0; d < numberOfZones; d++ )
                    {
                        this.ToEmmeFloat( flatData[o][d], strBuilder );
                        build.AppendFormat( "{0,7:G}{1,7:G} {2,9:G}\r\n",
                            convertedO, zoneNumbers[d], strBuilder );
                    }
                } );
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    writer.Write( builders[i] );
                }
            }
        }
    }
}