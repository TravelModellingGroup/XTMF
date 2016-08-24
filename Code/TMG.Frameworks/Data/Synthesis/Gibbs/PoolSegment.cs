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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMG.Frameworks.Data.Synthesis.Gibbs
{
    internal sealed class PoolSegment
    {
        private int Seed;

        private Attribute[] Attributes;
        private Conditional[] Conditionals;
        private int IterationsBeforeAccept;

        internal int[][] Result;

        public PoolSegment(Pool parent, int seed)
        {
            Seed = seed;
            Attributes = parent.Attributes;
            Conditionals = parent.Conditionals;
            IterationsBeforeAccept = parent.IterationsBeforeAccept;
        }

        internal void ProcessSegment(int elementsToCreate)
        {
            var r = new Random(Seed);
            var result = new int[elementsToCreate][];
            for (int el = 0; el < result.Length; el++)
            {
                result[el] = CreateElement(r);
            }
            Result = result;
        }

        private int[] CreateElement(Random r)
        {
            int[] result = new int[Attributes.Length];
            var conditionals = Conditionals;
            //create the initial values
            for (int i = 0; i < Attributes.Length; i++)
            {
                // assign everything randomly from the possible values
                result[i] = (int)(Attributes[i].PossibleValues.Length * r.NextDouble());
            }
            //run the conditionals, going through them all the number of iterations before saving the state
            for (int iteration = 0; iteration < IterationsBeforeAccept; iteration++)
            {
                for (int i = 0; i < conditionals.Length; i++)
                {
                    var pop = (float)r.NextDouble();
                    conditionals[i].Apply(result, pop);
                }
            }
            return result;
        }
    }
}
