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
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Synthesis.Gibbs
{

    public class Pool : XTMF.IModule
    {
        [SubModelInformation(Description = "The conditionals to apply.")]
        public Conditional[] Conditionals;

        [SubModelInformation(Description = "The attributes for the pool")]
        public Attribute[] Attributes;

        [RunParameter("Random Seed", 12345, "A seed to fix the random generator to ensure multiple runs will give the same results.")]
        public int RandomSeed;

        [RunParameter("Size", 1000, "The number of elements to create.")]
        public int SizeToGenerate;

        [RunParameter("Segment Size", 100, "The population to process for each pool segment. (Orders parallel processing.)")]
        public int SegmentSize;

        [RunParameter("Iterations Before Accept", 100, "How many iterations should we spin before we accept a solution?")]
        public int IterationsBeforeAccept;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public int[][] PoolChoices;

        public void GeneratePool()
        {
            var poolSegments = new PoolSegment[(int)Math.Ceiling((float)SizeToGenerate / SegmentSize)];
            Random r = new Random(RandomSeed);
            for (int i = 0; i < poolSegments.Length; i++)
            {
                poolSegments[i] = new PoolSegment(this, r.Next());
            }
            
            System.Threading.Tasks.Parallel.For(0, poolSegments.Length, (int i) =>
            {
                poolSegments[i].ProcessSegment(SegmentSize);
            });
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
