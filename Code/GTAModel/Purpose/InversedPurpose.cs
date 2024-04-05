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
    public class InversedPurpose : IPurpose
    {
        [RunParameter( "Other Purpose Name", "Work", "The name of the purpose to inverse." )]
        public string OtherPurposeName;

        [RootModule]
        public I4StepModel Root;

        [RunParameter( "Save Mode Split Output", false, "Should we save the output?" )]
        public bool SaveModeChoiceOutput;

        private int NumberOfModes = 1;

        [DoNotAutomate]
        private IPurpose OtherPurpose;

        public List<TreeData<float[][]>> Flows
        {
            get;
            set;
        }

        [DoNotAutomate]
        public IMultiModeSplit ModeSplit { get; set; }

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
            get;
            set;
        }

        [RunParameter( "Purpose Name", "Inverse", "The name of the purpose." )]
        public string PurposeName
        {
            get;
            set;
        }

        public void Run()
        {
            Progress = 0;
            Flows = MirrorModeTree.CreateMirroredTree<float[][]>( Root.Modes );
            LoadFlows();
            Progress = 1;
            if ( SaveModeChoiceOutput )
            {
                if ( !Directory.Exists( PurposeName ) )
                {
                    Directory.CreateDirectory( PurposeName );
                }
                for ( int i = 0; i < Flows.Count; i++ )
                {
                    WriteModeSplit( Flows[i], Root.Modes[i], PurposeName );
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach ( var purp in Root.Purpose )
            {
                if ( purp.PurposeName == OtherPurposeName )
                {
                    OtherPurpose = purp;
                    break;
                }
            }
            if ( OtherPurpose == null )
            {
                error = "The purpose " + OtherPurposeName + " can not be found by " + PurposeName + " in order to be inversed!";
                return false;
            }
            return true;
        }

        private int CountNumberOfModes()
        {
            int index = 0;
            var length = Flows.Count;
            for ( int i = 0; i < length; i++ )
            {
                CountNumberOfModes( Flows[i], ref index );
            }
            return index;
        }

        private void CountNumberOfModes(TreeData<float[][]> treeData, ref int index)
        {
            index++;
            if ( treeData.Children != null )
            {
                for ( int i = 0; i < treeData.Children.Length; i++ )
                {
                    CountNumberOfModes( treeData.Children[i], ref index );
                }
            }
        }

        private void LoadFlows()
        {
            var length = Flows.Count;
            int index = 0;
            NumberOfModes = CountNumberOfModes();
            for ( int i = 0; i < length; i++ )
            {
                LoadFlows( Flows[i], OtherPurpose.Flows[i], ref index );
            }
        }

        private void LoadFlows(TreeData<float[][]> ourNode, TreeData<float[][]> copyNode, ref int index)
        {
            if ( ourNode.Children != null )
            {
                var length = ourNode.Children.Length;
                for ( int i = 0; i < length; i++ )
                {
                    LoadFlows( ourNode.Children[i], copyNode, ref index );
                }
            }
            var otherData = copyNode.Result;
            if ( otherData == null ) return;
            var numberOfZones = otherData.Length;
            var data = new float[numberOfZones][];
            for ( int j = 0; j < numberOfZones; j++ )
            {
                data[j] = new float[numberOfZones];
            }
            Progress = (float)index / NumberOfModes;
            index++;
            Parallel.For( 0, numberOfZones, delegate(int i)
            {
                var row = otherData[i];
                if ( row == null ) return;
                for ( int j = 0; j < numberOfZones; j++ )
                {
                    data[j][i] = row[j];
                }
            } );
            ourNode.Result = data;
        }

        private void WriteModeSplit(TreeData<float[][]> split, IModeChoiceNode modeNode, string directoryName)
        {
            if ( !Directory.Exists( directoryName ) )
            {
                Directory.CreateDirectory( directoryName );
            }
            Task writeTask = new( delegate
            {
                using StreamWriter writer = new(Path.Combine(directoryName, modeNode.ModeName + ".csv"));
                var header = true;
                var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
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
                    var row = split.Result == null ? null : split.Result[i];
                    writer.Write(zones[i].ZoneNumber);
                    for (int j = 0; j < zones.Length; j++)
                    {
                        writer.Write(',');
                        writer.Write(row == null ? 0 : row[j]);
                    }
                    writer.WriteLine();
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
    }
}