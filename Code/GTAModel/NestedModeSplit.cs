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
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using TMG.Input;
using TMG.ModeSplit;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel
{
    [ModuleInformation( Description =
        @"Nested Mode Split provides a solution for the generic case of a Nested Logit model. 
It will use the Root’s modes and solve for them looking at the modes feasibility while computing. 
This module will also work for a regular Logit model as well."
        )]
    public class NestedModeSplit : IInteractiveModeSplit
    {
        [RootModule]
        public I4StepModel Root;

        [RunParameter( "Save Directory", "", typeof( FileFromOutputDirectory ), "The directory to save the mode splits to.  Leave this blank to not." )]
        public FileFromOutputDirectory SaveDirectory;

        [RunParameter( "Save Utilities", "", typeof( FileFromOutputDirectory ), "The directory to save the utilities" )]
        public FileFromOutputDirectory SaveUtilities;

        [RunParameter( "Simulation Time", "7:00 AM", typeof( Time ), "The time that this mode split will be running as." )]
        public Time SimulationTime;

        public int TimesRun;
        private float _MinLogSum;

        private List<TreeData<float[]>> InteractiveUtilityTrees;

        // This variable is the one we are actually going to be working with, and will be set by XTMF
        private float MinLogSumToE;

        private int NumberOfInteractiveCategories;

        [RunParameter( "Min LogSum", -70.0f, "The cutoff point for the logsum term for nested mode choices." )]
        public float MinLogSum
        {
            get
            {
                return _MinLogSum;
            }

            set
            {
                _MinLogSum = value;
                MinLogSumToE = (float)Math.Exp( value );
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

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public float ComputeUtility(IZone o, IZone d)
        {
            if ( InteractiveUtilityTrees == null )
            {
                throw new XTMFRuntimeException(this, Name + " tried to use interactive mode without being initialized first!" );
            }
            var zones = Root.ZoneSystem.ZoneArray;
            var flatZones = zones.GetFlatData();
            var flatO = zones.GetFlatIndex( o.ZoneNumber );
            var flatD = zones.GetFlatIndex( d.ZoneNumber );
            var numberOfZones = flatZones.Length;
            var tree = InteractiveUtilityTrees;
            GatherUtility( tree, flatO, flatD, flatZones );
            var sum = 0f;
            bool any = false;
            var length = tree.Count;
            for ( int i = 0; i < length; i++ )
            {
                var res = tree[i].Result;
                if ( res != null )
                {
                    var util = res[flatO * numberOfZones + flatD];
                    if ( !float.IsNaN( util ) )
                    {
                        any = true;
                        sum += util;
                    }
                }
            }
            return any ? sum : float.NaN;
        }

        public void EndInterativeModeSplit()
        {
            if ( SaveDirectory.ContainsFileName() )
            {
                SaveData();
            }
            InteractiveUtilityTrees = null;
        }

        public List<TreeData<float[][]>> ModeSplit(IEnumerable<SparseTwinIndex<float>> flowMatrix, int numberOfCategories)
        {
            Progress = 0;
            var ret = MirrorModeTree.CreateMirroredTree<float[][]>( Root.Modes );
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            int flows = 0;
            //double flowTotal = 0f;
            foreach ( var flow in flowMatrix )
            {
                //flowTotal += SumFlow( flow );
                if ( InteractiveUtilityTrees == null )
                {
                    TraditionalModeSplit( numberOfCategories, ret, zones, flows, flow );
                }
                else
                {
                    InteractiveModeSplit(ret, zones, flows, flow );
                    // reset ourselves out of interactive mode
                    EndInterativeModeSplit();
                }
                flows++;
            }
            //The ratios are too close to 1 to really justify the extra processing, and could lead to additional errors
            //Reconstitute( ret, flowTotal );
            return ret;
        }

        public List<TreeData<float[][]>> ModeSplit(SparseTwinIndex<float> flowMatrix)
        {
            return ModeSplit( SingleEnumerator( flowMatrix ), 1 );
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public IEnumerable<SparseTwinIndex<float>> SingleEnumerator(SparseTwinIndex<float> single)
        {
            yield return single;
        }

        public void StartNewInteractiveModeSplit(int numberOfCategories)
        {
            var numberOfZones = Root.ZoneSystem.ZoneArray.GetFlatData().Length;
            NumberOfInteractiveCategories = numberOfCategories;
            if ( InteractiveUtilityTrees == null )
            {
                InteractiveUtilityTrees = MirrorModeTree.CreateMirroredTree<float[]>( Root.Modes );
            }
            InitializeTree( InteractiveUtilityTrees, numberOfZones * numberOfZones );
        }

        protected void WriteModeSplit(TreeData<float[]> split, IModeChoiceNode modeNode, string directoryName)
        {
            Task writeTask = new Task( delegate
            {
                if ( split.Result != null )
                {
                    using StreamWriter writer = new StreamWriter(Path.Combine(directoryName, modeNode.ModeName + ".csv"));
                    var header = true;
                    var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                    var data = split.Result;
                    for (int i = 0; i < zones.Length; i++)
                    {
                        if (header)
                        {
                            header = false;
                            writer.Write("Zones O\\D");
                            for (int j = 0; j < zones.Length; j++)
                            {
                                writer.Write(',');
                                writer.Write(zones[j].ZoneNumber);
                            }
                            writer.WriteLine();
                        }
                        writer.Write(zones[i].ZoneNumber);
                        for (int j = 0; j < zones.Length; j++)
                        {
                            writer.Write(',');
                            writer.Write(data[i * zones.Length + j]);
                        }
                        writer.WriteLine();
                    }
                }
            } );
            writeTask.Start();
            if ( split.Children != null )
            {
                for ( int i = 0; i < split.Children.Length; i++ )
                {
                    WriteModeSplit( split.Children[i], ( (IModeCategory)modeNode ).Children[i], directoryName );
                }
            }
            writeTask.Wait();
        }

        private static void InitializeTree(List<TreeData<float[]>> list, int arrayLength)
        {
            var length = list.Count;
            for ( int i = 0; i < length; i++ )
            {
                InitializeTree( list[i], arrayLength );
            }
        }

        private static void InitializeTree(TreeData<float[]> tree, int arrayLength)
        {
            var treechildren = tree.Children;
            if ( treechildren != null )
            {
                var length = treechildren.Length;
                for ( int i = 0; i < length; i++ )
                {
                    InitializeTree( treechildren[i], arrayLength );
                }
            }
            var data = tree.Result = new float[arrayLength];
            Parallel.For( 0, (int)Math.Ceiling( arrayLength / 100f ), delegate(int section)
            {
                var start = section * 100;
                var end = start + 100;
                for ( int i = start; i < end & i < arrayLength; i++ )
                {
                    data[i] = float.NaN;
                }
            } );
        }

        private void ConvertToFlow(List<TreeData<float[]>> utility, IZone[] zones, int o, int d, float flow)
        {
            int index = o * zones.Length + d;
            var length = utility.Count;
            float totalUtility = 0f;
            int feasibleChoices = 0;
            for ( int i = 0; i < length; i++ )
            {
                var res = utility[i].Result[index];
                if ( !float.IsNaN( res ) )
                {
                    totalUtility += utility[i].Result[index];
                    feasibleChoices++;
                }
            }
            if ( feasibleChoices == 0 )
            {
                return;
            }
            // inverse the total utility
            if ( totalUtility == 0 || float.IsInfinity( ( totalUtility = 1 / totalUtility ) ) )
            {
                // if there was no utility break it up evenly
                totalUtility = 1f / feasibleChoices;
            }
            for ( int i = 0; i < length; i++ )
            {
                utility[i].Result[index] = flow * utility[i].Result[index] * totalUtility;
            }
            // now see if we have children
            for ( int i = 0; i < length; i++ )
            {
                if ( utility[i].Children != null && !float.IsNaN( utility[i].Result[index] ) )
                {
                    if ( utility[i].Children != null )
                    {
                        ConvertToFlow( utility[i], zones, o, d, utility[i].Result[index] );
                    }
                }
            }
        }

        private void ConvertToFlow(TreeData<float[]> treeData, IZone[] zones, int o, int d, float flow)
        {
            int index = o * zones.Length + d;
            float totalUtility = 0f;
            var length = treeData.Children.Length;
            int feasibleChoices = 0;
            var children = treeData.Children;
            for ( int i = 0; i < length; i++ )
            {
                var res = children[i].Result[index];
                if ( !float.IsNaN( res ) )
                {
                    feasibleChoices++;
                    totalUtility += children[i].Result[index];
                }
            }
            if ( feasibleChoices == 0 )
            {
                return;
            }
            // inverse the total utility
            if ( totalUtility == 0 || float.IsInfinity( ( totalUtility = 1 / totalUtility ) ) )
            {
                // if there was no utility break it up evenly
                totalUtility = 1f / feasibleChoices;
            }
            for ( int i = 0; i < length; i++ )
            {
                children[i].Result[index] = flow * ( children[i].Result[index] * totalUtility );
            }
            // now see if we have children
            for ( int i = 0; i < length; i++ )
            {
                if ( children[i].Children != null && !float.IsNaN( children[i].Result[index] ) )
                {
                    ConvertToFlow( children[i], zones, o, d, children[i].Result[index] );
                }
            }
        }

        private void ConvertToFlow(List<TreeData<float>> utility, float flow)
        {
            var length = utility.Count;
            float totalUtility = 0f;
            int feasibleChoices = 0;
            for ( int i = 0; i < length; i++ )
            {
                var res = utility[i].Result;
                if ( !float.IsNaN( res ) )
                {
                    totalUtility += utility[i].Result;
                    feasibleChoices++;
                }
            }
            if ( feasibleChoices == 0 )
            {
                return;
            }
            // inverse the total utility
            if ( totalUtility == 0 )
            {
                // if there was no utility break it up evenly
                totalUtility = 1f / feasibleChoices;
            }
            else
            {
                totalUtility = 1 / totalUtility;
            }
            for ( int i = 0; i < length; i++ )
            {
                utility[i].Result = flow * ( utility[i].Result * totalUtility );
            }
            // now see if we have children
            for ( int i = 0; i < length; i++ )
            {
                if ( utility[i].Children != null && !float.IsNaN( utility[i].Result ) )
                {
                    if ( utility[i].Children != null )
                    {
                        ConvertToFlow( utility[i], utility[i].Result );
                    }
                }
            }
        }

        private void ConvertToFlow(TreeData<float> treeData, float flow)
        {
            float totalUtility = 0f;
            var length = treeData.Children.Length;
            int feasibleChoices = 0;
            var children = treeData.Children;
            for ( int i = 0; i < length; i++ )
            {
                var res = children[i].Result;
                if ( !float.IsNaN( res ) )
                {
                    feasibleChoices++;
                    totalUtility += children[i].Result;
                }
            }
            if ( feasibleChoices == 0 )
            {
                return;
            }
            if ( totalUtility == 0 )
            {
                totalUtility = 1f / feasibleChoices;
            }
            else
            {
                // inverse the total utility
                totalUtility = 1 / totalUtility;
            }
            for ( int i = 0; i < length; i++ )
            {
                var result = children[i].Result;
                if ( float.IsNaN( result ) )
                {
                    children[i].Result = 0;
                }
                else
                {
                    children[i].Result = flow * ( result * totalUtility );
                }
            }
            // now see if we have children
            for ( int i = 0; i < length; i++ )
            {
                if ( children[i].Children != null && !float.IsNaN( children[i].Result ) )
                {
                    ConvertToFlow( children[i], children[i].Result );
                }
            }
        }

        private void GatherUtility(List<TreeData<float[]>> utility, int o, int d, IZone[] zones)
        {
            var length = utility.Count;
            for ( int i = 0; i < length; i++ )
            {
                GatherUtility( utility[i], Root.Modes[i], o, d, zones );
            }
        }

        private bool GatherUtility(TreeData<float[]> treeData, IModeChoiceNode node, int o, int d, IZone[] zones)
        {
            var cat = node as IModeCategory;
            int index = o * zones.Length + d;
            if ( !node.Feasible( zones[o], zones[d], SimulationTime ) )
            {
                treeData.Result[index] = float.NaN;
                return false;
            }
            if ( cat == null )
            {
                treeData.Result[index] = node.CurrentlyFeasible > 0 ? (float)Math.Exp( node.CalculateV( zones[o], zones[d], SimulationTime ) ) : float.NaN;
                return !float.IsNaN( treeData.Result[index] );
            }
            if ( cat.Correlation > 0 )
            {
                bool hasAlternatives = false;
                float totalUtility = 0;
                var treeChildren = treeData.Children;
                var catChildren = cat.Children;
                for ( int i = 0; i < treeData.Children.Length; i++ )
                {
                    if ( GatherUtility( treeChildren[i], catChildren[i], o, d, zones ) )
                    {
                        hasAlternatives = true;
                        totalUtility += treeChildren[i].Result[index];
                    }
                }
                if ( hasAlternatives )
                {
                    if ( totalUtility >= MinLogSumToE )
                    {
                        var localUtility = cat.CalculateCombinedV( zones[o], zones[d], SimulationTime );
                        if ( cat.Correlation == 1f )
                        {
                            if ( localUtility == 0f )
                            {
                                treeData.Result[index] = totalUtility;
                            }
                            else
                            {
                                treeData.Result[index] = (float)( totalUtility * Math.Exp( localUtility ) );
                            }
                        }
                        else
                        {
                            treeData.Result[index] = (float)( Math.Pow( totalUtility, cat.Correlation ) * Math.Exp( localUtility ) );
                        }
                        return true;
                    }
                }
            }
            treeData.Result[index] = float.NaN;
            return false;
        }

        private void GatherUtility(List<TreeData<float>> utility, int o, int d, IZone[] zones)
        {
            var length = utility.Count;
            for ( int i = 0; i < length; i++ )
            {
                GatherUtility( utility[i], Root.Modes[i], o, d, zones );
            }
        }

        private bool GatherUtility(TreeData<float> treeData, IModeChoiceNode node, int o, int d, IZone[] zones)
        {
            var cat = node as IModeCategory;
            treeData.Result = float.NaN;
            if ( !node.Feasible( zones[o], zones[d], SimulationTime ) )
            {
                treeData.Result = float.NaN;
                return false;
            }
            if ( cat == null )
            {
                treeData.Result = ( node.CurrentlyFeasible > 0 ? (float)Math.Exp( node.CalculateV( zones[o], zones[d], SimulationTime ) ) : float.NaN );
                return !float.IsNaN( treeData.Result );
            }
            if ( cat.CurrentlyFeasible > 0 )
            {
                bool hasAlternatives = false;
                float totalUtility = 0;
                var treeChildren = treeData.Children;
                var catChildren = cat.Children;
                for ( int i = 0; i < treeData.Children.Length; i++ )
                {
                    if ( GatherUtility( treeChildren[i], catChildren[i], o, d, zones ) )
                    {
                        hasAlternatives = true;
                        totalUtility += treeChildren[i].Result;
                    }
                }
                if ( hasAlternatives )
                {
                    if ( totalUtility >= MinLogSumToE )
                    {
                        var localUtility = cat.CalculateCombinedV( zones[o], zones[d], SimulationTime );
                        treeData.Result = (float)( Math.Pow( totalUtility, cat.Correlation ) * Math.Exp( localUtility ) );
                        return true;
                    }
                }
            }
            treeData.Result = float.NaN;
            return false;
        }

        private int ModeUtilitiesProcessed;
        private void InteractiveModeSplit(List<TreeData<float[][]>> ret, IZone[] zones, int flows, SparseTwinIndex<float> flow)
        {
            int soFar = 0;
            if ( SaveUtilities.ContainsFileName() )
            {
                var dir = Path.Combine( SaveUtilities.GetFileName(), ( ModeUtilitiesProcessed++ ).ToString() );
                Directory.CreateDirectory( dir );
                for ( int i = 0; i < InteractiveUtilityTrees.Count; i++ )
                {
                    WriteModeSplit( InteractiveUtilityTrees[i], Root.Modes[i], dir );
                }
            }
            Parallel.For( 0, zones.Length, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int o)
                {
                    var flatFlows = flow.GetFlatData();
                    var utility = InteractiveUtilityTrees;
                    for ( int d = 0; d < zones.Length; d++ )
                    {
                        var odFlow = flatFlows[o][d];
                        if ( odFlow > 0 )
                        {
                            //utility will have already been calculated
                            ConvertToFlow( utility, zones, o, d, odFlow );
                            SaveResults( utility, ret, o, d, zones.Length );
                        }
                    }
                    Progress = ( ( Interlocked.Increment( ref soFar ) / (float)zones.Length ) / NumberOfInteractiveCategories ) 
                        + ( flows / (float)NumberOfInteractiveCategories );
                } );
        }

        private void SaveData()
        {
            // only save the data if we processed something
            if ( InteractiveUtilityTrees == null ) return;
            var dir = Path.Combine( SaveDirectory.GetFileName(), TimesRun.ToString() );
            TimesRun++;
            if ( !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
            for ( int i = 0; i < InteractiveUtilityTrees.Count; i++ )
            {
                WriteModeSplit( InteractiveUtilityTrees[i], Root.Modes[i], dir );
            }
        }

        private void SaveResults(List<TreeData<float[]>> utility, List<TreeData<float[][]>> ret, int o, int d, int numberOfZones)
        {
            var length = utility.Count;
            for ( int i = 0; i < length; i++ )
            {
                SaveResults( utility[i], ret[i], o, d, numberOfZones );
            }
        }

        private void SaveResults(TreeData<float[]> utility, TreeData<float[][]> ret, int o, int d, int numberOfZones)
        {
            int index = o * numberOfZones + d;
            // if the memory has not been allocated yet, allocate it
            if ( ret.Result == null )
            {
                lock ( ret )
                {
                    Thread.MemoryBarrier();
                    if ( ret.Result == null )
                    {
                        ret.Result = new float[numberOfZones][];
                        Thread.MemoryBarrier();
                    }
                }
            }
            // if we have a utility then add us in
            if ( !float.IsNaN( utility.Result[index] ) )
            {
                if ( ret.Result[o] == null )
                {
                    lock ( ret )
                    {
                        Thread.MemoryBarrier();
                        if ( ret.Result[o] == null )
                        {
                            ret.Result[o] = new float[numberOfZones];
                            Thread.MemoryBarrier();
                        }
                    }
                }
                ret.Result[o][d] += utility.Result[index];
                // and if we did check our children as well
                if ( utility.Children != null )
                {
                    var length = utility.Children.Length;
                    for ( int i = 0; i < length; i++ )
                    {
                        SaveResults( utility.Children[i], ret.Children[i], o, d, numberOfZones );
                    }
                }
            }
        }

        private void SaveResults(List<TreeData<float>> utility, List<TreeData<float[][]>> ret, int o, int d, int numberOfZones)
        {
            var length = utility.Count;
            for ( int i = 0; i < length; i++ )
            {
                SaveResults( utility[i], ret[i], o, d, numberOfZones );
            }
        }

        private void SaveResults(TreeData<float> utility, TreeData<float[][]> ret, int o, int d, int numberOfZones)
        {
            // if the memory has not been allocated yet, allocate it
            if ( ret.Result == null )
            {
                lock ( ret )
                {
                    Thread.MemoryBarrier();
                    if ( ret.Result == null )
                    {
                        ret.Result = new float[numberOfZones][];
                        Thread.MemoryBarrier();
                    }
                }
            }
            // if we have a utility then add us in
            if ( !float.IsNaN( utility.Result ) )
            {
                if ( ret.Result[o] == null )
                {
                    lock ( ret )
                    {
                        Thread.MemoryBarrier();
                        if ( ret.Result[o] == null )
                        {
                            ret.Result[o] = new float[numberOfZones];
                            Thread.MemoryBarrier();
                        }
                    }
                }
                ret.Result[o][d] += utility.Result;
                // and if we did check our children as well
                if ( utility.Children != null )
                {
                    var length = utility.Children.Length;
                    for ( int i = 0; i < length; i++ )
                    {
                        SaveResults( utility.Children[i], ret.Children[i], o, d, numberOfZones );
                    }
                }
            }
        }

        private void TraditionalModeSplit(int numberOfCategories, List<TreeData<float[][]>> ret, IZone[] zones, int flows, SparseTwinIndex<float> flow)
        {
            try
            {
                int soFar = 0;
                Parallel.For( 0, zones.Length, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    delegate {
                        return MirrorModeTree.CreateMirroredTree<float>( Root.Modes );
                    },
                    delegate(int o, ParallelLoopState unused, List<TreeData<float>> utility)
                    {
                        var flatFlows = flow.GetFlatData();
                        for ( int d = 0; d < zones.Length; d++ )
                        {
                            var odFlow = flatFlows[o][d];
                            if ( odFlow > 0 )
                            {
                                GatherUtility( utility, o, d, zones );
                                ConvertToFlow( utility, odFlow );
                                SaveResults( utility, ret, o, d, zones.Length );
                            }
                        }
                        Progress = ( ( Interlocked.Increment( ref soFar ) / (float)zones.Length ) / numberOfCategories ) + ( flows / (float)numberOfCategories );
                        return utility;
                    },
                delegate
                {
                    // do nothing
                } );
            }
            catch ( AggregateException e )
            {
                throw new XTMFRuntimeException(this, e.InnerException?.Message + "\r\n" + e.InnerException?.StackTrace );
            }
        }
    }
}