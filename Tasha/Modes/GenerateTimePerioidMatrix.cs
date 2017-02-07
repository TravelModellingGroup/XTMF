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
using Datastructure;
using Tasha.Common;
using XTMF;

namespace Tasha.Modes
{
    [ModuleInformation( Name = "Generate Time Period Factor Matrices",
                        Description = "Generates 4x4 TPF matrices from TTS data. Any trips which fall outside the definition of the three time periods will be considered 'offpeak'" )]
    public class GenerateTimePerioidMatrix : IPostHousehold
    {
        [RunParameter( "Afternoon Period Definition", "1500-1829", typeof( RangeSet ), "RANGE of afternnon peak period, in TTS-formatted hours (e.g. 400-2800)." )]
        public RangeSet AfternoonTimePeriod;

        [RunParameter( "Allowed Deviations", 0, "[Incomplete] The maximum number of acceptable non-primary activities which agents can insert into thei trip-chains." )]
        public int Degrees;

        [RunParameter( "Home Anchor Override", "", "The name of the variable used to store an agent's initial activity. If blank, this will default to 'Home'" )]
        public string HomeAnchorOverrideName;

        [RunParameter( "Midday Period Definition", "900-1500", typeof( RangeSet ), "RANGE of midday time period, in TTS-formatted hours (e.g. 400-2800)." )]
        public RangeSet MiddayTimePeriod;

        [RunParameter( "Morning Period Definition", "600-859", typeof( RangeSet ), "RANGE of morning peak period, in TTS-formatted hours (e.g. 400-2800)." )]
        public RangeSet MorningTimePeriod;

        [RunParameter( "Results File", "tpfResults.txt", "The file to save result matrices into." )]
        public string ResultsFile;

        [RootModule]
        public ITashaRuntime Root;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );
        private Dictionary<Activity, string> classifier;
        private Dictionary<string, float[,]> schoolMatrices;
        private Dictionary<string, float[,]> workMatrices;

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
            get { return _ProgressColour; }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock ( this )
            {
                foreach ( var p in household.Persons )
                {
                    if ( p.TripChains.Count < 1 )
                        continue; //Skip people with no trips

                    string key = p.Occupation.ToString() + "," + p.EmploymentStatus.ToString() + "," + p.StudentStatus.ToString(); //The key to determine which table the person's trips belong to.

                    string prevAct;
                    if ( string.IsNullOrEmpty( HomeAnchorOverrideName ) )
                    {
                        prevAct = Activity.Home.ToString();
                    }
                    else
                    {
                        var x = p.TripChains[0].GetVariable( HomeAnchorOverrideName );
                        if ( x != null ) prevAct = x.ToString();
                        else prevAct = Activity.Home.ToString();
                    }

                    var trips = p.TripChains[0].Trips; //Assumes that each person has at most one trip chain.
                    Time outgoingTripTime = trips[0].TripStartTime;

                    int workActCounter = 0;
                    int schoolActCounter = 0;

                    foreach ( var trip in p.TripChains[0].Trips )
                    {
                        var nextAct = trip.Purpose;

                        if ( prevAct == Activity.Home.ToString() ) //Starting from home
                        {
                            if ( nextAct == Activity.PrimaryWork || nextAct == Activity.SecondaryWork || nextAct == Activity.WorkBasedBusiness )
                            {
                                //Outgoing work trip
                                outgoingTripTime = trip.TripStartTime;
                                workActCounter++;
                            }
                            else if ( nextAct == Activity.School )
                            {
                                //Outgoing school trip
                                outgoingTripTime = trip.TripStartTime;
                                schoolActCounter++;
                            }
                        }
                        else if ( nextAct == Activity.Home && ( workActCounter > 0 || schoolActCounter > 0 ) ) //Ending at home
                        {
                            Time incomingTripTime = trip.TripStartTime;

                            //Save tour in matrix
                            if ( ( workActCounter <= ( 1 + Degrees ) ) && ( schoolActCounter <= ( Degrees + 1 ) ) ) //Only if there were fewer deviations than allowed.
                            {
                                float[,] matrix = null;
                                if ( !workMatrices.ContainsKey( key ) ) //Check if this specific key has already been mapped.
                                {
                                    matrix = new float[4, 4];
                                    matrix[_getTimePeriod( outgoingTripTime ), _getTimePeriod( incomingTripTime )] = household.ExpansionFactor;
                                    workMatrices.Add( key, matrix );
                                }
                                else
                                {
                                    workMatrices.TryGetValue( key, out matrix );
                                    matrix[_getTimePeriod( outgoingTripTime ), _getTimePeriod( incomingTripTime )] += household.ExpansionFactor;
                                }
                                //matrix[_getTimePeriod(outgoingTripTime), _getTimePeriod(incomingTripTime)] += household.ExpansionFactor;
                            }

                            //Reset counters
                            workActCounter = 0;
                            schoolActCounter = 0;
                        }
                        else //Otherwise
                        {
                            workActCounter += ( workActCounter > 0 ) ? 1 : 0;
                            schoolActCounter += ( schoolActCounter > 0 ) ? 1 : 0;
                        }

                        prevAct = nextAct.ToString();
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            var path = ResultsFile;

            using ( StreamWriter sw = new StreamWriter( path ) )
            {
                sw.WriteLine( "Time Period Matrices" );
                sw.WriteLine();
                sw.WriteLine( "Morning Period [0]: " + MorningTimePeriod.ToString() );
                sw.WriteLine( "Midday Period [1]: " + MiddayTimePeriod.ToString() );
                sw.WriteLine( "Afternoon Period [2]: " + AfternoonTimePeriod.ToString() );
                sw.WriteLine( "Offpeak [3]" );
                sw.WriteLine();
                sw.WriteLine( "Table Names = [Occupation], [Employment Status], [Student Status]" );

                foreach ( var e in workMatrices )
                {
                    sw.WriteLine();
                    var table = e.Value;
                    sw.WriteLine( "Table: '" + e.Key + "':" );
                    for ( int i = 0; i < 4; i++ )
                    {
                        string s = "";
                        for ( int j = 0; j < 4; j++ )
                        {
                            s += "\t" + table[i, j];
                        }
                        sw.WriteLine( s );
                    }
                }
            }
        }

        public void Load(int maxIterations)
        {
            workMatrices = new Dictionary<string, float[,]>();
            schoolMatrices = new Dictionary<string, float[,]>();
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( MorningTimePeriod.Overlaps( MiddayTimePeriod ) )
            {
                error = "Morning period overlaps midday period!";
                return false;
            }
            else if ( MorningTimePeriod.Overlaps( AfternoonTimePeriod ) )
            {
                error = "Morning period overlaps afternoon period!";
                return false;
            }
            else if ( MiddayTimePeriod.Overlaps( AfternoonTimePeriod ) )
            {
                error = "Midday period overlaps afteroon period!";
                return false;
            }

            return true;
        }

        public void IterationStarting(int iteration)
        {
            //throw new NotImplementedException();
        }

        private int _classifyTripChain(ITripChain chain)
        {
            // Returns 1 if H-W-H or W-H-W trip, 2 if H-S-H or S-H-S, 0 otherwise.

            string prevAct = "";
            if ( !string.IsNullOrEmpty( HomeAnchorOverrideName ) )
            {
                var x = chain.GetVariable( HomeAnchorOverrideName );
                if ( x != null )
                {
                    var q = (Activity)x;
                    prevAct = classifier[q];
                }
            }
            if ( prevAct == "" )
                prevAct = "H";

            int homeWorkCount = 0;
            int homeSchoolCount = 0;

            foreach ( var trip in chain.Trips )
            {
                string nextAct = classifier[trip.Purpose];

                if ( homeWorkCount > 0 )
                {
                    if ( prevAct == "W" && nextAct == "H" )
                    {
                        if ( homeWorkCount <= ( Degrees + 1 ) )
                            return 1;
                        homeWorkCount = 0;
                    }
                    else
                    {
                        homeWorkCount++;
                    }
                }

                if ( homeSchoolCount > 0 )
                {
                    if ( prevAct == "S" && nextAct == "H" )
                    {
                        if ( homeSchoolCount <= ( Degrees + 1 ) )
                            return 2;
                        homeSchoolCount = 0;
                    }
                    else
                    {
                        homeSchoolCount++;
                    }
                }

                if ( prevAct == "H" && nextAct == "W" )
                    homeWorkCount = 1;
            }

            return 0;
        }

        private int _getTimePeriod(Time tTime)
        {
            int iTime = tTime.Hours * 100 + tTime.Minutes;

            if ( iTime > 2800 )
                throw new XTMFRuntimeException( "Cannot have a time of more than 28:00!" );

            if ( MorningTimePeriod.Contains( iTime ) )
                return 0;
            else if ( MiddayTimePeriod.Contains( iTime ) )
                return 1;
            else if ( AfternoonTimePeriod.Contains( iTime ) )
                return 2;

            return 3;
        }

        private void _loadClassifier()
        {
            classifier = new Dictionary<Activity, string>();
            classifier[Activity.Daycare] = "O";
            classifier[Activity.Dropoff] = "F";
            classifier[Activity.DropoffAndReturn] = "F";
            classifier[Activity.FacilitatePassenger] = "F";
            classifier[Activity.Home] = "H";
            classifier[Activity.IndividualOther] = "O";
            classifier[Activity.Intermediate] = "0";
            classifier[Activity.JointOther] = "J";
            classifier[Activity.Market] = "M";
            classifier[Activity.JointMarket] = "JM";
            classifier[Activity.NullActivity] = "0";
            classifier[Activity.Pickup] = "F";
            classifier[Activity.PickupAndReturn] = "F";
            classifier[Activity.PrimaryWork] = "W";
            classifier[Activity.ReturnFromSchool] = "S";
            classifier[Activity.ReturnFromWork] = "W";
            classifier[Activity.School] = "S";
            classifier[Activity.SecondaryWork] = "W";
            classifier[Activity.ServeDespendents] = "F";
            classifier[Activity.StayAtHome] = "H";
            classifier[Activity.Travel] = "O";
            classifier[Activity.Unknown] = "O";
            classifier[Activity.WorkAtHomeBusiness] = "HW"; //Given its own classification so as not to be confused with Work or Home.
            classifier[Activity.WorkBasedBusiness] = "W";
        }
    }
}