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
using TMG.Functions;
using TMG.GTAModel.DataUtility;
using TMG.ModeSplit;
using XTMF;

namespace TMG.GTAModel
{
    public class BlendedHBODistribution : IDemographicDistribution
    {
        [RunParameter( "Auto Network Name", "Auto", "The name of the auto network." )]
        public string AutoNetworkName;

        [SubModelInformation( Description = "The Sets of demographic categories to blend together", Required = true )]
        public List<MultiBlendSet> BlendSets;

        [RunParameter( "Load Friction File Name", "", "The start of the name of the file (First Default = FrictionCache1.bin) to load for friction, leaving this empty will generate new friction." )]
        public string LoadFrictionFileName;

        [ParentModel]
        public IPurpose Parent;

        [RunParameter( "Region Auto Parameters", "1,2,3,4,5", typeof( FloatList ), "The region parameters for Auto Times." )]
        public FloatList RegionAutoParameter;

        [RunParameter( "Region Employment Parameters", "1,2,3,4,5", typeof( FloatList ), "The region parameters for Employment besides manufacturing." )]
        public FloatList RegionNonManufacturingEmploymentParameter;

        [RunParameter( "Region Numbers", "1,2,3,4,5", typeof( NumberList ), "The space to be reading region parameters in from.\r\nThis is used as an inverse lookup for the parameters." )]
        public NumberList RegionNumbers;

        [RunParameter( "Region Population Parameters", "1,2,3,4,5", typeof( FloatList ), "The region parameters for the Population." )]
        public FloatList RegionPopulationParameter;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        [RunParameter( "Save Friction File Name", "", "The start of the name of the file (First Default = FrictionCache1.bin). If this is empty nothing will be saved." )]
        public string SaveFrictionFileName;

        [RunParameter( "Simulation Time", "7:00AM", typeof( Time ), "The time of day this will be simulating." )]
        public Time SimulationTime;

        [RunParameter( "Transpose Distribution", false, "Transpose the final result of the model." )]
        public bool Transpose;

        [DoNotAutomate]
        protected IInteractiveModeSplit InteractiveModeSplit;

        private int currentNumber;
        private int lastIteration = -1;
        private INetworkData NetworkData;

        private int TotalBlendSets;

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

        public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> eps, IEnumerable<SparseArray<float>> eas, IEnumerable<IDemographicCategory> ecs)
        {
            float[] friction = null;
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var productions = new List<SparseArray<float>>();
            var cats = new List<IDemographicCategory>();
            var ep = eps.GetEnumerator();
            var ec = ecs.GetEnumerator();
            while ( ep.MoveNext() && ec.MoveNext() )
            {
                productions.Add( ep.Current );
                cats.Add( ec.Current );
            }
            SparseArray<float> production = this.Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
            this.TotalBlendSets = 0;
            for ( int i = 0; i < this.BlendSets.Count; i++ )
            {
                this.TotalBlendSets += this.BlendSets[i].Subsets.Count;
            }
            foreach ( var multiset in this.BlendSets )
            {
                var setLength = multiset.Subsets.Count;
                var productionSet = new float[setLength][][];
                var catSet = new IDemographicCategory[setLength][];
                SetupFrictionData( productions, cats, multiset, productionSet, catSet );
                for ( int subIndex = 0; subIndex < multiset.Subsets.Count; subIndex++ )
                {
                    friction = this.ComputeFriction( zones, catSet, productionSet, friction, production.GetFlatData(), subIndex );
                    var ret = SinglyConstrainedGravityModel.Process( production, friction );
                    if ( this.Transpose )
                    {
                        TransposeMatrix( ret );
                    }
                    yield return ret;
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( !LoadNetwork() )
            {
                error = "In " + this.Name + " we were unable to find the network data '" + this.AutoNetworkName + "' to use as the auto network!";
                return false;
            }
            if ( !CompareParameterCount( this.RegionAutoParameter ) )
            {
                error = "In " + this.Name + " the number of parameters for Auto does not match the number of regions!";
                return false;
            }
            if ( !CompareParameterCount( this.RegionAutoParameter ) )
            {
                error = "In " + this.Name + " the number of parameters for Auto does not match the number of regions!";
                return false;
            }
            if ( !CompareParameterCount( this.RegionPopulationParameter ) )
            {
                error = "In " + this.Name + " the number of parameters for Population does not match the number of regions!";
                return false;
            }
            if ( !CompareParameterCount( this.RegionNonManufacturingEmploymentParameter ) )
            {
                error = "In " + this.Name + " the number of parameters for Professional Employment does not match the number of regions!";
                return false;
            }
            this.InteractiveModeSplit = this.Parent.ModeSplit as IInteractiveModeSplit;
            if ( this.InteractiveModeSplit == null )
            {
                error = "In module '" + this.Name + "' we we require the mode choice for the purpose '" + this.Parent.PurposeName + "' to be of type IInteractiveModeSplit!";
                return false;
            }
            return true;
        }

        private static void SetupFrictionData(List<SparseArray<float>> productions,
            List<IDemographicCategory> cats, MultiBlendSet multiset, float[][][] productionSet,
            IDemographicCategory[][] multiCatSet)
        {
            int subsetIndex = -1;
            foreach ( var blendSet in multiset.Subsets )
            {
                subsetIndex++;
                var set = blendSet.Set;
                var length = set.Count;
                int place = 0;
                int blendSetCount = 0;
                for ( int i = 0; i < length; i++ )
                {
                    for ( int pos = set[i].Start; pos <= set[i].Stop; pos++ )
                    {
                        blendSetCount++;
                    }
                }
                productionSet[subsetIndex] = new float[blendSetCount][];
                multiCatSet[subsetIndex] = new IDemographicCategory[blendSetCount];
                for ( int i = 0; i < length; i++ )
                {
                    for ( int pos = set[i].Start; pos <= set[i].Stop; pos++ )
                    {
                        productionSet[subsetIndex][place] = productions[pos].GetFlatData();
                        multiCatSet[subsetIndex][place] = cats[pos];
                        place++;
                    }
                }
            }
        }

        private static void SetupModeChoiceParameters(IDemographicCategory[] cats, float[] ratio, IModeParameterDatabase mpd)
        {
            mpd.InitializeBlend();
            for ( int c = 0; c < cats.Length; c++ )
            {
                mpd.SetBlendWeight( ratio[c] );
                cats[c].InitializeDemographicCategory();
            }
            mpd.CompleteBlend();
        }

        private static void TransposeMatrix(SparseTwinIndex<float> ret)
        {
            var flatData = ret.GetFlatData();
            var length = flatData.Length;
            for ( int i = 0; i < length; i++ )
            {
                for ( int j = 0; j < i; j++ )
                {
                    var temp = flatData[i][j];
                    flatData[i][j] = flatData[j][i];
                    flatData[j][i] = temp;
                }
            }
        }

        private bool CompareParameterCount(FloatList data)
        {
            return this.RegionNumbers.Count == data.Count;
        }

        private float[] ComputeFriction(IZone[] zones, IDemographicCategory[][] cats, float[][][] productionSet, float[] friction, float[] production, int subsetIndex)
        {
            var numberOfZones = zones.Length;
            float[] ret = friction == null ? new float[numberOfZones * numberOfZones] : friction;
            var rootModes = this.Root.Modes;
            var numberOfModes = rootModes.Count;
            if ( !String.IsNullOrWhiteSpace( this.LoadFrictionFileName ) )
            {
                LoadFriction( ret );
            }
            else
            {
                ComputeFriction( zones, numberOfZones, ret );
            }
            this.InteractiveModeSplit.StartNewInteractiveModeSplit( this.TotalBlendSets );
            SumProduction( production, productionSet, subsetIndex );
            try
            {
                float[] ratio = new float[cats[subsetIndex].Length];
                var mpd = this.Root.ModeParameterDatabase;
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    // let it setup the modes so we can compute friction
                    ProcessRatio( i, ratio, production, productionSet[subsetIndex] );
                    SetupModeChoiceParameters( cats[subsetIndex], ratio, mpd );
                    SaveModeChoice( zones, numberOfZones, i );
                }
            }
            catch ( AggregateException e )
            {
                throw e.InnerException;
            }
            // Use the Log-Sum from the V's as the impedence function
            if ( !String.IsNullOrWhiteSpace( this.SaveFrictionFileName ) )
            {
                SaveFriction( ret );
            }
            return ret;
        }

        private void ComputeFriction(IZone[] zones, int numberOfZones, float[] ret)
        {
            Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int i)
            {
                int regionIndex;
                var origin = zones[i];
                if ( !this.InverseLookup( origin.RegionNumber, out regionIndex ) )
                {
                    return;
                }
                int index = ( i * numberOfZones );
                for ( int j = 0; j < numberOfZones; j++ )
                {
                    var destination = zones[j];
                    ret[index++] = (float)( this.RegionAutoParameter[regionIndex] * this.NetworkData.TravelTime( origin, destination, this.SimulationTime ).ToMinutes()
                        // population
                        + this.RegionPopulationParameter[regionIndex] * Math.Log( destination.Population + 1 )
                        // employment
                        + this.RegionNonManufacturingEmploymentParameter[regionIndex] * Math.Log( destination.ProfessionalEmployment
                        + destination.GeneralEmployment + destination.RetailEmployment + 1 ) );
                }
            } );
        }

        private string GetFrictionFileName(string baseName)
        {
            if ( this.Root.CurrentIteration != lastIteration )
            {
                currentNumber = 0;
                lastIteration = this.Root.CurrentIteration;
            }
            return String.Concat( baseName, currentNumber++, ".bin" );
        }

        private bool InverseLookup(int regionNumber, out int regionIndex)
        {
            return ( regionIndex = this.RegionNumbers.IndexOf( regionNumber ) ) != -1;
        }

        private void LoadFriction(float[] ret)
        {
            try
            {
                BinaryHelpers.ExecuteReader( (reader) =>
                    {
                        for ( int i = 0; i < ret.Length; i++ )
                        {
                            ret[i] = reader.ReadSingle();
                        }
                    }, GetFrictionFileName( this.LoadFrictionFileName ) );
            }
            catch ( IOException e )
            {
                throw new XTMFRuntimeException( "Unable to load distribution cache file!\r\n" + e.Message );
            }
        }

        private bool LoadNetwork()
        {
            foreach ( var data in this.Root.NetworkData )
            {
                if ( data.NetworkType == this.AutoNetworkName )
                {
                    this.NetworkData = data;
                    return true;
                }
            }
            return false;
        }

        private void ProcessRatio(int i, float[] ratio, float[] production, float[][] productions)
        {
            var denom = production[i];
            if ( denom != 0 )
            {
                denom = 1 / denom;
            }
            for ( int j = 0; j < ratio.Length; j++ )
            {
                if ( denom == 0 )
                {
                    ratio[j] = 0;
                }
                else
                {
                    ratio[j] = productions[j][i] * denom;
                }
            }
        }

        private void SaveFriction(float[] ret)
        {
            try
            {
                var fileName = GetFrictionFileName( this.SaveFrictionFileName );
                var dirName = Path.GetDirectoryName( fileName );
                if ( !Directory.Exists( dirName ) )
                {
                    Directory.CreateDirectory( dirName );
                }
                BinaryHelpers.ExecuteWriter( (writer) =>
                    {
                        for ( int i = 0; i < ret.Length; i++ )
                        {
                            writer.Write( ret[i] );
                        }
                    }, fileName );
            }
            catch ( IOException e )
            {
                throw new XTMFRuntimeException( "Unable to save distribution cache file!\r\n" + e.Message );
            }
        }

        private void SaveModeChoice(IZone[] zones, int numberOfZones, int i)
        {
            if ( this.Transpose )
            {
                Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int j)
                {
                    if ( zones[j].RegionNumber > 0 )
                    {
                        this.InteractiveModeSplit.ComputeUtility( zones[j], zones[i] );
                    }
                } );
            }
            else
            {
                Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int j)
                {
                    if ( zones[j].RegionNumber > 0 )
                    {
                        this.InteractiveModeSplit.ComputeUtility( zones[i], zones[j] );
                    }
                } );
            }
        }

        private void SumProduction(float[] production, float[][][] productions, int subset)
        {
            // i is the zone number
            for ( int i = 0; i < production.Length; i++ )
            {
                float productionSum = 0f;
                // for each blend set in the subset
                for ( int j = 0; j < productions[subset].Length; j++ )
                {
                    // add up the production
                    productionSum += productions[subset][j][i];
                }
                // save the total
                production[i] = productionSum;
            }
        }
    }
}