/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

// Ignore Spelling: Microsim

using System;
using System.Threading.Tasks;
using Tasha.Common;
using XTMF;

namespace Tasha.Validation.Report;

[ModuleInformation(Description = "This module will produce a calibration report of the current model system.")]
public sealed class CreateValidationReport : ISelfContainedModule
{

    [SubModelInformation(Required = true, Description = "The data-set that we are going to compare against.")]
    public IDataLoader<ITashaHousehold> SurveyHouseholdsWithTrips;

    [SubModelInformation(Required = true, Description = "Provides Microsim Data for analysis.")]
    public MicrosimData MicrosimData;

    [SubModelInformation(Required = true, Description = "The time periods used for analysis.")]
    public TimePeriod[] TimePeriods;

    [SubModelInformation(Required = true, Description = "The different analyses to execute.")]
    public Analysis[] Analyses;

    [RunParameter("Observed Mode Attachment Name", "ObservedMode", "The name of the attachment for the observed mode.")]
    public string ObservedModeAttachment;

    public void Start()
    {
        try
        {
            var surveyData = LoadSurveyData();
            MicrosimData.Load();

            Parallel.ForEach(Analyses, analysis =>
            {
                analysis.Execute(TimePeriods, MicrosimData, surveyData);
            });
        }
        catch (XTMFRuntimeException)
        {
            // Do nothing
            throw;
        }
        catch(Exception e)
        {
            throw new XTMFRuntimeException(this, e);
        }
    }

    private ITashaHousehold[] LoadSurveyData()
    {
        SurveyHouseholdsWithTrips.LoadData();
        var ret = SurveyHouseholdsWithTrips.ToArray();
        // Setup the Mode to be the observed mode
        Parallel.ForEach(ret, household =>
        {
            foreach (var person in household.Persons)
            {
                foreach(var tripChain in person.TripChains)
                {
                    foreach(var trip in tripChain.Trips)
                    {
                        trip.Mode = trip[ObservedModeAttachment] as ITashaMode;
                    }
                }
            }
        });
        return ret;
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
