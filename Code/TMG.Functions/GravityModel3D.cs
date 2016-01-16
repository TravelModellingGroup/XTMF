/*
    Copyright 2014-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
namespace TMG.Functions
{
    public static class GravityModel3D
    {
        public static float[] ProduceFlows(int maxIterations, float epsilon, float[] categoriesByOrigin, float[] destinations, float[] friction, int categories, int numberofZones)
        {

            var ret = new float[categories * numberofZones * numberofZones];
            var destinationStar = new float[numberofZones];
            for (int i = 0; i < destinationStar.Length; i++)
            {
                destinationStar[i] = destinations[i];
            }
            int iterations = 1;
            bool balanced;
            float[] columnTotals = new float[numberofZones];
            do
            {
                Array.Clear(columnTotals, 0, columnTotals.Length);
                VectorApply(ret, categoriesByOrigin, friction, destinationStar, columnTotals, categories);
                balanced = VectorBalance(ret, destinations, destinationStar, columnTotals, epsilon, categories);
            } while (iterations++ < maxIterations & !balanced);
            return ret;
        }

        private static void VectorApply(float[] ret, float[] categoriesByOrigin, float[] friction, float[] dStar, float[] columnTotals, int categories)
        {
            var numberOfZones = columnTotals.Length;
            // For now parallel has been taken out to ensure that there is no rounding error level difference between runs.  If this becomes a problem we can rebuild it with a parallel algorithm
            // that guarantees constancy between runs.
            // Currently the efficiency of using Multiply2Scalar1AndColumnSum beats doing parallel with 8 cores and just multiplying and then going back to calculate the column sums
            for (int i = 0; i < numberOfZones; i++)
            {
                for (int k = 0; k < categories; k++)
                {
                    var catByOrigin = categoriesByOrigin[i + k * numberOfZones];
                    if (catByOrigin <= 0) continue;
                    int index = (k * numberOfZones * numberOfZones) + (i * numberOfZones);
                    var sumAF = VectorHelper.MultiplyAndSum(friction, index, dStar, 0, numberOfZones);
                    if (sumAF <= 0) continue;
                    VectorHelper.Multiply2Scalar1AndColumnSum(ret, index, friction, index, dStar, 0, catByOrigin / sumAF, columnTotals, 0, numberOfZones);
                }
            }
        }

        private static bool VectorBalance(float[] ret, float[] destinations, float[] destinationStar, float[] columnTotals, float epsilon, int categories)
        {
            bool balanced = true;
            VectorHelper.Divide(columnTotals, 0, destinations, 0, columnTotals, 0, columnTotals.Length);
            VectorHelper.Multiply(destinationStar, 0, destinationStar, 0, columnTotals, 0, destinationStar.Length);
            VectorHelper.ReplaceIfNotFinite(destinationStar, 0, 1.0f, destinationStar.Length);
            balanced = VectorHelper.AreBoundedBy(columnTotals, 0, 1.0f, epsilon, columnTotals.Length);
            return balanced;
        }
    }
}
