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
using System.Threading;
using System.Timers;
using XTMF;
using XTMF.Logging;
using Timer = System.Timers.Timer;

namespace TMG.Frameworks.Testing
{
    /// <summary>
    ///     A simple test module that simulates a module that requires an extended period of time to finish executing.
    /// </summary>
    ///
    [ModuleInformation(Description = "This is just a simple module that runs for a pre determined amount of time.", Name = "Test Required Module", IconURI = "CodeBraces")]
    public class TestRequiredModule : ISelfContainedModule
    {
        private readonly ILogger _logger;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        public TestRequiredModule(IConfiguration configuration,
            ILogger logger)
        {
            _logger = logger;
        }

        [SubModelInformation(Description ="Description",Required =true)]
        public IModule ARequiredModule { get; set; }



        [RunParameter("Test Boolean", false, "Just a simple test boolean")]
        public bool TestBool { get; set; }


        public string Name { get; set; }

        public float Progress { get; private set; }

        public Tuple<byte, byte, byte> ProgressColour { get; } = new Tuple<byte, byte, byte>(100, 120, 200);

        public bool RuntimeValidation(ref string error)
        {

            return true;
        }

        public void Start()
        {
            

        }

    }
}