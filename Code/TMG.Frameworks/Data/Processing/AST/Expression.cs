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
    public abstract class Expression : ASTNode
    {

        public Expression(int start) : base(start)
        {

        }

        private static int FindEndOfBracket(char[] buffer, int start, int length, ref string error)
        {
            int bracketLevel = 1;
            int i = start;
            for (; i < start + length && bracketLevel > 0; i++)
            {
                if (buffer[i] == ')')
                {
                    bracketLevel--;
                }
                else if (buffer[i] == '(')
                {
                    bracketLevel++;
                }
            }
            if (bracketLevel == 0)
            {
                return i - 1;
            }
            error = "Unable to find end of bracket starting at position " + start;
            return -1;
        }

        internal static bool Optimize(ref Expression ex, ref string error)
        {
            // if this ever becomes a real problem try to add some optimization to the expression tree
            return true;
        }

        private static int FindStartOfBracket(char[] buffer, int start, int length, ref string error)
        {
            int bracketLevel = 1;
            int i = start + length - 1;
            for (; i >= start && bracketLevel > 0; i--)
            {
                if (buffer[i] == '(')
                {
                    bracketLevel--;
                }
                else if (buffer[i] == ')')
                {
                    bracketLevel++;
                }
            }
            if (bracketLevel == 0)
            {
                return i + 1;
            }
            error = "Unable to find start of bracket with the end bracket at position " + start;
            return -1;
        }

        public static bool Compile(char[] buffer, int start, int length, out Expression ex, ref string error)
        {
            ex = null;
            var endPlusOne = (length + start);
            // try to extract + and -
            for (int i = start; i < endPlusOne; i++)
            {
                switch (buffer[i])
                {
                    case '(':
                        {
                            int endIndex = FindEndOfBracket(buffer, i + 1, endPlusOne - (i + 1), ref error);
                            if (endIndex < 0)
                            {
                                return false;
                            }
                            i = endIndex;
                        }
                        break;
                    case '+':
                    case '-':
                        {
                            BinaryExpression toReturn = buffer[i] == '+' ? (BinaryExpression)new Add(i) : (BinaryExpression)new Subtract(i);
                            if (!Compile(buffer, start, i - start, out toReturn.LHS, ref error)) return false;
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.RHS, ref error)) return false;
                            ex = toReturn;
                            return true;
                        }
                }
            }
            // if there are no adds work on multiplies fix this for division
            for (int i = start; i < endPlusOne; i++)
            {
                switch (buffer[i])
                {
                    case '(':
                        {
                            int endIndex = FindEndOfBracket(buffer, i + 1, endPlusOne - (i + 1), ref error);
                            if (endIndex < 0)
                            {
                                return false;
                            }
                            i = endIndex;
                        }
                        break;
                    case '*':
                        {
                            BinaryExpression toReturn = (BinaryExpression)new Multiply(i);
                            if (!Compile(buffer, start, i - start, out toReturn.LHS, ref error)) return false;
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.RHS, ref error)) return false;
                            ex = toReturn;
                            return true;
                        }
                }
            }
            // if there are no adds work on division
            for (int i = length + start - 1; i >= start; i--)
            {
                switch (buffer[i])
                {
                    case ')':
                        {
                            int endIndex = FindStartOfBracket(buffer, start, i - start, ref error);
                            if (endIndex < 0)
                            {
                                return false;
                            }
                            i = endIndex;
                        }
                        break;
                    case '/':
                        {
                            BinaryExpression toReturn = (BinaryExpression)new Divide(i);
                            if (!Compile(buffer, start, i - start, out toReturn.LHS, ref error)) return false;
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.RHS, ref error)) return false;
                            ex = toReturn;
                            return true;
                        }
                }
            }
            StringBuilder builder = new StringBuilder();
            bool first = true;
            bool complete = false;
            // check for function calls
            for (int i = start; i < start + length && !complete; i++)
            {
                switch (buffer[i])
                {
                    case ' ':
                        if (!first)
                        {
                            complete = true;
                        }
                        break;
                    case '(':
                        {
                            if (first)
                            {
                                complete = true;
                            }
                            else
                            {
                                int endIndex = FindEndOfBracket(buffer, i + 1, endPlusOne - (i + 1), ref error);
                                if (endIndex < 0)
                                {
                                    return false;
                                }
                                FunctionCall toReturn;
                                List<Expression> parameters = new List<Expression>();
                                int lastStart = i + 1;
                                Expression p;
                                for (int j = i + 1; j < endIndex; j++)
                                {
                                    if (buffer[j] == ',')
                                    {
                                        if (!Compile(buffer, lastStart, j - lastStart, out p, ref error))
                                        {
                                            return false;
                                        }
                                        parameters.Add(p);
                                        lastStart = j + 1;
                                    }
                                }
                                // add the final parameter
                                if (!Compile(buffer, lastStart, endIndex - lastStart, out p, ref error))
                                {
                                    return false;
                                }
                                parameters.Add(p);
                                if (!FunctionCall.GetCall(start, builder.ToString(), parameters.ToArray(), out toReturn, ref error))
                                {
                                    return false;
                                }
                                ex = toReturn;
                                return true;
                            }
                        }
                        break;
                    default:
                        first = false;
                        builder.Append(buffer[i]);
                        break;
                }
            }
            // deal with brackets
            for (int i = start; i < start + length; i++)
            {
                switch (buffer[i])
                {
                    case '(':
                        {
                            int endIndex = FindEndOfBracket(buffer, i + 1, endPlusOne - (i + 1), ref error);
                            if (endIndex < 0)
                            {
                                return false;
                            }
                            var toReturn = new Bracket(i);
                            if (!Compile(buffer, i + 1, (endIndex - i) - 1, out toReturn.InnerExpression, ref error))
                            {
                                return false;
                            }
                            ex = toReturn;
                            return true;
                        }
                }
            }
            // try to extract literal / variable name
            builder.Clear();
            first = true;
            complete = false;
            int index = -1;
            for (int i = start; i < endPlusOne && !complete; i++)
            {
                switch (buffer[i])
                {
                    case ' ':
                        if (first)
                        {
                            // just skip
                        }
                        else
                        {
                            complete = true;
                            // end of string
                        }
                        break;
                    default:
                        if (first)
                        {
                            first = false;
                            index = i;
                        }
                        builder.Append(buffer[i]);
                        break;
                }
            }
            var value = builder.ToString();
            float f;
            if (value.Length <= 0)
            {
                error = "We were unable to read in a value at position " + start;
                return false;
            }
            if (float.TryParse(value, out f))
            {
                // if we can read it in as a floating point number
                ex = new Literal(index, f);
            }
            else
            {
                ex = new Variable(index, value);
            }
            return true;
        }
    }


    public abstract class MonoExpression : Expression
    {
        public Expression InnerExpression;

        public MonoExpression(int start) : base(start)
        {

        }
    }

    public abstract class BinaryExpression : Expression
    {
        public Expression LHS;
        public Expression RHS;

        public BinaryExpression(int start) : base(start)
        {

        }

        public override ComputationResult Evaluate(IDataSource[] dataSources)
        {
            var lhs = LHS.Evaluate(dataSources);
            var rhs = RHS.Evaluate(dataSources);
            if (lhs.Error)
            {
                return lhs;
            }
            else if (rhs.Error)
            {
                return rhs;
            }
            return Evaluate(lhs, rhs);
        }

        public abstract ComputationResult Evaluate(ComputationResult lhs, ComputationResult rhs);
    }

    public abstract class Value : Expression
    {
        public Value(int start) : base(start)
        {

        }
    }

    public class FunctionCall : Value
    {

        public enum FunctionType
        {
            Undefined,
            Transpose,
            SumRows,
            SumColumns,
            AsHorizontal,
            AsVertical,
            Sum
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
            }
            return new ComputationResult("An undefined function was executed!");
        }

        private ComputationResult Sum(ComputationResult computationResult)
        {
            if(computationResult.IsVectorResult)
            {
                return new ComputationResult(VectorHelper.Sum(computationResult.VectorData.GetFlatData(), 0, computationResult.VectorData.GetFlatData().Length));
            }
            else if(computationResult.IsODResult)
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
                flatRet[i] = VectorHelper.Sum(flatData[i], 0, flatData.Length);
            }
            return new ComputationResult(ret, true, ComputationResult.VectorDirection.Vertical);
        }
    }

    public class Add : BinaryExpression
    {
        public Add(int start) : base(start)
        {

        }

        public override ComputationResult Evaluate(ComputationResult lhs, ComputationResult rhs)
        {
            // see if we have two values, in this case we can skip doing the matrix operation
            if (lhs.IsValue && rhs.IsValue)
            {
                return new ComputationResult(lhs.LiteralValue + rhs.LiteralValue);
            }
            // float / matrix
            if (lhs.IsValue)
            {
                if (rhs.IsVectorResult)
                {
                    var retVector = rhs.Accumulator ? rhs.VectorData : rhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.Add(flat, rhs.VectorData.GetFlatData(), lhs.LiteralValue);
                    return new ComputationResult(retVector, true);
                }
                else
                {
                    var retMatrix = rhs.Accumulator ? rhs.ODData : rhs.ODData.CreateSimilarArray<float>();
                    VectorHelper.Add(retMatrix.GetFlatData(), lhs.LiteralValue, rhs.ODData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
            else if (rhs.IsValue)
            {
                if (lhs.IsVectorResult)
                {
                    var retVector = lhs.Accumulator ? lhs.VectorData : lhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.Add(flat, lhs.VectorData.GetFlatData(), rhs.LiteralValue);
                    return new ComputationResult(retVector, true);
                }
                else
                {
                    // matrix / float
                    var retMatrix = lhs.Accumulator ? lhs.ODData : lhs.ODData.CreateSimilarArray<float>();
                    VectorHelper.Add(retMatrix.GetFlatData(), lhs.ODData.GetFlatData(), rhs.LiteralValue);
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
                        VectorHelper.Add(retMatrix.GetFlatData(), 0, lhs.VectorData.GetFlatData(), 0, rhs.VectorData.GetFlatData(), 0, retMatrix.GetFlatData().Length);
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
                                VectorHelper.Add(flatRet[i], flatRHS[i], flatLHS[i]);
                            }
                        }
                        else if (lhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            for (int i = 0; i < flatRHS.Length; i++)
                            {
                                VectorHelper.Add(flatRet[i], 0, flatLHS, 0, flatRHS[i], 0, flatRet[i].Length);
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
                                VectorHelper.Add(flatLHS[i], flatLHS[i], flatRHS[i]);
                            }
                        }
                        else if (rhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            for (int i = 0; i < flatRet.Length; i++)
                            {
                                VectorHelper.Add(flatRet[i], 0, flatRHS, 0, flatLHS[i], 0, flatRet[i].Length);
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
                    VectorHelper.Add(retMatrix.GetFlatData(), lhs.ODData.GetFlatData(), rhs.ODData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
        }
    }

    public class Subtract : BinaryExpression
    {
        public Subtract(int start) : base(start)
        {

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
                    var retMatrix = rhs.Accumulator ? rhs.ODData : rhs.ODData.CreateSimilarArray<float>();
                    VectorHelper.Subtract(retMatrix.GetFlatData(), lhs.LiteralValue, rhs.ODData.GetFlatData());
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
                    var retMatrix = lhs.Accumulator ? lhs.ODData : lhs.ODData.CreateSimilarArray<float>();
                    VectorHelper.Subtract(retMatrix.GetFlatData(), lhs.ODData.GetFlatData(), rhs.LiteralValue);
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
                        var retMatrix = rhs.Accumulator ? rhs.ODData : rhs.ODData.CreateSimilarArray<float>();
                        var flatRet = retMatrix.GetFlatData();
                        var flatRHS = rhs.ODData.GetFlatData();
                        var flatLHS = lhs.VectorData.GetFlatData();
                        if (lhs.Direction == ComputationResult.VectorDirection.Vertical)
                        {
                            for (int i = 0; i < flatRHS.Length; i++)
                            {
                                VectorHelper.Subtract(flatRet[i], flatRHS[i], flatLHS[i]);
                            }
                        }
                        else if (lhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            for (int i = 0; i < flatRHS.Length; i++)
                            {
                                VectorHelper.Subtract(flatRet[i], 0, flatLHS, 0, flatRHS[i], 0, flatRet[i].Length);
                            }
                        }
                        else
                        {
                            return new ComputationResult("Unable to subtract vector without directionality starting at position " + LHS.Start + "!");
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
                                VectorHelper.Subtract(flatLHS[i], flatLHS[i], flatRHS[i]);
                            }
                        }
                        else if (rhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            for (int i = 0; i < flatRet.Length; i++)
                            {
                                VectorHelper.Subtract(flatRet[i], 0, flatRHS, 0, flatLHS[i], 0, flatRet[i].Length);
                            }
                        }
                        else
                        {
                            return new ComputationResult("Unable to subtract vector without directionality starting at position " + LHS.Start + "!");
                        }
                        return new ComputationResult(retMatrix, true);
                    }
                }
                else
                {
                    var retMatrix = lhs.Accumulator ? lhs.ODData : (rhs.Accumulator ? rhs.ODData : lhs.ODData.CreateSimilarArray<float>());
                    VectorHelper.Subtract(retMatrix.GetFlatData(), lhs.ODData.GetFlatData(), rhs.ODData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
        }
    }

    public class Multiply : BinaryExpression
    {
        public Multiply(int start) : base(start)
        {

        }

        public override ComputationResult Evaluate(ComputationResult lhs, ComputationResult rhs)
        {
            // see if we have two values, in this case we can skip doing the matrix operation
            if (lhs.IsValue && rhs.IsValue)
            {
                return new ComputationResult(lhs.LiteralValue * rhs.LiteralValue);
            }
            // float / matrix
            if (lhs.IsValue)
            {
                if (rhs.IsVectorResult)
                {
                    var retVector = rhs.Accumulator ? rhs.VectorData : rhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.Multiply(flat, rhs.VectorData.GetFlatData(), lhs.LiteralValue);
                    return new ComputationResult(retVector, true);
                }
                else
                {
                    var retMatrix = rhs.Accumulator ? rhs.ODData : rhs.ODData.CreateSimilarArray<float>();
                    VectorHelper.Multiply(retMatrix.GetFlatData(), lhs.LiteralValue, rhs.ODData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
            else if (rhs.IsValue)
            {
                if (lhs.IsVectorResult)
                {
                    var retVector = lhs.Accumulator ? lhs.VectorData : lhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.Multiply(flat, lhs.VectorData.GetFlatData(), rhs.LiteralValue);
                    return new ComputationResult(retVector, true);
                }
                else
                {
                    // matrix / float
                    var retMatrix = lhs.Accumulator ? lhs.ODData : lhs.ODData.CreateSimilarArray<float>();
                    VectorHelper.Multiply(retMatrix.GetFlatData(), lhs.ODData.GetFlatData(), rhs.LiteralValue);
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
                        VectorHelper.Multiply(retMatrix.GetFlatData(), 0, lhs.VectorData.GetFlatData(), 0, rhs.VectorData.GetFlatData(), 0, retMatrix.GetFlatData().Length);
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
                                VectorHelper.Multiply(flatRet[i], flatRHS[i], flatLHS[i]);
                            }
                        }
                        else if (lhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            for (int i = 0; i < flatRHS.Length; i++)
                            {
                                VectorHelper.Multiply(flatRet[i], 0, flatLHS, 0, flatRHS[i], 0, flatRet[i].Length);
                            }
                        }
                        else
                        {
                            return new ComputationResult("Unable to multiply vector without directionality starting at position " + LHS.Start + "!");
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
                                VectorHelper.Multiply(flatLHS[i], flatLHS[i], flatRHS[i]);
                            }
                        }
                        else if (rhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            for (int i = 0; i < flatRet.Length; i++)
                            {
                                VectorHelper.Multiply(flatRet[i], 0, flatRHS, 0, flatLHS[i], 0, flatRet[i].Length);
                            }
                        }
                        else
                        {
                            return new ComputationResult("Unable to multiply vector without directionality starting at position " + LHS.Start + "!");
                        }
                        return new ComputationResult(retMatrix, true);
                    }
                }
                else
                {
                    var retMatrix = lhs.Accumulator ? lhs.ODData : (rhs.Accumulator ? rhs.ODData : lhs.ODData.CreateSimilarArray<float>());
                    VectorHelper.Multiply(retMatrix.GetFlatData(), lhs.ODData.GetFlatData(), rhs.ODData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
        }
    }

    public class Divide : BinaryExpression
    {
        public Divide(int start) : base(start)
        {

        }

        public override ComputationResult Evaluate(ComputationResult lhs, ComputationResult rhs)
        {
            // see if we have two values, in this case we can skip doing the matrix operation
            if (lhs.IsValue && rhs.IsValue)
            {
                return new ComputationResult(lhs.LiteralValue / rhs.LiteralValue);
            }
            // float / matrix
            if (lhs.IsValue)
            {
                if (rhs.IsVectorResult)
                {
                    var retVector = rhs.Accumulator ? rhs.VectorData : rhs.VectorData.CreateSimilarArray<float>();
                    var flat = retVector.GetFlatData();
                    VectorHelper.Divide(flat, lhs.LiteralValue, rhs.VectorData.GetFlatData());
                    return new ComputationResult(retVector, true);
                }
                else
                {
                    var retMatrix = rhs.Accumulator ? rhs.ODData : rhs.ODData.CreateSimilarArray<float>();
                    VectorHelper.Divide(retMatrix.GetFlatData(), lhs.LiteralValue, rhs.ODData.GetFlatData());
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
                    var retMatrix = lhs.Accumulator ? lhs.ODData : lhs.ODData.CreateSimilarArray<float>();
                    VectorHelper.Divide(retMatrix.GetFlatData(), lhs.ODData.GetFlatData(), rhs.LiteralValue);
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
                        VectorHelper.Divide(retMatrix.GetFlatData(), 0, lhs.VectorData.GetFlatData(), 0, rhs.VectorData.GetFlatData(), 0, retMatrix.GetFlatData().Length);
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
                                VectorHelper.Divide(flatRet[i], flatRHS[i], flatLHS[i]);
                            }
                        }
                        else if (lhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            for (int i = 0; i < flatRHS.Length; i++)
                            {
                                VectorHelper.Divide(flatRet[i], 0, flatLHS, 0, flatRHS[i], 0, flatRet[i].Length);
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
                                VectorHelper.Divide(flatLHS[i], flatLHS[i], flatRHS[i]);
                            }
                        }
                        else if (rhs.Direction == ComputationResult.VectorDirection.Horizontal)
                        {
                            for (int i = 0; i < flatRet.Length; i++)
                            {
                                VectorHelper.Divide(flatRet[i], 0, flatRHS, 0, flatLHS[i], 0, flatRet[i].Length);
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
                    VectorHelper.Divide(retMatrix.GetFlatData(), lhs.ODData.GetFlatData(), rhs.ODData.GetFlatData());
                    return new ComputationResult(retMatrix, true);
                }
            }
        }
    }

    public class Bracket : MonoExpression
    {
        public Bracket(int start) : base(start)
        {

        }

        public override ComputationResult Evaluate(IDataSource[] dataSources)
        {
            return InnerExpression.Evaluate(dataSources);
        }
    }

    public class Literal : Value
    {
        public readonly float Value;

        public Literal(int start, float value) : base(start)
        {
            Value = value;
        }

        public override ComputationResult Evaluate(IDataSource[] dataSources)
        {
            return new ComputationResult(Value);
        }
    }

    public class Variable : Value
    {
        public readonly string Name;

        public Variable(int start, string name) : base(start)
        {
            Name = name;
        }

        public override ComputationResult Evaluate(IDataSource[] dataSources)
        {
            var source = dataSources.FirstOrDefault(d => d.Name == Name);
            if (source == null)
            {
                return new ComputationResult("Unable to find a data source named '" + Name + "'!");
            }
            if (!source.Loaded)
            {
                source.LoadData();
            }
            var odSource = source as IDataSource<SparseTwinIndex<float>>;
            if (odSource != null)
            {
                return new ComputationResult(odSource.GiveData(), false);
            }
            var vectorSource = source as IDataSource<SparseArray<float>>;
            if (vectorSource != null)
            {
                return new ComputationResult(vectorSource.GiveData(), false);
            }
            var valueSource = source as IDataSource<float>;
            if (valueSource != null)
            {
                return new ComputationResult(valueSource.GiveData());
            }
            return new ComputationResult("The data source '" + Name + "' was not of a valid resource type!");
        }
    }

}
