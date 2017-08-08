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
using XTMF;

namespace TMG.Frameworks.Data.Processing.AST
{
    public sealed class FusedMultiplyAdd : Expression
    {

        public Expression MulLhs;
        public Expression MulRhs;
        public Expression Add;

        public FusedMultiplyAdd(int start) : base(start)
        {

        }

        public override ComputationResult Evaluate(IDataSource[] dataSources)
        {
            var mulLhs = MulLhs.Evaluate(dataSources);
            var mulRhs = MulRhs.Evaluate(dataSources);
            var add = Add.Evaluate(dataSources);
            if (mulLhs.Error)
            {
                return mulLhs;
            }
            else if (mulRhs.Error)
            {
                return mulRhs;
            }
            else if (add.Error)
            {
                return add;
            }
            return Evaluate(mulLhs, mulRhs, add);
        }

        private ComputationResult Evaluate(ComputationResult mulLhs, ComputationResult mulRhs, ComputationResult add)
        {
            if (add.IsValue)
            {
                return EvaluateAddIsValue(mulLhs, mulRhs, add);
            }
            else if (add.IsVectorResult)
            {
                return EvaluateAddIsVector(mulLhs, mulRhs, add);
            }
            else
            {
                return EvaluateAddIsMatrix(mulLhs, mulRhs, add);
            }
        }

        private ComputationResult EvaluateAddIsValue(ComputationResult lhs, ComputationResult rhs, ComputationResult add)
        {
            if (add.IsValue && lhs.IsValue && rhs.IsValue)
            {
                return new ComputationResult(lhs.LiteralValue * rhs.LiteralValue + add.LiteralValue);
            }
            // float / matrix
            if (lhs.IsValue)
            {
                if (rhs.IsVectorResult)
                {
                    var retVector = rhs.Accumulator ? rhs.VectorData : rhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.FusedMultiplyAdd(flat, rhs.VectorData.GetFlatData(), lhs.LiteralValue, add.LiteralValue);
                    return new ComputationResult(retVector, true, rhs.Direction);
                }
                else
                {
                    var retMatrix = rhs.Accumulator ? rhs.OdData : rhs.OdData.CreateSimilarArray<float>();
                    // inverted lhs, rhs since order does not matter
                    VectorHelper.FusedMultiplyAdd(retMatrix.GetFlatData(), rhs.OdData.GetFlatData(), lhs.LiteralValue, add.LiteralValue);
                    return new ComputationResult(retMatrix, true);
                }
            }
            else if (rhs.IsValue)
            {
                if (lhs.IsVectorResult)
                {
                    var retVector = lhs.Accumulator ? lhs.VectorData : lhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.FusedMultiplyAdd(flat, lhs.VectorData.GetFlatData(), rhs.LiteralValue, add.LiteralValue);
                    return new ComputationResult(retVector, true, lhs.Direction);
                }
                else
                {
                    // matrix / float
                    var retMatrix = lhs.Accumulator ? lhs.OdData : lhs.OdData.CreateSimilarArray<float>();
                    VectorHelper.FusedMultiplyAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.LiteralValue, add.LiteralValue);
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
                        VectorHelper.FusedMultiplyAdd(retMatrix.GetFlatData(), lhs.VectorData.GetFlatData(), rhs.VectorData.GetFlatData(), add.LiteralValue);
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
                                VectorHelper.FusedMultiplyAdd(flatRet[i], flatRhs[i], flatLhs[i], add.LiteralValue);
                            });
                        }
                        else if (lhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            System.Threading.Tasks.Parallel.For(0, flatRet.Length, i =>
                            {
                                VectorHelper.FusedMultiplyAdd(flatRet[i], flatLhs, flatRhs[i], add.LiteralValue);
                            });
                        }
                        else
                        {
                            return new ComputationResult("Unable to add vector without directionality starting at position " + MulLhs.Start + "!");
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
                                VectorHelper.FusedMultiplyAdd(flatRet[i], flatLhs[i], flatRhs[i], add.LiteralValue);
                            });
                        }
                        else if (rhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            System.Threading.Tasks.Parallel.For(0, flatRet.Length, i =>
                            {
                                VectorHelper.FusedMultiplyAdd(flatRet[i], flatRhs, flatLhs[i], add.LiteralValue);
                            });
                        }
                        else
                        {
                            return new ComputationResult("Unable to add vector without directionality starting at position " + MulLhs.Start + "!");
                        }
                        return new ComputationResult(retMatrix, true);
                    }
                }
                else
                {
                    var retMatrix = lhs.Accumulator ? lhs.OdData : (rhs.Accumulator ? rhs.OdData : lhs.OdData.CreateSimilarArray<float>());
                    VectorHelper.FusedMultiplyAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.OdData.GetFlatData(), add.LiteralValue);
                    return new ComputationResult(retMatrix, true);
                }
            }
        }

        private static void Swap<T>(ref T first, ref T second) where T : class
        {
            var temp = first;
            first = second;
            second = temp;
        }

        private ComputationResult EvaluateAddIsVector(ComputationResult lhs, ComputationResult rhs, ComputationResult add)
        {
            // Test the simple case of this really just being an add with a constant multiply
            if (lhs.IsValue && rhs.IsValue)
            {
                var retVector = add.Accumulator ? add.VectorData : add.VectorData.CreateSimilarArray<float>();
                VectorHelper.Add(retVector.GetFlatData(), add.VectorData.GetFlatData(), lhs.LiteralValue * rhs.LiteralValue);
                return new ComputationResult(retVector, true, add.Direction);
            }
            if (lhs.IsOdResult || rhs.IsOdResult)
            {
                if (lhs.IsVectorResult && lhs.Direction == ComputationResult.VectorDirection.Unassigned)
                {
                    return new ComputationResult("Unable to multiply vector without directionality starting at position " + MulLhs.Start + "!");
                }
                if (rhs.IsVectorResult && lhs.Direction == ComputationResult.VectorDirection.Unassigned)
                {
                    return new ComputationResult("Unable to multiply vector without directionality starting at position " + MulRhs.Start + "!");
                }
                if (add.Direction == ComputationResult.VectorDirection.Unassigned)
                {
                    return new ComputationResult("Unable to add vector without directionality starting at position " + Add.Start + "!");
                }
                // if the lhs is a value just swap the two around
                if (!lhs.IsOdResult)
                {
                    Swap(ref lhs, ref rhs);
                }
                //LHS is a matrix
                if (rhs.IsOdResult)
                {
                    var retMatrix = rhs.Accumulator ? rhs.OdData :
                        (lhs.Accumulator ? lhs.OdData : lhs.OdData.CreateSimilarArray<float>());
                    if (add.Direction == ComputationResult.VectorDirection.Vertical)
                    {
                        VectorHelper.FusedMultiplyAddVerticalAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.OdData.GetFlatData(), add.VectorData.GetFlatData());
                    }
                    else
                    {
                        VectorHelper.FusedMultiplyAddHorizontalAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.OdData.GetFlatData(), add.VectorData.GetFlatData());
                    }
                    return new ComputationResult(retMatrix, true);
                }
                else if (rhs.IsVectorResult)
                {
                    var retMatrix = lhs.Accumulator ? lhs.OdData : lhs.OdData.CreateSimilarArray<float>();
                    if (rhs.Direction == ComputationResult.VectorDirection.Vertical)
                    {
                        if (add.Direction == ComputationResult.VectorDirection.Vertical)
                        {
                            VectorHelper.FusedMultiplyAddVerticalRhsVerticalAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.VectorData.GetFlatData(), add.VectorData.GetFlatData());
                        }
                        else
                        {
                            VectorHelper.FusedMultiplyAddVerticalRhsHorizontalAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.VectorData.GetFlatData(), add.VectorData.GetFlatData());
                        }
                    }
                    else
                    {
                        if (add.Direction == ComputationResult.VectorDirection.Vertical)
                        {
                            VectorHelper.FusedMultiplyAddHorizontalRhsVerticalAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.VectorData.GetFlatData(), add.VectorData.GetFlatData());
                        }
                        else
                        {
                            VectorHelper.FusedMultiplyAddHorizontalRhsHorizontalAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.VectorData.GetFlatData(), add.VectorData.GetFlatData());
                        }
                    }
                    return new ComputationResult(retMatrix, true);
                }
                else
                {
                    //RHS is a scalar
                    var retMatrix = lhs.Accumulator ? lhs.OdData : lhs.OdData.CreateSimilarArray<float>();
                    if (add.Direction == ComputationResult.VectorDirection.Vertical)
                    {
                        VectorHelper.FusedMultiplyAddVerticalAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.LiteralValue, add.VectorData.GetFlatData());
                    }
                    else
                    {
                        VectorHelper.FusedMultiplyAddHorizontalAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.LiteralValue, add.VectorData.GetFlatData());
                    }
                    return new ComputationResult(retMatrix, true);
                }
            }
            // vector cases
            else
            {
                // if the lhs is a value just swap the two around
                if (lhs.IsValue)
                {
                    Swap(ref lhs, ref rhs);
                }
                // vector * vector + vector
                if (rhs.IsVectorResult)
                {
                    var retVector = add.Accumulator ? add.VectorData :
                        (rhs.Accumulator ? rhs.VectorData :
                        (lhs.Accumulator ? lhs.VectorData : lhs.VectorData.CreateSimilarArray<float>()));
                    VectorHelper.FusedMultiplyAdd(retVector.GetFlatData(), lhs.VectorData.GetFlatData(), rhs.VectorData.GetFlatData(), add.VectorData.GetFlatData());
                    return new ComputationResult(retVector, true, add.Direction == lhs.Direction && add.Direction == rhs.Direction ? add.Direction : ComputationResult.VectorDirection.Unassigned);
                }
                // vector * lit + vector
                else
                {
                    var retVector = add.Accumulator ? add.VectorData :
                        (lhs.Accumulator ? lhs.VectorData : lhs.VectorData.CreateSimilarArray<float>());
                    VectorHelper.FusedMultiplyAdd(retVector.GetFlatData(), lhs.VectorData.GetFlatData(), rhs.LiteralValue, add.VectorData.GetFlatData());
                    return new ComputationResult(retVector, true, add.Direction == lhs.Direction && add.Direction == rhs.Direction ? add.Direction : ComputationResult.VectorDirection.Unassigned);
                }
            }
        }

        private ComputationResult EvaluateAddIsMatrix(ComputationResult lhs, ComputationResult rhs, ComputationResult add)
        {
            if (lhs.IsVectorResult && lhs.Direction == ComputationResult.VectorDirection.Unassigned)
            {
                return new ComputationResult("Unable to multiply vector without directionality starting at position " + MulLhs.Start + "!");
            }
            if (rhs.IsVectorResult && rhs.Direction == ComputationResult.VectorDirection.Unassigned)
            {
                return new ComputationResult("Unable to multiply vector without directionality starting at position " + MulRhs.Start + "!");
            }
            // Ensure that the LHS is a higher or equal order to the RHS (Matrix > Vector > Scalar)
            if (!lhs.IsOdResult)
            {
                Swap(ref lhs, ref rhs);
            }
            if (lhs.IsValue)
            {
                Swap(ref lhs, ref rhs);
            }
            // LHS is now a higher or equal to the order of RHS
            if (lhs.IsOdResult)
            {
                if (rhs.IsOdResult)
                {
                    var retMatrix = add.Accumulator ? add.OdData :
                        (lhs.Accumulator ? lhs.OdData :
                        (rhs.Accumulator ? rhs.OdData : add.OdData.CreateSimilarArray<float>()));
                    VectorHelper.FusedMultiplyAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.OdData.GetFlatData(), add.OdData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
                else if (rhs.IsVectorResult)
                {
                    var retMatrix = add.Accumulator ? add.OdData :
                        (lhs.Accumulator ? lhs.OdData : add.OdData.CreateSimilarArray<float>());
                    if (rhs.Direction == ComputationResult.VectorDirection.Vertical)
                    {
                        VectorHelper.FusedMultiplyAddVerticalRhs(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.VectorData.GetFlatData(), add.OdData.GetFlatData());
                    }
                    else
                    {
                        VectorHelper.FusedMultiplyAddHorizontalRhs(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.VectorData.GetFlatData(), add.OdData.GetFlatData());
                    }
                    return new ComputationResult(retMatrix, true);
                }
                else
                {
                    //RHS is scalar
                    var retMatrix = add.Accumulator ? add.OdData :
                        (lhs.Accumulator ? lhs.OdData : add.OdData.CreateSimilarArray<float>());
                    VectorHelper.FusedMultiplyAdd(retMatrix.GetFlatData(), lhs.OdData.GetFlatData(), rhs.LiteralValue, add.OdData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
            else if (lhs.IsVectorResult)
            {
                var retMatrix = add.Accumulator ? add.OdData : add.OdData.CreateSimilarArray<float>();
                var tempVector = lhs.Accumulator ? lhs.VectorData : (rhs.IsVectorResult && rhs.Accumulator ? rhs.VectorData : lhs.VectorData.CreateSimilarArray<float>());
                // compute the multiplication separately in this case for better performance (n multiplies instead of n^2)
                if (rhs.IsVectorResult)
                {
                    if (lhs.Direction != rhs.Direction)
                    {
                        // if the directions don't add up then the sum operation would be undefined!
                        return new ComputationResult("Unable to add vector without directionality starting at position " + MulLhs.Start + "!");
                    }
                    VectorHelper.Multiply(tempVector.GetFlatData(), 0, lhs.VectorData.GetFlatData(), 0, rhs.VectorData.GetFlatData(), 0, tempVector.GetFlatData().Length);
                }
                else
                {
                    VectorHelper.Multiply(tempVector.GetFlatData(), lhs.VectorData.GetFlatData(), rhs.LiteralValue);
                }
                if (lhs.Direction == ComputationResult.VectorDirection.Vertical)
                {
                    VectorHelper.AddVertical(retMatrix.GetFlatData(), add.OdData.GetFlatData(), tempVector.GetFlatData());
                }
                else
                {
                    VectorHelper.AddHorizontal(retMatrix.GetFlatData(), add.OdData.GetFlatData(), tempVector.GetFlatData());
                }
                return new ComputationResult(retMatrix, true);
            }
            else
            {
                // in this case LHS is a scalar, and therefore RHS is also a scalar
                var retMatrix = add.Accumulator ? add.OdData : add.OdData.CreateSimilarArray<float>();
                VectorHelper.Add(retMatrix.GetFlatData(), add.OdData.GetFlatData(), lhs.LiteralValue * rhs.LiteralValue);
                return new ComputationResult(retMatrix, true);
            }
        }

        internal override bool OptimizeAst(ref Expression ex, ref string error)
        {
            return !(!MulLhs.OptimizeAst(ref MulLhs, ref error)
                    || !MulLhs.OptimizeAst(ref MulRhs, ref error)
                    || !MulLhs.OptimizeAst(ref Add, ref error));
        }
    }
}
