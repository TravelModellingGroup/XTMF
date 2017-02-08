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

using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
// ReSharper disable ArrangeRedundantParentheses
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable PossibleLossOfFraction

namespace XTMF.Testing
{
    [TestClass]
    public class TestTime
    {
        [TestMethod]
        public void TestAdding()
        {
            Time a, b;
            if (!Time.TryParse("10:00", out a))
            {
                Assert.Fail("We were unable to parse \"10:00\"");
            }
            if (!Time.TryParse("30s", out b))
            {
                Assert.Fail("We were unable to parse \"30s\"");
            }
            CheckTime(b, 0, 0, 30);
            CheckTime(a + b, 10, 0, 30);
        }

        [TestMethod]
        public void TestDivision()
        {
            Time a, b;
            if (!Time.TryParse("10:00", out a))
            {
                Assert.Fail("We were unable to parse \"10:00\"");
            }
            if (!Time.TryParse("30s", out b))
            {
                Assert.Fail("We were unable to parse \"30s\"");
            }
            CheckTime(b, 0, 0, 30);
            if ((a / b) != a.ToMinutes() / b.ToMinutes())
            {
                Assert.Fail("Division returned the wrong result!");
            }
        }

        [TestMethod]
        public void TestFloatingConstructorMinus()
        {
            var times = 1000;
            Time baseTime = new Time() { Hours = times / 2 };
            for (int i = times; i >= 0; i--)
            {
                Time test = new Time((i / 2) + ((i % 2) == 0 ? 0 : 0.30f));
                if (test != baseTime)
                {
                    Assert.Fail("Base Time:'" + baseTime + "' is not the same as '" + test + "'");
                }
                baseTime -= Time.FromMinutes(30);
            }
        }

        [TestMethod]
        public void TestFloatingConstructorPlus()
        {
            Time baseTime = new Time();
            for (int i = 0; i < 1000; i++)
            {
                Time test = new Time(i / 2 + ((i % 2) == 0 ? 0 : 0.30f));
                if (test != baseTime)
                {
                    Assert.Fail("Base Time:'" + baseTime + "' is not the same as '" + test + "'");
                }
                baseTime += Time.FromMinutes(30);
            }
        }

        [TestMethod]
        public void TestIntersection()
        {
            Time start1 = new Time();
            Time end1 = new Time() { Hours = 3 };
            Time start2 = new Time() { Hours = 1 };
            Time end2 = new Time() { Hours = 2 };
            Time intersection;
            if (!Time.Intersection(start1, end1, start2, end2, out intersection))
            {
                Assert.Fail("Intersection failed in a case where there was an intersection!");
            }
            if (intersection.ToMinutes() != 60f)
            {
                Assert.Fail("We were expecting to have an intersection of 1 hour, instead of had " + intersection);
            }
        }

        [TestMethod]
        public void TestIntersectionWindow()
        {
            TestIntersection(new Time() { Hours = 4 }, new Time() { Hours = 10 }, new Time() { Hours = 5 }, new Time() { Hours = 6 });
        }

        [TestMethod]
        public void TestNegative()
        {
            Time a, b;
            if (!Time.TryParse("10:00", out a))
            {
                Assert.Fail("We were unable to parse \"10:00\"");
            }
            if (!Time.TryParse("30s", out b))
            {
                Assert.Fail("We were unable to parse \"30s\"");
            }
            CheckTime(-a, -10, 0, 0);
            CheckTime(-b, 0, 0, -30);
            if ((-a) >= Time.Zero)
            {
                Assert.Fail("Negatives are greater than 0!");
            }
        }

        [TestMethod]
        public void TestNegativeAdd()
        {
            Time a, b;
            if (!Time.TryParse("10:00", out a))
            {
                Assert.Fail("We were unable to parse \"10:00\"");
            }
            if (!Time.TryParse("30s", out b))
            {
                Assert.Fail("We were unable to parse \"30s\"");
            }
            CheckTime(-a, -10, 0, 0);
            CheckTime(-b, 0, 0, -30);
            CheckTime((-a + -b), -10, 0, -30);
            CheckTime(a + -b, 9, 59, 30);
            CheckTime((a + -b) + (-a + -b), 0, -1, 0);
        }

        [TestMethod]
        public void TestParseByWords()
        {
            Time a = new Time("15 minutes");
            if (a.Minutes != 15)
            {
                Assert.Fail("We were expecting 15 minutes, instead we found " + a);
            }
        }

        [TestMethod]
        public void TestSubtraction()
        {
            Time a, b;
            if (!Time.TryParse("10:00", out a))
            {
                Assert.Fail("We were unable to parse \"10:00\"");
            }
            if (!Time.TryParse("30s", out b))
            {
                Assert.Fail("We were unable to parse \"30s\"");
            }
            CheckTime(b, 0, 0, 30);
            CheckTime(a - b, 9, 59, 30);
            CheckTime(a - b - b - b, 9, 58, 30);
        }

        [TestMethod]
        public void TestSubtraction2()
        {
            Time a, b;
            if (!Time.TryParse("10:00 PM", out a))
            {
                Assert.Fail("We were unable to parse \"10:00\"");
            }
            if (!Time.TryParse("9:00 PM", out b))
            {
                Assert.Fail("We were unable to parse \"30s\"");
            }
            CheckTime(b, 21, 0, 0);
            CheckTime(a - b, 1, 0, 0);
        }

        [TestMethod]
        public void TestTashaTimeFails()
        {
            Time a;
            if (!Time.TryParse("8:45 AM", out a))
            {
                Assert.Fail("We were unable to parse \"8:45 AM\"");
            }
            CheckTime(a, 8, 45, 0);
            if (!Time.TryParse("12:00 PM", out a))
            {
                Assert.Fail("We were unable to parse \"12:00 PM\"");
            }
            CheckTime(a, 12, 0, 0);
            if (!Time.TryParse("12:00 AM", out a))
            {
                Assert.Fail("We were unable to parse \"12:00 PM\"");
            }
            CheckTime(a, 0, 0, 0);
        }

        [TestMethod]
        public void TestToFloat()
        {
            Time a;
            if (!Time.TryParse("10:30:30", out a))
            {
                Assert.Fail("We were unable to parse \"10:30:30\"");
            }
            CheckTime(a, 10, 30, 30);
            if (a.ToFloat() != 10.3030f)
            {
                Assert.Fail("We received the wrong ToFloat from 10:30:30");
            }
        }

        [TestMethod]
        public void TestToMinutes()
        {
            Time a;
            if (!Time.TryParse("10:30:30", out a))
            {
                Assert.Fail("We were unable to parse \"10:30:30\"");
            }
            CheckTime(a, 10, 30, 30);
            if (a.ToMinutes() != (10 * 60 + 30 + 0.5f))
            {
                Assert.Fail("We received the wrong ToMinutes from 10:30:30");
            }
        }

        [TestMethod]
        public void TestToString()
        {
            Time a = new Time() { Hours = 23, Minutes = 22, Seconds = 21 };
            if (a.ToString() != "23:22:21")
            {
                Assert.Fail("The ToString() of '23:22:21' was given as '" + a + "' instead!");
            }
            a.Seconds = 0;
            if (a.ToString() != "23:22")
            {
                Assert.Fail("The ToString() of '23:22' was given as '" + a + "' instead!");
            }
        }

        [TestMethod]
        public void TestTryParse()
        {
            Time t;
            if (!Time.TryParse("10:00", out t))
            {
                Assert.Fail("We were unable to parse \"10:00\"");
            }
            CheckTime(t, 10, 0, 0);
            if (!Time.TryParse("10:00 AM", out t))
            {
                Assert.Fail("We were unable to parse \"10:00 AM\"");
            }
            CheckTime(t, 10, 0, 0);
            if (!Time.TryParse("10:00 PM", out t))
            {
                Assert.Fail("We were unable to parse \"10:00 PM\"");
            }
            CheckTime(t, 22, 0, 0);
            if (!Time.TryParse("10:00AM", out t))
            {
                Assert.Fail("We were unable to parse \"10:00AM\"");
            }
            CheckTime(t, 10, 0, 0);
            if (!Time.TryParse("10:00am", out t))
            {
                Assert.Fail("We were unable to parse \"10:00am\"");
            }
            CheckTime(t, 10, 0, 0);
            if (!Time.TryParse("10:00pm", out t))
            {
                Assert.Fail("We were unable to parse \"10:00pm\"");
            }
            CheckTime(t, 22, 0, 0);
            if (!Time.TryParse("10:00:00", out t))
            {
                Assert.Fail("We were unable to parse \"10:00:00\"");
            }
            CheckTime(t, 10, 0, 0);
            if (!Time.TryParse("10:00:30", out t))
            {
                Assert.Fail("We were unable to parse \"10:00:30\"");
            }
            CheckTime(t, 10, 0, 30);
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private void CheckTime(Time time, int hours, int minutes, int seconds)
        {
            if ((time.Hours != hours) | (time.Minutes != minutes) | (time.Seconds != seconds))
            {
                Assert.Fail($"{time} is not equal to {hours}:{minutes}:{seconds}!");
            }
        }

        private void TestIntersection(Time start1, Time end1, Time start2, Time end2)
        {
            Time intersectionStart, intersectionEnd;
            if (!Time.Intersection(start1, end1, start2, end2, out intersectionStart, out intersectionEnd))
            {
                Assert.Fail("Intersection failed in a case where there was an intersection!");
            }
        }
    }
}