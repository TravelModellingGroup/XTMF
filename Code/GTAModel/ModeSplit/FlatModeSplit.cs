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
using TMG.Input;
using TMG.ModeSplit;
using XTMF;

namespace TMG.GTAModel.ModeSplit
{
    public class FlatModeSplit : IInteractiveModeSplit
    {
        [SubModelInformation( Required = false, Description = "Apply factors to the exponated utility of modes" )]
        public ModeAdjustments Adjustments;

        [RootModule]
        public I4StepModel Root;

        [RunParameter( "Simulation Time", "7:00 AM", typeof( Time ), "The time that this mode split will be running as." )]
        public Time SimulationTime;

        private float CurrentInteractiveCategory;
        private float[] CurrentUtility;
        private bool InterativeMode = false;
        private bool LoadedAdjustments = false;
        private IModeChoiceNode[] Modes;
        private int NumberOfInteractiveCategories = 0;

        private TreeData<float[][]>[] Results;
        private IZone[] Zones;

        private SparseArray<IZone> ZoneSystem;

        public string Name { get; set; }

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
            float sum = 0f;
            var flatO = this.ZoneSystem.GetFlatIndex( o.ZoneNumber );
            var flatD = this.ZoneSystem.GetFlatIndex( d.ZoneNumber );
            bool any = false;
            var zoneIndex = ( flatO * this.Zones.Length + flatD ) * this.Modes.Length;
            for ( int mode = 0; mode < this.Modes.Length; mode++ )
            {
                EnsureResult( flatO, mode );
                if ( this.Modes[mode].Feasible( o, d, this.SimulationTime ) )
                {
                    var res = this.Modes[mode].CalculateV( o, d, this.SimulationTime );
                    if ( !float.IsNaN( res ) )
                    {
                        float v = (float)Math.Exp( res );
                        if ( this.Adjustments != null )
                        {
                            v *= this.Adjustments.GiveAdjustment( o, d, mode, (int)this.CurrentInteractiveCategory );
                        }
                        this.CurrentUtility[zoneIndex + mode] = v;
                        sum += v;
                        any = true;
                    }
                }
            }
            return any ? (float)sum : float.NaN;
        }

        public void EndInterativeModeSplit()
        {
            this.Results = null;
            this.CurrentUtility = null;
        }

        public List<TreeData<float[][]>> ModeSplit(IEnumerable<SparseTwinIndex<float>> flowMatrix, int numberOfCategories)
        {
            if ( this.Modes == null )
            {
                SetModes();
            }
            this.Progress = 0f;
            this.CurrentInteractiveCategory = 0;
            foreach ( var matrix in flowMatrix )
            {
                AddModeSplit( matrix );
                this.CurrentInteractiveCategory++;
            }
            this.Progress = 1f;
            return CreateList();
        }

        public List<TreeData<float[][]>> ModeSplit(SparseTwinIndex<float> flowMatrix)
        {
            AddModeSplit( flowMatrix );
            return CreateList();
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void StartNewInteractiveModeSplit(int numberOfInteractiveCategories)
        {
            this.NumberOfInteractiveCategories = numberOfInteractiveCategories;
            this.ZoneSystem = this.Root.ZoneSystem.ZoneArray;
            this.Zones = this.ZoneSystem.GetFlatData();
            this.InterativeMode = true;
            SetModes();
            this.InitializeResults();
            if ( !LoadedAdjustments & this.Adjustments != null )
            {
                this.Adjustments.Load();
            }
        }

        private void AddModeSplit(SparseTwinIndex<float> matrix)
        {
            if ( this.Results == null )
            {
                InitializeResults();
            }
            if ( this.InterativeMode )
            {
                ProduceResultsForInteractive( matrix.GetFlatData() );
            }
            else
            {
                throw new XTMFRuntimeException( "Only Interactive mode is supported!" );
            }
        }

        private List<TreeData<float[][]>> CreateList()
        {
            var ret = new List<TreeData<float[][]>>( this.Results.Length );
            for ( int i = 0; i < this.Results.Length; i++ )
            {
                ret.Add( this.Results[i] );
            }
            EndInterativeModeSplit();
            return ret;
        }

        private void EnsureResult(int flatO, int mode)
        {
            if ( this.Results[mode].Result[flatO] == null )
            {
                lock ( this.Results )
                {
                    Thread.MemoryBarrier();
                    if ( this.Results[mode].Result[flatO] == null )
                    {
                        this.Results[mode].Result[flatO] = new float[this.Zones.Length];
                        Thread.MemoryBarrier();
                    }
                }
            }
        }

        private void InitializeResults()
        {
            var numberOfZones = this.Zones.Length;
            var numberOfModes = this.Root.Modes.Count;
            if ( this.CurrentUtility == null )
            {
                this.CurrentUtility = new float[numberOfZones * numberOfZones * numberOfModes];
            }
            // in all cases reset this value
            for ( int i = 0; i < this.CurrentUtility.Length; i++ )
            {
                this.CurrentUtility[i] = float.NaN;
            }
            if ( this.Results == null )
            {
                this.Results = new TreeData<float[][]>[numberOfModes];
                for ( int i = 0; i < this.Results.Length; i++ )
                {
                    this.Results[i] = new TreeData<float[][]>();
                    this.Results[i].Result = new float[numberOfZones][];
                }
            }
        }

        private void ProduceResultsForInteractive(float[][] flows)
        {
            Parallel.For( 0, flows.Length, (int flatO) =>
                {
                    var row = flows[flatO];
                    if ( row == null ) return;
                    var numberOfModes = this.Modes.Length;
                    for ( int j = 0; j < row.Length; j++ )
                    {
                        var flow = flows[flatO][j];
                        // skip processing this OD if there are no trips between them
                        if ( flow <= 0 ) continue;
                        var zoneIndex = ( flatO * row.Length + j ) * numberOfModes;
                        // get the sum
                        float sum = 0f;
                        bool any = false;
                        for ( int mode = 0; mode < numberOfModes; mode++ )
                        {
                            var cur = this.CurrentUtility[zoneIndex + mode];
                            if ( !float.IsNaN( cur ) )
                            {
                                sum += cur;
                                any = true;
                            }
                        }
                        if ( !any )
                        {
                            continue;
                        }
                        // procude probabilities
                        var factor = 1 / sum;
                        for ( int mode = 0; mode < numberOfModes; mode++ )
                        {
                            var temp = this.CurrentUtility[zoneIndex + mode] * factor;
                            if ( !float.IsNaN( temp ) )
                            {
                                this.Results[mode].Result[flatO][j] += temp * flow;
                            }
                        }
                    }
                } );
            this.Progress = ( ( this.CurrentInteractiveCategory + 1 ) / (float)this.NumberOfInteractiveCategories );
        }

        private void SetModes()
        {
            if ( this.Modes == null )
            {
                this.Modes = new IModeChoiceNode[this.Root.Modes.Count];
                for ( int i = 0; i < this.Modes.Length; i++ )
                {
                    this.Modes[i] = (IModeChoiceNode)this.Root.Modes[i];
                }
            }
        }
    }

    public class ModeAdjustments : IModule
    {
        [Parameter( "Adjustment Matrix File", "Distribution/WorkModeAdjustments.csv", typeof( FileFromInputDirectory ),
            "The file that contains the mode adjustments.  In CSV form (Occ,OriginPdStart,OriginPdEnd,DestinationPDStart,DesinstaionPDEnd,[1 column for each mode])" )]
        public FileFromInputDirectory InputFile;

        [Parameter( "Matrices Per Occupation", 20, "The number of matrices processed before switching occupation." )]
        public int MatriciesPerOccupation;

        [RunParameter( "Number of Occupations", 4, "The number of different occupations for this model." )]
        public int NumberOfOccupations;

        [RunParameter( "Occupation Start Index", 1, "The number for the first occupation." )]
        public int OccupationStartIndex;

        [RootModule]
        public I4StepModel Root;

        private Segment[][] Data;

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

        public float GiveAdjustment(IZone origin, IZone destination, int mode, int currentMatrix)
        {
            var occNumber = currentMatrix / this.MatriciesPerOccupation;
            var oPD = origin.PlanningDistrict;
            var dPD = destination.PlanningDistrict;
            var row = this.Data[occNumber];
            var adjFactor = 1f;
            for ( int i = 0; i < row.Length; i++ )
            {
                if ( row[i].Origin.ContainsInclusive( oPD ) & row[i].Destination.ContainsInclusive( dPD ) )
                {
                    adjFactor *= row[i].ModificationForMode[mode];
                }
            }
            return adjFactor;
        }

        public void Load()
        {
            List<Segment>[] temp = new List<Segment>[this.NumberOfOccupations];
            for ( int i = 0; i < temp.Length; i++ )
            {
                temp[i] = new List<Segment>();
            }
            var numberOfModes = this.Root.Modes.Count;
            using ( CsvReader reader = new CsvReader( this.InputFile.GetFileName( this.Root.InputBaseDirectory ) ) )
            {
                // burn header
                reader.LoadLine();
                while ( !reader.EndOfFile )
                {
                    if ( reader.LoadLine() >= numberOfModes + 5 )
                    {
                        int occ, os, oe, ds, de;
                        reader.Get( out occ, 0 );
                        reader.Get( out os, 1 );
                        reader.Get( out oe, 2 );
                        reader.Get( out ds, 3 );
                        reader.Get( out de, 4 );
                        float[] modeData = new float[numberOfModes];
                        for ( int i = 0; i < modeData.Length; i++ )
                        {
                            reader.Get( out modeData[i], 5 + i );
                        }
                        temp[occ - this.OccupationStartIndex].Add( new Segment()
                            {
                                Origin = new Range() { Start = os, Stop = oe },
                                Destination = new Range() { Start = ds, Stop = de },
                                ModificationForMode = modeData
                            } );
                    }
                }
            }
            this.Data = new Segment[this.NumberOfOccupations][];
            for ( int i = 0; i < this.Data.Length; i++ )
            {
                this.Data[i] = temp[i].ToArray();
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Unload()
        {
            this.Data = null;
        }

        private struct Segment
        {
            internal Range Destination;
            internal float[] ModificationForMode;
            internal Range Origin;
        }
    }
}