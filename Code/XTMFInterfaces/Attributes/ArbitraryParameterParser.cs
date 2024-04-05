/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace XTMF
{
    public static class ArbitraryParameterParser
    {
        private static ConcurrentDictionary<Type, KeyValuePair<int, MethodInfo>> ParserLookup =
            new();

        /// <summary>
        /// Parse the input for the given type
        /// </summary>
        /// <param name="t">The type to try to parse it with</param>
        /// <param name="input">The string to parse</param>
        /// <param name="error">An error returned if we are unable to parse the string</param>
        /// <returns>Null if we are unable to parse it, otherwise an object of the given type</returns>
        public static object? ArbitraryParameterParse(Type t, string input, ref string? error)
        {
            // strings are always just themselves
            if (t == null)
            {
                error = "We are not able to parse the null type!";
                return null;
            }
            else if (t == typeof(string))
            {
                return input;
            }
            else if (t == typeof(float))
            {
                return ParseFloat(input, ref error);
            }
            else if (t == typeof(double))
            {
                return ParseDouble(input, ref error);
            }
            else if (t.IsEnum)
            {
                if (!Enum.IsDefined(t, input))
                {
                    error = "'" + input + "' is not a valid input!";
                    return null;
                }
                return Enum.Parse(t, input);
            }
            else
            {
                if (!ParserLookup.TryGetValue(t, out KeyValuePair<int, MethodInfo> info))
                {
                    // If we are not a string to try find a try parse with an error first
                    string typeParse = "TryParse";
                    var errorTryParse = t.GetMethod(typeParse, new[] { typeof(string).MakeByRefType(), typeof(string), t.MakeByRefType() });
                    if (errorTryParse != null && errorTryParse.IsStatic)
                    {
                        ParserLookup.TryAdd(t, new KeyValuePair<int, MethodInfo>(3, errorTryParse));
                        return ErrorTryParse(input, ref error, errorTryParse);
                    }
                    // if there is no error try parse, just try the TryParse
                    var regularTryParse = t.GetMethod(typeParse, new[] { typeof(string), t.MakeByRefType() });
                    if (regularTryParse != null && regularTryParse.IsStatic)
                    {
                        ParserLookup.TryAdd(t, new KeyValuePair<int, MethodInfo>(2, regularTryParse));
                        return TryParse(input, ref error, regularTryParse);
                    }
                    // If there is no TryParse at all, fall back to the regular Parse method
                    var regularParse = t.GetMethod("Parse", new[] { typeof(string) });
                    if (regularParse != null && regularParse.IsStatic)
                    {
                        ParserLookup.TryAdd(t, new KeyValuePair<int, MethodInfo>(1, regularParse));
                        return RegularParse(input, ref error, regularParse);
                    }
                    // If it doesn't have any parse method we need to return null and let them know that this type can not have a parameter
                    error = "Unable to find a static method to parse type " + t.FullName;
                    return null;
                }
                else
                {
                    switch (info.Key)
                    {
                        case 1:
                            return RegularParse(input, ref error, info.Value);

                        case 2:
                            return TryParse(input, ref error, info.Value);

                        case 3:
                            return ErrorTryParse(input, ref error, info.Value);
                        // if we get here there is a new type of parse that we are not handling
                        default:
                            return null;
                    }
                }
            }
        }

        private static readonly NumberFormatInfo _numberFormat = CultureInfo.InvariantCulture.NumberFormat;

        private static object? ParseFloat(string input, ref string? error)
        {
            float ret;
            if(!float.TryParse(input, out ret))
            {
                if(!float.TryParse(input, NumberStyles.Any, _numberFormat, out ret))
                {
                    error = $"Unable to parse '{input}' as a floating point number!";
                    return null;
                }
            }
            return ret;
        }

        private static object? ParseDouble(string input, ref string? error)
        {
            double ret;
            if (!double.TryParse(input, out ret))
            {
                if (!double.TryParse(input, NumberStyles.Any, _numberFormat, out ret))
                {
                    error = $"Unable to parse '{input}' as a floating point number!";
                    return null;
                }
            }
            return ret;
        }

        /// <summary>
        /// Check to make sure that the value can be converted
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <param name="value">The value held as a string</param>
        /// <param name="error">Contains an error if this returns false</param>
        /// <returns>True if it is a value value, false otherwise with a reason inside of error.</returns>
        public static bool Check(Type type, string value, ref string? error)
        {
            return ArbitraryParameterParse(type, value, ref error) != null;
        }

        private static object? ErrorTryParse(string input, ref string? error, MethodInfo errorTryParse)
        {
            object? output = null;
            // ReSharper disable once ExpressionIsAlwaysNull
            var parameters = new[] { error, input, output };
            try
            {
                if ((bool?)errorTryParse.Invoke(null, parameters) == true)
                {
                    // a fail appears
                    output = parameters[2];
                }
                else
                {
                    error = parameters[0] as string;
                }
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    error = e.InnerException.Message;
                }
                else
                {
                    error = e.Message;
                }
            }
            return output;
        }

        private static object? RegularParse(string input, ref string? error, MethodInfo regularParse)
        {
            object? output = null;
            var parameters = new object[] { input };
            try
            {
                output = regularParse.Invoke(null, parameters);
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    error = e.InnerException.Message;
                }
                else
                {
                    error = e.Message;
                }
            }
            return output;
        }

        private static object? TryParse(string input, ref string? error, MethodInfo errorTryParse)
        {
            object? output = null;
            // ReSharper disable once ExpressionIsAlwaysNull
            var parameters = new[] { input, output };
            try
            {
                if ((bool?)errorTryParse.Invoke(null, parameters) == true)
                {
                    // a fail appears
                    output = parameters[1];
                }
                else
                {
                    error = "The input was in an invalid format.";
                }
            }
            catch (Exception e)
            {
                if (e.InnerException != null)
                {
                    error = e.InnerException.Message;
                }
                else
                {
                    error = e.Message;
                }
            }
            return output;
        }
    }
}