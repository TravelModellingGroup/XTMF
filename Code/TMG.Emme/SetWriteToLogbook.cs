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
using XTMF;
namespace TMG.Emme
{
    public class SetWriteToLogbook : IEmmeTool
    {
        public string Name { get;set; }

        public float Progress
        {
            get
            {
                return 0f;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return null;
            }
        }

        [RunParameter("Enable Writing", false, "True to write to logbook, false otherwise.")]
        public bool WriteToLogbook;

        public bool Execute(Controller controller)
        {
            ModellerController modeller;
            if((modeller = controller as ModellerController) != null)
            {
                modeller.WriteToLogbook = WriteToLogbook;
                return true;
            }
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
