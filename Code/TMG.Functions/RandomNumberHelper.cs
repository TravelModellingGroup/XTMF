/*
    Copyright 2015-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Runtime.CompilerServices;

namespace TMG.Functions
{
    public static class RandomNumberHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="r"></param>
        ///<seealso cref="http://gsl.sourcearchive.com/documentation/1.14plus-pdfsg-1/randist_2gauss_8c-source.html"/>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SampleNormalDistribution(Random r)
        {
            /* Ratio method (Kinderman-Monahan); see Knuth v2, 3rd ed, p130.
             * K+M, ACM Trans Math Software 3 (1977) 257-260.
             *
             * [Added by Charles Karney] This is an implementation of Leva's
             * modifications to the original K+M method; see:
             * J. L. Leva, ACM Trans Math Software 18 (1992) 449-453 and 454-455. */
            double u, v, x, y, q;
            const double s = 0.449871;    /* Constants from Leva */
            const double t = -0.386595;
            const double a = 0.19600;
            const double b = 0.25472;
            const double r1 = 0.27597;
            const double r2 = 0.27846;

            do                            /* This loop is executed 1.369 times on average  */
            {
                /* Generate a point P = (u, v) uniform in a rectangle enclosing
                   the K+M region v^2 <= - 4 u^2 log(u). */

                /* u in (0, 1] to avoid singularity at u = 0 */
                u = 1 - r.NextDouble();

                /* v is in the asymmetric interval [-0.5, 0.5).  However v = -0.5
                   is rejected in the last part of the while clause.  The
                   resulting normal deviate is strictly symmetric about 0
                   (provided that v is symmetric once v = -0.5 is excluded). */
                v = r.NextDouble() - 0.5;

                /* Constant 1.7156 > sqrt(8/e) (for accuracy); but not by too
                   much (for efficiency). */
                v *= 1.7156;

                /* Compute Leva's quadratic form Q */
                x = u - s;
                y = Math.Abs(v) - t;
                q = x * x + y * (a * y - b * x);

                /* Accept P if Q < r1 (Leva) */
                /* Reject P if Q > r2 (Leva) */
                /* Accept if v^2 <= -4 u^2 log(u) (K+M) */
                /* This final test is executed 0.012 times on average. */
            }
            while(q >= r1 && (q > r2 || v * v > -4 * u * u * Math.Log(u)));
            // Return slope
            return (v / u);
        }
    }
}
