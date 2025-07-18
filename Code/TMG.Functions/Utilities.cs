/*
    Copyright 2025 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

namespace TMG.Functions;

/// <summary>
/// This class contains utility functions that would exist across libraries.
/// </summary>
public static class Utilities
{
    /// <summary>
    /// Reads a float by trying to parse in the system language,
    /// if that fails it will then try again with the invariant culture.
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static float ParseFloat(ReadOnlySpan<char> str)
    {
        if (!float.TryParse(str, out float value))
        {
            _ = float.TryParse(str, CultureInfo.InvariantCulture, out value);
        }
        return value;
    }

    /// <summary>
    /// Reads a float by trying to parse in the system language,
    /// if that fails it will then try again with the invariant culture.
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryParse(ReadOnlySpan<char> str, out float result)
    {
        if (float.TryParse(str, out result))
        {
            return true;
        }
        return float.TryParse(str, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Reads a float by trying to parse in the system language,
    /// if that fails it will then try again with the invariant culture.
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static double ParseDouble(ReadOnlySpan<char> str)
    {
        if (!double.TryParse(str, out double value))
        {
            _ = double.TryParse(str, CultureInfo.InvariantCulture, out value);
        }
        return value;
    }

    /// <summary>
    /// Reads a float by trying to parse in the system language,
    /// if that fails it will then try again with the invariant culture.
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryParse(ReadOnlySpan<char> str, out double result)
    {
        if (double.TryParse(str, out result))
        {
            return true;
        }
        return double.TryParse(str, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Reads a float by trying to parse in the system language,
    /// if that fails it will then try again with the invariant culture.
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int ParseInt(ReadOnlySpan<char> str)
    {
        if (!int.TryParse(str, out int value))
        {
            _ = int.TryParse(str, CultureInfo.InvariantCulture, out value);
        }
        return value;
    }

    /// <summary>
    /// Reads a float by trying to parse in the system language,
    /// if that fails it will then try again with the invariant culture.
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryParse(ReadOnlySpan<char> str, out int result)
    {
        if (int.TryParse(str, out result))
        {
            return true;
        }
        return int.TryParse(str, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Reads a float by trying to parse in the system language,
    /// if that fails it will then try again with the invariant culture.
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool ParseBool(ReadOnlySpan<char> str)
    {
        // There is only the invariant culture for bool parsing in .NET at the moment.
        _ = bool.TryParse(str, out bool value);
        return value;
    }

    /// <summary>
    /// Reads a float by trying to parse in the system language,
    /// if that fails it will then try again with the invariant culture.
    /// </summary>
    /// <param name="str">The string to parse</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TryParse(ReadOnlySpan<char> str, out bool result)
    {
        // There is only the invariant culture for bool parsing in .NET at the moment.
        return bool.TryParse(str, out result);
    }

    /// <summary>
    /// Writes a float to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Write(StreamWriter writer, float data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, provider: CultureInfo.InvariantCulture))
        {
            writer.Write(buffer[0..charactersWritten]);
        }
        else
        {
            writer.Write(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a float to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Write(StreamWriter writer, float data, ReadOnlySpan<char> format, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, format, provider: CultureInfo.InvariantCulture))
        {
            writer.Write(buffer[0..charactersWritten]);
        }
        else
        {
            writer.Write(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a double to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 64 characters long to avoid needing to allocate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Write(StreamWriter writer, double data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, provider: CultureInfo.InvariantCulture))
        {
            writer.Write(buffer[0..charactersWritten]);
        }
        else
        {
            writer.Write(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a float to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Write(StreamWriter writer, double data, ReadOnlySpan<char> format, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, format, provider: CultureInfo.InvariantCulture))
        {
            writer.Write(buffer[0..charactersWritten]);
        }
        else
        {
            writer.Write(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes an integer to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Write(StreamWriter writer, int data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, provider: CultureInfo.InvariantCulture))
        {
            writer.Write(buffer[0..charactersWritten]);
        }
        else
        {
            writer.Write(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a boolean to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Write(StreamWriter writer, bool data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten))
        {
            writer.Write(buffer[0..charactersWritten]);
        }
        else
        {
            writer.Write(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a DateTime to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 128 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Write(StreamWriter writer, DateTime data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, provider: CultureInfo.InvariantCulture))
        {
            writer.Write(buffer[0..charactersWritten]);
        }
        else
        {
            writer.Write(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a TimeSpan to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 128 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void Write(StreamWriter writer, TimeSpan data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, "c"))
        {
            writer.Write(buffer[0..charactersWritten]);
        }
        else
        {
            writer.Write(data.ToString("c"));
        }
    }

    /// <summary>
    /// Writes an object to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 128 characters long to avoid needing to allocate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
    public static void Write(StreamWriter writer, object data, Span<char> buffer)
    {
        switch(data)
        {
            case float f:
                Write(writer, f, buffer);
                return;
            case double d:
                Write(writer, d, buffer);
                return;
            case int i:
                Write(writer, i, buffer);
                return;
            case bool b:
                Write(writer, b, buffer);
                return;
            case char c:
                writer.Write(c);
                return;
            case string s:
                writer.Write(s);
                return;
            case DateTime dt:
                Write(writer, dt, buffer);
                return;
            case TimeSpan ts:
                Write(writer, ts, buffer);
                return;
            default:
                writer.Write(data?.ToString() ?? string.Empty);
                return;
        }
        
    }

    /// <summary>
    /// Writes an object to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 128 characters long to avoid needing to allocate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
    public static void WriteLine(StreamWriter writer, object data, Span<char> buffer)
    {
        switch (data)
        {
            case float f:
                WriteLine(writer, f, buffer);
                return;
            case double d:
                WriteLine(writer, d, buffer);
                return;
            case int i:
                WriteLine(writer, i, buffer);
                return;
            case bool b:
                WriteLine(writer, b, buffer);
                return;
            case char c:
                writer.Write(c);
                return;
            case string s:
                writer.Write(s);
                return;
            case DateTime dt:
                WriteLine(writer, dt, buffer);
                return;
            case TimeSpan ts:
                WriteLine(writer, ts, buffer);
                return;
            default:
                writer.WriteLine(data?.ToString() ?? string.Empty);
                return;
        }

    }

    /// <summary>
    /// Writes a float to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void WriteLine(StreamWriter writer, float data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, provider: CultureInfo.InvariantCulture))
        {
            writer.WriteLine(buffer[0..charactersWritten]);
        }
        else
        {
            writer.WriteLine(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a double to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void WriteLine(StreamWriter writer, float data, ReadOnlySpan<char> format, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, format, provider: CultureInfo.InvariantCulture))
        {
            writer.WriteLine(buffer[0..charactersWritten]);
        }
        else
        {
            writer.WriteLine(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a double to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 64 characters long to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void WriteLine(StreamWriter writer, double data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, provider: CultureInfo.InvariantCulture))
        {
            writer.WriteLine(buffer[0..charactersWritten]);
        }
        else
        {
            writer.WriteLine(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a double to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="format">The format to use.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 64 characters long to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void WriteLine(StreamWriter writer, double data, ReadOnlySpan<char> format, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, format, provider: CultureInfo.InvariantCulture))
        {
            writer.WriteLine(buffer[0..charactersWritten]);
        }
        else
        {
            writer.WriteLine(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes an integer to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void WriteLine(StreamWriter writer, int data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, provider: CultureInfo.InvariantCulture))
        {
            writer.WriteLine(buffer[0..charactersWritten]);
        }
        else
        {
            writer.WriteLine(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a boolean to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void WriteLine(StreamWriter writer, bool data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten))
        {
            writer.WriteLine(buffer[0..charactersWritten]);
        }
        else
        {
            writer.WriteLine(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a DateTime to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 128 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void WriteLine(StreamWriter writer, DateTime data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, provider: CultureInfo.InvariantCulture))
        {
            writer.WriteLine(buffer[0..charactersWritten]);
        }
        else
        {
            writer.WriteLine(data.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Writes a TimeSpan to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 128 characters long in order to avoid allocation.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void WriteLine(StreamWriter writer, TimeSpan data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, "c"))
        {
            writer.WriteLine(buffer[0..charactersWritten]);
        }
        else
        {
            writer.WriteLine(data.ToString("c"));
        }
    }
}
