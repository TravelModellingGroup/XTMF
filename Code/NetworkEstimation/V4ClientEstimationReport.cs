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
using System.Text;
using TMG.Emme;
using TMG.Estimation;
using TMG.Estimation.Utilities;
using TMG.Input;
using XTMF;

namespace TMG.NetworkEstimation
{
    [ModuleInformation( Description = "Maintains an on-going CSV report for each Task assigned to a client, reporting Generation, Task index, " +
                                    "Emme transit line boardings by mode and operator, and parameters." +
                                    "<br><br>Results are reported in the following columns (in order):" +
                                    "<ol><li>Generation<li>Index<li>Fitness<li>Brampton<li>Durham<li>GO Bus<li>GO Train<li>Halton<li>Hamilton<li>" +
                                    "Mississauga<li>Streetcar<li><Subway<li>TTC Bus<li>VIVA<li>YRT</ol>" +
                                    "<br>Subsequent columns report the value for each parameter being estimated (same order as Results.csv)")]
    public sealed class V4ClientEstimationReport : ClientFileAggregation, IEmmeTool
    {
        [RootModule]
        public IEstimationClientModelSystem Root;

        [RunParameter( "Scenario", 0, "The Emme scenario from which to extract results." )]
        public int ScenarioNumber;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );
        private const string _ToolName = "TMG2.XTMF.returnGroupBoardings";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if ( mc == null )
            {
                throw new XTMFRuntimeException( "Controller is not a ModellerController" );
            }
            string result = "";
            mc.Run( _ToolName, this.ScenarioNumber.ToString(), ( p => this._Progress = p ), ref result );

            StringBuilder builder = new StringBuilder();
            builder.Append(this.Root.CurrentTask.Generation);
            builder.Append(',');
            builder.Append(this.Root.CurrentTask.Index);

            //Append the fitness value for this task
            builder.Append(',');
            var func = this.Root.RetrieveValue;
            builder.Append((func == null) ? "null" : func().ToString());

            //Results coming out of Emme/Python are already a string of comma-separated values
            builder.Append(',');
            builder.Append(result);

            foreach ( var val in this.Root.CurrentTask.ParameterValues )
            {
                builder.Append( ',' );
                builder.Append( val.ToString() );
            }
            builder.AppendLine();

            //now that we have built up the data, send it to the host
            this.SendToHost( builder.ToString() );
            return true;
        }

        private float _Progress;

        override public float Progress
        {
            get { return _Progress; }
        }

        override public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        override public bool RuntimeValidation(ref string error)
        {
            return true;
        }

    }
}
