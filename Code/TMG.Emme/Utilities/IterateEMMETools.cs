/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
    public class IterateEMMETools : IEmmeTool
    {
        public string Name { get; set; }

        public float Progress { get; private set; }

        public Tuple<byte, byte, byte> ProgressColour => throw new NotImplementedException();

        [SubModelInformation(Required = false, Description = "The tools to iterate")]
        public IEmmeTool[] ToExecute;

        [RunParameter("Iterations", 1, "The number of iterations to run the tools for.")]
        public int Iterations;

        private int CurrentIteration;
        private int CurrentTool;

        public bool Execute(Controller controller)
        {
            for (int i = 0; i < Iterations; i++)
            {
                CurrentIteration = i;
                for (int j = 0; j < ToExecute.Length; j++)
                {
                    CurrentTool = j;
                    Progress = ((float)i / Iterations) + (((float)j / ToExecute.Length) / Iterations);
                    ToExecute[j].Execute(controller);
                }
            }
            Progress = 1.0f;
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if (Iterations < 0)
            {
                error = $"In {Name} the number of iterations is less than zero!";
                return false;
            }
            return true;
        }

        public override string ToString()
        {
            if (ToExecute.Length == 0)
            {
                return "Nothing to execute";
            }
            return $"Iteration {CurrentIteration + 1} executing {ToExecute[CurrentTool].ToString()}";
        }
    }
}
