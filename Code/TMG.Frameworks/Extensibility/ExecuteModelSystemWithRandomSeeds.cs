/*
    Copyright 2021 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Extensibility;

[ModuleInformation(Description = "This model system template is designed to facilitate the testing of many random seeds in order to evaluate their effect on model results.")]
public class ExecuteModelSystemWithRandomSeeds : IModelSystemTemplate
{
    [RunParameter("Input Base Directory", "../../Input", "The base directory for input.")]
    public string InputBaseDirectory { get; set; }
    public string OutputBaseDirectory { get; set; }
    public string Name { get; set; }

    public float Progress => _computeProgress?.Invoke() ?? 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    [SubModelInformation(Required = true, Description = "The model system to run the random seeds on.")]
    public IModelSystemTemplate ModelSystem;

    [RunParameter("RandomSeed", 12345, "The random seed to use to generate random seeds to test if a file is not provided.")]
    public int RandomSeed;

    [RunParameter("ParametersToTest", 1, "The number of different random seeds to execute.")]
    public int ParametersToTest;

    [SubModelInformation(Required = false, Description = "Optional file containing the random seeds to execute.")]
    public FileLocation RandomSeedFile;

    [RunParameter("Row to Start From", 0, "The index into the random seed file to start processing from.")]
    public int RowToStartFrom;

    [SubModelInformation(Required = true, Description = "The parameters to assign to.  There must be at least one.")]
    public ParameterLink[] ParametersToAssign;

    private Func<float> _computeProgress = () => 0f;
    private Func<string> _computeStatus = () => "Initializing";
    private bool _exit = false;

    public bool ExitRequest()
    {
        _exit = true;
        ModelSystem.ExitRequest();
        return true;
    }

    public bool RuntimeValidation(ref string error)
    {
        if(RowToStartFrom < 0)
        {
            error = "Unable to start at negative indexed line! Please select non-negative value for 'Row to Start From'!";
            return false;
        }
        return true;
    }

    public void Start()
    {
        _computeProgress = () => 0.0f;
        var baseDirectory = Path.GetFullPath(Directory.GetCurrentDirectory());
        List<int> seedsToTest = GetRandomSeeds();
        try
        {
            int i = 0;
            _computeProgress = () => (i + ModelSystem.Progress) / seedsToTest.Count;
            _computeStatus = () => $"Processing random seed {i + 1} of {seedsToTest.Count}: {ModelSystem}";
            for (; i < seedsToTest.Count && !_exit; i++)
            {
                SetCWD(baseDirectory, i);
                AssignRandomSeeds(seedsToTest[i]);
                ModelSystem.Start();
            }
            _computeProgress = () => 1.0f;
        }
        finally
        {
            Directory.SetCurrentDirectory(baseDirectory);
        }
    }

    [ModuleInformation(Description = "This module is used to find the parameters to assign random seeds to.")]
    public sealed class ParameterLink : IModule
    {
        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

        private readonly IConfiguration _config;

        private IModuleParameter _parameter;

        [RunParameter("Parameter Path", "", "The path through the model system to the desired parameter to alter.  This contains the name of all ancestors in order separated by periods.")]
        public string ParameterPath;

        public ParameterLink(IConfiguration config)
        {
            _config = config;
        }

        public void AssignValue(int seed)
        {
            Functions.ModelSystemReflection.AssignValueRunOnly(_config, _parameter, seed);
        }

        public bool RuntimeValidation(ref string error)
        {
            try
            {
                _parameter = Functions.ModelSystemReflection.FindParameter(_config, this, ParameterPath);
                if(_parameter.Type != typeof(int))
                {
                    error = $"The parameter path '{ParameterPath}' leads to a parameter that is not backed by an int!";
                    return false;
                }
            }
            catch(XTMFRuntimeException e)
            {
                error = e.Message;
                return false;
            }
            return true;
        }
    }

    private void AssignRandomSeeds(int randomSeedScenario)
    {
        Random r = new(randomSeedScenario);
        for(int i = 0; i < ParametersToAssign.Length; i++)
        {
            ParametersToAssign[i].AssignValue(r.Next());
        }
    }

    private List<int> GetRandomSeeds()
    {
        if(RandomSeedFile is object)
        {
            try
            {
                using var reader = new StreamReader(RandomSeedFile);
                for (int i = 0; i < RowToStartFrom; i++)
                {
                    // burn the unneeded lines
                    if (reader.ReadLine() == null)
                    {
                        throw new XTMFRuntimeException(this, $"There is no line {RowToStartFrom} in file '{RandomSeedFile}' to load.  The last line number is {i + 1}.");
                    }
                }
                return new List<int>(Enumerable.Range(0, ParametersToTest).Select(i =>
                {
                    var line = reader.ReadLine() ?? throw new XTMFRuntimeException(this, $"We ran out of lines to read from file '{RandomSeedFile}' at line {i + RowToStartFrom + 1}!");
                    if (!int.TryParse(line, out int value))
                    {
                        throw new XTMFRuntimeException(this, $"Unable to read the random seed {line} on line {i + RowToStartFrom + 1} in file '{RandomSeedFile}'!");
                    }
                    return value;
                }));
            }
            catch(IOException e)
            {
                throw new XTMFRuntimeException(this, e, "Failed to be able to read from random seed file!\n"+ e.Message);
            }
        }
        else
        {
            var r = new Random(RandomSeed);
            return new List<int>(Enumerable.Range(0, ParametersToTest).Select(_ => r.Next()));
        }
    }

    private static void SetCWD(string baseDirectory, int iterationNumber)
    {
        var subDirPath = Path.Combine(baseDirectory, iterationNumber.ToString());
        if (!Directory.Exists(subDirPath))
        {
            Directory.CreateDirectory(subDirPath);
        }
        Directory.SetCurrentDirectory(subDirPath);
    }

    public override string ToString()
    {
        return _computeStatus();
    }
}
