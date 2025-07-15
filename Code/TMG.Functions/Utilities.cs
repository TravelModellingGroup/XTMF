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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
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
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveInlining)]
    public static void Write(StreamWriter writer, float data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, provider: CultureInfo.InvariantCulture))
        {
            writer.Write(buffer[0..charactersWritten]);
        }
        else
        {
            writer.Write(data.ToString());
        }
    }

    /// <summary>
    /// Writes a float to a stream writer without doing an allocation using
    /// the invariant culture where possible.
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="data">The floating point data to write.</param>
    /// <param name="buffer">The temporary buffer to use. Should be 32 characters long.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLine(StreamWriter writer, float data, Span<char> buffer)
    {
        if (data.TryFormat(buffer, out int charactersWritten, provider: CultureInfo.InvariantCulture))
        {
            writer.WriteLine(buffer[0..charactersWritten]);
        }
        else
        {
            writer.WriteLine(data.ToString());
        }
    }

}
