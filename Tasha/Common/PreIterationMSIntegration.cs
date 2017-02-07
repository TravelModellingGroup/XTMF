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
using XTMF;

namespace Tasha.Common
{
    [ModuleInformation(Description = "This module is designed to allow the integration of ISelfContainedModules to execute during the pre iteration phase in the TASHA pipeline.")]
    // ReSharper disable once InconsistentNaming
    public class PreIterationMSIntegration : IPreIteration
    {
        [SubModelInformation( Required = false, Description = "The model systems to host after an iteration has completed." )]
        public List<ISelfContainedModule> ModelSystems;

        [RunParameter("Execute Model Systems", true, "Should we execute the contained model systems?")]
        public bool ExecuteModelSystems;

        private Func<float> GetProgress = () => 0f;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return GetProgress(); }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Execute(int iterationNumber, int totalIterations)
        {
            if(ExecuteModelSystems)
            {
                foreach(var ms in ModelSystems)
                {
                    GetProgress = () => ms.Progress;
                    ms.Start();
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Load(int totalIterations)
        {
            
        }
    }
}