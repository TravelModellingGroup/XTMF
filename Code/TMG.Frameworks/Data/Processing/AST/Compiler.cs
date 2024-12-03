/*
    Copyright 2016-2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Diagnostics.CodeAnalysis;

namespace TMG.Frameworks.Data.Processing.AST;

public static class Compiler
{
    /// <summary>
    /// Compiles the expression and returns the AST.
    /// </summary>
    /// <param name="expression">The expression to compile.</param>
    /// <param name="ex">The resulting AST.</param>
    /// <param name="error">An error message if it fails to compile.</param>
    /// <returns>True if it succeeds, false with an error message if it fails.</returns>
    public static bool Compile(string expression, [NotNullWhen(true)] out Expression ex, [NotNullWhen(false)] ref string error)
    {
        var buffer = expression.AsSpan();
        return Expression.Compile(buffer, 0, buffer.Length, out ex, ref error) && Expression.Optimize(ref ex, ref error);
    }

    /// <summary>
    /// Compiles the expression and returns the AST.
    /// </summary>
    /// <param name="expression">The expression to compile.</param>
    /// <param name="ex">The resulting AST.</param>
    /// <param name="error">An error message if it fails to compile.</param>
    /// <returns>True if it succeeds, false with an error message if it fails.</returns>
    public static bool Compile(ReadOnlySpan<char> expression, [NotNullWhen(true)] out Expression ex, [NotNullWhen(false)] ref string error)
    {
        return Expression.Compile(expression, 0, expression.Length, out ex, ref error) && Expression.Optimize(ref ex, ref error);
    }
}
