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
using XTMF;
using TMG;

namespace Tasha.Common
{

    public abstract class IterationConditional : IModule
    {
        [RootModule]
        public IIterativeModel Root;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public enum Condition
        {
            LessThan,
            GreaterThan,
            LessThanOrEqualTo,
            GreaterThanOrEqualTo,
            Equals,
            NotEquals
        }

        [RunParameter("Iteration Condition", "Equals", typeof(Condition), "Checks to see if the current iteration is X compared to our input.")]
        public Condition IterationCondition;

        [RunParameter("Value", 0, "The value to compare the iteration again.  For example (CurrentIteration < Value).")]
        public int Value;

        protected bool DoesIterationPass()
        {
            var currentIteration = Root.CurrentIteration;
            var ret = false;
            switch(IterationCondition)
            {
                case Condition.LessThan:
                    ret = currentIteration < Value;
                    break;
                case Condition.GreaterThan:
                    ret = currentIteration > Value;
                    break;
                case Condition.LessThanOrEqualTo:
                    ret = currentIteration <= Value;
                    break;
                case Condition.GreaterThanOrEqualTo:
                    ret = currentIteration >= Value;
                    break;
                case Condition.Equals:
                    ret = currentIteration == Value;
                    break;
                case Condition.NotEquals:
                    ret = currentIteration != Value;
                    break;
            }
            return ret;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
