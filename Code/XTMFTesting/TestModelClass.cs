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

namespace XTMF.Testing;

public class TestModelClass : IModule
{
    public const string NumberOfZonesName = "NumberOfZones";
    public const string PropertyName = "TestProperty";
    public const string StringName = "TestString";

    [Parameter( NumberOfZonesName, 0, "The number of zones in the model" )]
    public int NumberOfZones;

    [Parameter( StringName, "Success", "Used for testing that this is in fact working." )]
    public string OurInputString;

    private Tuple<byte, byte, byte>[] Colours = new[]
    {
        new Tuple<byte,byte,byte>(200, 150, 150),
        new Tuple<byte,byte,byte>(150, 200, 150),
        new Tuple<byte,byte,byte>(200, 150, 200),
        new Tuple<byte,byte,byte>(200, 0, 0)
    };

    private int ColourToSend;

    public TestModelClass()
    {
        Name = "TestModel";
    }

    public string Name { get; set; }

    public float Progress { get; } = 0f;

    public Tuple<byte, byte, byte> ProgressColour
    {
        get
        {
            int toSend = ColourToSend;
            ColourToSend = ( ColourToSend + 1 ) % Colours.Length;
            return Colours[toSend];
        }
    }

    [Parameter( PropertyName, "Serious Data", "This data is very serious and should never be questioned!" )]
    public string Property { get; set; }

    /// <summary>
    /// This is called before the start method as a way to pre-check that all of the parameters that are selected
    /// are in fact valid for this module.
    /// </summary>
    /// <param name="error">A string that should be assigned a detailed error</param>
    /// <returns>If the validation was successful or if there was a problem</returns>
    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}