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
using XTMF;
namespace TMG.Estimation.Utilities.AIEstimation
{
    // ReSharper disable once InconsistentNaming
    public class TF2 : TestEquation
    {
        [RunParameter("X", 0.0f, "The value of x to test for.")]
        public float X;

        [RunParameter("Global Minimum", -2.850227f, "The minimum value of the function in the given range of X.")]
        public float GlobalMinimum;

        public override float Evaluate()
        {
            return -Math.Min(0f, GlobalMinimum - (float)(-1.0 - X * Math.Sin(X * Math.PI * 10.0)));   
        }
    }
}
