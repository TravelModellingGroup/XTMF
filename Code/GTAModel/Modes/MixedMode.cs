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
using XTMF;

namespace TMG.GTAModel.Modes;

[ModuleInformation(Description=
    @"MixedMode provides the ability to build composite modes such as Drive-Access-Subway by 
combining Auto times with transit times through an interchange zone." )]
public class MixedMode : IMode
{
    [DoNotAutomate]
    public IMode First;

    [RunParameter( "First Mode Name", "Auto", "The name of the mode to use to get to the interchange." )]
    public string FirstModeName;

    [RunParameter( "Interchange Zone", 7000, "The zone number to use as the point of interchange." )]
    public int InterchangeZoneNumber;

    [RootModule]
    public I4StepModel Root;

    [DoNotAutomate]
    public IMode Second;

    [RunParameter( "Second Mode Name", "Transit", "The name of the mode to use after the interchange." )]
    public string SecondModeName;

    private IZone InterchangeZone;

    [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
    public float CurrentlyFeasible { get; set; }

    [RunParameter( "Mode Name", "DAS 7000", "The name of this mixed mode option" )]
    public string ModeName
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    }

    public string NetworkType
    {
        get { return null; }
    }

    public bool NonPersonalVehicle
    {
        get { return First.NonPersonalVehicle | Second.NonPersonalVehicle; }
    }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public float CalculateV(IZone origin, IZone destination, Time time)
    {
        CheckInterChangeZone();
        return First.CalculateV( origin, InterchangeZone, time ) + Second.CalculateV( InterchangeZone, destination, time );
    }

    public float Cost(IZone origin, IZone destination, Time time)
    {
        CheckInterChangeZone();
        return First.Cost( origin, InterchangeZone, time ) + Second.Cost( origin, InterchangeZone, time );
    }

    public bool Feasible(IZone origin, IZone destination, Time time)
    {
        CheckInterChangeZone();
        return First.Feasible( origin, InterchangeZone, time ) && Second.Feasible( InterchangeZone, destination, time );
    }

    public bool RuntimeValidation(ref string error)
    {
        if ( !AttachMode( FirstModeName, ref First, ref error )
            || !AttachMode( SecondModeName, ref Second, ref error ) )
        {
            return false;
        }
        // we can not check the interchange zone here because it may have not been loaded yet
        return true;
    }

    public Time TravelTime(IZone origin, IZone destination, Time time)
    {
        CheckInterChangeZone();
        return First.TravelTime( origin, InterchangeZone, time ) + Second.TravelTime( InterchangeZone, destination, time );
    }

    private bool AttachMode(string modeName, ref IMode mode, ref string error)
    {
        foreach ( var m in Root.Modes )
        {
            if ( AttachMode( modeName, ref mode, m ) )
            {
                return true;
            }
        }
        error = "We were unable to find a mode with the name " + modeName + " for the mixed mode '" + ModeName + "!";
        return false;
    }

    private bool AttachMode(string modeName, ref IMode mode, IModeChoiceNode current)
    {
        // ReSharper disable once SuspiciousTypeConversion.Global
        if (mode is IModeCategory cat)
        {
            foreach (var m in Root.Modes)
            {
                if (AttachMode(modeName, ref mode, m))
                {
                    return true;
                }
            }
        }
        else
        {
            if (modeName == current.ModeName)
            {
                return current is IMode;
            }
        }
        return false;
    }

    private void CheckInterChangeZone()
    {
        if ( InterchangeZone == null )
        {
            var zone = Root.ZoneSystem.ZoneArray[InterchangeZoneNumber];
            InterchangeZone = zone ?? throw new XTMFRuntimeException(this, "The zone " + InterchangeZoneNumber + " does not exist!  Please check the mode '" + ModeName + "!" );
        }
    }
}