using System;
using System.Globalization;

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
    public static bool ParseBool(ReadOnlySpan<char> str)
    {
        // There is only the invariant culture for bool parsing in .NET at the moment.
        _ = bool.TryParse(str, out bool value);
        return value;
    }

}
