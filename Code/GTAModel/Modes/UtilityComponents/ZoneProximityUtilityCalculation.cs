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
using Datastructure;
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.Modes.UtilityComponents
{
    [ModuleInformation( Description = "Provides the ability to add a constant given proximity to a set of zones." )]
    public class ZoneProximityUtilityCalculation : IUtilityComponent
    {
        [RunParameter( "Constant", 0f, "The value to add when close enough to a given zone?" )]
        public float Constant;

        [RunParameter( "Max Distance", 1000f, "The maximum distance to be to add the constant, in metres." )]
        public float MaxDistance;

        [RunParameter( "Origin Based", true, "Should we test against the origin zone.  If false the destination will be used." )]
        public bool Origin;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Zone Numbers", "6000-6999", typeof( RangeSet ), "The zone numbers that represent the locations we will test against." )]
        public RangeSet TargetedZones;

        private SparseArray<bool> ProximityCache;

        public string Name { get; set; }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [RunParameter( "Component Name", "", "The name of this Utility Component.  This name should be unique for each mode." )]
        public string UtilityComponentName { get; set; }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            if ( ProximityCache == null )
            {
                lock ( this )
                {
                    Thread.MemoryBarrier();
                    if ( ProximityCache == null )
                    {
                        LoadCache();
                        Thread.MemoryBarrier();
                    }
                }
            }
            return ProximityCache[Origin ? origin.ZoneNumber : destination.ZoneNumber] ? this.Constant : 0f;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private float CheckOWS(IZone origin)
        {
            if ( ProximityCache == null )
            {
                lock ( this )
                {
                    Thread.MemoryBarrier();
                    if ( ProximityCache == null )
                    {
                        LoadCache();
                        Thread.MemoryBarrier();
                    }
                }
            }
            return ProximityCache[origin.ZoneNumber] ? Constant : 0f;
        }

        private void FindCloseZonesToTargets(IZone[] flatZones, bool[] flatData, List<int> targetedZones)
        {
            var distances = this.Root.ZoneSystem.Distances.GetFlatData();
            var numberOfSubwayZones = targetedZones.Count;
            for ( int i = 0; i < flatZones.Length; i++ )
            {
                bool any = false;
                for ( int j = 0; j < numberOfSubwayZones; j++ )
                {
                    if ( distances[i][targetedZones[j]] < this.MaxDistance )
                    {
                        any = true;
                        break;
                    }
                }
                flatData[i] = any;
            }
        }

        private List<int> GetTargetedZones(IZone[] flatZones, bool[] flatData)
        {
            List<int> targetedZones = new List<int>( flatData.Length );
            for ( int i = 0; i < flatZones.Length; i++ )
            {
                if ( this.TargetedZones.Contains( flatZones[i].ZoneNumber ) )
                {
                    targetedZones.Add( i );
                }
            }
            return targetedZones;
        }

        private void LoadCache()
        {
            var zoneArray = this.Root.ZoneSystem.ZoneArray;
            var flatZones = zoneArray.GetFlatData();
            var temp = zoneArray.CreateSimilarArray<bool>();
            var flatData = temp.GetFlatData();
            var subwayZones = GetTargetedZones( flatZones, flatData );
            FindCloseZonesToTargets( flatZones, flatData, subwayZones );
            this.ProximityCache = temp;
        }
    }
}