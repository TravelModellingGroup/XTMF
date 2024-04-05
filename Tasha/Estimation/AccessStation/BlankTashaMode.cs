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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Estimation.AccessStation;

[ModuleInformation(Description = "Do not use this module as a real mode, this is used for testing purposes only.")]
public class BlankTashaMode : ITashaMode
{
    public float CurrentlyFeasible
    {
        get; set;
    }

    [RunParameter("Mode Name", "Blank", "The name for this blank mode.")]
    public string ModeName { get; set; }


    public string Name { get; set; }

    public string NetworkType
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    public bool NonPersonalVehicle
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    public float Progress
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get
        {
            throw new NotImplementedException();
        }
    }

    public IVehicleType RequiresVehicle { get; set; }

    public double VarianceScale
    {
        get
        {
            throw new NotImplementedException();
        }

        set
        {
            throw new NotImplementedException();
        }
    }

    public double CalculateV(ITrip trip)
    {
        throw new NotImplementedException();
    }

    public float CalculateV(IZone origin, IZone destination, Time time)
    {
        throw new NotImplementedException();
    }

    public float Cost(IZone origin, IZone destination, Time time)
    {
        throw new NotImplementedException();
    }

    public bool Feasible(ITripChain tripChain)
    {
        throw new NotImplementedException();
    }

    public bool Feasible(ITrip trip)
    {
        throw new NotImplementedException();
    }

    public bool Feasible(IZone origin, IZone destination, Time time)
    {
        throw new NotImplementedException();
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public Time TravelTime(IZone origin, IZone destination, Time time)
    {
        throw new NotImplementedException();
    }
}
