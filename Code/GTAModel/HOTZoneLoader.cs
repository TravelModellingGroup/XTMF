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
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation(Description = "This module provides a loader for a zone system that includes planning districts "
    + "and separate data for employment information. This module does not have a restriction on what type of model system it can be in." )]
    public class HOTZoneLoader : IZoneSystem
    {
        [RunParameter("Employment File", "Employment.csv", "The file that contains the employment for each zone." )]
        public string EmploymentFile;

        [RunParameter("Generate Error If Zone Not Found", true, "Should we throw an exception if a zone record does not exist when mapping to planning districts?" )]
        public bool GeneratePDErrors;

        [SubModelInformation(Required = false, Description = "Origin contains the zone number, data contains the cost of parking." )]
        public IReadODData<float> ParkingCosts;

        [RunParameter("Planning District", "PlanningDistrict.csv", "The location of the file that contains the planning districts" )]
        public string PlanningDistrictFile;

        [RunParameter("Population File", "Population.csv", "The file that contains the population for each zone." )]
        public string PopulationFile;

        [SubModelInformation(Description = "Used to load in the region information", Required = false )]
        public IReadODData<float> ReadRegions;

        [RootModule]
        public IModelSystemTemplate Root;

        [RunParameter("Zone Attributes File", "Network/ZoneAttributes.csv", "A csv file containing 'Zone,Parking Cost,Parking Cap,InternalDistance,InternalArea" )]
        public string ZoneAttributesFile;

        [RunParameter("Zone File", "Zones.211", "The location of the file that contains the zone special information (.211 format)" )]
        public string ZoneFile;

        private SparseArray<IZone> ZoneData;

        public SparseTwinIndex<float> Distances { get; private set; }

        public string Name
        {
            get;
            set;
        }

        public int NumberOfExternalZones
        {
            get;
            internal set;
        }

        public int NumberOfInternalZones
        {
            get;
            internal set;
        }

        public int NumberOfZones
        {
            get { return ZoneData.Count; }
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [RunParameter("Roaming Zone Number", -1, "The zone number for a roaming place of work, -1 if there is none." )]
        public int RoamingZoneNumber
        {
            get;
            set;
        }

        public SparseArray<IZone> ZoneArray
        {
            get { return ZoneData; }
        }

        public IZone Get(int zoneNumber)
        {
            return ZoneData[zoneNumber];
        }

        public IZoneSystem GiveData()
        {
            return this;
        }

        public bool Loaded
        {
            get { return ZoneData != null; }
        }

        public void LoadData()
        {
            SparseArray<Node> nodes;
            using (Network network = new Network( GetFullPath( ZoneFile ) ) )
            {
                nodes = network.GetNodes();
            }
            var zones = InitializeZones( nodes );
            LoadPDs( zones );
            LoadPopulation( zones );
            LoadEmployment( zones );
            LoadAttributes( zones );
            LoadRegions( zones );
            ComputeDistances( zones );
            LoadParking( zones );
            NumberOfInternalZones = zones.Count;
            NumberOfExternalZones = 0;
            ZoneData = zones;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( !File.Exists( GetFullPath( ZoneFile ) ) )
            {
                error = string.Format( "The zone file '{0}' was not found.  Please check your input file and try again.", GetFullPath( ZoneFile ) );
                return false;
            }
            if ( !string.IsNullOrWhiteSpace( PlanningDistrictFile ) && !File.Exists( GetFullPath( PlanningDistrictFile ) ) )
            {
                error = string.Format( "The aggregation file '{0}' was not found.  Please check your input file and try again.", GetFullPath( PlanningDistrictFile ) );
                return false;
            }
            if ( !string.IsNullOrWhiteSpace( PopulationFile ) && !File.Exists( GetFullPath( PopulationFile ) ) )
            {
                error = string.Format( "The population file '{0}' was not found.  Please check your input file and try again.", GetFullPath( PopulationFile ) );
                return false;
            }
            return true;
        }

        public void UnloadData()
        {
            ZoneData = null;
        }

        public bool ZoneHasEmploymentData(IZone zone)
        {
            return false;
        }

        /// <summary>
        /// Calculate the distance between two zones
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <returns>The distance is meters</returns>
        private static float CalcDistance(IZone origin, IZone destination)
        {
            var deltaX = origin.X - destination.X;
            var deltaY = origin.Y - destination.Y;
            return (float)Math.Sqrt( ( deltaX * deltaX ) + ( deltaY * deltaY ) );
        }

        private static SparseArray<IZone> InitializeZones(SparseArray<Node> nodes)
        {
            var flatNodes = nodes.GetFlatData();
            List<int> centroids = new List<int>();
            for ( int i = 0; i < flatNodes.Length; i++ )
            {
                if ( flatNodes[i].IsCentroid )
                {
                    centroids.Add( flatNodes[i].Number );
                }
            }
            var centroidIndexes = centroids.ToArray();
            var flatzones = new IZone[centroidIndexes.Length];
            for ( int i = 0; i < centroidIndexes.Length; i++ )
            {
                HOTZone zone = new HOTZone();
                var node = nodes[centroidIndexes[i]];
                // update the centroid indexes to the sparse space
                zone.ZoneNumber = (short)centroidIndexes[i];
                zone.X = node.X;
                zone.Y = node.Y;
                flatzones[i] = zone;
            }
            return SparseArray<IZone>.CreateSparseArray( centroidIndexes, flatzones );
        }

        private void ComputeDistances(SparseArray<IZone> zoneSparseArray)
        {
            var distances = zoneSparseArray.CreateSquareTwinArray<float>();
            var flatDistnaces = distances.GetFlatData();
            var zones = zoneSparseArray.GetFlatData();
            var length = zones.Length;
            // build all of the distances in parallel
            Parallel.For( 0, length, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int i)
            {
                for ( int j = 0; j < length; j++ )
                {
                    flatDistnaces[i][j] = ( i == j ) ? zones[i].InternalDistance
                        : CalcDistance( zones[i], zones[j] );
                }
            } );
            Distances = distances;
        }

        private string GetFullPath(string localPath)
        {
            var fullPath = localPath;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( Root.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        private void LoadAttributes(SparseArray<IZone> zones)
        {
            // optional
            if ( string.IsNullOrWhiteSpace( ZoneAttributesFile ) ) return;
            try
            {
                using (CommentedCsvReader reader = new CommentedCsvReader( GetFullPath( ZoneAttributesFile ) ) )
                {
                    while ( reader.NextLine() )
                    {
                        var colRead = reader.NumberOfCurrentCells;
                        if ( colRead < 5 )
                        {
                            continue;
                        }
                        int zoneNumber;
                        float parkingCost, parkingCap, intraZoneDistance, area;

                        reader.Get( out zoneNumber, 0 );
                        reader.Get( out parkingCost, 1 );
                        reader.Get( out parkingCap, 2 );
                        reader.Get( out intraZoneDistance, 3 );
                        reader.Get( out area, 4 );
                        var zone = zones[zoneNumber];
                        if ( zone == null )
                        {
                            if ( GeneratePDErrors )
                            {
                                throw new XTMFRuntimeException( "The planning district file contained a zone " + zoneNumber + " however the zone file did not contain this zone." );
                            }
                        }
                        else
                        {
                            zone.ParkingCost = parkingCost;
                            zone.InternalDistance = intraZoneDistance;
                            zone.InternalArea = area;
                        }
                    }
                }
            }
            catch (IOException)
            {
                throw new XTMFRuntimeException( "Please make sure that the file " + GetFullPath( ZoneAttributesFile ) + " exists and is not being used by any other program." );
            }
        }

        private void LoadEmployment(SparseArray<IZone> zones)
        {
            if ( string.IsNullOrWhiteSpace( EmploymentFile ) ) return;
            try
            {
                using (CommentedCsvReader reader = new CommentedCsvReader( GetFullPath( EmploymentFile ) ) )
                {
                    while ( reader.NextLine() )
                    {
                        var colRead = reader.NumberOfCurrentCells;
                        if ( colRead < 2 )
                        {
                            continue;
                        }
                        int zoneNumber;
                        int employment;
                        reader.Get( out zoneNumber, 0 );
                        reader.Get( out employment, 1 );
                        var zone = zones[zoneNumber];
                        if ( zone == null )
                        {
                            if ( GeneratePDErrors )
                            {
                                throw new XTMFRuntimeException( "When loading the Employment we found a distribution for a zone "
                                + zoneNumber + " however that zone does not exist!" );
                            }
                        }
                        else
                        {
                            zone.Employment = employment;
                            zone.TotalEmployment = employment;
                        }
                    }
                }
            }
            catch (IOException)
            {
                throw new XTMFRuntimeException( "Please make sure that the file " + GetFullPath( EmploymentFile ) + " exists and is not being used by any other program." );
            }
        }

        private void LoadParking(SparseArray<IZone> zones)
        {
            if ( ParkingCosts == null ) return;
            foreach ( var point in ParkingCosts.Read() )
            {
                var origin = point.O;
                if ( zones.ContainsIndex( origin ) )
                {
                    var z = zones[origin];
                    z.ParkingCost = point.Data;
                }
            }
        }

        private void LoadPDs(SparseArray<IZone> zones)
        {
            if ( string.IsNullOrWhiteSpace( PlanningDistrictFile ) ) return;
            try
            {
                using (CommentedCsvReader reader = new CommentedCsvReader( GetFullPath( PlanningDistrictFile ) ) )
                {
                    while ( reader.NextLine() )
                    {
                        var colRead = reader.NumberOfCurrentCells;
                        if ( colRead < 2 )
                        {
                            continue;
                        }
                        int zoneNumber;
                        int pd;
                        reader.Get( out zoneNumber, 0 );
                        reader.Get( out pd, 1 );
                        var zone = zones[zoneNumber];
                        if ( zone == null )
                        {
                            if ( GeneratePDErrors )
                            {
                                throw new XTMFRuntimeException( "The planning district file contained a zone " + zoneNumber + " however the zone file did not contain this zone." );
                            }
                        }
                        else
                        {
                            zone.PlanningDistrict = pd;
                        }
                    }
                }
            }
            catch (IOException)
            {
                throw new XTMFRuntimeException( "Please make sure that the file " + GetFullPath( PlanningDistrictFile ) + " exists and is not being used by any other program." );
            }
        }

        private void LoadPopulation(SparseArray<IZone> zones)
        {
            if ( string.IsNullOrWhiteSpace( PopulationFile ) ) return;
            try
            {
                using (CommentedCsvReader reader = new CommentedCsvReader( GetFullPath( PopulationFile ) ) )
                {
                    while ( reader.NextLine() )
                    {
                        var colRead = reader.NumberOfCurrentCells;
                        if ( colRead < 2 )
                        {
                            continue;
                        }
                        int zoneNumber;
                        int population;
                        reader.Get( out zoneNumber, 0 );
                        reader.Get( out population, 1 );
                        var zone = zones[zoneNumber];

                        if ( zone == null )
                        {
                            if ( GeneratePDErrors )
                            {
                                throw new XTMFRuntimeException( "When loading the population we found a distribution for a zone "
                                + zoneNumber + " however that zone does not exist!" );
                            }
                        }
                        else
                        {
                            zone.Population = population;
                        }
                    }
                }
            }
            catch (IOException)
            {
                throw new XTMFRuntimeException( "Please make sure that the file " + GetFullPath( PopulationFile ) + " exists and is not being used by any other program." );
            }
        }

        private void LoadRegions(SparseArray<IZone> zones)
        {
            // if there is nothing to load the regions with just don't assign them to anything
            if ( ReadRegions == null ) return;
            foreach ( var data in ReadRegions.Read() )
            {
                var zone = zones[data.O];
                if ( zone != null )
                {
                    zone.RegionNumber = (int)data.Data;
                }
            }
        }
    }
}