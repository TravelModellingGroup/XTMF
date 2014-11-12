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
    [ModuleInformation( Description = @"Produces a report of various transit line attributes, saved to a
                     comma-separated-values (CSV) table format. Transit line results can be optionally
                     aggregated using a two-column correspondence table, with the first column
                     referenced to Emme transit line IDs.
                     <br><br>For each transit line (or line grouping), the following attributes are
                     exported:<ul>
                     <li>Number of Emme routes (branches) in the line (=1 if no aggregation)</li>
                     <li>Total line boardings</li>
                     <li>Line peak volume</li>
                     <li>Line peak volume/capacity ratio</li>
                     <li>Weighted average of volume/capacity ratio</ul>" )]
    public class ExtractTransitLineResults : IEmmeTool
    {
        [RunParameter( "Aggregation file", "", "Optional: A two-column table (.txt or .csv) matching Emme network transit line IDs to that of some external source" +
                        " (e.g., TTS data). The first column is assumed to be Emme transit line IDs. No header." )]
        public FileFromInputDirectory AggregationFile;

        [RunParameter( "Line filter expression", "all", "Optional: expression for filtering transit lines, in the format of the Emme Network Calculator tool." +
                        "For example, an expression to export results for streetcar and bus lines would be 'mode=bs'. See Emme help for further details." )]
        public string LineFilterExpression;

        [RunParameter( "Output file", "*.csv", "The name & location of the file to save transit line data to. Output file format is Commented CSV." )]
        public FileFromOutputDirectory OutputFile;

        [RootModule]
        public IModelSystemTemplate Root;

        [RunParameter( "Scenario", 1, "The number of the Emme scenario with transit assignment results." )]
        public int ScenarioNumber;

        private Tuple<byte, byte, byte> _progressColour = new Tuple<byte, byte, byte>( 255, 173, 28 );
        private const string ToolName = "TMG2.Analysis.Transit.ExportLineResults";
        private const string AlternateToolName = "TMG2.Analysis.ExportLineResults";

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            private set;
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

            string aggregationPath = Path.GetFullPath( AggregationFile.GetFileName( Root.InputBaseDirectory ) );
            string outputPath = Path.GetFullPath( OutputFile.GetFileName() );

            // ScenarioNumber, OutputFile, LineSelectorExpression, AggregationFile

            var builder = new StringBuilder();
            builder.AppendFormat( "{0} {1} {2} {3}", ScenarioNumber, outputPath, LineFilterExpression, aggregationPath );
            string result = null;

            var toolName = ToolName;
            if (!mc.CheckToolExists(toolName))
            {
                toolName = AlternateToolName;
            }

            return mc.Run(toolName, builder.ToString(), (p => this.Progress = p), ref result);
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}