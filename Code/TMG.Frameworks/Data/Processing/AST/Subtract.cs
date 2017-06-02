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

using TMG.Functions;

namespace TMG.Frameworks.Data.Processing.AST
{
    public sealed class Subtract : BinaryExpression
    {
        public Subtract(int start) : base(start)
        {

        }

        internal override bool OptimizeAst(ref Expression ex, ref string error)
        {
            if(!base.OptimizeAst(ref ex, ref error))
            {
                return false;
            }
            var lhs = Lhs as Literal;
            var rhs = Rhs as Literal;
            if (lhs != null && rhs != null)
            {
                ex = new Literal(Start, lhs.Value - rhs.Value);
            }
            return true;
        }

        public override ComputationResult Evaluate(ComputationResult lhs, ComputationResult rhs)
        {
            // see if we have two values, in this case we can skip doing the matrix operation
            if (lhs.IsValue && rhs.IsValue)
            {
                return new ComputationResult(lhs.LiteralValue - rhs.LiteralValue);
            }
            // float / matrix
            if (lhs.IsValue)
            {
                if (rhs.IsVectorResult)
                {
                    var retVector = rhs.Accumulator ? rhs.VectorData : rhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.Subtract(flat, lhs.VectorData.GetFlatData(), rhs.LiteralValue);
                    return new ComputationResult(retVector, true);
                }
                else
                {
                    var retMatrix = rhs.Accumulator ? rhs.OdData : rhs.OdData.CreateSimilarArray<float>();
                    VectorHelper.Subtract(retMatrix.GetFlatData(), lhs.LiteralValue, rhs.OdData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
            else if (rhs.IsValue)
            {
                if (lhs.IsVectorResult)
                {
                    var retVector = lhs.Accumulator ? lhs.VectorData : lhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.Subtract(flat, lhs.VectorData.GetFlatData(), rhs.LiteralValue);
                    return new ComputationResult(retVector, true);
                }
                else
                {
                    // matrix / float
                    var retMatrix = lhs.Accumulator ? lhs.OdData : lhs.OdData.CreateSimilarArray<float>();
                    VectorHelper.Subtract(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.LiteralValue);
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
                        VectorHelper.Subtract(retMatrix.GetFlatData(), 0, lhs.VectorData.GetFlatData(), 0, rhs.VectorData.GetFlatData(), 0, retMatrix.GetFlatData().Length);
                        return new ComputationResult(retMatrix, true, lhs.Direction);
                    }
                    else if (lhs.IsVectorResult)
                    {
                        var retMatrix = rhs.Accumulator ? rhs.OdData : rhs.OdData.CreateSimilarArray<float>();
                        var flatRet = retMatrix.GetFlatData();
                        var flatRhs = rhs.OdData.GetFlatData();
                        var flatLhs = lhs.VectorData.GetFlatData();
                        if (lhs.Direction == ComputationResult.VectorDirection.Vertical)
                        {
                            System.Threading.Tasks.Parallel.For(0, flatRet.Length, i =>
                            {
                                VectorHelper.Subtract(flatRet[i], flatLhs[i], flatRhs[i]);
                            });
                        }
                        else if (lhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            System.Threading.Tasks.Parallel.For(0, flatRet.Length, i =>
                            {
                                VectorHelper.Subtract(flatRet[i], 0, flatLhs, 0, flatRhs[i], 0, flatRet[i].Length);
                            });
                        }
                        else
                        {
                            return new ComputationResult("Unable to subtract vector without directionality starting at position " + Lhs.Start + "!");
                        }
                        return new ComputationResult(retMatrix, true);
                    }
                    else
                    {
                        var retMatrix = lhs.Accumulator ? lhs.OdData : lhs.OdData.CreateSimilarArray<float>();
                        var flatRet = retMatrix.GetFlatData();
                        var flatLhs = lhs.OdData.GetFlatData();
                        var flatRhs = rhs.VectorData.GetFlatData();
                        if (rhs.Direction == ComputationResult.VectorDirection.Vertical)
                        {
                            System.Threading.Tasks.Parallel.For(0, flatRet.Length, i =>
                            {
                                VectorHelper.Subtract(flatRet[i], flatLhs[i], flatRhs[i]);
                            });
                        }
                        else if (rhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            System.Threading.Tasks.Parallel.For(0, flatRet.Length, i =>
                            {
                                VectorHelper.Subtract(flatRet[i], 0, flatLhs[i], 0, flatRhs, 0, flatRet[i].Length);
                            });
                        }
                        else
                        {
                            return new ComputationResult("Unable to subtract vector without directionality starting at position " + Lhs.Start + "!");
                        }
                        return new ComputationResult(retMatrix, true);
                    }
                }
                else
                {
                    var retMatrix = lhs.Accumulator ? lhs.OdData : (rhs.Accumulator ? rhs.OdData : lhs.OdData.CreateSimilarArray<float>());
                    VectorHelper.Subtract(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.OdData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
        }
    }
}
