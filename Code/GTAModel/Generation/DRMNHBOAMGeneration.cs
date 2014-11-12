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
using System.Threading.Tasks;
using Datastructure;
using TMG.GTAModel.DataUtility;
using XTMF;

namespace TMG.GTAModel.Generation
{
    public class DRMNHBOAMGeneration : IDemographicCategoryGeneration
    {
        [RunParameter( "Auto Drive Name", "ADrive", "The name of the auto drive mode." )]
        public string AutoDriveModeName;

        public List<int> AutoDrivePath;

        [RunParameter( "Demographic Parameter Set Index", 0, "The 0 indexed index of parameters to use when calculating utility" )]
        public int DemographicParameterSetIndex;

        [RunParameter( "HBO Purpose Name", "HBO", "The name of the work purpose." )]
        public string HomeBasedOtherPurposeName;

        [RunParameter( "ModeChoice Parameter Set Index", 0, "The 0 indexed index of parameters to use when calculating utility" )]
        public int ModeChoiceParameterSetIndex;

        [RunParameter( "Region Constant Parameters", "1.591954326,-2.812772298,-2.203225809,-34.79059286", typeof( FloatList ), "The region parameters for Auto Times." )]
        public FloatList RegionConstantsParameter;

        [RunParameter( "Region General Parameters", "0,0,0,0", typeof( FloatList ), "The region parameters for the General Employment." )]
        public FloatList RegionEmploymentGeneralParameter;

        [RunParameter( "Region Manufacturing Parameters", "0.097629818,0.208416925,0.096070043,0.162521643", typeof( FloatList ), "The region parameters for the Manufacturing Employment." )]
        public FloatList RegionEmploymentManufacturingParameter;

        [RunParameter( "Region Professional Parameters", "0,0,0,0", typeof( FloatList ), "The region parameters for the Professional Employment." )]
        public FloatList RegionEmploymentProfessionalParameter;

        [RunParameter( "Region Sales Parameters", "0,0,0,0", typeof( FloatList ), "The region parameters for the Sales Employment." )]
        public FloatList RegionEmploymentSalesParameter;

        [RunParameter( "Region Home-Other Drive Parameters", "0.899332965,0.801837132,0.680872577,0.752931716", typeof( FloatList ), "The region parameters for the Population." )]
        public FloatList RegionHomeOtherDriveParameter;

        [RunParameter( "Region Home-Other Other Parameters", "0.155969455,0.190094001,0.573941756,0.882749914", typeof( FloatList ), "The region parameters for the Population." )]
        public FloatList RegionHomeOtherOtherParameter;

        [RunParameter( "Region Home-School Drive Parameters", "0,0,0,0", typeof( FloatList ), "The region parameters for the Population." )]
        public FloatList RegionHomeSchoolDriveParameter;

        [RunParameter( "Region Home-School Other Parameters", "0.010840853,0.056333868,0.094647873,0.063186155", typeof( FloatList ), "The region parameters for the Population." )]
        public FloatList RegionHomeSchoolOtherParameter;

        [RunParameter( "Region Home-Work Drive Parameters", "0,0,0,0", typeof( FloatList ), "The region parameters for the Population." )]
        public FloatList RegionHomeWorkDriveParameter;

        [RunParameter( "Region Home-Work Other Parameters", "0,0,0,0", typeof( FloatList ), "The region parameters for the Population." )]
        public FloatList RegionHomeWorkOtherParameter;

        [RunParameter( "Region Numbers", "1,2,3,4", typeof( NumberList ), "The space to be reading region parameters in from.\r\nThis is used as an inverse lookup for the parameters." )]
        public NumberList RegionNumbers;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        [RunParameter( "School Purpose Name", "School", "The name of the school purpose." )]
        public string SchoolPurposeName;

        [RunParameter( "Work Purpose Name", "Work", "The name of the work purpose." )]
        public string WorkPurposeName;

        [DoNotAutomate]
        private IPurpose HomeBasedOtherPurpose;

        [DoNotAutomate]
        private IPurpose SchoolPurpose;

        [DoNotAutomate]
        private IPurpose WorkPurpose;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Generate(SparseArray<float> production, SparseArray<float> attractions)
        {
            // no init since we are using raw auto times
            //this.InitializeDemographicCategory();
            var rootModes = this.Root.Modes;
            var numberOfModes = rootModes.Count;
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            // Work
            AddPurposeData( this.WorkPurpose, LookUpPurposeData( this.WorkPurpose ), production,
                this.RegionHomeWorkDriveParameter, this.RegionHomeWorkOtherParameter );
            // School
            AddPurposeData( this.SchoolPurpose, LookUpPurposeData( this.SchoolPurpose ), production,
                this.RegionHomeSchoolDriveParameter, this.RegionHomeSchoolOtherParameter );
            //HBO
            AddPurposeData( this.HomeBasedOtherPurpose, LookUpPurposeData( this.HomeBasedOtherPurpose ), production,
                this.RegionHomeOtherDriveParameter, this.RegionHomeOtherOtherParameter );
            // Now that all of the purposes have been added in we can go and add in the professions
            var flatProduction = production.GetFlatData();
            Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int i)
                    {
                        var increment = 0f;
                        int regionIndex;
                        if ( !this.InverseLookup( zones[i].RegionNumber, out regionIndex ) )
                        {
                            // if this zone isn't part of a region we are processing just continue
                            return;
                        }
                        increment += this.RegionEmploymentProfessionalParameter[regionIndex] * zones[i].ProfessionalEmployment;
                        increment += this.RegionEmploymentGeneralParameter[regionIndex] * zones[i].GeneralEmployment;
                        increment += this.RegionEmploymentSalesParameter[regionIndex] * zones[i].RetailEmployment;
                        increment += this.RegionEmploymentManufacturingParameter[regionIndex] * zones[i].ManufacturingEmployment;
                        flatProduction[i] += increment + this.RegionConstantsParameter[regionIndex];
                        // make sure we don't end up with negative trips
                        if ( flatProduction[i] < 0 ) flatProduction[i] = 0;
                    } );
        }

        public void InitializeDemographicCategory()
        {
            this.Root.ModeParameterDatabase.ApplyParameterSet( this.ModeChoiceParameterSetIndex, this.DemographicParameterSetIndex );
        }

        public bool IsContained(IPerson person)
        {
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            // link the purpose names
            if ( !LinkPurposeName( this.WorkPurposeName, out this.WorkPurpose ) )
            {
                error = "We were unable to find a purpose named '" + this.WorkPurposeName + "' for the work purpose of '" + this.Name + "'!";
                return false;
            }
            if ( !LinkPurposeName( this.SchoolPurposeName, out this.SchoolPurpose ) )
            {
                error = "We were unable to find a purpose named '" + this.WorkPurposeName + "' for the work purpose of '" + this.Name + "'!";
                return false;
            }
            if ( !LinkPurposeName( this.HomeBasedOtherPurposeName, out this.HomeBasedOtherPurpose ) )
            {
                error = "We were unable to find a purpose named '" + this.WorkPurposeName + "' for the work purpose of '" + this.Name + "'!";
                return false;
            }
            // link in the mode path so we can quickly look it up
            if ( !LinkModeName( this.AutoDriveModeName ) )
            {
                error = "We were unable to find the mode that represents auto drive!";
                return false;
            }
            return true;
        }

        private void AddPurposeData(IPurpose purpose, float[][] driveData, SparseArray<float> production, FloatList purposeDrive, FloatList purposeOther)
        {
            var modes = purpose.Flows;
            if ( modes == null ) return;
            var length = modes.Count;
            for ( int i = 0; i < length; i++ )
            {
                AddPurposeData( driveData, modes[i], production, purposeDrive, purposeOther );
            }
        }

        private void AddPurposeData(float[][] driveData, TreeData<float[][]> current, SparseArray<float> production, FloatList purposeDrive, FloatList purposeOther)
        {
            var cat = current.Children;
            if ( cat != null )
            {
                var length = cat.Length;
                for ( int i = 0; i < length; i++ )
                {
                    AddPurposeData( driveData, cat[i], production, purposeDrive, purposeOther );
                }
            }
            else
            {
                // we only add in end nodes
                var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
                var numberOfZones = zones.Length;
                var data = current.Result;
                // shortcut invalid modes
                if ( data == null ) return;
                var flatProduction = production.GetFlatData();
                if ( driveData == data )
                {
                    // Auto drive case
                    Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int i)
                    {
                        var increment = 0f;
                        int regionIndex;
                        if ( !this.InverseLookup( zones[i].RegionNumber, out regionIndex ) )
                        {
                            // if this zone isn't part of a region we are processing just continue
                            return;
                        }
                        increment = purposeDrive[regionIndex] * SumWentHere( current, i );
                        flatProduction[i] += increment;
                    } );
                }
                else
                {
                    // not auto drive case
                    Parallel.For( 0, numberOfZones, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate(int i)
                    {
                        var increment = 0f;
                        int regionIndex;
                        if ( !this.InverseLookup( zones[i].RegionNumber, out regionIndex ) )
                        {
                            // if this zone isn't part of a region we are processing just continue
                            return;
                        }
                        increment = purposeOther[regionIndex] * SumWentHere( current, i );
                        flatProduction[i] += increment;
                    } );
                }
            }
        }

        private bool InverseLookup(int regionNumber, out int regionIndex)
        {
            return ( regionIndex = this.RegionNumbers.IndexOf( regionNumber ) ) != -1;
        }

        private bool LinkModeName(string p)
        {
            var modes = this.Root.Modes;
            var length = modes.Count;
            this.AutoDrivePath = new List<int>();
            for ( int i = 0; i < length; i++ )
            {
                if ( LinkModeName( p, modes[i] ) )
                {
                    // insert at the front
                    this.AutoDrivePath.Insert( 0, i );
                    return true;
                }
            }
            return false;
        }

        private bool LinkModeName(string p, IModeChoiceNode current)
        {
            if ( current.ModeName == p )
            {
                return true;
            }
            var cat = current as IModeCategory;
            if ( cat != null )
            {
                var modes = cat.Children;
                var length = modes.Count;
                for ( int i = 0; i < length; i++ )
                {
                    if ( LinkModeName( p, modes[i] ) )
                    {
                        // insert at the front
                        this.AutoDrivePath.Insert( 0, i );
                        return true;
                    }
                }
            }
            return false;
        }

        private bool LinkPurposeName(string purposeName, out IPurpose destinationPurpose)
        {
            var purposes = this.Root.Purpose;
            foreach ( var p in purposes )
            {
                if ( p.PurposeName == purposeName )
                {
                    destinationPurpose = p;
                    return true;
                }
            }
            // if we haven't found it, fill it in here
            destinationPurpose = null;
            return false;
        }

        private float[][] LookUpPurposeData(IPurpose purpose)
        {
            var data = purpose.Flows;
            if ( data == null ) return null;
            TreeData<float[][]> currentPoint = data[this.AutoDrivePath[0]];
            var length = this.AutoDrivePath.Count;
            for ( int i = 1; i < length; i++ )
            {
                currentPoint = currentPoint.Children[this.AutoDrivePath[i]];
            }
            return currentPoint.Result;
        }

        private float SumWentHere(TreeData<float[][]> current, int flatZoneIndex)
        {
            var data = current.Result;
            // fast way to ditch modes
            if ( data == null ) return 0;
            var length = data.Length;
            var count = 0f;
            for ( int i = 0; i < length; i++ )
            {
                if ( data[i] != null )
                {
                    count += data[i][flatZoneIndex];
                }
            }
            return count;
        }
    }
}