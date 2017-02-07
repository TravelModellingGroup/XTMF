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
using System.Threading;
using System.Threading.Tasks;
using TMG.ParameterDatabase;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel.ParameterDatabase
{
    public class AdvancedModeParameterDatabase : IModeParameterDatabase
    {
        [RunParameter("Mode Choice Database File", "ModeChoiceParameters.csv", "A file containing all of the parameters to be used for each parameter set.")]
        public string DatabaseFile;

        [RunParameter("Demographic Database File", "ModeChoiceDemographicAlternatives.csv", "A file containing all of the alternative values to be used if a parameter is disabled.")]
        public string DemographicDatabaseFile;

        [RunParameter("Demographic Switch File", "ModeChoiceDemographicSwitches.csv", "A file containing all of the parameters whether or not to use the original parameter or the disabled parameter.")]
        public string DemographicSwitchFile;

        [SubModelInformation(Description = "Modes", Required = false)]
        public List<IModeParameterAssignment> Modes;

        [RootModule]
        public IModelSystemTemplate Root;

        private bool Blending;

        private float CurrentBlendWeight;

        private List<string[]> DemographicAlternativeParameters = new List<string[]>();

        private List<bool[]> DemographicSwitches = new List<bool[]>();

        private bool Loaded;

        private List<string[]> ParameterSets = new List<string[]>();

        public string Name
        {
            get;
            set;
        }

        public int NumberOfParameterSets
        {
            get
            {
                if (ParameterSets != null)
                {
                    return ParameterSets.Count;
                }
                return 0;
            }
        }

        [SubModelInformation(Description = "Parameters", Required = false)]
        public List<Parameter> Parameters { get; private set; }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void ApplyParameterSet(int parameterSetIndex, int demographicIndex)
        {
            // Check to see if we need to load in our data
            if (!Loaded)
            {
                Load();
            }
            // Now that we have our data loaded in go and take in our parameters
            SetupParameters(parameterSetIndex, demographicIndex);
            // Check to see if we are doing a blending assignment
            if (Blending)
            {
                AssignBlendedParameters();
            }
            else
            {
                // Now that we have our parameters assign the parameters
                AssignParameters();
            }
        }

        public void CompleteBlend()
        {
            Parallel.For(0, Modes.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate (int i)
                {
                    Modes[i].FinishBlending();
                });
            Blending = false;
        }

        public void InitializeBlend()
        {
            Blending = true;
            Parallel.For(0, Modes.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate (int i)
                {
                    Modes[i].StartBlend();
                });
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void SetBlendWeight(float currentBlendWeight)
        {
            CurrentBlendWeight = currentBlendWeight;
        }

        protected string GetInputFileName(string localPath)
        {
            var fullPath = localPath;
            if (!Path.IsPathRooted(fullPath))
            {
                fullPath = Path.Combine(Root.InputBaseDirectory, fullPath);
            }
            return fullPath;
        }

        private void AssignBlendedParameters()
        {
            // now in parallel setup all of our modes at the same time
            if (CurrentBlendWeight == 0) return;
            Parallel.For(0, Modes.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate (int i)
                {
                    Modes[i].AssignBlendedParameters(Parameters, CurrentBlendWeight);
                });
        }

        private void AssignParameters()
        {
            // now in parallel setup all of our modes at the same time
            Parallel.For(0, Modes.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate (int i)
                {
                    Modes[i].AssignParameters(Parameters);
                });
        }

        private string[] GetAllButFirst(string[] split)
        {
            string[] temp = new string[split.Length - 1];
            Array.Copy(split, 1, temp, 0, temp.Length);
            return temp;
        }

        private void Load()
        {
            lock (this)
            {
                Thread.MemoryBarrier();
                if (Loaded) return;
                // First load in the parameters
                var headers = LoadParameters();
                // Next we can load the demographic switches and the alternative values at the same time.
                Parallel.Invoke(
                    delegate
                    {
                        LoadSwitches(headers);
                    },
                    delegate
                    {
                        LoadAlternatives(headers);
                    });
                // now that we have finished loading, flip that switch
                Loaded = true;
                Thread.MemoryBarrier();
            }
        }

        private void LoadAlternatives(string[] headers)
        {
            try
            {
                using (StreamReader reader = new StreamReader(GetInputFileName(DemographicDatabaseFile)))
                {
                    string line;
                    // burn header
                    reader.ReadLine();
                    while ((line = reader.ReadLine()) != null)
                    {
                        var split = line.Split(',');
                        if (split.Length < headers.Length + 1)
                        {
                            continue;
                        }
                        DemographicAlternativeParameters.Add(GetAllButFirst(split));
                    }
                }
            }
            catch (IOException)
            {
                throw new XTMFRuntimeException("We were unable to read the file '" + GetInputFileName(DemographicDatabaseFile) + "'. Please make sure this file exists and is not in use.");
            }
        }

        private string[] LoadParameters()
        {
            string[] headers;
            try
            {
                using (StreamReader reader = new StreamReader(GetInputFileName(DatabaseFile)))
                {
                    string line = reader.ReadLine();
                    headers = ParseHeader(line);
                    SetupParameterObjects(headers);
                    while ((line = reader.ReadLine()) != null)
                    {
                        var split = line.Split(',');
                        if (split.Length < headers.Length + 1)
                        {
                            continue;
                        }
                        ParameterSets.Add(GetAllButFirst(split));
                    }
                }
            }
            catch (IOException)
            {
                throw new XTMFRuntimeException("We were unable to read the file '" + GetInputFileName(DatabaseFile) + "'. Please make sure this file exists and is not in use.");
            }
            return headers;
        }

        private void LoadSwitches(string[] headers)
        {
            try
            {
                using (StreamReader reader = new StreamReader(GetInputFileName(DemographicSwitchFile)))
                {
                    int lineNumber = 1;
                    string line;
                    // burn header
                    reader.ReadLine();
                    lineNumber++;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var split = line.Split(',');
                        if (split.Length < headers.Length + 1)
                        {
                            continue;
                        }
                        bool[] switchLine = new bool[headers.Length];
                        for (int i = 0; i < switchLine.Length; i++)
                        {
                            if (!bool.TryParse(split[i + 1], out switchLine[i]))
                            {
                                throw new XTMFRuntimeException("In the file '" + GetInputFileName(DemographicSwitchFile)
                                    + "' on line " + lineNumber + " under column '" + headers[i] + "' we were unable to parse the value '"
                                    + split[i + 1] + "' as a boolean.  Please fix this to be either 'true' or 'false'!");
                            }
                        }
                        DemographicSwitches.Add(switchLine);
                        lineNumber++;
                    }
                }
            }
            catch (IOException)
            {
                throw new XTMFRuntimeException("We were unable to read the file '" + DemographicSwitchFile + "'. Please make sure this file exists and is not in use.");
            }
        }

        private string[] ParseHeader(string line)
        {
            return GetAllButFirst(line.Split(','));
        }

        private void SetupParameterObjects(string[] headers)
        {
            var length = headers.Length;
            Parameters = new List<Parameter>(length);
            for (int i = 0; i < length; i++)
            {
                Parameters.Add(new Parameter(headers[i]));
            }
        }

        private void SetupParameters(int parameterSetIndex, int demographicIndex)
        {
            var length = Parameters.Count;
            if (parameterSetIndex < 0)
            {
                throw new XTMFRuntimeException("The Mode Choice Parameter Set has to have a non negative index!");
            }
            if (demographicIndex < 0)
            {
                throw new XTMFRuntimeException("The Mode Choice Demographic Parameter Set has to have a non negative index!");
            }
            if (parameterSetIndex >= ParameterSets.Count)
            {
                throw new XTMFRuntimeException("The Mode Choice Parameter Set " + parameterSetIndex + " does not exist, please check!");
            }
            if (parameterSetIndex >= DemographicAlternativeParameters.Count)
            {
                throw new XTMFRuntimeException("The Demographic Alternative Parameter Set " + parameterSetIndex + " does not exist, please check!");
            }
            if (demographicIndex >= DemographicSwitches.Count)
            {
                throw new XTMFRuntimeException("The Mode Choice Demographic Parameter Set " + demographicIndex + " does not exist, please check!");
            }
            var parameterSet = ParameterSets[parameterSetIndex];
            var demographicAlternative = DemographicAlternativeParameters[parameterSetIndex];
            var demographicSwitchLine = DemographicSwitches[demographicIndex];
            for (int i = 0; i < length; i++)
            {
                // the first part is to check to see which value we should be loading
                if (demographicSwitchLine[i])
                {
                    // if it is true, then we use the default value
                    Parameters[i].Value = parameterSet[i];
                }
                else
                {
                    // if it is false then we use the alternative value
                    Parameters[i].Value = demographicAlternative[i];
                }
            }
        }
    }
}