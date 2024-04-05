/*
    Copyright 2016-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

using System.Collections.Generic;
using System.Text;
using XTMF;

namespace TMG.Frameworks.Data.Processing.AST
{
    public abstract class Expression : AstNode
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
            return ex.OptimizeAst(ref ex, ref error);
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

        private static bool AnyNonWhitespace(char[] buffer, int start, int length)
        {
            for (int i = start; i < start + length; i++)
            {
                if (buffer[i] != ' ')
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsCompareType(Expression e)
        {
            var t = e.GetType();
            if(t == typeof(Bracket))
            {
                return IsCompareType(((Bracket)e).InnerExpression);
            }
            return t == typeof(CompareEqual)
                || t == typeof(CompareNotEquals)
                || t == typeof(CompareLessThan)
                || t == typeof(CompareGreaterThan)
                || t == typeof(CompareLessThanOrEqual)
                || t == typeof(CompareGreaterThanOrEqual)
                || t == typeof(CompareAnd)
                || t == typeof(CompareOr);
        }

        public static bool Compile(char[] buffer, int start, int length, out Expression ex, ref string error)
        {
            ex = null;
            var endPlusOne = (length + start);
            // support AND and OR for our compare operations
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
                    case '&':
                        {
                            BinaryExpression toReturn = new CompareAnd(i);
                            if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.Rhs, ref error)) return false;
                            // test LHS to make sure it is a compare
                            if(!IsCompareType(toReturn.Lhs) && !IsCompareType(toReturn.Rhs))
                            {
                                error = $"At position {i} we found an '&' character where neither the LHS and the RHS were flag types, at least one is required!";
                                return false;
                            }
                            ex = toReturn;
                            return true;
                        }
                    case '|':
                        {
                            BinaryExpression toReturn = new CompareOr(i);
                            if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.Rhs, ref error)) return false;
                            // test LHS to make sure it is a compare
                            if (!IsCompareType(toReturn.Lhs))
                            {
                                error = $"At position {i} we found a '|' character where the LHS was not a flag type!";
                                return false;
                            }
                            // test RHS to make sure it is a compare
                            if (!IsCompareType(toReturn.Rhs))
                            {
                                error = $"At position {i} we found a '|' character where the RHS was not a flag type!";
                                return false;
                            }
                            ex = toReturn;
                            return true;
                        }
                }
            }
            // support compare
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
                    case '=':
                        {
                            if (i + 1 < endPlusOne && buffer[i + 1] == '=')
                            {
                                BinaryExpression toReturn = new CompareEqual(i);
                                if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                                if (!Compile(buffer, i + 2, endPlusOne - i - 2, out toReturn.Rhs, ref error)) return false;
                                ex = toReturn;
                                return true;
                            }
                            else
                            {
                                error = $"At position {i} we found an '=' character without an accompanying '='!";
                                return false;
                            }
                        }
                    case '!':
                        {
                            if (i + 1 < endPlusOne && buffer[i + 1] == '=')
                            {
                                BinaryExpression toReturn = new CompareNotEquals(i);
                                if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                                if (!Compile(buffer, i + 2, endPlusOne - i - 2, out toReturn.Rhs, ref error)) return false;
                                ex = toReturn;
                                return true;
                            }
                            else
                            {
                                error = $"At position {i} we found an '!' character without an accompanying '='!";
                                return false;
                            }
                        }
                    case '>':
                        {
                            if (i + 1 < endPlusOne && buffer[i + 1] == '=')
                            {
                                BinaryExpression toReturn = new CompareGreaterThanOrEqual(i);
                                if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                                if (!Compile(buffer, i + 2, endPlusOne - i - 2, out toReturn.Rhs, ref error)) return false;
                                ex = toReturn;
                                return true;
                            }
                            else
                            {
                                BinaryExpression toReturn = new CompareGreaterThan(i);
                                if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                                if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.Rhs, ref error)) return false;
                                ex = toReturn;
                                return true;
                            }
                        }
                    case '<':
                        {
                            if (i + 1 < endPlusOne && buffer[i + 1] == '=')
                            {
                                BinaryExpression toReturn = new CompareLessThanOrEqual(i);
                                if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                                if (!Compile(buffer, i + 2, endPlusOne - i - 2, out toReturn.Rhs, ref error)) return false;
                                ex = toReturn;
                                return true;
                            }
                            else
                            {
                                BinaryExpression toReturn = new CompareLessThan(i);
                                if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                                if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.Rhs, ref error)) return false;
                                ex = toReturn;
                                return true;
                            }
                        }
                }
            }
            // try to extract +
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
                        {
                            BinaryExpression toReturn = new Add(i);
                            if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error))
                            {
                                return false;
                            }
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.Rhs, ref error)) return false;
                            ex = toReturn;
                            return true;
                        }
                }
            }
            // try to extract -
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
                    case '-':
                        {
                            BinaryExpression toReturn = new Subtract(i);
                            if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error))
                            {
                                // check to see if it is negate
                                if (buffer[i] == '-')
                                {
                                    bool anythingAfter = false;
                                    for (int k = i + 1; k < endPlusOne; k++)
                                    {
                                        if (!char.IsWhiteSpace(buffer[k]))
                                        {
                                            anythingAfter = true;
                                            break;
                                        }
                                    }
                                    if(anythingAfter)
                                    {
                                        error = null;
                                        continue;
                                    }
                                }
                                return false;
                            }
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.Rhs, ref error)) return false;
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
                            BinaryExpression toReturn = new Multiply(i);
                            if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.Rhs, ref error)) return false;
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
                            BinaryExpression toReturn = new Divide(i);
                            if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.Rhs, ref error)) return false;
                            ex = toReturn;
                            return true;
                        }
                }
            }
            // support exponents
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
                    case '^':
                        {
                            BinaryExpression toReturn = new Exponent(i);
                            if (!Compile(buffer, start, i - start, out toReturn.Lhs, ref error)) return false;
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.Rhs, ref error)) return false;
                            ex = toReturn;
                            return true;
                        }
                }
            }
            // support negate
            for (int i = start; i < endPlusOne; i++)
            {
                switch (buffer[i])
                {
                    case '(':
                        int endIndex = FindEndOfBracket(buffer, i + 1, endPlusOne - (i + 1), ref error);
                        if (endPlusOne < 0)
                        {
                            return false;
                        }
                        i = endIndex;
                        break;
                    case '-':
                        {
                            MonoExpression toReturn = new Negate(start);
                            if (!Compile(buffer, i + 1, endPlusOne - i - 1, out toReturn.InnerExpression, ref error)) return false;
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
                                List<Expression> parameters = [];
                                int lastStart = i + 1;
                                Expression p;
                                for (int j = i + 1; j < endIndex; j++)
                                {
                                    if (buffer[j] == '(')
                                    {
                                        // skip to the end
                                        var innerEndIndex = FindEndOfBracket(buffer, j + 1, endIndex, ref error);
                                        if(innerEndIndex < 0)
                                        {
                                            return false;
                                        }
                                        j = innerEndIndex;
                                    }
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
                                if (AnyNonWhitespace(buffer, lastStart, endIndex - lastStart))
                                {
                                    // add the final parameter
                                    if (!Compile(buffer, lastStart, endIndex - lastStart, out p, ref error))
                                    {
                                        return false;
                                    }
                                    parameters.Add(p);
                                }
                                if (!FunctionCall.GetCall(start, builder.ToString(), parameters.ToArray(), out FunctionCall toReturn, ref error))
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
            if (value.Length <= 0)
            {
                error = "We were unable to read in a value at position " + start;
                return false;
            }
            if (float.TryParse(value, out float f))
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

        internal override bool OptimizeAst(ref Expression ex, ref string error)
        {
            return InnerExpression.OptimizeAst(ref InnerExpression, ref error);
        }
    }

    public abstract class BinaryExpression : Expression
    {
        public Expression Lhs;
        public Expression Rhs;

        public BinaryExpression(int start) : base(start)
        {

        }

        public override ComputationResult Evaluate(IDataSource[] dataSources)
        {
            var lhs = Lhs.Evaluate(dataSources);
            var rhs = Rhs.Evaluate(dataSources);
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

        internal override bool OptimizeAst(ref Expression ex, ref string error)
        {
            if(!Lhs.OptimizeAst(ref Lhs, ref error) || !Rhs.OptimizeAst(ref Rhs, ref error))
            {
                return false;
            }
            return true;
        }
    }

    public abstract class Value : Expression
    {
        public Value(int start) : base(start)
        {

        }

        internal override bool OptimizeAst(ref Expression ex, ref string error)
        {
            return true;
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

        internal override bool OptimizeAst(ref Expression ex, ref string error)
        {
            if(!InnerExpression.OptimizeAst(ref ex, ref error))
            {
                return false;
            }
            ex = InnerExpression;
            return true;
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
}
