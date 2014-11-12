/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.Estimation.Utilities.AIEstimation
{
    public class Rosenbrock : TestEquation
    {
        [RunParameter("X", 0.0f, "The value of x.")]
        public float X;

        [RunParameter("Y", 0.0f, "The value of y.")]
        public float Y;

        public override float Evaluate()
        {
            // compute the error from the Rosenbrock function's optimal value 0, which occurs at 1,1
            return Math.Abs(((1 - X) * (1 - X)) + 100.0f * ((Y - X * X) * (Y - X * X)));
        }
    }
}
