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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Analysis
{
    public class ValidateDistribution : ISelfContainedModule
    {
        [RunParameter( "Average by zones", false, "Should we average the spatial sums based on number of zones?" )]
        public bool AverageSums;

        [RunParameter( "Run Validation", true, "Should we run this validation?" )]
        public bool Execute;

        [RunParameter( "Planing Relative District Validation File", "PDRelativeDistributionValidation.csv", typeof( FileFromOutputDirectory ), "The location of the file to save the relative PD level validation, leave blank to skip." )]
        public FileFromOutputDirectory PDRelativeValidationFile;

        [RunParameter( "Planing District Validation File", "PDDistributionValidation.csv", typeof( FileFromOutputDirectory ), "The location of the file to save the PD level validation, leave blank to skip." )]
        public FileFromOutputDirectory PDValidationFile;

        [RunParameter( "Region Relative Validation File", "RegionRelativeDistributionValidation.csv", typeof( FileFromOutputDirectory ), "The location of the file to save the relative region level validation, leave blank to skip." )]
        public FileFromOutputDirectory RegionRelativeValidationFile;

        [RunParameter( "Region Validation File", "RegionDistributionValidation.csv", typeof( FileFromOutputDirectory ), "The location of the file to save the region level validation, leave blank to skip." )]
        public FileFromOutputDirectory RegionValidationFile;

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation( Description = "The data that we generated this run.", Required = true )]
        public IReadODData<float> RunData;

        [SubModelInformation( Description = "The data that we are trying to fit to.", Required = false )]
        public IReadODData<float> TruthData;

        [RunParameter( "Zone Relative Validation File", "ZoneRelativeDistributionValidation.csv", typeof( FileFromOutputDirectory ), "The location of the file to save the relative zone level validation, leave blank to skip." )]
        public FileFromOutputDirectory ZoneRelativeValidationFile;

        [RunParameter( "Zone Validation File", "ZoneDistributionValidation.csv", typeof( FileFromOutputDirectory ), "The location of the file to save the zone level validation, leave blank to skip." )]
        public FileFromOutputDirectory ZoneValidationFile;

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

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            if ( !Execute ) return;
            var runData = this.Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            var truthData = this.Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            LoadInData( runData, truthData );
            RunValidationChecks( runData, truthData );
        }

        private static void ComputeDifferences(SparseTwinIndex<float> runData, SparseTwinIndex<float> truthData, float[][] diff)
        {
            var runDataSum = Sum( runData );
            var truthDataSum = Sum( truthData );
            var runDataFactor = truthDataSum / runDataSum;
            var flatRunData = runData.GetFlatData();
            var flatTruthData = truthData.GetFlatData();
            Parallel.For( 0, diff.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int i)
                {
                    var diffRow = diff[i];
                    var runRow = flatRunData[i];
                    var truthRow = flatTruthData[i];
                    for ( int j = 0; j < diffRow.Length; j++ )
                    {
                        diffRow[j] = runRow[j] - truthRow[j];
                    }
                } );
        }

        private static float Sum(SparseTwinIndex<float> data)
        {
            var flatData = data.GetFlatData();
            double total = 0;
            for ( int i = 0; i < flatData.Length; i++ )
            {
                var local = 0f;
                var row = flatData[i];
                for ( int j = row.Length - 1; j >= 0; j-- )
                {
                    local += row[j];
                }
                total += local;
            }
            return (float)total;
        }

        private void Clear(float[][] matrix)
        {
            for ( int i = 0; i < matrix.Length; i++ )
            {
                var row = matrix[i];
                for ( int j = 0; j < row.Length; j++ )
                {
                    row[j] = 0;
                }
            }
        }

        private SparseArray<int> CreatePDArray()
        {
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            List<int> pdNumbersFound = new List<int>( 10 );
            for ( int i = 0; i < zones.Length; i++ )
            {
                var pdID = zones[i].PlanningDistrict;
                if ( !pdNumbersFound.Contains( pdID ) )
                {
                    pdNumbersFound.Add( pdID );
                }
            }
            var pdArray = pdNumbersFound.ToArray();
            return SparseArray<int>.CreateSparseArray( pdArray, pdArray );
        }

        private SparseArray<int> CreateRegionArray()
        {
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            List<int> regionNumbersFound = new List<int>( 10 );
            for ( int i = 0; i < zones.Length; i++ )
            {
                var regionID = zones[i].RegionNumber;
                if ( !regionNumbersFound.Contains( regionID ) )
                {
                    regionNumbersFound.Add( regionID );
                }
            }
            var regionArray = regionNumbersFound.ToArray();
            return SparseArray<int>.CreateSparseArray( regionArray, regionArray );
        }

        private SparseTwinIndex<float> CreateRelativeDifference<T>(SparseTwinIndex<float> runData, SparseTwinIndex<float> baseData, SparseArray<T> refernceArray, Func<IZone, int> getAgg)
        {
            var ret = refernceArray.CreateSquareTwinArray<float>();
            var truth = refernceArray.CreateSquareTwinArray<float>();
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var flatRun = runData.GetFlatData();
            var flatBaseData = baseData.GetFlatData();
            for ( int i = 0; i < flatRun.Length; i++ )
            {
                var oAgg = getAgg( zones[i] );
                for ( int j = 0; j < flatRun[i].Length; j++ )
                {
                    var jAgg = getAgg( zones[j] );
                    ret[oAgg, jAgg] += flatRun[i][j];
                    truth[oAgg, jAgg] += flatBaseData[i][j];
                }
            }
            var factor = Sum( truth ) / Sum( ret );
            var flatRetData = ret.GetFlatData();
            var flatTruthData = truth.GetFlatData();
            for ( int i = 0; i < flatRetData.Length; i++ )
            {
                for ( int j = 0; j < flatRetData[i].Length; j++ )
                {
                    flatRetData[i][j] = ( flatRetData[i][j] * factor ) / flatTruthData[i][j];
                }
            }
            return ret;
        }

        private void GatherData(SparseTwinIndex<float> storage, IReadODData<float> dataSource, string taskName)
        {
            foreach ( var point in dataSource.Read() )
            {
                if ( storage.ContainsIndex( point.O, point.D ) )
                {
                    storage[point.O, point.D] = point.Data;
                }
                else
                {
                    throw new XTMFRuntimeException( "In '" + this.Name + "' while gathering the " + taskName + " we encountered an invalid data point at " + point.O
                        + ":" + point.D + " trying to assign a value of " + point.Data );
                }
            }
        }

        private void LoadInData(SparseTwinIndex<float> runData, SparseTwinIndex<float> truthData)
        {
            GatherData( runData, RunData, "run data" );
            if ( this.TruthData != null )
            {
                GatherData( truthData, TruthData, "truth data" );
            }
        }

        private void ProducePDData(float[][] diff, SparseTwinIndex<float> runData, SparseTwinIndex<float> baseData)
        {
            var pdMap = this.CreatePDArray();
            var flatMap = pdMap.GetFlatData();
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var pdData = pdMap.CreateSquareTwinArray<float>().GetFlatData();
            var pdDataentries = pdMap.CreateSquareTwinArray<int>().GetFlatData();
            for ( int i = 0; i < diff.Length; i++ )
            {
                var o = pdMap.GetFlatIndex( zones[i].PlanningDistrict );
                for ( int j = 0; j < diff[i].Length; j++ )
                {
                    var d = pdMap.GetFlatIndex( zones[j].PlanningDistrict );
                    pdData[o][d] += diff[i][j];
                    pdDataentries[o][d]++;
                }
            }

            if ( this.AverageSums )
            {
                for ( int i = 0; i < pdData.Length; i++ )
                {
                    var row = pdData[i];
                    var numberOfEntries = pdDataentries[i];
                    for ( int j = 0; j < row.Length; j++ )
                    {
                        row[j] = row[j] / numberOfEntries[j];
                    }
                }
            }

            SparseTwinIndex<float> relativeDiff = CreateRelativeDifference( runData, baseData, pdMap, ( zone => zone.PlanningDistrict ) );

            if ( this.PDValidationFile.ContainsFileName() )
            {
                WriteOut( pdMap, pdData, this.PDValidationFile.GetFileName(), ( i => i ) );
            }

            if ( this.PDRelativeValidationFile.ContainsFileName() )
            {
                WriteOut( pdMap, relativeDiff.GetFlatData(), this.PDRelativeValidationFile.GetFileName(), ( i => i ) );
            }
        }

        private void ProduceRegionData(float[][] diff, SparseTwinIndex<float> runData, SparseTwinIndex<float> baseData)
        {
            var regionMap = this.CreateRegionArray();
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var regionData = regionMap.CreateSquareTwinArray<float>().GetFlatData();
            var regionDataEntries = regionMap.CreateSquareTwinArray<int>().GetFlatData();
            // take the zoneal differences and aggregate them to the region level
            for ( int i = 0; i < diff.Length; i++ )
            {
                var o = regionMap.GetFlatIndex( zones[i].RegionNumber );
                for ( int j = 0; j < diff[i].Length; j++ )
                {
                    var d = regionMap.GetFlatIndex( zones[j].RegionNumber );
                    regionData[o][d] += diff[i][j];
                    regionDataEntries[o][d]++;
                }
            }

            if ( this.AverageSums )
            {
                for ( int i = 0; i < regionData.Length; i++ )
                {
                    var row = regionData[i];
                    var numberOfEntries = regionDataEntries[i];
                    for ( int j = 0; j < row.Length; j++ )
                    {
                        row[j] = row[j] / numberOfEntries[j];
                    }
                }
            }

            if ( this.RegionValidationFile.ContainsFileName() )
            {
                WriteOut( regionMap, regionData, this.RegionValidationFile.GetFileName(), ( i => i ) );
            }

            SparseTwinIndex<float> relativeDiff = CreateRelativeDifference( runData, baseData, regionMap, ( zone => zone.RegionNumber ) );

            if ( this.RegionRelativeValidationFile.ContainsFileName() )
            {
                WriteOut( regionMap, relativeDiff.GetFlatData(), this.RegionRelativeValidationFile.GetFileName(), ( i => i ) );
            }
        }

        private void ProduceZoneData(float[][] diff, SparseTwinIndex<float> runData, SparseTwinIndex<float> baseData)
        {
            SparseTwinIndex<float> relativeDiff = CreateRelativeDifference( runData, baseData, this.Root.ZoneSystem.ZoneArray, ( zone => zone.ZoneNumber ) );

            if ( this.ZoneValidationFile.ContainsFileName() )
            {
                WriteOut( this.Root.ZoneSystem.ZoneArray, diff, this.ZoneValidationFile.GetFileName(), ( zone => zone.ZoneNumber ) );
            }

            if ( this.ZoneRelativeValidationFile.ContainsFileName() )
            {
                WriteOut( this.Root.ZoneSystem.ZoneArray, relativeDiff.GetFlatData(), this.ZoneRelativeValidationFile.GetFileName(), ( zone => zone.ZoneNumber ) );
            }
        }

        private void RunValidationChecks(SparseTwinIndex<float> runData, SparseTwinIndex<float> truthData)
        {
            var diff = runData.CreateSimilarArray<float>().GetFlatData();
            // Compute the differences at the zone level
            ComputeDifferences( runData, truthData, diff );
            // we can save all of the files at the same time and work out the aggregations
            Parallel.Invoke(
                delegate()
                {
                    ProduceZoneData( diff, runData, truthData );
                },
                delegate()
                {
                    ProducePDData( diff, runData, truthData );
                },
                delegate()
                {
                    ProduceRegionData( diff, runData, truthData );
                }
            );
        }

        private void WriteOut<T>(SparseArray<T> aggregation, float[][] data, string fileName, Func<T, int> getValue)
        {
            var flatAggregation = aggregation.GetFlatData();
            using ( StreamWriter writer = new StreamWriter( fileName ) )
            {
                // write the top hat
                writer.Write( "Origin\\Destination" );
                for ( int i = 0; i < flatAggregation.Length; i++ )
                {
                    var iNumber = getValue( flatAggregation[i] );
                    writer.Write( ',' );
                    writer.Write( iNumber );
                }
                writer.WriteLine();
                for ( int i = 0; i < flatAggregation.Length; i++ )
                {
                    writer.Write( getValue( flatAggregation[i] ) );
                    for ( int j = 0; j < data[i].Length; j++ )
                    {
                        writer.Write( ',' );
                        writer.Write( data[i][j] );
                    }
                    writer.WriteLine();
                }
            }
        }
    }
}