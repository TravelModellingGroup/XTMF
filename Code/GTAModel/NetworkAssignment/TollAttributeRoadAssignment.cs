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
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAssignment
{
    public class TollAttributeRoadAssignment : IEmmeTool
    {
        [RunParameter("Best Relative Gap", 0.01f, "(%) Best Relative Gap convergence criteria.")]
        public float BestRelativeGap;

        [RunParameter("Travel Cost Matrix Number", 13, "The matrix number which will store the auto travel costs matrix. If the matrix exists already, it will be overwritten.")]
        public int CostMatrixNumber;

        [RunParameter("Demand File Name", "", @"For debugging. Optional file name to export the tallied demand matrix. Otherwise, a temporary file will be used.")]
        public string DemandFileName;

        [RunParameter("Demand Matrix Number", 10, "The matrix number which will store th auto OD matrix. If the matrix exists already, it will be overwritten.")]
        public int DemandMatrixNumber;

        [Parameter("Peak Hour Factor", 0.468f, "Factor to convert the modeled time period into a one-hour assignment period.")]
        public float PeakHourFactor;

        [Parameter("Link Unit Cost", 0.153f, "The link unit cost in $/km, applied to all links")]
        public float LinkUnitCost;

        [Parameter("Link Toll Attribute", "@toll", "Select a LINK extra attribute containing tolls (in $).")]
        public string LinkTollAttribute;

        [RunParameter("High Performance Flag", true, "When enabled, tells Emme to use all available cores for faster computation, but this will result in " +
            "slower performance in other Windows processes. Disabling this option will leave at least one core available for other work while Emme is running.")]
        public bool HighPerformanceMode;

        [RunParameter("Max Iterations", 100, "Maximum road assignment iterations")]
        public int MaxIterations;

        [RunParameter("Normalized Gap", 0.01f, "Normalized Gap convergence criteria.")]
        public float NormalizedGap;

        [RunParameter("Relative Gap", 0.0f, "Relative gap convergence criteria.")]
        public float RelativeGap;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("Scenario Number", 1, "The desired Emme network scenario. Must exist inside the databank.")]
        public int ScenarioNumber;

        [SubModelInformation(Description = "Tallies used for counting the number of trips between Origin and Destination", Required = false)]
        public List<IModeAggregationTally> Tallies;

        [RunParameter("Tolls Matrix Number", 14, "The matrix number which will store the auto tolls matrix. If the matrix exists already, it will be overwritten.")]
        public int TollMatrixNumber;

        [Parameter("Toll Perception Factor", 50.0f, "Auto value of time, in $/hr")]
        public float TollPerceptionFactor;

        [RunParameter("Travel Time Matrix Number", 12, "The matrix number which will store the auto travel times matrix. If the matrix exists already, it will be overwritten.")]
        public int TravelTimeMatrixNumber;

        [Parameter("Transit Background Traffic Flag", true, "Flag to enable custom transit background traffic, ignoring segments with TTF = 2 (XROW lanes)")]
        public bool UseTransitBackground;

        [Parameter("SOLA Flag", true, "Emme 4.1 and newer ONLY! Flag to use SOLA traffic assignment algorithm instead of standard.")]
        public bool SOLAFlag;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );
        private const string _ToolName = "TMG2.Assignment.RoadAssignment.RoadAssignmentTollAttribute";
        private const string _ToolNameWithBGTraffic = "TMG2.Assignment.RoadAssignment.TollAttributeTVPH";


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
                throw new XTMFRuntimeException( "Controller is not a ModellerController!" );

            if ( Tallies.Count > 0 )
            {
                PassMatrixIntoEmme( mc );
            }

            var runName = "\"" + Path.GetFileName( Directory.GetCurrentDirectory() ) + "\"";


            string args = string.Join( " ", ScenarioNumber,
                                    DemandMatrixNumber,
                                    ( "mf" + TravelTimeMatrixNumber ),
                                    ( "mf" + CostMatrixNumber ),
                                    ( "mf" + TollMatrixNumber ),
                                    PeakHourFactor,
                                    LinkUnitCost,
                                    TollPerceptionFactor,
                                    MaxIterations,
                                    RelativeGap,
                                    BestRelativeGap,
                                    NormalizedGap,
                                    HighPerformanceMode,
                                    runName,
                                    LinkTollAttribute,
                                    SOLAFlag);

            /*
            Call args:
             *
             def __call__(self, xtmf_ScenarioNumber, xtmf_DemandMatrixNumber, TimesMatrixId, CostMatrixId, TollsMatrixId,
                 PeakHourFactor, LinkCost, TollWeight, Iterations, rGap, brGap, normGap, PerformanceFlag,
                 RunTitle, LinkTollAttributeId)
             *
            */

            if ( UseTransitBackground )
            {
                return mc.Run( _ToolNameWithBGTraffic, args, ( p => this.Progress = p ), ref string result = "" );
            }
            else
            {
                return mc.Run( _ToolName, args, ( p => this.Progress = p ), ref string result = "" );
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void PassMatrixIntoEmme(ModellerController mc)
        {
            var flatZones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = flatZones.Length;
            // Load the data from the flows and save it to our temporary file
            var useTempFile = string.IsNullOrWhiteSpace( DemandFileName );
            string outputFileName = useTempFile ? Path.GetTempFileName() : DemandFileName;
            float[][] tally = new float[numberOfZones][];
            for ( int i = 0; i < numberOfZones; i++ )
            {
                tally[i] = new float[numberOfZones];
            }
            for ( int i = Tallies.Count - 1; i >= 0; i-- )
            {
                Tallies[i].IncludeTally( tally );
            }
            var dir = Path.GetDirectoryName( outputFileName );
            if ( !string.IsNullOrWhiteSpace( dir ) && !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
            using (StreamWriter writer = new StreamWriter( outputFileName ))
            {
                writer.WriteLine( "t matrices\r\na matrix=mf{0} name=drvtot default=0 descr=from_xtmf", DemandMatrixNumber );
                StringBuilder[] builders = new StringBuilder[numberOfZones];
                Parallel.For( 0, numberOfZones, delegate (int o)
                {
                    var build = builders[o] = new StringBuilder();
                    var strBuilder = new StringBuilder( 10 );
                    var convertedO = flatZones[o].ZoneNumber;
                    for ( int d = 0; d < numberOfZones; d++ )
                    {
                        mc.ToEmmeFloat( tally[o][d], strBuilder );
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
                mc.Run( "TMG2.XTMF.ImportMatrix", "\"" + Path.GetFullPath( outputFileName ) + "\" " + ScenarioNumber );
            }
            finally
            {
                if ( useTempFile )
                {
                    File.Delete( outputFileName );
                }
            }
        }
    }
}
