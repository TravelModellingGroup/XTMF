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
using System.Linq;
using System.Text;
using System.IO;
using XTMF;
using TMG;
using Tasha.Common;
using Datastructure;

namespace Beijing
{
    public class BuildTripMatrix : ITashaRuntime
    {

        [SubModelInformation( Description = "The available resources for this model system.", Required = false )]
        public List<IResource> Resources { get; set; }

        [SubModelInformation( Description = "The loader of the households", Required = true )]
        public IDataLoader<ITashaHousehold> HouseholdLoader
        {
            get;
            set;
        }

        [SubModelInformation( Description = "The auto vehicle type", Required = true )]
        public IVehicleType AutoType
        {
            get;
            set;
        }

        [SubModelInformation( Description = "All of the modes", Required = false )]
        public List<ITashaMode> AllModes
        {
            get;
            set;
        }

        [SubModelInformation( Description = "Our Zone System", Required = true )]
        public IZoneSystem ZoneSystem
        {
            get;
            set;
        }

        [SubModelInformation( Description = "The network information", Required = false )]
        public IList<INetworkData> NetworkData
        {
            get;
            set;
        }

        [RunParameter( "District Radius File", "TravelTimeBaseData/18districtradius.csv", "A CSV file containing districts and their radius." )]
        public string DistrictRadiusFile;

        [RunParameter( "Observed Mode", "ObservedMode", "The name of the attachment for observed modes for trips." )]
        public string ObservedModeString;

        [RunParameter( "311 Format", false, "Should we save the data in .311 format or as a 2D csv? (true for .311)" )]
        public bool Format311;

        [RunParameter( "Matrix Numbers", "12,5,36", "The name of the attachment for observed modes for trips." )]
        public string MatrixNumbers;

        [RunParameter( "Input Base Directory", "../../Input", "The base location of our input" )]
        public string InputBaseDirectory { get; set; }

        public string OutputBaseDirectory { get; set; }

        public bool ExitRequest()
        {
            return false;
        }

        SparseTwinIndex<float>[][] TripCountMatrixes;
        SparseTwinIndex<float>[][] TripAvgTimeMatrixes;
        private string Status = "Building 295 matrix";

        /// <summary>
        /// The program starts here
        /// </summary>
        public void Start()
        {
            this.ZoneSystem.LoadData();
            this.HouseholdLoader.LoadData();
            this.Status = "Loading households";
            ITashaHousehold[] allHouseholds = this.HouseholdLoader.ToArray();
            this.Status = "Initializing data";
            this.InitializeData();
            this.Status = "Loading Matricies with base data";
            this.AddToMatrix( allHouseholds );
            this.Status = "Producing Averages";
            this.CalculateAverage();
            // new code
            this.Status = "Sanitizing Travel Times1";
            this.EuclideanFilter( false );
            this.Status = "Filling IntraDistrict Travel Times";
            this.FillRatioIntraZonalTravelTime();
            this.Status = "Filling Zone to District";
            this.ZoneToOtherDistrictAverage();
            this.Status = "Filling District to Zone";
            this.DistrictToZoneAverage();
            this.Status = "Loading Google Maps Data";
            this.WorstCaseDistrictToDistrictTimes();
            this.Status = "Applying Heavy Weight to unknown times";
            this.ApplyHeavyWeight();
            this.Status = "Sanitizing Travel Times2";
            this.EuclideanFilter( true );
            // output
            this.Status = "Outputting data";
            this.OutputMatricies();
            this.Status = "Unloading zones";
            this.ZoneSystem.UnloadData();
            this.Status = "Complete";
        }

        private void ApplyHeavyWeight()
        {
            var numberOfModes = this.AllModes.Count;
            var flatZones = this.ZoneSystem.ZoneArray.GetFlatData();
            for ( int mode = 0; mode < numberOfModes; mode++ )
            {
                for ( int time = 0; time < 3; time++ )
                {
                    var data = this.TripAvgTimeMatrixes[mode][time].GetFlatData();
                    for ( int i = 0; i < flatZones.Length; i++ )
                    {
                        for ( int j = 0; j < flatZones.Length; j++ )
                        {
                            if ( data[i][j] == 0 )
                            {
                                data[i][j] = 999;
                            }
                        }
                    }
                }
            }
        }

        private void WorstCaseDistrictToDistrictTimes()
        {
            int mode = 0;
            var flatZones = this.ZoneSystem.ZoneArray.GetFlatData();
            var timeArray = new Time[] { Six, ThreeThirty, new Time() { Hours = 12 } };
            var origin = new BadZone();
            var destination = new BadZone();
            foreach ( var dataSource in this.NetworkData )
            {
                dataSource.LoadData();
                for ( int time = 0; time < 3; time++ )
                {
                    Time timeObj = timeArray[time];

                    var data = this.TripAvgTimeMatrixes[mode][time].GetFlatData();
                    for ( int i = 0; i < flatZones.Length; i++ )
                    {
                        for ( int j = 0; j < flatZones.Length; j++ )
                        {
                            if ( data[i][j] == 0 )
                            {
                                origin.ZoneNumber = flatZones[i].PlanningDistrict;
                                destination.ZoneNumber = flatZones[j].PlanningDistrict;
                                data[i][j] = dataSource.TravelTime( origin, destination, timeObj ).ToMinutes();
                            }
                        }
                    }
                }
                dataSource.UnloadData();
                mode++;
            }
        }

        private void DistrictToZoneAverage()
        {
            var flatZones = this.ZoneSystem.ZoneArray.GetFlatData();
            var length = flatZones.Length;
            var modes = this.AllModes;
            var numberOfModes = modes.Count;
            SparseArray<float> districtRadius = BuildDistrictRadius();
            var districts = districtRadius.ValidIndexArray();
            for ( int time = 0; time < 3; time++ )
            {
                for ( int mode = 0; mode < numberOfModes; mode++ )
                {
                    var matrix = this.TripAvgTimeMatrixes[mode][time];
                    var data = matrix.GetFlatData();
                    for ( int i = 0; i < length; i++ )
                    {
                        // for each i to another place
                        var districtI = flatZones[i].PlanningDistrict;
                        for ( int j = 0; j < length; j++ )
                        {
                            var districtJ = flatZones[j].PlanningDistrict;
                            // make sure we are not within the same district
                            if ( districtI == districtJ ) continue;
                            if ( data[i][j] == 0 )
                            {
                                // if we do not have any data already, compute the average if any trips exist
                                var average = 0.0;
                                int count = 0;
                                for ( int k = 0; k < length; k++ )
                                {
                                    var districtK = flatZones[k].PlanningDistrict;
                                    if ( districtI == districtK )
                                    {
                                        if ( data[k][j] > 0 )
                                        {
                                            average += data[k][j];
                                            count++;
                                        }
                                    }
                                }
                                average /= count;
                                if ( count != 0 )
                                {
                                    data[i][j] = (float)average;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ZoneToOtherDistrictAverage()
        {
            var flatZones = this.ZoneSystem.ZoneArray.GetFlatData();
            var length = flatZones.Length;
            var modes = this.AllModes;
            var numberOfModes = modes.Count;
            SparseArray<float> districtRadius = BuildDistrictRadius();
            var districts = districtRadius.ValidIndexArray();
            for ( int time = 0; time < 3; time++ )
            {
                for ( int mode = 0; mode < numberOfModes; mode++ )
                {
                    var matrix = this.TripAvgTimeMatrixes[mode][time];
                    var data = matrix.GetFlatData();
                    for ( int i = 0; i < length; i++ )
                    {
                        // for each i to another place
                        var districtI = flatZones[i].PlanningDistrict;
                        for ( int j = 0; j < length; j++ )
                        {
                            var districtJ = flatZones[j].PlanningDistrict;
                            // make sure we are not within the same district
                            if ( districtI == districtJ ) continue;
                            if ( data[i][j] == 0 )
                            {
                                // if we do not have any data already, compute the average if any trips exist
                                var average = 0.0;
                                int count = 0;
                                for ( int k = 0; k < length; k++ )
                                {
                                    var districtK = flatZones[k].PlanningDistrict;
                                    if ( districtJ == districtK )
                                    {
                                        if ( data[i][k] > 0 )
                                        {
                                            average += data[i][k];
                                            count++;
                                        }
                                    }
                                }
                                average /= count;
                                if ( count != 0 )
                                {
                                    data[i][j] = (float)average;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AverageIntraDistrictTravelTimes(int districtNumber, IZone[] flatZones, SparseTwinIndex<float> matrix)
        {
            var flatData = matrix.GetFlatData();
            var length = flatData.Length;
            var average = GetAverageIntraDistrictNonIntraZonalTravelTime( districtNumber, flatZones, flatData );
            // after we have the average apply it to the rest of the district's intra zonal trips
            for ( int i = 0; i < length; i++ )
            {
                if ( flatZones[i].PlanningDistrict != districtNumber ) continue;
                for ( int j = 0; j < length; j++ )
                {
                    if ( i == j ) continue;
                    if ( flatZones[j].PlanningDistrict != districtNumber ) continue;
                    if ( flatData[i][j] <= 0 )
                    {
                        flatData[i][j] = average;
                    }
                }
            }
        }

        private static float GetAverageIntraDistrictNonIntraZonalTravelTime(int districtNumber, IZone[] flatZones, float[][] flatData)
        {
            var average = 0.0;
            var count = 0;
            int length = flatZones.Length;
            // find the average intrazonal travel time
            for ( int i = 0; i < length; i++ )
            {
                if ( flatZones[i].PlanningDistrict != districtNumber ) continue;
                for ( int j = 0; j < length; j++ )
                {
                    if ( i == j ) continue;
                    if ( flatZones[j].PlanningDistrict != districtNumber ) continue;
                    average += flatData[i][j];
                    if ( flatData[i][j] > 0 )
                    {
                        count++;
                    }
                }
            }
            average /= count;
            return (float)average;
        }

        private float GetAverageIntraZonalTravelTime(int districtNumber, IZone[] flatZones, float[][] flatData)
        {
            var average = 0.0;
            var count = 0;
            int length = flatZones.Length;
            // find the average intrazonal travel time
            for ( int i = 0; i < length; i++ )
            {
                if ( flatZones[i].PlanningDistrict != districtNumber ) continue;
                average += flatData[i][i];
                if ( flatData[i][i] > 0 )
                {
                    count++;
                }
            }
            if ( count == 0 )
            {
                return 0;
            }
            average /= count;
            return (float)average;
        }

        private void FillRatioIntraZonalTravelTime()
        {
            var flatZones = this.ZoneSystem.ZoneArray.GetFlatData();
            var length = flatZones.Length;
            var modes = this.AllModes;
            var numberOfModes = modes.Count;
            SparseArray<float> districtRadius = BuildDistrictRadius();
            var districts = districtRadius.ValidIndexArray();
            for ( int time = 0; time < 3; time++ )
            {
                for ( int mode = 0; mode < numberOfModes; mode++ )
                {
                    var matrix = this.TripAvgTimeMatrixes[mode][time];
                    var data = matrix.GetFlatData();
                    for ( int districtID = 0; districtID < districts.Length; districtID++ )
                    {
                        if ( AnyTripIntraDistrict( districts[districtID], flatZones, matrix ) )
                        {
                            this.AverageDiagonals( districts[districtID], flatZones, matrix );
                            this.AverageIntraDistrictTravelTimes( districts[districtID], flatZones, matrix );
                        }
                        else
                        {
                            this.FillRatioIntraZonalTravelTime( districts[districtID], flatZones, matrix, districtRadius );
                        }
                    }
                }
            }
        }

        private void AverageDiagonals(int districtNumber, IZone[] flatZones, SparseTwinIndex<float> matrix)
        {
            var flatData = matrix.GetFlatData();
            var length = flatData.Length;
            var average = GetAverageIntraZonalTravelTime( districtNumber, flatZones, flatData );
            if ( average == 0 )
            {
                // if the average is 0 then there were no trips
                for ( int i = 0; i < length; i++ )
                {
                    var otherPD = flatZones[i].PlanningDistrict;
                    if ( otherPD != districtNumber )
                    {
                        var otherAverage = GetAverageIntraZonalTravelTime( otherPD, flatZones, flatData );
                        if ( otherAverage != 0 )
                        {
                            var ratio = this.GetNumberOfZonesRatio( flatZones, otherPD, districtNumber );
                            var radius = BuildDistrictRadius();
                            var distanceRatio = radius[districtNumber] / radius[otherPD];
                            average = otherAverage * distanceRatio * ratio;
                        }
                    }
                }
            }

            // after we have the average apply it to the rest of the district's intra zonal trips
            for ( int i = 0; i < length; i++ )
            {
                if ( flatZones[i].PlanningDistrict != districtNumber ) continue;
                if ( flatData[i][i] <= 0 )
                {
                    flatData[i][i] = average;
                }
            }
        }

        private void FillRatioIntraZonalTravelTime(int districtNumber, IZone[] flatZones, SparseTwinIndex<float> matrix, SparseArray<float> radius)
        {
            var validDistricts = radius.ValidIndexArray();
            var flatRadius = radius.GetFlatData();
            for ( int otherDistrict = 0; otherDistrict < validDistricts.Length; otherDistrict++ )
            {
                var sparseOther = radius.GetSparseIndex( otherDistrict );
                if ( sparseOther == districtNumber ) continue;
                if ( this.AnyTripIntraDistrict( otherDistrict, flatZones, matrix ) )
                {
                    var distanceRatio = radius[districtNumber] / flatRadius[otherDistrict];
                    var data = matrix.GetFlatData();
                    var averageTT = GetAverageIntraDistrictNonIntraZonalTravelTime( sparseOther, flatZones, data );
                    var averageIntraZonealTT = GetAverageIntraZonalTravelTime( sparseOther, flatZones, data );
                    var zoneRatio = GetNumberOfZonesRatio( flatZones, districtNumber, sparseOther );
                    averageTT *= distanceRatio * zoneRatio;
                    averageIntraZonealTT *= distanceRatio * zoneRatio;
                    for ( int i = 0; i < flatZones.Length; i++ )
                    {
                        if ( flatZones[i].PlanningDistrict != districtNumber ) continue;
                        for ( int j = 0; j < flatZones.Length; j++ )
                        {
                            if ( flatZones[j].PlanningDistrict != districtNumber ) continue;
                            if ( i == j )
                            {
                                data[i][j] = averageIntraZonealTT;
                            }
                            else
                            {
                                data[i][j] = averageTT;
                            }
                        }
                    }
                    break;
                }
            }
        }

        private float GetNumberOfZonesRatio(IZone[] flatZones, int districtNumber, int sparseOther)
        {
            float first = 0;
            float second = 0;
            for ( int i = 0; i < flatZones.Length; i++ )
            {
                var pd = flatZones[i].PlanningDistrict;
                if ( pd == districtNumber )
                {
                    first++;
                }
                else if ( pd == sparseOther )
                {
                    second++;
                }
            }
            return first / second;
        }

        private bool AnyTripIntraDistrict(int districtNumber, IZone[] flatZones, SparseTwinIndex<float> matrix)
        {
            var flatData = matrix.GetFlatData();
            var length = flatData.Length;
            for ( int i = 0; i < length; i++ )
            {
                if ( flatZones[i].PlanningDistrict != districtNumber ) continue;
                for ( int j = 0; j < length; j++ )
                {
                    if ( i == j ) continue;
                    if ( flatZones[j].PlanningDistrict != districtNumber ) continue;
                    if ( flatData[i][j] > 0 )
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private SparseArray<float> BuildDistrictRadius()
        {
            List<int> districts = new List<int>();
            List<float> radiusList = new List<float>();
            using ( CsvReader reader = new CsvReader( this.GetFullPath( this.DistrictRadiusFile ) ) )
            {
                // burn header
                reader.LoadLine( );
                while ( !reader.EndOfFile )
                {
                    // make sure that we actually loaded data in
                    if ( reader.LoadLine( ) == 0 )
                    {
                        continue;
                    }
                    // if we have data process it
                    int district;
                    float radius;
                    reader.Get( out district, 0 );
                    reader.Get( out radius, 1 );
                    districts.Add( district );
                    radiusList.Add( radius );
                }
            }
            return SparseArray<float>.CreateSparseArray( districts.ToArray(), radiusList );
        }

        private string GetFullPath(string localPath)
        {
            var fullPath = localPath;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( this.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        private void EuclideanFilter(bool fillInData)
        {
            var flatZones = this.ZoneSystem.ZoneArray.GetFlatData();
            var length = flatZones.Length;
            var modes = this.AllModes;
            var numberOfModes = modes.Count;

            for ( int time = 0; time < 3; time++ )
            {
                for ( int mode = 0; mode < numberOfModes; mode++ )
                {
                    var matrix = this.TripAvgTimeMatrixes[mode][time];
                    var data = matrix.GetFlatData();
                    bool changed = false;
                    for ( int i = 0; i < length; i++ )
                    {
                        for ( int j = 0; j < length; j++ )
                        {
                            if ( !fillInData )
                            {
                                if ( i == j )
                                {
                                    continue;
                                }
                                if ( ( data[i][j] == 0 ) )
                                {
                                    continue;
                                }
                            }
                            var baseTime = data[i][j];
                            if ( baseTime == 0 )
                            {
                                baseTime = float.MaxValue;
                            }
                            for ( int k = 0; k < length; k++ )
                            {
                                var part1 = data[i][k];
                                var part2 = data[k][j];
                                if ( ( part1 <= 0 ) || ( part2 <= 0 ) )
                                {
                                    continue;
                                }
                                var newTime = part1 + part2;
                                if ( baseTime > newTime )
                                {
                                    baseTime = newTime;
                                    changed = true;
                                }
                            }
                            if ( baseTime == float.MaxValue )
                            {
                                baseTime = 0;
                            }
                            data[i][j] = baseTime;
                        }
                    }
                    if ( !changed )
                    {
                        break;
                    }
                }
            }
        }

        private void InitializeData()
        {
            var numberOfModes = this.AllModes.Count;
            this.TripCountMatrixes = new SparseTwinIndex<float>[numberOfModes][];
            this.TripAvgTimeMatrixes = new SparseTwinIndex<float>[numberOfModes][];
            for ( int i = 0; i < numberOfModes; i++ )
            {
                this.TripCountMatrixes[i] = new SparseTwinIndex<float>[3];
                this.TripAvgTimeMatrixes[i] = new SparseTwinIndex<float>[3];
            }
            for ( int i = 0; i < numberOfModes; i++ )
            {
                for ( int j = 0; j < 3; j++ )
                {
                    this.TripCountMatrixes[i][j] = this.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
                    this.TripAvgTimeMatrixes[i][j] = this.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
                }
            }
        }

        private void AddToMatrix(ITashaHousehold[] allHouseholds)
        {
            foreach ( var household in allHouseholds )
            {
                foreach ( var person in household.Persons )
                {
                    foreach ( var tripchain in person.TripChains )
                    {
                        foreach ( var trip in tripchain.Trips )
                        {
                            this.AddToMatrix( trip );
                        }
                    }
                }
            }
        }

        Time Six = new Time() { Hours = 6 };
        Time Nine = new Time() { Hours = 9 };
        Time ThreeThirty = new Time() { Hours = 15, Minutes = 30 };
        Time SixThirty = new Time() { Hours = 18, Minutes = 30 };
        private int GetTimePeriod(ITrip trip)
        {
            var startTime = trip.ActivityStartTime;
            if ( startTime >= ThreeThirty && startTime <= SixThirty )
            {
                return 1;
            }
            else if ( startTime >= Six && startTime <= Nine )
            {
                return 0;
            }
            return 2;
        }

        private void CalculateAverage()
        {
            var numberOfModes = this.TripAvgTimeMatrixes.Length;
            for ( int i = 0; i < numberOfModes; i++ )
            {
                for ( int j = 0; j < 3; j++ )
                {
                    var avgMatrix = this.TripAvgTimeMatrixes[i][j];
                    foreach ( var validI in this.TripCountMatrixes[i][j].ValidIndexes() )
                    {
                        foreach ( var validJ in this.TripCountMatrixes[i][j].ValidIndexes( validI ) )
                        {
                            var numberOfTrips = this.TripCountMatrixes[i][j][validI, validJ];
                            if ( numberOfTrips != 0 )
                            {
                                avgMatrix[validI, validJ] = avgMatrix[validI, validJ] / numberOfTrips;
                            }
                        }
                    }
                }
            }
        }

        private void OutputMatricies()
        {
            var length = this.AllModes.Count;
            var type = new string[] { "Average" };
            var times = new string[] { "AM", "PM", "OP" };
            for ( int i = 0; i < length; i++ )
            {
                for ( int j = 0; j < times.Length; j++ )
                {
                    for ( int t = 0; t < type.Length; t++ )
                    {
                        if ( this.Format311 )
                        {
                            Output311( type, times, i, j, t );
                        }
                        else
                        {
                            OutputCSV( type, times, i, j, t );
                        }
                    }
                }
            }
        }

        private void Output311(string[] typeNames, string[] times, int mode, int time, int type)
        {
            var data = this.TripAvgTimeMatrixes[mode][time];
            var matrixNumber = this.MatrixNumbers.Split( ',' );
            var zoneNumbers = data.ValidIndexArray();
            var flatData = data.GetFlatData();
            var numberOfZones = zoneNumbers.Length;
            using ( StreamWriter writer = new StreamWriter( this.AllModes[mode].ModeName + times[time] + typeNames[type] + ".311" ) )
            {
                // We need to know what the head should look like.
                writer.WriteLine( "t matrices\r\nd matrix=mf{0}\r\na matrix=mf{0} name=drvtot default=incr descr=generated", matrixNumber.Length <= mode ? "12" : matrixNumber[mode] );
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

        private void OutputCSV(string[] type, string[] times, int i, int j, int t)
        {
            using ( StreamWriter writer = new StreamWriter( this.AllModes[i].ModeName + times[j] + type[t] + ".csv" ) )
            {
                writer.Write( "Zone" );
                foreach ( var validI in this.TripCountMatrixes[i][j].ValidIndexes() )
                {
                    writer.Write( ',' );
                    writer.Write( validI );
                }
                writer.WriteLine();
                foreach ( var validI in this.TripCountMatrixes[i][j].ValidIndexes() )
                {
                    writer.Write( validI );
                    foreach ( var validJ in this.TripCountMatrixes[i][j].ValidIndexes( validI ) )
                    {
                        writer.Write( ',' );
                        switch ( t )
                        {
                            case 0:
                                writer.Write( this.TripAvgTimeMatrixes[i][j][validI, validJ] );
                                break;
                        }
                    }
                    writer.WriteLine();
                }
            }
        }

        private void AddToMatrix(ITrip trip)
        {
            var length = this.AllModes.Count;
            var observedMode = trip[ObservedModeString] as ITashaMode;
            int index = -1;
            for ( int i = 0; i < length; i++ )
            {
                if ( this.AllModes[i] == observedMode )
                {
                    index = i;
                    break;
                }
            }
            if ( index >= 0 )
            {
                var ttPeriod = this.GetTimePeriod( trip );
                this.TripCountMatrixes[index][ttPeriod][trip.OriginalZone.ZoneNumber, trip.DestinationZone.ZoneNumber] += 1;
                this.TripAvgTimeMatrixes[index][ttPeriod][trip.OriginalZone.ZoneNumber, trip.DestinationZone.ZoneNumber] += trip.TravelTime.ToMinutes();
            }
        }

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

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 50, 150, 50 );
        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public override string ToString()
        {
            return this.Status;
        }

        #region WorkaroundCode
        class BadZone : IZone
        {
            public int ZoneNumber
            {
                get;
                set;
            }

            public int PlanningDistrict
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public int Population
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float WorkGeneral
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float WorkManufacturing
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float TotalEmployment
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float WorkProfessional
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float WorkRetail
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float WorkUnknown
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float Employment
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float GeneralEmployment
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float ManufacturingEmployment
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float ProfessionalEmployment
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float RetailEmployment
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float UnknownEmployment
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float X
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float Y
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float InternalDistance
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float InternalArea
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float RetailActivityLevel
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float OtherActivityLevel
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float WorkActivityLevel
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float ParkingCost
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float IntrazonalDensity
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }

            public float ArterialRoadRatio
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }


            public int RegionNumber
            {
                get
                {
                    throw new NotImplementedException();
                }
                set
                {
                    throw new NotImplementedException();
                }
            }
        }

        public ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime)
        {
            throw new NotImplementedException();
        }

        public int RandomSeed { get; set; }

        public int Iterations { get; set; }

        public int HouseholdIterations { get; set; }

        public bool Parallel { get; set; }

        public Time StartOfDay { get; set; }


        public Time EndOfDay { get; set; }


        public int GetIndexOfMode(ITashaMode mode)
        {
            throw new NotImplementedException();
        }

        [DoNotAutomate]
        public List<IPreIteration> PreIteration { get; set; }

        [DoNotAutomate]
        public ITashaMode AutoMode { get; set; }

        [DoNotAutomate]
        public List<IPostScheduler> PostScheduler { get; set; }


        [DoNotAutomate]
        public List<ISharedMode> SharedModes { get; set; }


        [DoNotAutomate]
        public List<ITashaMode> OtherModes { get; set; }


        [DoNotAutomate]
        public List<IVehicleType> VehicleTypes { get; set; }


        [DoNotAutomate]
        public List<IPostHousehold> PostHousehold { get; set; }

        [DoNotAutomate]
        public List<IPostIteration> PostIteration { get; set; }


        [DoNotAutomate]
        public List<ISelfContainedModule> PostRun { get; set; }


        [DoNotAutomate]
        public List<ISelfContainedModule> PreRun { get; set; }


        [DoNotAutomate]
        public List<ITashaMode> NonSharedModes { get; set; }


        [DoNotAutomate]
        public ITashaModeChoice ModeChoice { get; set; }

        #endregion
    }
}
