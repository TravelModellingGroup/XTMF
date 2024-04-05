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
using System.Linq;
using TMG.Input;
using XTMF;
using Datastructure;
namespace TMG.Estimation.AI;

[ModuleInformation( Description =
    @"This module is designed to go through a TMG Result file and process all of parameters and execute them on remote clients.  This 
can be useful for post processing the results of an estimation to gather more detailed information that you wouldn't want to do for every
parameter set.  This is best combined by using ExecuteGivenParameters in order to select the best results before running." )]
public class ExecuteGivenParameters : IEstimationAI
{
    [RootModule]
    public IEstimationHost Root;

    [SubModelInformation( Required = true, Description = "The location of the result file to read in." )]
    public FileLocation ResultFile;

    [RunParameter("Rows", 0, "The number of rows to run, 0 means all.")]
    public int NumberOfRows;

    public List<Job> CreateJobsForIteration()
    {
        var ret = new List<Job>();
        var parameters = Root.Parameters.ToArray();
        int totalParameters = parameters.Sum(p => p.Names.Length);
        using ( CsvReader reader = new( ResultFile.GetFilePath() ) )
        {
            int[] headerToParameterIndex = ProcessHeader(parameters, reader, reader.LoadLine());
            while (reader.LoadLine(out int columns))
            {
                //+2 for generation and value
                if (columns >= totalParameters + 2)
                {
                    var jobParameters = new ParameterSetting[parameters.Length];
                    var job = new Job()
                    {
                        ProcessedBy = null,
                        Processing = false,
                        Processed = false,
                        Value = float.NaN,
                        Parameters = jobParameters
                    };
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        jobParameters[i] = new ParameterSetting()
                        {
                            Names = parameters[i].Names,
                            Minimum = parameters[i].Minimum,
                            Maximum = parameters[i].Maximum
                        };
                    }
                    for (int i = 0; i < headerToParameterIndex.Length; i++)
                    {
                        if(headerToParameterIndex[i] >= 0)
                        {
                            reader.Get(out jobParameters[headerToParameterIndex[i]].Current, i + 2);
                        }
                    }
                    ret.Add(job);
                    if (NumberOfRows > 0 & ret.Count >= NumberOfRows)
                    {
                        break;
                    }
                }
            }
        }
        return ret;
    }

    private int[] ProcessHeader(ParameterSetting[] parameters, CsvReader reader, int numberOfColumns)
    {
        if(numberOfColumns <= 2)
        {
            throw new XTMFRuntimeException(this, "There are no parameters found in the header of the parameter file!");
        }
        var ret = new int[numberOfColumns - 2];
        for (int i = 0; i < ret.Length; i++)
        {
            ret[i] = -1;
        }
        for (int i = 2; i < numberOfColumns; i++)
        {
            reader.Get(out string header, i);
            for (int j = 0; j < parameters.Length; j++)
            {
                if(parameters[j].Names.Contains(header))
                {
                    ret[i - 2] = j;
                    break;
                }
            }
        }
        return ret;
    }

    public void IterationComplete()
    {

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

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
