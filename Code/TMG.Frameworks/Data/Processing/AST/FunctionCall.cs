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
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            PI,
            Length,
            LengthColumns,
            LengthRows,
            ZeroMatrix,
            Matrix,
            IdentityMatrix
        }

        private FunctionType Type;

        private Expression[] Parameters;

        private FunctionCall(int start, FunctionType call, Expression[] parameters) : base(start)
        {
            Parameters = parameters;
            Type = call;
        }

        public static bool GetCall(int start, string call, Expression[] parameters, out FunctionCall ex, ref string error)
        {
            //decode the call to a type
            FunctionType type;
            ex = null;
            if (!Decode(call, ref error, out type))
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
                    type = FunctionType.PI;
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
                        if (values[0].IsODResult)
                        {
                            return TransposeOD(values[0]);
                        }
                        return new ComputationResult("Unsupported data type for Transpose at position " + Start + ".");
                    }
                case FunctionType.SumRows:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("SumRows was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsODResult)
                    {
                        return new ComputationResult("SumRows was executed with a parameter that was not a matrix!");
                    }
                    return SumRows(values[0]);
                case FunctionType.SumColumns:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("SumColumns was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsODResult)
                    {
                        return new ComputationResult("SumColumns was executed with a parameter that was not a matrix!");
                    }
                    return SumColumns(values[0]);
                case FunctionType.AvgRows:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("AvgRows was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsODResult)
                    {
                        return new ComputationResult("AvgRows was executed with a parameter that was not a matrix!");
                    }
                    return AvgRows(values[0]);
                case FunctionType.AvgColumns:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("AvgColumns was executed with the wrong number of parameters!");
                    }
                    if (!values[0].IsODResult)
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
                case FunctionType.PI:
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
                    if (values[0].IsODResult)
                    {
                        return new ComputationResult("LengthColumns must be applied to a Matrix!");
                    }
                    return LengthColumns(values[0]);
                case FunctionType.LengthRows:
                    if (values.Length != 1)
                    {
                        return new ComputationResult("LengthRows was executed with the wrong number of parameters!");
                    }
                    if (values[0].IsODResult)
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
                    if(values.Length != 1)
                    {
                        return new ComputationResult("Matrix was executed with the wrong number of parameters!");
                    }
                    if(!values[0].IsVectorResult)
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
            }
            return new ComputationResult("An undefined function was executed!");
        }

        private ComputationResult IdentityMatrix(ComputationResult computationResult)
        {
            SparseTwinIndex<float> ret;
            if(computationResult.IsVectorResult)
            {
                var vector = computationResult.VectorData;
                ret = vector.CreateSquareTwinArray<float>();
            }
            else
            {
                var matrix = computationResult.ODData;
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
                return new ComputationResult(values[0].ODData.CreateSimilarArray<float>(), true);
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
                var flat = computationResult.ODData.GetFlatData();
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
                var retMatrix = computationResult.Accumulator ? computationResult.ODData : computationResult.ODData.CreateSimilarArray<float>();
                var flat = retMatrix.GetFlatData();
                VectorHelper.Abs(flat, computationResult.ODData.GetFlatData());
                return new ComputationResult(retMatrix, true);
            }
        }

        private ComputationResult Sum(ComputationResult computationResult)
        {
            if (computationResult.IsVectorResult)
            {
                return new ComputationResult(VectorHelper.Sum(computationResult.VectorData.GetFlatData(), 0, computationResult.VectorData.GetFlatData().Length));
            }
            else if (computationResult.IsODResult)
            {
                float total = 0.0f;
                var data = computationResult.ODData.GetFlatData();
                for (int i = 0; i < data.Length; i++)
                {
                    total += VectorHelper.Sum(data[i], 0, data[i].Length);
                }
                return new ComputationResult(total);
            }
            return new ComputationResult("Unknown data type to sum!");
        }

        private ComputationResult TransposeOD(ComputationResult computationResult)
        {
            var ret = computationResult.Accumulator ? computationResult.ODData : computationResult.ODData.CreateSimilarArray<float>();
            var flatRet = ret.GetFlatData();
            var flatOrigin = computationResult.ODData.GetFlatData();
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
            var data = computationResult.ODData;
            var ret = new SparseArray<float>(data.Indexes);
            var flatRet = ret.GetFlatData();
            var flatData = data.GetFlatData();
            for (int i = 0; i < flatData.Length; i++)
            {
                VectorHelper.Add(flatRet, 0, flatRet, 0, flatData[i], 0, flatData[i].Length);
            }
            return new ComputationResult(ret, true, ComputationResult.VectorDirection.Horizontal);
        }

        private ComputationResult SumRows(ComputationResult computationResult)
        {
            var data = computationResult.ODData;
            var ret = new SparseArray<float>(data.Indexes);
            var flatRet = ret.GetFlatData();
            var flatData = data.GetFlatData();
            for (int i = 0; i < flatData.Length; i++)
            {
                flatRet[i] = VectorHelper.Sum(flatData[i], 0, flatData[i].Length);
            }
            return new ComputationResult(ret, true, ComputationResult.VectorDirection.Vertical);
        }

        private ComputationResult AvgColumns(ComputationResult computationResult)
        {
            var data = computationResult.ODData;
            var ret = new SparseArray<float>(data.Indexes);
            var flatRet = ret.GetFlatData();
            var flatData = data.GetFlatData();
            for (int i = 0; i < flatData.Length; i++)
            {
                VectorHelper.Add(flatRet, 0, flatRet, 0, flatData[i], 0, flatData[i].Length);
            }
            VectorHelper.Divide(flatRet, flatRet, flatData.Length);
            return new ComputationResult(ret, true, ComputationResult.VectorDirection.Horizontal);
        }

        private ComputationResult AvgRows(ComputationResult computationResult)
        {
            var data = computationResult.ODData;
            var ret = new SparseArray<float>(data.Indexes);
            var flatRet = ret.GetFlatData();
            var flatData = data.GetFlatData();
            for (int i = 0; i < flatData.Length; i++)
            {
                flatRet[i] = VectorHelper.Sum(flatData[i], 0, flatData[i].Length) / flatData[i].Length;
            }
            return new ComputationResult(ret, true, ComputationResult.VectorDirection.Vertical);
        }

        private ComputationResult Length(ComputationResult computationResult)
        {
            if (computationResult.IsODResult)
            {
                return new ComputationResult((float)computationResult.ODData.Count);
            }
            if (computationResult.IsVectorResult)
            {
                return new ComputationResult((float)computationResult.VectorData.Count);
            }
            return new ComputationResult("An unknown data type was processed through Length!");
        }

        private ComputationResult LengthRows(ComputationResult computationResult)
        {
            if (computationResult.IsODResult)
            {
                var data = computationResult.ODData;
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
            if (computationResult.IsODResult)
            {
                var data = computationResult.ODData;
                var ret = new SparseArray<float>(data.Indexes);
                var flatRet = ret.GetFlatData();
                var flatData = data.GetFlatData();
                for (int i = 0; i < flatData.Length; i++)
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
                }
                return new ComputationResult(ret, true, ComputationResult.VectorDirection.Horizontal);
            }
            return new ComputationResult("An unknown data type was processed through LengthColumns!");
        }
    }
}
