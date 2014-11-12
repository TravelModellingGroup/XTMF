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
    public class PolynomialCoefficients : TestEquation
    {
        [RunParameter("True A", 1.2f, "The true value for A.")]
        public float TrueA;
        [RunParameter("True B", -2.0f, "The true value for B.")]
        public float TrueB;
        [RunParameter("True C", -1.0f, "The true value for C.")]
        public float TrueC;

        [RunParameter("A", 0.0f, "The guessed value of A.")]
        public float A;
        [RunParameter("B", 0.0f, "The guessed value of B.")]
        public float B;
        [RunParameter("C", 0.0f, "The guessed value of C.")]
        public float C;

        public override float Evaluate()
        {
            float error = 0.0f;
            for(int i = 0; i < 100; i++)
            {
                var e = ErrorAtPoint(i - 50.0f);
                error += e * e;
            }
            return error;
        }

        private float ErrorAtPoint(float x)
        {
            var x2 = x * x;
            var guess = A * x2 + B * x + C;
            var truth = TrueA * x2 + TrueB * x + TrueC;
            return guess - truth;
        }
    }
}
