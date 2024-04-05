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

namespace TMG.Estimation.Utilities.AIEstimation;

public class Polynomial : TestEquation
{
    [RunParameter("X", 0.0f, "The value of x.")]
    public float X;

    [RunParameter("A", 1.0f, "The value of A.")]
    public float A;

    [RunParameter("B", -12.0f, "The value of B.")]
    public float B;

    [RunParameter("C", 15.0f, "The value of C.")]
    public float C;

    [RunParameter("D", 56.0f, "The value of D.")]
    public float D;

    [RunParameter("E", -60.0f, "The value of E.")]
    public float E;

    [RunParameter("Global Minimum", -88.891568, "The minimum value of the function in the given range of X.")]
    public float GlobalMinimum;

    public override float Evaluate()
    {
        var x2 = X * X;
        var x3 = x2 * X;
        var x4 = x3 * X;
        return -Math.Min(0.0f, GlobalMinimum - (A * x4 + B * x3 + C * x2 + D * X + E));
    }
}
