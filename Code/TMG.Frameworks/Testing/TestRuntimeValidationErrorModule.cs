/*
    Copyright 2015-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using XTMF;

namespace TMG.Frameworks.Testing
{
    [ModuleInformation(Description =
        @"A test module that explicitly generates a runtime validation failure. The `RuntimeValidation` module method of this module returns false in all circumstances.",
        IconURI = "TestTube")]
    public class TestRuntimeValidationErrorModule : ISelfContainedModule
    {
        public string Name { get; set; } = "Test Runtime Validation Error Module";
        public float Progress { get; }
        public Tuple<byte, byte, byte> ProgressColour { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        public bool RuntimeValidation(ref string error)
        {
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Start()
        {
            
        }
    }
}
