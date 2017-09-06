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
using System.Text;
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis
{
    [ModuleInformation( Name = "Report Screenline Results",
                    Description = "This tool loads Screenlines from a shapefile and then determines" +
                                "which links intersect the screenline's geometry. This tool then exports the volume " +
                                "crossing each element of the screenline and in each direction on that link into a " +
                                "CSV file. The shapefile's attribute table must contain fields labeled 'Id', 'Descr' " +
                                "'PosDirName' and 'NegDirName'. Results are written to a text file. Results will only " +
                                "be reported for the modes selected by the user." )]
    public class ReportScreenlineResults : IEmmeTool
    {
        [RunParameter( "Modes", "", "A string of mode character IDs. This tool will report results for these modes ONLY." )]
        public string Modes;

        [RunParameter( "Report File", "*.txt", typeof( FileFromOutputDirectory ), "A name and location to save the reported results to." )]
        public FileFromOutputDirectory ReportFile;

        [RunParameter( "Scenario Number", 0, "The number of the Emme scenario with assignment results to analyze" )]
        public int ScenarioNumber;

        [RunParameter( "Screenline File", "*.shp", typeof( FileFromInputDirectory ), "The shapefile with screenline data. Its attribute table must include fields labelled"
            + " 'Id', 'Descr', 'PosDirName', and 'NegDirName'." )]
        public FileFromInputDirectory ScreenlineFile; //Input file

        //Output file

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
                throw new XTMFRuntimeException(this, "Controller is not a modeller controller!" );

            var sb = new StringBuilder();
            sb.AppendFormat( "{0} {1} {2} {3}",
                ScenarioNumber, Modes, ScreenlineFile, ReportFile );

            /*
             * ScrenarioNumner, ModesStr, OpenPath, SavePath
             * */

            string result = null;
            return mc.Run(this, "tmg.analysis.traffic.export_screenline_results", sb.ToString(), ( p => Progress = p ), ref result );
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}