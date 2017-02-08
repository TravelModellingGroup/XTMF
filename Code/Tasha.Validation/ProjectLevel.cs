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
using Tasha.Common;
using XTMF;

namespace Tasha.Validation
{
    [ModuleInformation(
        Description = "This module is used as a validation step to ensure that a logical amount of trip " +
                        "purposes are being scheduled after the scheduler is complete. It counts both the frequency of " +
                        "trip purposes and the summation of the respective expansion factors."
        )]
    public class ProjectLevel : IPostScheduler, IDisposable
    {
        public Dictionary<string, int> NumberDict = new Dictionary<string, int>();
        public Dictionary<string, float> ResultDict = new Dictionary<string, float>();

        [RunParameter( "File Name", "ProjectResults.csv", "The file that we will store the results into." )]
        public string ResultsFileName;

        [RootModule]
        public ITashaRuntime Root;

        private StreamWriter Writer;

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
            get { return null; }
        }

        public void Execute(ITashaHousehold household)
        {
            lock ( this )
            {
                var householdData = (Tasha.Scheduler.SchedHouseholdData) household["SData"];

                ResultDict["JointMarket"] += household.ExpansionFactor * householdData.JointMarketProject.Schedule.EpisodeCount;
                NumberDict["JointMarket"] += householdData.JointMarketProject.Schedule.EpisodeCount;

                ResultDict["JointOther"] += household.ExpansionFactor * householdData.JointOtherProject.Schedule.EpisodeCount;
                NumberDict["JointOther"] += householdData.JointOtherProject.Schedule.EpisodeCount;

                foreach ( var person in household.Persons )
                {
                    var personData = (Tasha.Scheduler.SchedulerPersonData) person["SData"];
                    var workSched = personData.WorkSchedule.Schedule;
                    var schoolSched = personData.SchoolSchedule.Schedule;
                    var marketSched = personData.MarketSchedule.Schedule;
                    var otherSched = personData.OtherSchedule.Schedule;

                    ResultDict["Work"] += household.ExpansionFactor * workSched.EpisodeCount;
                    NumberDict["Work"] += workSched.EpisodeCount;

                    ResultDict["School"] += schoolSched.EpisodeCount * household.ExpansionFactor;
                    NumberDict["School"] += schoolSched.EpisodeCount;

                    ResultDict["Market"] += marketSched.EpisodeCount * household.ExpansionFactor;
                    NumberDict["Market"] += marketSched.EpisodeCount;

                    ResultDict["Other"] += household.ExpansionFactor * otherSched.EpisodeCount;
                    NumberDict["Other"] += otherSched.EpisodeCount;
                }
            }
        }

        public void IterationFinished(int iterationNumber)
        {
            lock ( this )
            {
                Writer.WriteLine( "Trip Purpose, Summation of Expansion Factors" );
                foreach ( var key in ResultDict.Keys )
                {
                    Writer.WriteLine( "{0},{1}", key, ResultDict[key] );
                }

                Writer.WriteLine( " \nTrip Purpose, Frequency" );
                foreach ( var key1 in NumberDict.Keys )
                {
                    Writer.WriteLine( "{0},{1}", key1, NumberDict[key1] );
                }
                Dispose( true );
            }
        }

        public void Load(int maxIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void IterationStarting(int iteration)
        {
            lock ( this )
            {
                ResultDict.Clear();
                NumberDict.Clear();
                Writer = new StreamWriter( ResultsFileName, true );
                //Writer.WriteLine( "HouseholdNum, PersonNum, Activity Type, Original Duration, Duration, Expansion Factor" );
                ResultDict.Add( "Work", 0 );
                ResultDict.Add( "School", 0 );
                ResultDict.Add( "Other", 0 );
                ResultDict.Add( "Market", 0 );
                ResultDict.Add( "JointMarket", 0 );
                ResultDict.Add( "JointOther", 0 );
                foreach ( var key in ResultDict.Keys )
                {
                    NumberDict.Add( key, 0 );
                }
            }
        }

        public void Dispose()
        {
            Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose(bool all)
        {
            if ( Writer != null )
            {
                Writer.Dispose();
                Writer = null;
            }
        }
    }
}