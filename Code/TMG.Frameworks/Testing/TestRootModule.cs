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
    /// <summary>
    /// A dummmy / testing module to be used to help simulate estimation and scheduling for the XTMF GUI.
    /// </summary>
    [ModuleInformation(Description =
        @"A dummy module that can be used as a root module in model systems used for testing the GUI.",DocURL = "https://tmg.utoronto.ca",
        IconURI = "TestTube")]
    public class TestRootModule : IModelSystemTemplate
    {
        public string Name { get; set; }

        public float Progress => calculateProgress();
        public Tuple<byte, byte, byte> ProgressColour { get; }

        [SubModelInformation(Description = "Child Modules",Required = false)]
        public List<ISelfContainedModule> ChildModules { get; set; }

  
        private IConfiguration _configuration;

        public bool RuntimeValidation(ref string error)
        {

            return true;
        }

        private float calculateProgress()
        {
            float total = 0;
            ChildModules.ForEach((module) => total += module.Progress);
            return total / 3.0f;
        }

        public void Start()
        {
            Console.WriteLine("Starting model system");
            ChildModules.ForEach((module) => module.Start());
            return;
        }


        public TestRootModule(IConfiguration configuration)
        {
            this._configuration = configuration;

        }

        [RunParameter("Input Directory", "../../Input", "The input directory for the Model System")]
        public string InputBaseDirectory { get; set; }

        [RunParameter("Output Directory", "../../Output", "The output directory for the Model System")]
        public string OutputBaseDirectory { get; set; }


        public bool ExitRequest()
        {

            return true;
        }
    }
}
