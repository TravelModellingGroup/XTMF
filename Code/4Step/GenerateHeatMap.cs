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
using System.Linq;
using System.Text;
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace James.UTDM
{

    public class GenerateHeatMap : IEmmeTool
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RunParameter("Scenario", 11, "The scenario to run against.")]
        public int Scenario;

        [SubModelInformation(Required = true, Description = "The matrix to load into EMME for the travel times.")]
        public FileLocation BinaryMatrixFile;

        [RunParameter("Run Title", "Title", "The title to add to the heat map.")]
        public string RunTitle;

        [RunParameter("Zone of Interest", 3709, "The zone that the heat map will be radiating from.")]
        public int ZoneOfIterest;

        [SubModelInformation(Required = true, Description = "The base zone shapefile.")]
        public FileLocation BaseZoneShapeFile;

        [SubModelInformation(Required = true, Description = "The place to save the map for going to the zone.")]
        public FileLocation ToZoneOfInterest;

        [SubModelInformation(Required = true, Description = "THe place to save the map going from the zone.")]
        public FileLocation FromZoneOfInterest;

        [RunParameter("View Name", "GTHA Overview", "The name of the View in the EMME database to use. (Found in the views directory. Case sensitive.)")]
        public string ViewName;

        private const string ToolNamespace = "Airport.TimesChoropleth";

        public bool Execute(Controller controller)
        {
            var modeller = controller as ModellerController;
            if(modeller != null)
            {
                if(!File.Exists(BaseZoneShapeFile))
                {
                    throw new XTMFRuntimeException("In '" + BinaryMatrixFile + "' the base zone shape file does not exist!");
                }
                if(!File.Exists(BinaryMatrixFile))
                {
                    throw new XTMFRuntimeException("In '" + BinaryMatrixFile + "' the binary matrix file does not exist!");
                }
                if(!modeller.CheckToolExists(ToolNamespace))
                {
                    throw new XTMFRuntimeException("We were unable to find a tool in the EMME project called " + ToolNamespace);
                }
                return modeller.Run(ToolNamespace, BuildArguments());
            }
            return false;
        }

        private string BuildArguments()
        {
            return "\"" + Path.GetFullPath(BinaryMatrixFile.GetFilePath()) + "\" " + ZoneOfIterest.ToString() + " \"" + BaseZoneShapeFile.GetFilePath() + "\"" + " \"" + RunTitle
                + "\" \"" + Path.GetFullPath(ToZoneOfInterest.GetFilePath()) + "\"" + " \"" + Path.GetFullPath(FromZoneOfInterest.GetFilePath()) + "\"" + " \"" + ViewName + "\"";
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
