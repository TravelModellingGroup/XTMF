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
using System.IO;
using XTMF;
using Datastructure;
using TMG.Input;
namespace TMG.Estimation.Utilities;

[ModuleInformation(Description =
    "This module is designed to go through TMG.Estimation result files and select for the best results creating a new result file.")]
public class GetBestParameters : ISelfContainedModule
{
    [SubModelInformation(Required = true, Description = "The location of the result file to load in.")]
    public FileLocation InputResultFile;

    [SubModelInformation(Required = true, Description = "The location of the result file to save to.")]
    public FileLocation OutputResultFile;

    [RunParameter("Results to save", 10, "The maximum number of results to save.")]
    public int ResultsToSave;

    [RunParameter("Maximize", true, "Should this try to maximize or minimize the values?")]
    public bool Maximize;

    private class GenerationJob
    {
        internal Job Job;
        internal int Generation;

        public GenerationJob(Job job, int generation)
        {
            Job = job;
            Generation = generation;
        }
    }

    public void Start()
    {
        GenerationJob[] best;
        using (CsvReader reader = new( InputResultFile.GetFilePath() ))
        {
            best = GetBest( reader );
        }
        OutputBest( best );
    }

    private void OutputBest(GenerationJob[] best)
    {
        if ( best.Length == 0 ) return;
        using var writer = new StreamWriter(OutputResultFile.GetFilePath());
        writer.Write("Generation,Value");
        //write header
        foreach (var parameter in best[0].Job.Parameters)
        {
            foreach (var name in parameter.Names)
            {
                writer.Write(',');
                writer.Write(name);
            }
        }
        writer.WriteLine();
        for (int i = 0; i < best.Length; i++)
        {
            writer.Write(best[i].Generation);
            writer.Write(',');
            writer.Write(best[i].Job.Value);
            for (int j = 0; j < best[i].Job.Parameters.Length; j++)
            {
                for (int k = 0; k < best[i].Job.Parameters[j].Names.Length; k++)
                {
                    writer.Write(',');
                    writer.Write(best[i].Job.Parameters[j].Current);
                }
            }
            writer.WriteLine();
        }
    }

    private GenerationJob[] GetBest(CsvReader reader)
    {
        ParameterSetting[] parameters = ProcessHeader( reader );
        if ( Maximize )
        {
            return GetHighestBest( reader, parameters );
        }
        else
        {
            return GetLowestBest( reader, parameters );
        }
    }

    private ParameterSetting[] ProcessHeader(CsvReader reader)
    {
        List<ParameterSetting> ret = [];
        int columns = reader.LoadLine();
        for ( int i = 2; i < columns; i++ )
        {
            reader.Get(out string name, i);
            ret.Add( new ParameterSetting()
            {
                Names = [name]
            } );
        }
        return [.. ret];
    }

    private GenerationJob ReadJob(CsvReader reader, ParameterSetting[] parameters)
    {
        while (reader.LoadLine(out int columns))
        {
            if (columns >= parameters.Length + 2)
            {
                var localParameters = CloneParameters(parameters);
                var job = new Job()
                {
                    ProcessedBy = null,
                    Processed = false,
                    Processing = false,
                    Parameters = localParameters
                };
                reader.Get(out int generation, 0);
                GenerationJob genJob = new(job, generation);
                // we don't load the generation
                reader.Get(out job.Value, 1);
                for (int i = 0; i < localParameters.Length; i++)
                {
                    reader.Get(out localParameters[i].Current, i + 2);
                }
                return genJob;
            }
        }
        return null;
    }

    private ParameterSetting[] CloneParameters(ParameterSetting[] parameters)
    {
        var ret = new ParameterSetting[parameters.Length];
        for ( int i = 0; i < parameters.Length; i++ )
        {
            ret[i] = new ParameterSetting()
            {
                Current = parameters[i].Current,
                Maximum = parameters[i].Maximum,
                Minimum = parameters[i].Minimum,
                Names = parameters[i].Names
            };
        }
        return ret;
    }

    private GenerationJob[] GetLowestBest(CsvReader reader, ParameterSetting[] parameters)
    {
        List<GenerationJob> best = new( ResultsToSave + 2 );
        GenerationJob currentJob;
        var maxResults = ResultsToSave;
        while ( ( currentJob = ReadJob( reader, parameters ) ) != null )
        {
            int index = -1;
            // Always accept the first one
            if ( best.Count > 0 )
            {
                //check the last one first since they are in order to see if we need to check each one
                if ( currentJob.Job.Value < best[best.Count - 1].Job.Value )
                {
                    for ( int i = 0; i < best.Count; i++ )
                    {
                        if ( currentJob.Job.Value < best[i].Job.Value )
                        {
                            index = i;
                            break;
                        }
                    }
                }
            }
            if ( index >= 0 )
            {
                best.Insert( index, currentJob );
                if ( best.Count > maxResults )
                {
                    best.RemoveAt( best.Count - 1 );
                }
            }
            // if we don't have enough just add
            else if ( index == -1 & best.Count < maxResults )
            {
                best.Add( currentJob );
            }
        }
        return [.. best];
    }

    private GenerationJob[] GetHighestBest(CsvReader reader, ParameterSetting[] parameters)
    {
        List<GenerationJob> best = new( ResultsToSave + 2 );
        GenerationJob currentJob;
        var maxResults = ResultsToSave;
        while ( ( currentJob = ReadJob( reader, parameters ) ) != null )
        {
            int index = -1;
            // always accept the first one
            if ( best.Count > 0 )
            {
                //check the last one first since they are in order to see if we need to check each one
                if ( currentJob.Job.Value > best[best.Count - 1].Job.Value )
                {
                    for ( int i = 0; i < best.Count; i++ )
                    {
                        if ( currentJob.Job.Value > best[i].Job.Value )
                        {
                            index = i;
                            break;
                        }
                    }
                }
            }
            if ( index >= 0 )
            {
                best.Insert( index, currentJob );
                if ( best.Count > maxResults )
                {
                    best.RemoveAt( best.Count - 1 );
                }
            }
            // if we don't have enough just add
            else if ( index == -1 & best.Count < maxResults )
            {
                best.Add( currentJob );
            }
        }
        return [.. best];
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
