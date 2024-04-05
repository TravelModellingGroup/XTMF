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
using System.Collections.Generic;
using Tasha.Common;
using Tasha.Scheduler;
using XTMF;
using TashaProject = Tasha.Scheduler.IProject;

namespace Tasha.XTMFScheduler;

public class WorkSchoolProject : TashaProject
{
    [SubModelInformation( Required = true, Description = "The project that will generate school episodes." )]
    public TashaProject School;

    [SubModelInformation( Required = true, Description = "The project that will generate work episodes." )]
    public TashaProject Work;

    public bool IsHouseholdProject
    {
        get { return false; }
    }

    public string Name { get; set; }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public bool AssignStartTime(ITashaPerson person, int personIndex, ISchedule[] schedule, IActivityEpisode episode, Random rand)
    {
        throw new NotImplementedException( "This method should never be called, either the work or school project should be invoked." );
    }

    public void Generate(ITashaHousehold household, ITashaPerson person, List<IActivityEpisode> episodes, Random rand)
    {
        if ( person.StudentStatus == StudentStatus.FullTime )
        {
            School.Generate( household, person, episodes, rand );
            Work.Generate( household, person, episodes, rand );
        }
        else
        {
            Work.Generate( household, person, episodes, rand );
            School.Generate( household, person, episodes, rand );
        }
    }

    public void IterationComplete(int currentIteration, int totalIterations)
    {
        Work.IterationComplete( currentIteration, totalIterations );
        School.IterationComplete( currentIteration, totalIterations );
    }

    public void IterationStart(int currentIteration, int totalIterations)
    {
        Work.IterationStart( currentIteration, totalIterations );
        School.IterationStart( currentIteration, totalIterations );
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}