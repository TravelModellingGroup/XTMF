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
using Datastructure;
using System;
using System.Linq;
using System.Threading.Tasks;
using TMG.Functions;
using XTMF;

namespace TMG.Frameworks.Data.Processing.AST
{
    public sealed class FunctionCall : Value
    {

        public enum FunctionType
        {
            Undefined,
            Transpose,
            SumRows,
            SumColumns,
            AsHorizontal,
            AsVertical,
            Sum,
            Abs,
            Avg,
            AvgRows,
            AvgColumns,
            E,
            Pi,
            Length,
            LengthColumns,
            LengthRows,
            ZeroMatrix,
            Matrix,
            IdentityMatrix,
            Log,
            If,
            IfNaN,
            Normalize,
            NormalizeColumns,
            NormalizeRows
        }

        private FunctionType Type;

        private Expression[] Parameters;

        private FunctionCall(int start, FunctionType call, Expression[] parameters) : base(start)
        {
            Parameters = parameters;
            Type = call;
        }

        internal override bool OptimizeAst(ref Expression ex, ref string error)
        {
            for (int i = 0; i < Parameters.Length; i++)
            {
                if (!Parameters[i].OptimizeAst(ref Parameters[i], ref error))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool GetCall(int start, string call, Expression[] parameters, out FunctionCall ex, ref string error)
        {
            //decode the call to a type
            ex = null;
            if (!Decode(call, ref error, out FunctionType type))
            {
                return false;
            }
            ex = new FunctionCall(start, type, parameters);
            return true;
        }

        private static bool Decode(string call, ref string error, out FunctionType type)
        {
            type = FunctionType.Undefined;
            call = call.ToLowerInvariant();
            switch (call)
            {
                case "ashorizontal":
                    type = FunctionType.AsHorizontal;
                    return true;
                case "asvertical":
                    type = FunctionType.AsVertical;
                    return true;
                case "transpose":
                    type = FunctionType.Transpose;
                    return true;
                case "sumrows":
                    type = FunctionType.SumRows;
                    return true;
                case "sumcolumns":
                    type = FunctionType.SumColumns;
                    return true;
                case "sum":
                    type = FunctionType.Sum;
                    return true;
                case "abs":
                    type = FunctionType.Abs;
                    return true;
                case "avg":
                    type = FunctionType.Avg;
                    return true;
                case "avgrows":
                    type = FunctionType.AvgRows;
                    return true;
                case "avgcolumns":
                    type = FunctionType.AvgColumns;
                    return true;
                case "e":
                    type = FunctionType.E;
                    return true;
                case "pi":
                    type = FunctionType.Pi;
                    return true;
                case "length":
                    type = FunctionType.Length;
                    return true;
                case "lengthrows":
                    type = FunctionType.LengthRows;
                    return true;
                case "lengthcolumns":
                    type = FunctionType.LengthColumns;
                    return true;
                case "zeromatrix":
                    type = FunctionType.ZeroMatrix;
                    return true;
                case "matrix":
                    type = FunctionType.Matrix;
                    return true;
                case "identitymatrix":
                    type = FunctionType.IdentityMatrix;
                    return true;
                case "log":
                    type = FunctionType.Log;
                    return true;
                case "if":
                    type = FunctionType.If;
                    return true;
                case "ifnan":
                    type = FunctionType.IfNaN;
                    return true;
                case "normalize":
                    type = FunctionType.Normalize;
                    return true;
                case "normalizecolumns":
                    type = FunctionType.NormalizeColumns;
                    return true;
                case "normalizerows":
                    type = FunctionType.NormalizeRows;
                    return true;
                default:
                    error = "The function '" + call + "' is undefined!";
                    return false;
            }
        }

        public override ComputationResult Evaluate(IDataSource[] dataSources)
        {
            // first evaluate the parameters
            var values = new ComputationResult[Parameters.Length];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Parameters[i].Evaluate(dataSources);
                if (values[i].Error)
                {
                    return values[i];
                }
            }

            switch (Type)
            {
                case FunctionType.AsHorizontal:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("AsHorizontal at position " + Start + " was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsVectorResult)
                    {
                        return new ComputationResult("AsHorizontal at position " + Start + " was executed with a parameter that was not a vector!");
                    }
                    return new ComputationResult(values[0], ComputationResult.VectorDirection.Horizontal);
                case FunctionType.AsVertical:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("AsVertical at position " + Start + " was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsVectorResult)
                    {
                        return new ComputationResult("AsVertical at position " + Start + " was executed with a parameter that was not a vector!");
                    }
                    return new ComputationResult(values[0], ComputationResult.VectorDirection.Vertical);
                case FunctionType.Transpose:
                    {
                        if (values.Length != 1)
                        {
                            return new ComputationResult("Transpose at position " + Start + " was executed with the wrong number of parameters!");
                        }
                        if (values[0].IsVectorResult)
                        {
                            switch (values[0].Direction)
                            {
                                case ComputationResult.VectorDirection.Horizontal:
                                    return new ComputationResult(values[0], ComputationResult.VectorDirection.Vertical);
                                case ComputationResult.VectorDirection.Vertical:
                                    return new ComputationResult(values[0], ComputationResult.VectorDirection.Horizontal);
                                case ComputationResult.VectorDirection.Unassigned:
                                    return new ComputationResult("Unable to transpose an vector that does not have a directionality!");
                            }
                        }
                        if (values[0].IsValue)
                        {
                            return new ComputationResult("The parameter to Transpose at position " + Start + " was executed against a scalar!");
                        }
                        if (values[0].IsOdResult)
                        {
                            return TransposeOd(values[0]);
                        }
                        return new ComputationResult("Unsupported data type for Transpose at position " + Start + ".");
                    }
                case FunctionType.SumRows:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("SumRows was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsOdResult)
                    {
                        return new ComputationResult("SumRows was executed with a parameter that was not a matrix!");
                    }
                    return SumRows(values[0]);
                case FunctionType.SumColumns:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("SumColumns was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsOdResult)
                    {
                        return new ComputationResult("SumColumns was executed with a parameter that was not a matrix!");
                    }
                    return SumColumns(values[0]);
                case FunctionType.AvgRows:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("AvgRows was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsOdResult)
                    {
                        return new ComputationResult("AvgRows was executed with a parameter that was not a matrix!");
                    }
                    return AvgRows(values[0]);
                case FunctionType.AvgColumns:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("AvgColumns was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsOdResult)
                    {
                        return new ComputationResult("AvgColumns was executed with a parameter that was not a matrix!");
                    }
                    return AvgColumns(values[0]);
                case FunctionType.Sum:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("Sum was executed with the wrong number of parameters!");
                    }
                    if (values[0].IsValue)
                    {
                        return new ComputationResult("Sum was executed with a parameter that was already a scalar value!");
                    }
                    return Sum(values[0]);
                case FunctionType.Abs:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("Abs was executed with the wrong number of parameters!");
                    }
                    return Abs(values[0]);
                case FunctionType.Avg:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("Avg was executed with the wrong number of parameters!");
                    }
                    if (values[0].IsValue)
                    {
                        return new ComputationResult("Avg was executed with a parameter that was already a scalar value!");
                    }
                    return Avg(values[0]);
                case FunctionType.E:
                    return new ComputationResult((float)Math.E);
                case FunctionType.Pi:
                    return new ComputationResult((float)Math.PI);
                case FunctionType.Length:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("Length was executed with the wrong number of parameters!");
                    }
                    if (values[0].IsValue)
                    {
                        return new ComputationResult("Length can not be applied to a scalar!");
                    }
                    return Length(values[0]);
                case FunctionType.LengthColumns:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("LengthColumns was executed with the wrong number of parameters!");
                    }
                    if (values[0].IsOdResult)
                    {
                        return new ComputationResult("LengthColumns must be applied to a Matrix!");
                    }
                    return LengthColumns(values[0]);
                case FunctionType.LengthRows:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("LengthRows was executed with the wrong number of parameters!");
                    }
                    if (values[0].IsOdResult)
                    {
                        return new ComputationResult("LengthRows must be applied to a Matrix!");
                    }
                    return LengthRows(values[0]);
                case FunctionType.ZeroMatrix:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("ZeroMatrix was executed with the wrong number of parameters!");
                    }
                    if (values[0].IsValue)
                    {
                        return new ComputationResult("ZeroMatrix must be applied to a vector, or a matrix!");
                    }
                    return ZeroMatrix(values);
                case FunctionType.Matrix:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("Matrix was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsVectorResult)
                    {
                        return new ComputationResult("Matrix must be applied to a vector!");
                    }
                    return Matrix(values[0]);
                case FunctionType.IdentityMatrix:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("IdentityMatrix was executed with the wrong number of parameters!");
                    }
                    if (values[0].IsValue)
                    {
                        return new ComputationResult("IdentityMatrix must be applied to a vector, or a matrix!");
                    }
                    return IdentityMatrix(values[0]);
                case FunctionType.Log:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("Log must be executed with one parameter!");
                    }
                    return Log(values);
                case FunctionType.If:
                    if (values.Length != 3)
                    {
                        return new ComputationResult("If requires 3 parameters (condition, valueIfTrue, valueIfFalse)!");
                    }
                    return ComputeIf(values);
                case FunctionType.IfNaN:
                    if (values.Length != 2)
                    {
                        return new ComputationResult("IfNaN requires 2 parameters (original,replacement)!");
                    }
                    return ComputeIfNaN(values);
                case FunctionType.Normalize:
                    if(values.Length != 1)
                    {
                        return new ComputationResult("Normalize requires 1 parameter, a matrix to be normalized.");
                    }
                    return ComputeNormalize(values);
                case FunctionType.NormalizeColumns:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("NormalizeColumns requires 1 parameter, a matrix to be normalized.");
                    }
                    return ComputeNormalizeColumns(values);
                case FunctionType.NormalizeRows:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("NormalizeRows requires 1 parameter, a matrix to be normalized.");
                    }
                    return ComputeNormalizeRows(values);


            }
            return new ComputationResult("An undefined function was executed!");
        }

        private ComputationResult ComputeNormalizeColumns(ComputationResult[] values)
        {
            var toNormalize = values[0];
            if (toNormalize.IsValue)
            {
                return new ComputationResult($"{Start + 1}:Normalize requires its parameter to be of type Matrix, not a scalar.");
            }
            if (toNormalize.IsVectorResult)
            {
                return new ComputationResult($"{Start + 1}:Normalize requires its parameter to be of type Matrix, not a vector.");
            }
            var writeTo = toNormalize.Accumulator ? toNormalize.OdData : toNormalize.OdData.CreateSimilarArray<float>();
            var flatWrite = writeTo.GetFlatData();
            var flatRead = toNormalize.OdData.GetFlatData();
            // This could be executed in parallel if proved to be more efficient
            var columnTotals = new float[flatWrite.Length];
            for (int i = 0; i < flatRead.Length; i++)
            {
                VectorHelper.Add(columnTotals, 0, columnTotals, 0, flatRead[i], 0, flatRead.Length);
            }
            System.Threading.Tasks.Parallel.For(0, flatRead.Length, (int i) =>
            {
                var writeRow = flatWrite[i];
                var readRow = flatRead[i];
                for (int j = 0; j < readRow.Length; j++)
                {
                    writeRow[j] = columnTotals[j] != 0f ? readRow[j] / columnTotals[j] : 0f;
                }
            });
            return new ComputationResult(writeTo, true);
        }

        private ComputationResult ComputeNormalizeRows(ComputationResult[] values)
        {
            var toNormalize = values[0];
            if (toNormalize.IsValue)
            {
                return new ComputationResult($"{Start + 1}:Normalize requires its parameter to be of type Matrix, not a scalar.");
            }
            if (toNormalize.IsVectorResult)
            {
                return new ComputationResult($"{Start + 1}:Normalize requires its parameter to be of type Matrix, not a vector.");
            }
            var writeTo = toNormalize.Accumulator ? toNormalize.OdData : toNormalize.OdData.CreateSimilarArray<float>();
            var flatWrite = writeTo.GetFlatData();
            var flatRead = toNormalize.OdData.GetFlatData();
            System.Threading.Tasks.Parallel.For(0, flatRead.Length, (int i) =>
            {
                var denominator = VectorHelper.Sum(flatRead[i], 0, flatRead.Length);
                if(denominator != 0f)
                {
                    VectorHelper.Divide(flatWrite[i], flatRead[i], denominator);
                }
                else if(flatRead == flatWrite)
                {
                    // we only need to accumulate if we are going to return a previously accumulated matrix.
                    Array.Clear(flatWrite[i], 0, flatWrite.Length);
                }
            });
            return new ComputationResult(writeTo, true);
        }

        private ComputationResult ComputeNormalize(ComputationResult[] values)
        {
            var toNormalize = values[0];
            if(toNormalize.IsValue)
            {
                return new ComputationResult($"{Start + 1}:Normalize requires its parameter to be of type Matrix, not a scalar.");
            }
            if (toNormalize.IsVectorResult)
            {
                return new ComputationResult($"{Start + 1}:Normalize requires its parameter to be of type Matrix, not a vector.");
            }
            var writeTo = toNormalize.Accumulator ? toNormalize.OdData : toNormalize.OdData.CreateSimilarArray<float>();
            var flatWrite = writeTo.GetFlatData();
            var flatRead = toNormalize.OdData.GetFlatData();
            // sum the whole matrix in parallel using SIMD for each array
            var denominator = flatRead.AsParallel().AsOrdered().Sum(row => VectorHelper.Sum(row, 0, row.Length));
            if(denominator == 0f)
            {
                // only clear the write array if it was an accumulator
                if (flatRead == flatWrite)
                {
                    System.Threading.Tasks.Parallel.For(0, flatRead.Length, (int i) =>
                    {
                        Array.Clear(flatWrite[i], 0, flatRead.Length);
                    });
                }
            }
            else
            {
                System.Threading.Tasks.Parallel.For(0, flatRead.Length, (int i) =>
                {
                    VectorHelper.Divide(flatWrite[i], flatRead[i], denominator);
                });
            }
            return new ComputationResult(writeTo, true);
        }

        private ComputationResult ComputeIfNaN(ComputationResult[] values)
        {
            var condition = values[0];
            var replacement = values[1];
            // both must be the same size
            if (condition.IsValue && replacement.IsValue)
            {
                return new ComputationResult(!float.IsNaN(condition.LiteralValue) ? condition.LiteralValue : replacement.LiteralValue);
            }
            else if (condition.IsVectorResult && replacement.IsVectorResult)
            {
                var saveTo = values[0].Accumulator ? values[0].VectorData : values[0].VectorData.CreateSimilarArray<float>();
                VectorHelper.ReplaceIfNaN(saveTo.GetFlatData(), condition.VectorData.GetFlatData(), replacement.VectorData.GetFlatData());
                return new ComputationResult(saveTo, true, condition.Direction);
            }
            else if (condition.IsOdResult && replacement.IsOdResult)
            {
                var saveTo = values[0].Accumulator ? values[0].OdData : values[0].OdData.CreateSimilarArray<float>();
                var flatSave = saveTo.GetFlatData();
                var flatCond = condition.OdData.GetFlatData();
                var flatRep = replacement.OdData.GetFlatData();
                System.Threading.Tasks.Parallel.For(0, flatCond.Length, (int i) =>
                {
                    VectorHelper.ReplaceIfNaN(flatSave[i], flatCond[i], flatRep[i]);
                });
                return new ComputationResult(saveTo, true);
            }
            return new ComputationResult($"{Start + 1}:The Condition and Replacement case of an IfNaN expression must be of the same dimensionality.");
        }

        private ComputationResult ComputeIf(ComputationResult[] values)
        {
            var condition = values[0];
            var ifTrue = values[1];
            var ifFalse = values[2];
            if ((ifTrue.IsValue & !ifFalse.IsValue)
                || (ifTrue.IsVectorResult & !ifFalse.IsVectorResult)
                || (ifTrue.IsOdResult & !ifFalse.IsOdResult))
            {
                return new ComputationResult($"{Start + 1}:The True and False case of an if expression must be of the same dimensionality.");
            }
            if (condition.IsValue)
            {
                // in all cases we can just move the result to the next level
                return condition.LiteralValue > 0f ? ifTrue : ifFalse;
            }
            else if (condition.IsVectorResult)
            {
                if (ifTrue.IsValue)
                {
                    var saveTo = values[0].Accumulator ? values[0].VectorData : values[0].VectorData.CreateSimilarArray<float>();
                    var result = saveTo.GetFlatData();
                    var cond = condition.VectorData.GetFlatData();
                    var t = ifTrue.LiteralValue;
                    var f = ifFalse.LiteralValue;
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = cond[i] > 0f ? t : f;
                    }
                    return new ComputationResult(saveTo, true, condition.Direction);
                }
                else if (ifTrue.IsVectorResult)
                {
                    var saveTo = values[0].Accumulator ? values[0].VectorData : values[0].VectorData.CreateSimilarArray<float>();
                    var result = saveTo.GetFlatData();
                    var cond = condition.VectorData.GetFlatData();
                    var t = ifTrue.VectorData.GetFlatData();
                    var f = ifFalse.VectorData.GetFlatData();
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = cond[i] > 0f ? t[i] : f[i];
                    }
                    return new ComputationResult(saveTo, true, condition.Direction);
                }
                else
                {
                    switch (condition.Direction)
                    {
                        case ComputationResult.VectorDirection.Unassigned:
                            return new ComputationResult($"{Start + 1}:The directionality of the condition vector is required when working with a matrix values.");
                        case ComputationResult.VectorDirection.Vertical:
                            {
                                var saveTo = values[1].Accumulator ? values[1].OdData : values[1].OdData.CreateSimilarArray<float>();
                                var result = saveTo.GetFlatData();
                                var cond = condition.VectorData.GetFlatData();
                                var t = ifTrue.OdData.GetFlatData();
                                var f = ifFalse.OdData.GetFlatData();
                                for (int i = 0; i < result.Length; i++)
                                {
                                    var resRow = result[i];
                                    var toAssign = cond[i] > 0 ? t[i] : f[i];
                                    for (int j = 0; j < resRow.Length; j++)
                                    {
                                        resRow[j] = toAssign[j];
                                    }
                                }
                                return new ComputationResult(saveTo, true);
                            }
                        case ComputationResult.VectorDirection.Horizontal:
                            {
                                var saveTo = values[1].Accumulator ? values[1].OdData : values[1].OdData.CreateSimilarArray<float>();
                                var result = saveTo.GetFlatData();
                                var cond = condition.VectorData.GetFlatData();
                                var t = ifTrue.OdData.GetFlatData();
                                var f = ifFalse.OdData.GetFlatData();
                                for (int i = 0; i < result.Length; i++)
                                {
                                    var resRow = result[i];
                                    var tRow = t[i];
                                    var fRow = f[i];
                                    for (int j = 0; j < resRow.Length; j++)
                                    {
                                        resRow[j] = cond[j] > 0 ? tRow[j] : fRow[j];
                                    }
                                }
                                return new ComputationResult(saveTo, true);
                            }
                    }
                }
            }
            if (condition.IsOdResult)
            {
                if (!ifTrue.IsOdResult)
                {
                    return new ComputationResult($"{Start + 1}:The True and False cases must be a Matrix when the condition is a matrix.");
                }
                var saveTo = values[0].Accumulator ? values[0].OdData : values[0].OdData.CreateSimilarArray<float>();
                var cond = condition.OdData.GetFlatData();
                var tr = ifTrue.OdData.GetFlatData();
                var fa = ifFalse.OdData.GetFlatData();
                var sa = saveTo.GetFlatData();
                System.Threading.Tasks.Parallel.For(0, cond.Length, (int row) =>
                {
                    var condRow = cond[row];
                    var trRow = tr[row];
                    var faRow = fa[row];
                    var saveRow = sa[row];
                    for (int j = 0; j < condRow.Length; j++)
                    {
                        saveRow[j] = condRow[j] > 0 ? trRow[j] : faRow[j];
                    }
                });
                return new ComputationResult(saveTo, true);
            }
            return new ComputationResult($"{Start + 1}:This combination of parameter types has not been implemented for if!");
        }

        private ComputationResult Log(ComputationResult[] values)
        {
            if (values[0].IsValue)
            {
                return new ComputationResult((float)Math.Log(values[0].LiteralValue));
            }
            else if (values[0].IsVectorResult)
            {
                SparseArray<float> saveTo = values[0].Accumulator ? values[0].VectorData : values[0].VectorData.CreateSimilarArray<float>();
                var source = values[0].VectorData.GetFlatData();
                var flat = saveTo.GetFlatData();
                VectorHelper.Log(flat, 0, source, 0, source.Length);
                return new ComputationResult(saveTo, true);
            }
            else
            {
                SparseTwinIndex<float> saveTo = values[0].Accumulator ? values[0].OdData : values[0].OdData.CreateSimilarArray<float>();
                var source = values[0].OdData.GetFlatData();
                var flat = saveTo.GetFlatData();
                System.Threading.Tasks.Parallel.For(0, flat.Length, (int i) =>
                {
                    VectorHelper.Log(flat[i], 0, source[i], 0, source[i].Length);
                });
                return new ComputationResult(saveTo, true);
            }
        }

        private ComputationResult IdentityMatrix(ComputationResult computationResult)
        {
            SparseTwinIndex<float> ret;
            if (computationResult.IsVectorResult)
            {
                var vector = computationResult.VectorData;
                ret = vector.CreateSquareTwinArray<float>();
            }
            else
            {
                var matrix = computationResult.OdData;
                ret = matrix.CreateSimilarArray<float>();
            }
            var flatRet = ret.GetFlatData();
            for (int i = 0; i < flatRet.Length; i++)
            {
                flatRet[i][i] = 1.0f;
            }
            return new ComputationResult(ret, true);
        }

        private ComputationResult Matrix(ComputationResult computationResult)
        {
            var vectorData = computationResult.VectorData;
            var newMatrix = vectorData.CreateSquareTwinArray<float>();
            var flatVector = vectorData.GetFlatData();
            var flatMatrix = newMatrix.GetFlatData();
            switch (computationResult.Direction)
            {
                case ComputationResult.VectorDirection.Unassigned:
                    return new ComputationResult("Matrix was executed with an unassigned orientation vector!");
                case ComputationResult.VectorDirection.Vertical:
                    // each row is the single value
                    for (int i = 0; i < flatMatrix.Length; i++)
                    {
                        VectorHelper.Set(flatMatrix[i], flatVector[i]);
                    }
                    break;
                case ComputationResult.VectorDirection.Horizontal:
                    // each column is the single value
                    for (int i = 0; i < flatMatrix.Length; i++)
                    {
                        Array.Copy(flatVector, flatMatrix[i], flatVector.Length);
                    }
                    break;
            }
            return new ComputationResult(newMatrix, true);
        }

        private ComputationResult ZeroMatrix(ComputationResult[] values)
        {
            if (values[0].VectorData != null)
            {
                return new ComputationResult(values[0].VectorData.CreateSquareTwinArray<float>(), true);
            }
            else
            {
                return new ComputationResult(values[0].OdData.CreateSimilarArray<float>(), true);
            }
        }

        private ComputationResult Avg(ComputationResult computationResult)
        {
            if (computationResult.IsVectorResult)
            {
                var flat = computationResult.VectorData.GetFlatData();
                return new ComputationResult(VectorHelper.Sum(flat, 0, flat.Length) / flat.Length);
            }
            else
            {
                var flat = computationResult.OdData.GetFlatData();
                var total = 0.0f;
                var count = 0;
                for (int i = 0; i < flat.Length; i++)
                {
                    total += VectorHelper.Sum(flat[i], 0, flat[i].Length);
                    count += flat[i].Length;
                }
                return new ComputationResult(total / count);
            }
        }

        private ComputationResult Abs(ComputationResult computationResult)
        {
            if (computationResult.IsValue)
            {
                return new ComputationResult(Math.Abs(computationResult.LiteralValue));
            }
            else if (computationResult.IsVectorResult)
            {
                var retVector = computationResult.Accumulator ? computationResult.VectorData : computationResult.VectorData.CreateSimilarArray<float>();
                var flat = retVector.GetFlatData();
                VectorHelper.Abs(flat, computationResult.VectorData.GetFlatData());
                return new ComputationResult(retVector, true);
            }
            else
            {
                var retMatrix = computationResult.Accumulator ? computationResult.OdData : computationResult.OdData.CreateSimilarArray<float>();
                var flat = retMatrix.GetFlatData();
                VectorHelper.Abs(flat, computationResult.OdData.GetFlatData());
                return new ComputationResult(retMatrix, true);
            }
        }

        private ComputationResult Sum(ComputationResult computationResult)
        {
            if (computationResult.IsVectorResult)
            {
                return new ComputationResult(VectorHelper.Sum(computationResult.VectorData.GetFlatData(), 0, computationResult.VectorData.GetFlatData().Length));
            }
            else if (computationResult.IsOdResult)
            {
                float total = 0.0f;
                var data = computationResult.OdData.GetFlatData();
                var syncTarget = new object();

                System.Threading.Tasks.Parallel.For(0, data.Length,
                    () => 0.0f,
                    (int i, ParallelLoopState _, float localSum) =>
                    {
                        return VectorHelper.Sum(data[i], 0, data[i].Length) + localSum;
                    },
                    (float localTotal) =>
                    {
                        lock (syncTarget)
                        {
                            total += localTotal;
                        }
                    });
                return new ComputationResult(total);
            }
            return new ComputationResult("Unknown data type to sum!");
        }

        private ComputationResult TransposeOd(ComputationResult computationResult)
        {
            var ret = computationResult.Accumulator ? computationResult.OdData : computationResult.OdData.CreateSimilarArray<float>();
            var flatRet = ret.GetFlatData();
            var flatOrigin = computationResult.OdData.GetFlatData();
            for (int i = 0; i < flatOrigin.Length; i++)
            {
                for (int j = i + 1; j < flatOrigin[i].Length; j++)
                {
                    var temp = flatOrigin[i][j];
                    flatRet[i][j] = flatOrigin[j][i];
                    flatRet[j][i] = temp;
                }
            }
            // if this is a new matrix copy the diagonal
            if (!computationResult.Accumulator)
            {
                for (int i = 0; i < flatRet.Length; i++)
                {
                    flatRet[i][i] = flatOrigin[i][i];
                }
            }
            return new ComputationResult(ret, true);
        }

        private ComputationResult SumColumns(ComputationResult computationResult)
        {
            var data = computationResult.OdData;
            var ret = new SparseArray<float>(data.Indexes);
            var flatRet = ret.GetFlatData();
            var flatData = data.GetFlatData();
            System.Threading.Tasks.Parallel.For(0, flatData.Length, (int i) =>
            {
                VectorHelper.Add(flatRet, 0, flatRet, 0, flatData[i], 0, flatData[i].Length);
            });
            return new ComputationResult(ret, true, ComputationResult.VectorDirection.Horizontal);
        }

        private ComputationResult SumRows(ComputationResult computationResult)
        {
            var data = computationResult.OdData;
            var ret = new SparseArray<float>(data.Indexes);
            var flatRet = ret.GetFlatData();
            var flatData = data.GetFlatData();
            System.Threading.Tasks.Parallel.For(0, flatData.Length, (int i) =>
            {
                flatRet[i] = VectorHelper.Sum(flatData[i], 0, flatData[i].Length);
            });
            return new ComputationResult(ret, true, ComputationResult.VectorDirection.Vertical);
        }

        private ComputationResult AvgColumns(ComputationResult computationResult)
        {
            var data = computationResult.OdData;
            var ret = new SparseArray<float>(data.Indexes);
            var flatRet = ret.GetFlatData();
            var flatData = data.GetFlatData();
            System.Threading.Tasks.Parallel.For(0, flatData.Length, (int i) =>
            {
                VectorHelper.Add(flatRet, 0, flatRet, 0, flatData[i], 0, flatData[i].Length);
            });
            VectorHelper.Divide(flatRet, flatRet, flatData.Length);
            return new ComputationResult(ret, true, ComputationResult.VectorDirection.Horizontal);
        }

        private ComputationResult AvgRows(ComputationResult computationResult)
        {
            var data = computationResult.OdData;
            var ret = new SparseArray<float>(data.Indexes);
            var flatRet = ret.GetFlatData();
            var flatData = data.GetFlatData();
            System.Threading.Tasks.Parallel.For(0, flatData.Length, (int i) =>

            {
                flatRet[i] = VectorHelper.Sum(flatData[i], 0, flatData[i].Length) / flatData[i].Length;
            });
            return new ComputationResult(ret, true, ComputationResult.VectorDirection.Vertical);
        }

        private ComputationResult Length(ComputationResult computationResult)
        {
            if (computationResult.IsOdResult)
            {
                return new ComputationResult(computationResult.OdData.Count);
            }
            if (computationResult.IsVectorResult)
            {
                return new ComputationResult(computationResult.VectorData.Count);
            }
            return new ComputationResult("An unknown data type was processed through Length!");
        }

        private ComputationResult LengthRows(ComputationResult computationResult)
        {
            if (computationResult.IsOdResult)
            {
                var data = computationResult.OdData;
                var ret = new SparseArray<float>(data.Indexes);
                var flatRet = ret.GetFlatData();
                var flatData = data.GetFlatData();
                for (int i = 0; i < flatData.Length; i++)
                {
                    flatRet[i] = flatData[i].Length;
                }
                return new ComputationResult(ret, true, ComputationResult.VectorDirection.Vertical);
            }
            return new ComputationResult("An unknown data type was processed through LengthRows!");
        }

        private ComputationResult LengthColumns(ComputationResult computationResult)
        {
            if (computationResult.IsOdResult)
            {
                var data = computationResult.OdData;
                var ret = new SparseArray<float>(data.Indexes);
                var flatRet = ret.GetFlatData();
                var flatData = data.GetFlatData();
                System.Threading.Tasks.Parallel.For(0, flatData.Length, (int i) =>
                {
                    int temp = 0;
                    for (int j = 0; j < flatData.Length; j++)
                    {
                        if (flatData[j].Length > i)
                        {
                            temp++;
                        }
                    }
                    flatRet[i] = temp;
                });
                return new ComputationResult(ret, true, ComputationResult.VectorDirection.Horizontal);
            }
            return new ComputationResult("An unknown data type was processed through LengthColumns!");
        }
    }
}
