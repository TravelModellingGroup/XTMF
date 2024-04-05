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
using System.Runtime.CompilerServices;

namespace XTMF;

/// <summary>
/// Simple Time Struct for holding simple time data.
/// </summary>
public struct Time : IComparable<Time>
{
    public static Time EndOfDay = new() { Hours = 28 };

    public static Time OneQuantum;

    public static Time StartOfDay = new() { Hours = 4 };

    /// <summary>
    /// Our internal representation, to the millisecond
    /// </summary>
    private long InternalTime;

    /// <summary>
    /// Converts the given float time in format HH.MM to
    /// this class representation
    /// </summary>
    /// <param name="time"></param>
    public Time(float time)
    {
        var hours = (long)time;
        var minutes = (long)(Math.Round((time - (long)time) * 100));
        InternalTime = (hours * 3600000L + minutes * 60000L);
    }

    public Time(DateTime time)
    {
        InternalTime = ((60 * (60 * time.Hour) + time.Minute) + time.Second) * 1000 + time.Millisecond;
    }

    /// <summary>
    /// Creates a TashaTime given the string representation
    /// Example 4:25:00 4 hours 25 minutes and 0 seconds
    /// </summary>
    /// <param name="time"></param>
    public Time(string time)
    {
        if (!TryParse(time, out this))
        {
            throw new XTMFRuntimeException(null, "Unable to create a XTMF.Time from " + time);
        }
    }

    public static Time OneHour { get; } = new Time() { Hours = 1 };

    public static Time Zero { get; } = new Time();

    /// <summary>
    /// The number of Hours this Time Object represents
    /// </summary>
    public int Hours
    {
        get
        {
            return (int)(InternalTime / 3600000L);
        }

        set
        {
            InternalTime = (InternalTime % 3600000L + (value * 3600000L));
        }
    }

    /// <summary>
    ///
    /// </summary>
    public int Minutes
    {
        get
        {
            return (int)((InternalTime / 60000L) % 60L);
        }

        set
        {
            InternalTime = InternalTime - ((InternalTime / 60000L) % 60L) + (value * 60000L);
        }
    }

    /// <summary>
    /// The number of seconds this Time Object Represents
    /// </summary>
    public int Seconds
    {
        get
        {
            return (int)((InternalTime / 1000L) % 60L);
        }

        set
        {
            var temp = InternalTime / 1000L;
            InternalTime = ((temp - temp % 60L) + value) * 1000L;
        }
    }

    public static Time FromMinutes(float result)
    {
        return new Time() { InternalTime = (long)(result * 60000.0f) };
    }

    public static implicit operator DateTime(Time t)
    {
        return new DateTime(0, 0, 0, t.Hours, t.Minutes, t.Seconds, 0);
    }

    public static implicit operator Time(DateTime t)
    {
        return new Time(t);
    }

    public static bool Intersection(Time start1, Time end1, Time start2, Time end2)
    {
        return !((end1.InternalTime < start2.InternalTime)
            | (end2.InternalTime < start1.InternalTime));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Intersection(Time start1, Time end1, Time start2, Time end2, out Time intersection)
    {
        if ((end1.InternalTime < start2.InternalTime)
            | (end2.InternalTime < start1.InternalTime))
        {
            intersection = new Time();
            return false;
        }
        // passenger is first
        if (start1.InternalTime <= start2.InternalTime)
        {
            intersection.InternalTime =
                (end1.InternalTime >= end2.InternalTime) ? end2.InternalTime - start2.InternalTime : end1.InternalTime - start2.InternalTime;
            return true;
        }
        else
        {
            // passenger is second
            intersection.InternalTime =
                (end1.InternalTime >= end2.InternalTime) ? end2.InternalTime - start1.InternalTime : end1.InternalTime - start1.InternalTime;
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Intersection(Time start1, Time end1, Time start2, Time end2, out Time intersectionStart, out Time intersectionEnd)
    {
        if (end1.InternalTime < start2.InternalTime
            || end2.InternalTime < start1.InternalTime)
        {
            intersectionStart = new Time();
            intersectionEnd = new Time();
            return false;
        }
        // passenger is first
        if (start1.InternalTime <= start2.InternalTime)
        {
            intersectionStart.InternalTime = start2.InternalTime;
            intersectionEnd.InternalTime = (end1.InternalTime >= end2.InternalTime) ? end2.InternalTime : end1.InternalTime;
            return true;
        }
        else
        {
            // passenger is second
            intersectionStart.InternalTime = start1.InternalTime;
            intersectionEnd.InternalTime = (end1.InternalTime >= end2.InternalTime) ? end2.InternalTime : end1.InternalTime;
            return true;
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <returns></returns>
    public static Time operator -(Time t1, Time t2)
    {
        return new Time() { InternalTime = t1.InternalTime - t2.InternalTime };
    }

    public static Time operator -(Time t1)
    {
        return new Time() { InternalTime = -t1.InternalTime };
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <returns></returns>
    public static bool operator !=(Time t1, Time t2)
    {
        return t1.InternalTime != t2.InternalTime;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="time"></param>
    /// <param name="percent"></param>
    /// <returns></returns>
    public static Time operator *(float percent, Time time)
    {
        return new Time() { InternalTime = (long)(Math.Round(percent * time.InternalTime)) };
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <returns></returns>
    public static float operator /(Time t1, Time t2)
    {
        if (t2 == Zero)
        {
            throw new DivideByZeroException();
        }
        return (float)t1.InternalTime / t2.InternalTime;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Time operator +(Time t1, Time t2)
    {
        return new Time() { InternalTime = t1.InternalTime + t2.InternalTime };
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Time t1, Time t2)
    {
        return t1.InternalTime < t2.InternalTime;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Time t1, Time t2)
    {
        return t1.InternalTime <= t2.InternalTime;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Time t1, Time t2)
    {
        return t1.InternalTime == t2.InternalTime;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Time t1, Time t2)
    {
        return t1.InternalTime > t2.InternalTime;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="t1"></param>
    /// <param name="t2"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Time t1, Time t2)
    {
        return t1.InternalTime >= t2.InternalTime;
    }

    public static bool TryParse(string timeString, out Time time)
    {
        string? error = null;
        return TryParse(ref error, timeString, out time);
    }

    public static bool TryParse(ref string? error, string timeString, out Time time)
    {
        time = new Time();
        if (String.IsNullOrWhiteSpace(timeString))
        {
            return false;
        }
        int seconds = 0, minutes = 0, hours = 0;
        int state = 0;
        int currentTime = 0;
        int currentNumber = 0;
        for (int i = 0; i < timeString.Length; ++i)
        {
            char c = timeString[i];
            if (Char.IsWhiteSpace(c))
            {
                continue;
            }
            switch (state)
            {
                // Initial State
                case 0:
                    {
                        if ((c >= '0') & (c <= '9'))
                        {
                            currentNumber *= 10;
                            currentNumber += (c - '0');
                            state = 1;
                            continue;
                        }
                        else
                        {
                            error = "Expected a number but found '" + c + "' instead!";
                        }
                    }
                    return false;
                // Collect number
                case 1:
                    {
                        if ((c >= '0') & (c <= '9'))
                        {
                            currentNumber *= 10;
                            currentNumber += (c - '0');
                        }
                        else if (c == ':')
                        {
                            switch (currentTime)
                            {
                                case 0:
                                    hours = currentNumber;
                                    break;

                                case 1:
                                    minutes = currentNumber;
                                    break;

                                case 2:
                                    seconds = currentNumber;
                                    break;

                                default:
                                    error = "Invalid Time level!";
                                    return false;
                            }
                            currentNumber = 0;
                            currentTime++;
                            state = 0;
                        }
                        else if ((c == 'h') | (c == 'H'))
                        {
                            if (currentTime <= 0)
                            {
                                hours = currentNumber;
                                currentTime = 1;
                                currentNumber = 0;
                                state = 2;
                            }
                            else
                            {
                                error = "Invalid place to enter hours!";
                                return false;
                            }
                        }
                        else if ((c == 'm') | (c == 'M'))
                        {
                            if (currentTime <= 1)
                            {
                                minutes = currentNumber;
                                currentTime = 2;
                                currentNumber = 0;
                                state = 2;
                            }
                            else
                            {
                                error = "Invalid place to enter minutes!";
                                return false;
                            }
                        }
                        else if ((c == 's') | (c == 'S'))
                        {
                            if (currentTime <= 2)
                            {
                                seconds = currentNumber;
                                currentTime = 3;
                                currentNumber = 0;
                                state = 2;
                            }
                            else
                            {
                                error = "Invalid place to enter seconds!";
                                return false;
                            }
                        }
                        else if ((c == 'a') | (c == 'A'))
                        {
                            if (hours == 12)
                            {
                                hours -= 12;
                            }
                            state = 4;
                        }
                        else if ((c == 'p') | (c == 'P'))
                        {
                            if (hours != 12)
                            {
                                hours += 12;
                            }
                            state = 4;
                        }
                        else
                        {
                            error = "Unexpected symbol '" + c + "'";
                            return false;
                        }
                    }
                    break;

                case 2:
                    {
                        if (!Char.IsLetter(c))
                        {
                            if ((c >= '0') & (c <= '9'))
                            {
                                currentNumber = (c - '0');
                                state = 1;
                            }
                        }
                    }
                    break;
                // We received an A or a P, we need to find an M next or fail
                case 4:
                    {
                        if ((c == 'm' | c == 'M'))
                        {
                            switch (currentTime)
                            {
                                case 0:
                                    hours = currentNumber;
                                    break;

                                case 1:
                                    minutes = currentNumber;
                                    break;

                                case 2:
                                    seconds = currentNumber;
                                    break;

                                case 3:
                                    // do nothing in this case, we have had all of the data already entered
                                    if (currentNumber != 0)
                                    {
                                        error = "Too many time entries have been found!";
                                        return false;
                                    }
                                    break;

                                default:
                                    error = "Unexpected time state found!";
                                    return false;
                            }
                            state = 0;
                        }
                        else
                        {
                            error = "We were expecting a 'm' but found '" + c + "' instead!";
                            return false;
                        }
                    }
                    break;

                default:
                    return false;
            }
        }
        if (state == 1)
        {
            switch (currentTime)
            {
                case 0:
                    hours = currentNumber;
                    break;

                case 1:
                    minutes = currentNumber;
                    break;

                case 2:
                    seconds = currentNumber;
                    break;

                case 3:
                    // do nothing in this case, we have had all of the data already entered
                    if (currentNumber != 0)
                    {
                        error = "Too many time entries have been found!";
                        return false;
                    }
                    break;

                default:
                    error = "Unexpected time state found!";
                    return false;
            }
        }
        time.InternalTime = (long)(hours * 3600 + minutes * 60 + seconds) * 1000;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Time other)
    {
        return InternalTime < other.InternalTime ? -1 : (InternalTime == other.InternalTime ? 0 : 1);
    }

    public override bool Equals(object? obj)
    {
        if (obj is Time other)
        {
            return InternalTime == other.InternalTime;
        }
        else
        {
            return base.Equals(obj);
        }
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public float ToFloat()
    {
        return Hours + (Minutes * 0.01f) + (Seconds * 0.0001f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float ToMinutes()
    {
        return InternalTime * 1.6666666666666666666666666666667e-5f;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    override public string ToString()
    {
        if ((InternalTime / 1000) % 60 == 0)
        {
            return String.Format("{0}:{1:00}", Hours, Minutes);
        }
        else
        {
            return String.Format("{0}:{1:00}:{2:00}", Hours, Minutes, Seconds);
        }
    }
}