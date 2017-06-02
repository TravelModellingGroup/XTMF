/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.Emme.Utilities
{
    [ModuleInformation(
        Description = "This module is used in order to help organize the model system by grouping EMME tools together."
        )]
    // ReSharper disable once InconsistentNaming
    public class ChainEMMETools : IEmmeTool
    {

        public string Name { get; set; }

        public float Progress { get { return _Progress != null ? _Progress() : 0.0f; } }

        private Func<float> _Progress;

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = false, Description = "The tools to execute")]
        public IEmmeTool[] Tools;

        public bool Execute(Controller controller)
        {
            int i = 0;
            // ReSharper disable AccessToModifiedClosure
            _Progress = () => ((float)i / Tools.Length) + (Tools[i].Progress / Tools.Length);
            for (; i < Tools.Length; i++)
            {
                if (!Tools[i].Execute(controller))
                {
                    return false;
                }
            }
            _Progress = null;
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
