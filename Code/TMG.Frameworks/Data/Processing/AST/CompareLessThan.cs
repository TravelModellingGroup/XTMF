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
using TMG.Functions;
using XTMF;

namespace TMG.Frameworks.Data.Processing.AST
{
    public sealed class CompareLessThan : BinaryExpression
    {
        public CompareLessThan(int start) : base(start)
        {

        }

        public override ComputationResult Evaluate(ComputationResult lhs, ComputationResult rhs)
        {
            // see if we have two values, in this case we can skip doing the matrix operation
            if (lhs.IsValue && rhs.IsValue)
            {
                return new ComputationResult(lhs.LiteralValue < rhs.LiteralValue ? 1 : 0);
            }
            // float / matrix
            if (lhs.IsValue)
            {
                if (rhs.IsVectorResult)
                {
                    var retVector = rhs.Accumulator ? rhs.VectorData : rhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.FlagIfLessThan(flat, lhs.LiteralValue, rhs.VectorData.GetFlatData());
                    return new ComputationResult(retVector, true);
                }
                else
                {
                    var retMatrix = rhs.Accumulator ? rhs.ODData : rhs.ODData.CreateSimilarArray<float>();
                    VectorHelper.FlagIfLessThan(retMatrix.GetFlatData(), lhs.LiteralValue, rhs.ODData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
            else if (rhs.IsValue)
            {
                if (lhs.IsVectorResult)
                {
                    var retVector = lhs.Accumulator ? lhs.VectorData : lhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.FlagIfLessThan(flat, lhs.VectorData.GetFlatData(), rhs.LiteralValue);
                    return new ComputationResult(retVector, true);
                }
                else
                {
                    // matrix / float
                    var retMatrix = lhs.Accumulator ? lhs.ODData : lhs.ODData.CreateSimilarArray<float>();
                    VectorHelper.FlagIfLessThan(retMatrix.GetFlatData(), lhs.ODData.GetFlatData(), rhs.LiteralValue);
                    return new ComputationResult(retMatrix, true);
                }
            }
            else
            {
                if (lhs.IsVectorResult || rhs.IsVectorResult)
                {
                    if (lhs.IsVectorResult && rhs.IsVectorResult)
                    {
                        var retMatrix = lhs.Accumulator ? lhs.VectorData : (rhs.Accumulator ? rhs.VectorData : lhs.VectorData.CreateSimilarArray<float>());
                        VectorHelper.FlagIfLessThan(retMatrix.GetFlatData(), 0, lhs.VectorData.GetFlatData(), 0, rhs.VectorData.GetFlatData(), 0, retMatrix.GetFlatData().Length);
                        return new ComputationResult(retMatrix, true, lhs.Direction);
                    }
                    else if (lhs.IsVectorResult)
                    {
                        var retMatrix = rhs.Accumulator ? rhs.ODData : rhs.ODData.CreateSimilarArray<float>();
                        var flatRet = retMatrix.GetFlatData();
                        var flatRHS = rhs.ODData.GetFlatData();
                        var flatLHS = lhs.VectorData.GetFlatData();
                        if (lhs.Direction == ComputationResult.VectorDirection.Vertical)
                        {
                            for (int i = 0; i < flatRHS.Length; i++)
                            {
                                VectorHelper.FlagIfLessThan(flatRet[i], flatRHS[i], flatLHS[i]);
                            }
                        }
                        else if (lhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            for (int i = 0; i < flatRHS.Length; i++)
                            {
                                VectorHelper.FlagIfLessThan(flatRet[i], 0, flatLHS, 0, flatRHS[i], 0, flatRet[i].Length);
                            }
                        }
                        else
                        {
                            return new ComputationResult("Unable to add vector without directionality starting at position " + LHS.Start + "!");
                        }
                        return new ComputationResult(retMatrix, true);
                    }
                    else
                    {
                        var retMatrix = lhs.Accumulator ? lhs.ODData : lhs.ODData.CreateSimilarArray<float>();
                        var flatRet = retMatrix.GetFlatData();
                        var flatLHS = lhs.ODData.GetFlatData();
                        var flatRHS = rhs.VectorData.GetFlatData();
                        if (rhs.Direction == ComputationResult.VectorDirection.Vertical)
                        {
                            for (int i = 0; i < flatLHS.Length; i++)
                            {
                                VectorHelper.FlagIfLessThan(flatRet[i], flatLHS[i], flatRHS[i]);
                            }
                        }
                        else if (rhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            for (int i = 0; i < flatRet.Length; i++)
                            {
                                VectorHelper.FlagIfLessThan(flatRet[i], 0, flatRHS, 0, flatLHS[i], 0, flatRet[i].Length);
                            }
                        }
                        else
                        {
                            return new ComputationResult("Unable to add vector without directionality starting at position " + LHS.Start + "!");
                        }
                        return new ComputationResult(retMatrix, true);
                    }
                }
                else
                {
                    var retMatrix = lhs.Accumulator ? lhs.ODData : (rhs.Accumulator ? rhs.ODData : lhs.ODData.CreateSimilarArray<float>());
                    VectorHelper.FlagIfLessThan(retMatrix.GetFlatData(), lhs.ODData.GetFlatData(), rhs.ODData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
        }
    }
}
