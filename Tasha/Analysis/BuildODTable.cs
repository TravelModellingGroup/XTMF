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
using Tasha.Common;
using TMG;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace Tasha.Analysis
{
    public class BuildODTable : IPostHousehold
    {
        [RunParameter( "End Time", "28:00", typeof( Time ), "The ending time of this collection." )]
        public Time EndTime;

        [RunParameter( "Mode Names", "Auto", "The name of the modes that we will extract" )]
        public string ModeNames;

        [RunParameter( "Output File Name", "Matrix.csv", "The name of the output file for this ODTable." )]
        public FileFromOutputDirectory OutputFileName;

        [RootModule]
        public ITashaRuntime Root;

        [RunParameter( "Start Time", "4:00", typeof( Time ), "The starting time of this collection." )]
        public Time StartTime;

        private float[][] Data;

        private IMode[] Modes;

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

        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Reliability", "CA2002:DoNotLockOnObjectsWithWeakIdentity" )]
        public void Execute(ITashaHousehold household, int iteration)
        {
            var zones = this.Root.ZoneSystem.ZoneArray;
            var persons = household.Persons;
            var expFactor = household.ExpansionFactor;
            for ( int personIndex = 0; personIndex < persons.Length; personIndex++ )
            {
                var tripChains = persons[personIndex].TripChains;
                for ( int tcIndex = 0; tcIndex < tripChains.Count; tcIndex++ )
                {
                    var tripChain = tripChains[tcIndex].Trips;
                    for ( int trip = 0; trip < tripChain.Count; trip++ )
                    {
                        var t = tripChain[trip];
                        if ( !( t.TripStartTime >= this.StartTime && t.TripStartTime <= this.EndTime ) )
                        {
                            continue;
                        }
                        var origin = zones.GetFlatIndex( t.OriginalZone.ZoneNumber );
                        var destination = zones.GetFlatIndex( t.DestinationZone.ZoneNumber );
                        foreach ( var chosenMode in t.ModesChosen )
                        {
                            int index;
                            if ( ( index = IndexOf( this.Modes, chosenMode ) ) >= 0 )
                            {
                                lock ( this.Data[origin] )
                                {
                                    this.Data[origin][destination] += expFactor;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            SaveData.SaveMatrix( zones, this.Data, this.OutputFileName.GetFileName() );
        }

        public void Load(int maxIterations)
        {
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            this.Data = new float[zones.Length][];
            for ( int i = 0; i < this.Data.Length; i++ )
            {
                this.Data[i] = new float[zones.Length];
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            var modes = this.Root.AllModes;
            var modeNames = this.ModeNames.Split( ',' );
            this.Modes = new IMode[modeNames.Length];
            foreach ( var mode in modes )
            {
                for ( int i = 0; i < modeNames.Length; i++ )
                {
                    if ( mode.ModeName == modeNames[i] )
                    {
                        this.Modes[i] = mode;
                    }
                }
            }
            for ( int i = 0; i < this.Modes.Length; i++ )
            {
                if ( this.Modes[i] == null )
                {
                    error = "In '" + this.Name
                        + "' we were unable to find a mode called '" + modeNames[i] + "'";
                    return false;
                }
            }
            return true;
        }

        public void IterationStarting(int iteration)
        {
            for ( int i = 0; i < this.Data.Length; i++ )
            {
                Array.Clear( this.Data[i], 0, this.Data[i].Length );
            }
        }

        private static int IndexOf<K>(K[] array, K data) where K : class
        {
            for ( int i = 0; i < array.Length; i++ )
            {
                if ( data == array[i] )
                {
                    return i;
                }
            }
            return -1;
        }
    }
}