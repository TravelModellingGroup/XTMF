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
using System.Text;
using System.Threading.Tasks;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAssignment
{
    [ModuleInformation( Name = "Fare Based Emme Transit Assignment",
        Description = "Executes a transit assignment which can accumulate fares. " )]
    public class FareBasedEmmeTransitAssignment : IEmmeTool
    {
        private const string _ToolName = "tmg.assignment.road.tolled.toll_attribute_transit_background";
        private const string _OldToolName = "TMG2.Assignment.RoadAssignment.GTAModelTollBasedRoadAssignment";

        [RunParameter( "Boarding Perception", 1.0f, "The perception factor for boarding time." )]
        public float BoardingPerception;

        [RunParameter( "Demand Matrix Number", 9, "The matrix number which will store the transit OD matrix. If '0' is entered, a scalar matrix of 0 will be used. " )]
        public int DemandMatrixNumber;

        [RunParameter( "Flow Distribution Switch", true, "Permits distribution of flows based on travel time to destination. Requires Emme 3.4.2 or newer." )]
        public bool DistributeFlowsByTravelTime;

        [RunParameter( "Fare Perception", 0.0f, "The time-value-of-money of fares. To disable fare-based impedances, use 0.0 for this parameter." )]
        public float FarePerception;

        [RunParameter( "In Vehicle Perception", 1.0f, "The perception factor for in-vehicle time." )]
        public float InVehiclePerception;

        [Parameter( "Logit Scale Parameter", 0.2f, "The scale parameter for a logit model for distributing flows across centroid connectors. Enter '0.0' to disable this functionality." )]
        public float LogitScale;

        [Parameter( "Logit Truncation Parameter", 0.05f, "Logit truncation parameter for centroid connector distribution." )]
        public float LogitTrunc;

        [RunParameter( "Modes", "", "A string listing the case-sensitive modes available in the assignment." )]
        public string Modes;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Scenario Number", 1, "The desired Emme network scenario. Must exist inside the databank." )]
        public int ScenarioNumber;

        [SubModelInformation( Description = "Tallies used for counting the number of trips between Origin and Destination", Required = false )]
        public List<IModeAggregationTally> Tallies;

        [RunParameter( "Additive Demand Switch", false, "If true, this assignment's transit volumes will be superimposed on the network, instead of overwriting a prior assignment's results" )]
        public bool UseAdditiveDemand;

        [Parameter( "Wait Time Factor", 0.5f, "Affects how Emme estimates average waiting time at stops. See Spiess & Florian 1984, or the Emme Prompt Manual Chapter 6 for more information." )]
        public float WaitFactor;

        [RunParameter( "Wait Perception", 2.0f, "The perception factor for passenger waiting time." )]
        public float WaitPerception;

        [RunParameter( "Walk Perception", 2.0f, "The perception factor for walking time." )]
        public float WalkPerception;

        /*
        [RunParameter("Fare Impedance Switch", false, "The switch for including fares in path impedances.")]
        public bool IncludeFaresInImpedance;
        */
        /*
        [RunParameter("V3 Boardings Switch", true, "The switch for using 'Version 3' boardings (different values by agency and mode).")]
        public bool UseV3Boardings;
        */
        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );

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
            get { return _ProgressColour; }
        }

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a modeller controller!" );

            PassMatrixIntoEmme( mc );

            var sb = new StringBuilder();
            /*
            sb.AppendFormat("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12}",
                this.ScenarioNumber, this.DemandMatrixNumber, this.TravelTimeMatrixNumber, this.CostMatrixNumber,
                this.TollMatrixNumber, this.Factor, this.GasCost, this.TollUnitCost, this.TollPerceptionFactor,
                this.MaxIterations, this.RelativeGap, this.BestRelativeGap, this.NormalizedGap);
            */
            if(mc.CheckToolExists(_ToolName))
            {
                return mc.Run(_ToolName, sb.ToString());
            }
            else
            {
                return mc.Run(_OldToolName, sb.ToString());
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private float[][] GetResult(TreeData<float[][]> node, int modeIndex, ref int current)
        {
            if ( modeIndex == current )
            {
                return node.Result;
            }
            current++;
            if ( node.Children != null )
            {
                for ( int i = 0; i < node.Children.Length; i++ )
                {
                    float[][] temp = GetResult( node.Children[i], modeIndex, ref current );
                    if ( temp != null )
                    {
                        return temp;
                    }
                }
            }
            return null;
        }

        private void PassMatrixIntoEmme(ModellerController mc)
        {
            var flatZones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = flatZones.Length;
            // Load the data from the flows and save it to our temporary file
            string outputFileName = Path.GetTempFileName();
            float[][] tally = new float[numberOfZones][];
            for ( int i = 0; i < numberOfZones; i++ )
            {
                tally[i] = new float[numberOfZones];
            }
            for ( int i = Tallies.Count - 1; i >= 0; i-- )
            {
                Tallies[i].IncludeTally( tally );
            }
            using ( StreamWriter writer = new StreamWriter( outputFileName ) )
            {
                writer.WriteLine( "t matrices\r\nd matrix=mf{0}\r\na matrix=mf{0} name=drvtot default=0 descr=generated", this.DemandMatrixNumber );
                StringBuilder[] builders = new StringBuilder[numberOfZones];
                Parallel.For( 0, numberOfZones, delegate(int o)
                {
                    var build = builders[o] = new StringBuilder();
                    var strBuilder = new StringBuilder( 10 );
                    var convertedO = flatZones[o].ZoneNumber;
                    for ( int d = 0; d < numberOfZones; d++ )
                    {
                        this.ToEmmeFloat( tally[o][d], strBuilder );
                        build.AppendFormat( "{0,-4:G} {1,-4:G} {2,-4:G}\r\n",
                            convertedO, flatZones[d].ZoneNumber, strBuilder );
                    }
                } );
                for ( int i = 0; i < numberOfZones; i++ )
                {
                    writer.Write( builders[i] );
                }
            }

            try
            {
                mc.Run( "TMG2.XTMF.ImportMatrix", "\"" + outputFileName + "\" " + ScenarioNumber );
            }
            finally
            {
                File.Delete( outputFileName );
            }
        }

        /// <summary>
        /// Process floats to work with emme
        /// </summary>
        /// <param name="number">The float you want to send</param>
        /// <returns>A limited precision non scientific number in a string</returns>
        private void ToEmmeFloat(float number, StringBuilder builder)
        {
            builder.Clear();
            builder.Append( (int)number );
            number = number - (int)number;
            if ( number > 0 )
            {
                var integerSize = builder.Length;
                builder.Append( '.' );
                for ( int i = integerSize; i < 4; i++ )
                {
                    number = number * 10;
                    builder.Append( (int)number );
                    number = number - (int)number;
                    if ( number == 0 )
                    {
                        break;
                    }
                }
            }
        }
    }
}