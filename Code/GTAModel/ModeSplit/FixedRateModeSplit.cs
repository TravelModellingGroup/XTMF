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
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.ModeSplit
{
    public class FixedRateModeSplit : IMultiModeSplit
    {
        [SubModelInformation( Description = "Data to use to get data on modes.", Required = true )]
        public List<ModeData> Data;

        [RootModule]
        public I4StepModel Root;

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

        public List<TreeData<float[][]>> ModeSplit(IEnumerable<SparseTwinIndex<float>> flowMatrix, int numberOfCategories)
        {
            var ret = MirrorModeTree.CreateMirroredTree<float[][]>( Root.Modes );
            int matrixNumber = 0;
            foreach ( var matrix in flowMatrix )
            {
                var flatMatrix = matrix.GetFlatData();
                var numberOfZones = flatMatrix.Length;
                try
                {
                    Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                        delegate(int i)
                        {
                            var modes = Root.Modes;
                            for ( int j = 0; j < numberOfZones; j++ )
                            {
                                for ( int m = ret.Count - 1; m >= 0; m-- )
                                {
                                    ProcessMode( ret[m], i, j, flatMatrix[i][j], modes[m], matrixNumber );
                                }
                            }
                        } );
                }
                catch ( AggregateException e )
                {
                    if ( e.InnerException is XTMFRuntimeException )
                    {
                        throw new XTMFRuntimeException( e.InnerException.Message );
                    }
                    else
                    {
                        throw new XTMFRuntimeException( e.InnerException.Message + "\r\n" + e.InnerException.StackTrace );
                    }
                }
                matrixNumber++;
            }
            return ret;
        }

        public List<TreeData<float[][]>> ModeSplit(SparseTwinIndex<float> flowMatrix)
        {
            // no need to optimize this case since it is very rare.
            return ModeSplit( new[] { flowMatrix }, 1 );
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void ProcessMode(TreeData<float[][]> treeData, int i, int j, float flow, IModeChoiceNode node, int matrixNumber)
        {
            var cat = node as IModeCategory;
            if ( cat != null )
            {
                // then go 1 level deeper
                for ( int m = cat.Children.Count - 1; m >= 0; m-- )
                {
                    ProcessMode( treeData.Children[m], i, j, flow, cat.Children[m], matrixNumber );
                }
                // then sum
                var sum = 0f;
                for ( int m = cat.Children.Count - 1; m >= 0; m-- )
                {
                    var res = treeData.Children[m].Result;
                    if ( res == null || res[i] == null ) continue;
                    sum += res[i][j];
                }
                SetData( treeData.Result, i, j, sum );
            }
            else
            {
                for ( int dataIndex = 0; dataIndex < Data.Count; dataIndex++ )
                {
                    if ( Data[dataIndex].Mode == node )
                    {
                        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                        var data = Data[dataIndex].Data;
                        if ( data == null )
                        {
                            throw new XTMFRuntimeException( "In '" + Name + "' we tried to access the data for mode split from mode '" + Data[dataIndex].ModeName + "' however it was not initialized!" );
                        }
                        if ( treeData.Result == null )
                        {
                            lock ( treeData )
                            {
                                Thread.MemoryBarrier();
                                if ( treeData.Result == null )
                                {
                                    treeData.Result = new float[zones.Length][];
                                }
                            }
                        }
                        SetData( treeData.Result, i, j, flow * data.GetDataFrom( zones[i].ZoneNumber, zones[j].ZoneNumber, matrixNumber ) );
                    }
                }
            }
        }

        private void SetData(float[][] data, int i, int j, float value)
        {
            if ( data[i] == null )
            {
                data[i] = new float[data.Length];
            }
            data[i][j] += value;
        }

        public class ModeData : IModule
        {
            [SubModelInformation( Description = "The data that represents this mode's share.", Required = true )]
            public IODDataSource<float> Data;

            [RunParameter( "Mode Name", "Auto", "The name of the mode that the contained data will be used for." )]
            public string ModeName;

            [RootModule]
            public I4StepModel Root;

            private IModeChoiceNode _Mode;

            [DoNotAutomate]
            public IModeChoiceNode Mode
            {
                get
                {
                    if ( _Mode == null )
                    {
                        return LoadMode();
                    }
                    return _Mode;
                }
            }

            private IModeChoiceNode LoadMode()
            {
                var modes = Root.Modes;
                for ( int i = 0; i < modes.Count; i++ )
                {
                    if ( FindOurMode( modes[i] ) )
                    {
                        return _Mode;
                    }
                }
                throw new XTMFRuntimeException( "In '" + Name + "' we were unable to find a mode called '"
                    + ModeName + "', please make sure that this mode exists!" );
            }

            public string Name { get; set; }

            public float Progress { get { return 0; } }

            public Tuple<byte, byte, byte> ProgressColour { get { return null; } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }

            private bool FindOurMode(IModeChoiceNode node)
            {
                if ( node.ModeName == ModeName )
                {
                    _Mode = node;
                    return true;
                }
                var cat = node as IModeCategory;
                if ( cat != null )
                {
                    for ( int i = 0; i < cat.Children.Count; i++ )
                    {
                        if ( FindOurMode( cat ) )
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }
    }
}