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
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation(Description=
        @"The demographic purpose module glues together a list of IDemographicCategoryGeneration, 
runs them through the IDemographicDistribution, and then finally performs an IUtilityModifyModesplit. 
As all IPurpose, it provides the final flows per mode for the network assignment stage.
This module requires the root module of the model system to be an 'I4StepModel'." )]
    public sealed class DemographicPurpose : PurposeBase, IDemographicCategoyPurpose
    {
        [SubModelInformation( Description = "Trip Distribution", Required = true )]
        public IDemographicDistribution Distribution;

        [RunParameter( "Save Output", false, "Should we save the output?" )]
        public bool SaveOutput;

        [RunParameter( "Transpose Mode Choice", false, "Should we transpose the output of the mode choice?" )]
        public bool Transpose;

        private bool Generation = true;

        [SubModelInformation( Description = "Demographic Categories", Required = false )]
        public List<IDemographicCategoryGeneration> Categories { get; set; }

        public override float Progress
        {
            get { return Generation ? 0f : ModeSplit.Progress; }
        }

        public override void Run()
        {
            var numberOfCategories = Categories.Count;
            SparseArray<float>[] O = new SparseArray<float>[numberOfCategories];
            SparseArray<float>[] D = new SparseArray<float>[numberOfCategories];

            Generation = true;
            for ( int i = 0; i < numberOfCategories; i++ )
            {
                O[i] = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
                D[i] = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
                Categories[i].Generate( O[i], D[i] );
            }
            Generation = false;

            var modeSplit = ModeSplit.ModeSplit( Distribution.Distribute( O, D, Categories ), Categories.Count );
            if ( Transpose )
            {
                TransposeMatrix( modeSplit );
            }
            if ( SaveOutput )
            {
                if ( !Directory.Exists( PurposeName ) )
                {
                    Directory.CreateDirectory( PurposeName );
                }
                for ( int i = 0; i < modeSplit.Count; i++ )
                {
                    WriteModeSplit( modeSplit[i], Root.Modes[i], PurposeName );
                }
            }
            Flows = modeSplit;
        }

        public override bool RuntimeValidation(ref string error)
        {
            return base.RuntimeValidation( ref error );
        }

        private static void TransposeMatrix(TreeData<float[][]> treeData)
        {
            // no need to code this in parallel since we are doing all of them in parallel
            var matrix = treeData.Result;
            var length = matrix.Length;
            var halfLength = length / 2;
            // fill in the array
            for ( int i = 0; i < length; i++ )
            {
                if ( matrix[i] == null )
                {
                    matrix[i] = new float[length];
                }
            }
            for ( int i = 0; i < length; i++ )
            {
                for ( int j = 0; j < halfLength; j++ )
                {
                    var temp = matrix[i][j];
                    matrix[i][j] = matrix[j][i];
                    matrix[j][i] = temp;
                }
            }
        }

        private void GatherModes(List<TreeData<float[][]>> flatModes, TreeData<float[][]> treeData)
        {
            if ( treeData.Result != null )
            {
                flatModes.Add( treeData );
            }
            var children = treeData.Children;
            if ( children != null )
            {
                for ( var i = 0; i < children.Length; i++ )
                {
                    GatherModes( flatModes, children[i] );
                }
            }
        }

        private void TransposeMatrix(List<TreeData<float[][]>> modeSplit)
        {
            List<TreeData<float[][]>> flatModes = new List<TreeData<float[][]>>();
            for ( int i = 0; i < modeSplit.Count; i++ )
            {
                GatherModes( flatModes, modeSplit[i] );
            }
            Parallel.For( 0, flatModes.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int i)
                {
                    TransposeMatrix( flatModes[i] );
                } );
        }
    }
}