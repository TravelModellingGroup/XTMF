/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace TMG.Frameworks.Extensibility
{

    [ModuleInformation(
        Description = "This module is designed to execute the given submodules in parallel.  This module will finish its execution when all of the sub modules have completed."
        )]
    public class ExecuteInParallel : ISelfContainedModule
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        [SubModelInformation(Description = "The modules to run in parallel.")]
        public ISelfContainedModule[] RunInParallel;

        public void Start()
        {
            Parallel.Invoke(RunInParallel.Select(run => new Action(() => { run.Start(); })).ToArray());
        }
    }

}
