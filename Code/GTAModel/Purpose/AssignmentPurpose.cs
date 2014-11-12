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
using System.Collections.Generic;
using System.IO;
using Datastructure;
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation(Description=
        @"This module provides one of the basic types of purposes used by GTAModel. 
An assignment purpose is similar to any in a four step model however the generation and distribution 
step have been fused since they work at an individual agent level. The AssignmentPurpose module takes 
in an IAssignment sub-module, a list of IDemographicCategory modules and an IUtilityModifyModeSplit module.
This module requires the root module of the model system to be an ‘I4StepModel’." )]
    public sealed class AssignmentPurpose : PurposeBase
    {
        [SubModelInformation( Description = "Assignment Distribution", Required = true )]
        public IAssignment AssignmentDistribution;

        [RunParameter( "Number of Categories", 1, "The number of demographic categories that the assignment will be processing." )]
        public int NumberOfCategories;

        [RunParameter( "Save Mode Split Output", false, "Should we save the output?" )]
        public bool SaveModeChoiceOutput;

        private int CurrentCategory;

        public override float Progress
        {
            get { return ( (float)this.CurrentCategory / this.NumberOfCategories ) + ( this.ModeSplit.Progress / this.NumberOfCategories ); }
        }

        public override void Run()
        {
            // For Each Demographic Category
            var modeSplit = this.ModeSplit.ModeSplit( EnumerateDistributions(), NumberOfCategories );
            if ( this.SaveModeChoiceOutput )
            {
                if ( !Directory.Exists( this.PurposeName ) )
                {
                    Directory.CreateDirectory( this.PurposeName );
                }
                for ( int i = 0; i < modeSplit.Count; i++ )
                {
                    this.WriteModeSplit( modeSplit[i], this.Root.Modes[i], this.PurposeName );
                }
            }
            this.Flows = modeSplit;
        }

        private IEnumerable<SparseTwinIndex<float>> EnumerateDistributions()
        {
            this.CurrentCategory = 0;
            foreach ( var demographic in this.AssignmentDistribution.Assign() )
            {
                // let it setup the modes so we can compute friction
                yield return demographic;
                this.CurrentCategory++;
            }
        }
    }
}