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
using System.Linq;
using XTMF;

namespace TMG.GTAModel.Analysis
{
    [ModuleInformation( Description = "This module is designed to produce a file that builds a summary of the modes chosen for given purposes." )]
    public class SumPurposeModeChoice : ISelfContainedModule
    {
        [RunParameter( "OutputFile", "PurposeChoices.csv", "The name of the summary file.  Leave this blank to not generate a file." )]
        public string FileName;

        [RunParameter( "Purpose Names", "", "The names of the purposes that you wish to include in the summery.  Leave this blank to do all purposes.  e.g. \"HBW,HBS,HBO" )]
        public string PurposeNames;

        [RootModule]
        public I4StepModel Root;

        /// <summary>
        /// The purpose indexes to process
        /// </summary>
        protected int[] PurposeIndexes;

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

        public Tuple<byte, byte, byte> ProgressColour => null;

        public bool RuntimeValidation(ref string error)
        {
            if ( !ProcessPurposeNames( ref error ) )
            {
                return false;
            }
            return true;
        }

        public void Start()
        {
            var purposes = Root.Purpose;
            var numberOfModes = GetNumberOfModes();
            var totals = new double[numberOfModes];
            using var writer = new StreamWriter(FileName);
            WriteHeader(writer);
            for (var i = 0; i < PurposeIndexes.Length; i++)
            {
                AddPurpose(writer, totals, purposes[PurposeIndexes[i]]);
            }
            WriteFinalSummary(writer, totals, totals.Sum());
        }

        private void AddMode(StreamWriter writer, double[] totals, ref int index, TreeData<float[][]> treeData)
        {
            writer.Write( ',' );
            var modeTotal = Sum( treeData );
            writer.Write( modeTotal );
            totals[index] += modeTotal;
            index++;
            if ( treeData.Children == null ) return;
            for ( var i = 0; i < treeData.Children.Length; i++ )
            {
                AddMode( writer, totals, ref index, treeData.Children[i] );
            }
        }

        private void AddPurpose(StreamWriter writer, double[] totals, IPurpose purpose)
        {
            // start the line with the purpose name
            writer.Write( purpose.PurposeName );

            var flows = purpose.Flows;
            if ( flows == null )
            {
                for ( var i = 0; i < totals.Length; i++ )
                {
                    writer.Write( ',' );
                    writer.Write( 0 );
                }
            }
            else
            {
                var index = 0;
                for ( var i = 0; i < flows.Count; i++ )
                {
                    AddMode( writer, totals, ref index, flows[i] );
                }
            }
            // complete the line of data
            writer.WriteLine();
        }

        private int GetNumberOfModes()
        {
            var modes = Root.Modes;
            var total = 0;
            for ( var i = 0; i < modes.Count; i++ )
            {
                total += GetNumberOfModes( modes[i] );
            }
            return total;
        }

        private int GetNumberOfModes(IModeChoiceNode node)
        {
            var cat = node as IModeCategory;
            var total = 1;
            if ( cat != null )
            {
                var children = cat.Children;
                var length = children.Count;
                for ( var i = 0; i < length; i++ )
                {
                    total += GetNumberOfModes( children[i] );
                }
            }
            return total;
        }

        private bool ProcessPurposeNames(ref string error)
        {
            if ( String.IsNullOrWhiteSpace( PurposeNames ) )
            {
                var length = ( Root.Purpose ).Count;
                PurposeIndexes = new int[length];
                for ( var i = 0; i < length; i++ )
                {
                    PurposeIndexes[i] = i;
                }
            }
            else
            {
                var parts = PurposeNames.Split( ',' );
                var care = new List<int>();
                var purposes = Root.Purpose;
                var numberOfPurposes = purposes.Count;
                foreach ( var part in parts )
                {
                    var trimmed = part.Trim();
                    var found = false;
                    for ( var i = 0; i < numberOfPurposes; i++ )
                    {
                        if ( purposes[i].PurposeName == trimmed )
                        {
                            care.Add( i );
                            found = true;
                            break;
                        }
                    }
                    if ( !found )
                    {
                        error = "We were unable to finda purpose with the name \"" + trimmed + "\"!";
                        return false;
                    }
                }
                PurposeIndexes = care.ToArray();
            }
            return true;
        }

        private double Sum(TreeData<float[][]> treeData)
        {
            if ( treeData == null || treeData.Result == null ) return 0;
            var data = treeData.Result;
            var localTotal = 0.0;
            for ( var i = 0; i < data.Length; i++ )
            {
                if ( data[i] != null )
                {
                    localTotal += data[i].Sum();
                }
            }
            return localTotal;
        }

        private void WriteFinalSummary(StreamWriter writer, double[] totals, double grandTotal)
        {
            writer.WriteLine();
            writer.Write( "Totals" );
            for ( var i = 0; i < totals.Length; i++ )
            {
                writer.Write( ',' );
                writer.Write( totals[i] );
            }
            writer.Write( ',' );
            writer.WriteLine( grandTotal );
        }

        private void WriteHeader(StreamWriter writer)
        {
            var modes = Root.Modes;
            writer.WriteLine( "Model Run Summary" );
            writer.Write( "Purpose" );
            foreach ( var mode in modes )
            {
                writer.Write( ',' );
                WriteHeader( writer, mode );
            }
            writer.WriteLine();
        }

        private void WriteHeader(StreamWriter writer, IModeChoiceNode mode)
        {
            writer.Write( mode.ModeName );
            if (mode is IModeCategory cat)
            {
                foreach (var child in cat.Children)
                {
                    writer.Write(',');
                    WriteHeader(writer, child);
                }
            }
        }
    }
}