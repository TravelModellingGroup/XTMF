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
using System.IO;
using System.Text;
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis
{
    [ModuleInformation( Description = "Produces a table indicating which zones are within a 1km (or other user-specified distance) from subway or GO train stations." +
        " Subway stations are identified as stops of lines with mode = <b>'m'</b>. GO stations can be identified in one of two ways:" +
        "<ol><li> Station centroids, identified by a node selector expression; or</li><li> Stops of lines with mode = <b>'r'.</b></li></ol>" +
        "This data is saved into a CSV file with three columns: <em>'Zone', 'NearSubway' , 'NearGO'</em>. The first column identifies the zone, the second column " +
        "indicates whether a zone is within the radius of a subway station (0 or 1), and the third column indicates whether a zone is within the radius of a GO " +
        "Train station (0 or 1). Zones not in the radius of either are not listed." )]
    public class GetStationAccessFile : IEmmeTool
    {
        [RunParameter( "Export File", "", typeof( FileFromInputDirectory ), "The file to save the resultant CSV table to. The table will have the following header: 'Zone,NearSubway,NearGO'" +
            ", where a 1 or 0 next to a zone indicates that the zone is wthin the search radius to at least one subway or train station. Zones not near any station" +
            " are omitted from the file." )]
        public FileFromInputDirectory ExportFile;

        [RunParameter( "Go Station Selector", "i=7000,8000", "An optional Emme Node Selection Expression to select centroids that represent GO train stations. If left blank, " +
            "the tool will look for stops made by GO train lines inside the network (which is the same way the tool uses to find subway stations." )]
        public string GoStationSelectorExpression;

        [RootModule]
        public IModelSystemTemplate Root;

        [RunParameter( "Scenario Number", 0, "The number of the Emme scenario" )]
        public int ScenarioNumber;

        [RunParameter( "Search Radius", 1000.0f, "The radius around the centroid, in coordinate units (m), in which to search for subway and GO stations" )]
        public float SearchRadius;

        private Tuple<byte, byte, byte> _progressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );
        private const string ToolName = "tmg.analysis.transit.create_station_access_file";
        private const string AlternateToolName = "TMG2.Analysis.Transit.GetStationAccessFile";

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
            get { return _progressColour; }
        }

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a modeller controller!" );

            var sb = new StringBuilder();
            sb.AppendFormat( "{0} {1} {2} \"{3}\"", ScenarioNumber, SearchRadius, GoStationSelectorExpression,
                Path.GetFullPath( ExportFile.GetFileName( Root.InputBaseDirectory ) ) );
            string result = null;

            var toolName = ToolName;
            if (!mc.CheckToolExists(toolName))
            {
                toolName = AlternateToolName;
            }

            return mc.Run(toolName, sb.ToString(), (p => Progress = p), ref result);
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}