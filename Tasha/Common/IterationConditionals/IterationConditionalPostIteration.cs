
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

using XTMF;

namespace Tasha.Common.IterationConditionals
{
    [ModuleInformation(Description = "This module is designed to allow the model system to have conditional execution based upon the provided information and the model system's current iteration.")]
    public class IterationConditionalPostIteration : IterationConditional, IPostIteration
    {
        [SubModelInformation(Required = true, Description = "Executed if true")]
        public IPostIteration[] IfTrue;

        [SubModelInformation(Required = false, Description = "Executed if false")]
        public IPostIteration[] IfFalse;

        public void Execute(int iterationNumber, int totalIterations)
        {
            var toExecute = DoesIterationPass() ? IfTrue : IfFalse;

            if(toExecute != null)
            {
                foreach (var child in toExecute)
                {
                    child.Execute(iterationNumber, totalIterations);
                }
            }
        }

        public void Load(IConfiguration config, int totalIterations)
        {
            
        }
    }
}
