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
using System.Threading;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    public class ODFloatDataSource : IODDataSource<float>
    {
        [SubModelInformation( Description = "The data source for this module.", Required = true )]
        public IDataSource<SparseTriIndex<float>> DataSource;

        [RunParameter( "Default Value", 0f, "The value to use if the data does not exist." )]
        public float DefaultValue;

        [RunParameter( "Reason Offset", 0, "Offset the reason variables to help match to the data." )]
        public int ReasonOffset;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Use Planning Districts", true, "The given data references planning districts." )]
        public bool UsePlanningDistricts;

        private SparseTriIndex<float> Data;

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

        public float GetDataFrom(int origin, int destination, int reason = 0)
        {
            EnsureDataIsLoaded();
            reason += this.ReasonOffset;
            if ( this.UsePlanningDistricts )
            {
                ConvertToPlanningDistricts( ref origin, ref destination );
            }
            if ( !Data.ContainsIndex( origin, destination, reason ) )
            {
                return this.DefaultValue;
            }
            var res = Data[origin, destination, reason];
            return res;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void ConvertToPlanningDistricts(ref int origin, ref int destination)
        {
            var zoneArray = this.Root.ZoneSystem.ZoneArray;
            origin = GetPlanningDistrict( zoneArray, origin );
            destination = GetPlanningDistrict( zoneArray, destination );
        }

        private void EnsureDataIsLoaded()
        {
            if ( Data == null )
            {
                lock ( this )
                {
                    Thread.MemoryBarrier();
                    if ( Data == null )
                    {
                        LoadData();
                        Thread.MemoryBarrier();
                    }
                }
            }
        }

        private int GetPlanningDistrict(SparseArray<IZone> zoneArray, int zoneNumber)
        {
            var zone = zoneArray[zoneNumber];
            if ( zone == null )
            {
                throw new XTMFRuntimeException( "In '" + this.Name + "' we were unable to find a zone with the zone number '" + zoneNumber + "'. Please make sure that this zone exists!" );
            }
            return zone.PlanningDistrict;
        }

        private void LoadData()
        {
            this.DataSource.LoadData();
            this.Data = this.DataSource.GiveData();
            this.DataSource.UnloadData();
        }
    }
}