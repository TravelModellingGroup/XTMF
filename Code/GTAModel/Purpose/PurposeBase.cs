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
using TMG.Functions;
using XTMF;

namespace TMG.GTAModel
{
    public abstract class PurposeBase : IPurpose
    {
        [RootModule]
        public I4StepModel Root;

        protected bool CachesFlows;

        private WeakReference CachedFlows;

        private List<TreeData<float[][]>> UncachedFlows;

        public List<TreeData<float[][]>> Flows
        {
            get
            {
                if ( CachesFlows )
                {
                    var flows = CachedFlows.Target as List<TreeData<float[][]>>;
                    if ( flows == null )
                    {
                        flows = LoadData();
                        CachedFlows = new WeakReference( flows );
                    }
                    return flows;
                }
                return UncachedFlows;
            }

            set
            {
                if ( CachesFlows )
                {
                    CachedFlows = new WeakReference( value );
                    SaveData( value );
                }
                else
                {
                    UncachedFlows = value;
                }
            }
        }

        [SubModelInformation( Description = "Assign Modes", Required = true )]
        public IMultiModeSplit ModeSplit { get; set; }

        public string Name
        {
            get;
            set;
        }

        public abstract float Progress { get; }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [RunParameter( "Purpose Name", "Work", "The name of this purpose." )]
        public string PurposeName { get; set; }

        [RunParameter( "Result Cache File", "", "The name of the file to cache the results, blank will keep it in memory." )]
        public string ResultCacheFile { get; set; }

        public abstract void Run();

        public virtual bool RuntimeValidation(ref string error)
        {
            CachesFlows = !String.IsNullOrWhiteSpace( ResultCacheFile );
            return true;
        }

        protected void WriteModeSplit(TreeData<float[][]> split, IModeChoiceNode modeNode, string directoryName)
        {
            if ( !Directory.Exists( directoryName ) )
            {
                Directory.CreateDirectory( directoryName );
            }
            if ( split.Result != null )
            {
                var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                Functions.SaveData.SaveMatrix( zones, split.Result, Path.Combine( directoryName, modeNode.ModeName + ".csv" ) );
            }
            if ( split.Children != null )
            {
                for ( int i = 0; i < split.Children.Length; i++ )
                {
                    WriteModeSplit( split.Children[i], ( (IModeCategory)modeNode ).Children[i], directoryName );
                }
            }
        }

        private List<TreeData<float[][]>> LoadData()
        {
            var tree = MirrorModeTree.CreateMirroredTree<float[][]>( Root.Modes );
            try
            {
                BinaryHelpers.ExecuteReader(this, reader =>
                    {
                        // ReSharper disable once UnusedVariable
                        foreach ( var t in tree )
                        {
                            LoadData(reader );
                        }
                    }, ResultCacheFile );
            }
            catch ( IOException e )
            {
                throw new XTMFRuntimeException(this, e.Message );
            }
            return tree;
        }

        private void LoadData(BinaryReader reader)
        {
            var size = reader.ReadSingle();
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            // if the size is NaN then we skip it
            if ( !float.IsNaN( size ) )
            {
                // create the data
                byte[] buffer = null;
                float[][] ret = null;
                Parallel.Invoke( () =>
                    {
                        // fill the buffer
                        var length = numberOfZones * numberOfZones * sizeof( float );
                        buffer = new byte[length];
                        int count = 0;
                        while ( count < length )
                        {
                            count += reader.Read( buffer, count, length - count );
                        }
                    },
                    () =>
                    {
                        // and allocate memory at the same time
                        ret = new float[numberOfZones][];
                        for ( int i = 0; i < numberOfZones; i++ )
                        {
                            ret[i] = new float[numberOfZones];
                        }
                    } );
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    Buffer.BlockCopy( buffer, numberOfZones * i * sizeof( float ), ret[i], 0, numberOfZones * sizeof( float ) );
                }
            }
        }

        private void SaveData(List<TreeData<float[][]>> value)
        {
            try
            {
                var dir = Path.GetDirectoryName( ResultCacheFile );
                if ( !String.IsNullOrWhiteSpace( dir ) && !Directory.Exists( dir ) )
                {
                    Directory.CreateDirectory( dir );
                }
                BinaryHelpers.ExecuteWriter(this, writer =>
                    {
                        foreach ( var tree in value )
                        {
                            SaveData( tree, writer );
                        }
                    }, ResultCacheFile );
            }
            catch ( IOException e )
            {
                throw new XTMFRuntimeException(this, e.Message );
            }
        }

        private void SaveData(TreeData<float[][]> tree, BinaryWriter writer)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            var res = tree.Result;
            if ( res == null )
            {
                writer.Write( float.NaN );
            }
            else
            {
                // needs to be a float because we are using NaN to skip a matrix
                writer.Write( (float)( numberOfZones * numberOfZones ) );
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    if ( res[i] == null )
                    {
                        for ( int j = 0; j < numberOfZones; j++ )
                        {
                            writer.Write( 0f );
                        }
                    }
                    else
                    {
                        for ( int j = 0; j < numberOfZones; j++ )
                        {
                            writer.Write( res[i][j] );
                        }
                    }
                }
            }
            if ( tree.Children != null )
            {
                for ( int i = 0; i < tree.Children.Length; i++ )
                {
                    SaveData( tree.Children[i], writer );
                }
            }
        }
    }
}