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
using System.Collections.Generic;
using TMG;
using XTMF;

namespace Tasha.Common;

public interface ITashaRuntime : ITravelDemandModel, IResourceSource
{
    [DoNotAutomate]
    List<ITashaMode> AllModes { get; }

    [SubModelInformation( Description = "The Auto mode to use for Tasha", Required = true )]
    ITashaMode AutoMode { get; set; }

    [SubModelInformation( Description = "The type of vehicle used for auto trips", Required = true )]
    IVehicleType AutoType { get; set; }

    Time EndOfDay { get; set; }

    [SubModelInformation( Description = "The model that will load our household", Required = true )]
    IDataLoader<ITashaHousehold> HouseholdLoader { get; set; }

    int TotalIterations { get; set; }

    [SubModelInformation( Description = "The ModeChoice Module", Required = false )]
    ITashaModeChoice ModeChoice { get; set; }

    List<ITashaMode> NonSharedModes { get; set; }

    [SubModelInformation( Description = "A collection of modes other than shared modes and auto", Required = false )]
    List<ITashaMode> OtherModes { get; set; }

    bool Parallel { get; set; }

    [SubModelInformation( Description = "A collection of modules to run after the household has finished", Required = false )]
    List<IPostHousehold> PostHousehold { get; set; }

    [SubModelInformation( Description = "A collection of modules to run after an iteration is complete", Required = false )]
    List<IPostIteration> PostIteration { get; set; }

    [SubModelInformation( Description = "A Collection of models that will run before the Tasha Method.", Required = false )]
    List<ISelfContainedModule> PostRun { get; set; }

    [SubModelInformation( Description = "A collection of modules to run after the scheduler has finished", Required = false )]
    List<IPostScheduler> PostScheduler { get; set; }

    [SubModelInformation( Description = "A Collection of models that will run before the Tasha Method.", Required = false )]
    List<IPreIteration> PreIteration { get; set; }

    [SubModelInformation( Description = "A Collection of models that will run before the Tasha Method.", Required = false )]
    List<ISelfContainedModule> PreRun { get; set; }

    int RandomSeed { get; set; }

    [SubModelInformation( Description = "A collection of modes that can be shared.", Required = false )]
    List<ISharedMode> SharedModes { get; set; }

    Time StartOfDay { get; set; }

    [SubModelInformation( Description = "A collection of modes other than shared modes and auto", Required = false )]
    List<IVehicleType> VehicleTypes { get; set; }

    ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime);

    int GetIndexOfMode(ITashaMode mode);
}