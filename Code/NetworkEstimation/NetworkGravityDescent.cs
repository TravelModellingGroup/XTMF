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
using System.IO;
using System.Xml;
using TMG.Emme;
using XTMF;

namespace TMG.NetworkEstimation
{
    public class NetworkGravityDescent : INetworkEstimationAI
    {
        [RunParameter("Evaluation File", @"../ParameterEvaluation.csv", "The name of the file the macro creates")]
        public string EvaluationFile;

        [RunParameter("Initial Parameters", "", "Leave this empty if you don't want to set the initial Parameters, otherwise the file name for the parameters")]
        public string InitialParameterFile;

        [RunParameter("MABS Weight", 1f, "The weight applied to the mean absolute error in the evaluation function")]
        public float MabsWeight;

        [RunParameter("Parameter Instructions", "../../Input/ParameterInstructions.xml", "Describes which and how the parameters will be estimated.")]
        public string ParameterInstructions;

        [RunParameter("Random Seed", 12345, "The random seed to use for this estimation.")]
        public int RandomSeed;

        [RunParameter("Continue From Best", false, "Should we attempt to load the old values and find the one with the lowest error and continue from there?")]
        public bool RerunFromLastRunParameters;

        [RunParameter("RMSE Weight", 1f, "The weight applied to the root mean square error in the evaluation function")]
        public float RmseWeight;

        [RunParameter("StepWeight", 0.01f, "The ammount that we will move our focus in each parameter dimension per iteration (multiplied against the gradient).")]
        public float StepWeight;

        [RunParameter("TotalError Weight", 1f, "The weight applied to the total error in the evaluation function")]
        public float ErrorWeight;

        [RunParameter("Total Iterations", 25, "The random seed to use for this estimation.")]
        public int TotalIterations;

        [RunParameter("Whisker Length", 0.1f, "The ammount that we will search out to find orientation in each parameter dimension.")]
        public float WhiskerLength;

        private static char[] Comma = { ',' };
        private float BestRunError = float.MaxValue;

        private int CurrentIteration;

        private volatile bool Exit;

        private ParameterSetting[] Kernel;

        private int NumberOfExplorations;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool UseComplexErrorFunction
        {
            get { return false; }
        }

        public void CancelExploration()
        {
            Exit = true;
        }

        public float ComplexErrorFunction(ParameterSetting[] parameters, TransitLine[] transitLine, TransitLine[] predicted, float[] aggToTruth)
        {
            throw new NotImplementedException();
        }

        public float ErrorCombinationFunction(double rmse, double mabs, double terror)
        {
            return (float)((
                RmseWeight * rmse
                + MabsWeight * mabs
                + ErrorWeight * terror)
                / 3);
        }

        public void Explore(ParameterSetting[] parameters, Action updateProgress, Func<ParameterSetting[], float> evaluationfunction)
        {
            Progress = 0;
            Random rand = new Random((++NumberOfExplorations) * (RandomSeed));
            var numberOfParameters = parameters.Length;
            Kernel = parameters.Clone() as ParameterSetting[];
            GenerateRandomSeed(rand);
            float[,] explorationErrors = new float[numberOfParameters, 2];
            float[,] explorationGradients = new float[numberOfParameters, 2];
            ParameterSetting[,][] explorationPoints = new ParameterSetting[numberOfParameters, 2][];

            for (int i = 0; i < numberOfParameters; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    explorationPoints[i, j] = parameters.Clone() as ParameterSetting[];
                }
            }

            // Explore it for all of the pre-defined iterations
            for (int iteration = 0; iteration < TotalIterations; iteration++)
            {
                CurrentIteration = iteration;
                Progress = (float)iteration / TotalIterations;
                updateProgress();
                // figure out how good our point is
                if (Exit)
                {
                    break;
                }
                var kernelError = evaluationfunction(Kernel);
                // Calculate all of the errors
                GenerateExplorationPoints(numberOfParameters, Kernel, explorationPoints, explorationErrors, evaluationfunction, updateProgress);
                // Calculate the gradients from the errors
                ComputeGradients(numberOfParameters, explorationErrors, explorationGradients, kernelError);
                MoveKernel(explorationGradients, rand);
            }
            Progress = 1;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private static void ComputeGradients(int numberOfParameters, float[,] explorationErrors, float[,] explorationGradients, float kernelError)
        {
            for (int i = 0; i < numberOfParameters; i++)
            {
                explorationGradients[i, 0] = explorationErrors[i, 0] - kernelError;
                explorationGradients[i, 1] = explorationErrors[i, 1] - kernelError;
            }
        }

        private void GenerateExplorationPoints(int numberOfParameters, ParameterSetting[] kernel, ParameterSetting[,][] explorationPoints
            , float[,] explorationErrors, Func<ParameterSetting[], float> evaluationfunction, Action updateProgress)
        {
            for (int i = 0; i < numberOfParameters; i++)
            {
                for (int j = 0; j < numberOfParameters; j++)
                {
                    if (i != j)
                    {
                        explorationPoints[i, 0][j].Current = kernel[j].Current;
                        explorationPoints[i, 1][j].Current = kernel[j].Current;
                    }
                    else
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            explorationPoints[i, k][j].Current = kernel[j].Current + (kernel[j].Stop - kernel[j].Start) * (k == 0 ? -WhiskerLength : WhiskerLength);
                            if (explorationPoints[i, k][j].Current < explorationPoints[i, k][j].Start)
                            {
                                explorationPoints[i, k][j].Current = explorationPoints[i, k][j].Start;
                            }
                            else if (explorationPoints[i, k][j].Current > explorationPoints[i, k][j].Stop)
                            {
                                explorationPoints[i, k][j].Current = explorationPoints[i, k][j].Stop;
                            }
                        }
                    }
                }
                for (int k = 0; k < 2; k++)
                {
                    if (Exit)
                    {
                        break;
                    }
                    explorationErrors[i, k] = evaluationfunction(explorationPoints[i, k]);
                    Progress = (float)CurrentIteration / TotalIterations
                        + (1.0f / TotalIterations) * ((i * 2 + k) / (float)(numberOfParameters * 2));
                    updateProgress();
                }
            }
        }

        private void GenerateRandomSeed(Random rand)
        {
            var dimensions = Kernel.Length;

            if (RerunFromLastRunParameters && File.Exists(EvaluationFile))
            {
                // store the best values in the kernel
                using (StreamReader reader = new StreamReader(EvaluationFile))
                {
                    string line;
                    // Burn the header
                    reader.ReadLine();
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] split = line.Split(Comma);
                        // make sure our 3 metrics are here
                        if (split.Length == dimensions + 4)
                        {
                            int offset = split.Length - 3;
                            float rmse = float.Parse(split[offset + 0]);
                            float mse = float.Parse(split[offset + 1]);
                            float error = float.Parse(split[offset + 2]);
                            float value = ErrorCombinationFunction(rmse, mse, error);
                            if (value < BestRunError)
                            {
                                BestRunError = value;
                                for (int i = 0; i < Kernel.Length; i++)
                                {
                                    Kernel[i].Current = float.Parse(split[i]);
                                }
                            }
                        }
                    }
                }
            }
            else if (InitialParameterFile == null || InitialParameterFile == string.Empty || !File.Exists(InitialParameterFile))
            {
                for (int i = 0; i < dimensions; i++)
                {
                    var value = (rand.NextDouble() * (Kernel[i].Stop - Kernel[i].Start)) + Kernel[i].Start;
                    Kernel[i].Current = (float)value;
                }
            }
            else
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(ParameterInstructions);
                var children = doc["Root"]?.ChildNodes;
                if (children != null)
                {
                    foreach (XmlNode child in doc["Root"].ChildNodes)
                    {
                        if (child.Name == "Parameter")
                        {
                            var attributes = child.Attributes;
                            if (attributes != null)
                            {
                                var pName = attributes["Name"].InnerText;
                                for (int i = 0; i < Kernel.Length; i++)
                                {
                                    if (Kernel[i].ParameterName == pName)
                                    {
                                        Kernel[i].Current = float.Parse(attributes["Value"].InnerText);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Reset the best run error so we properly add in the new value (boarding file output from the macro)
            BestRunError = float.MaxValue;
        }

        private void MoveKernel(float[,] explorationGradients, Random r)
        {
            var dimensions = Kernel.Length;
            var randomWeight = (float)r.NextDouble() * StepWeight;
            for (int i = 0; i < dimensions; i++)
            {
                int direction = explorationGradients[i, 1] < explorationGradients[i, 0] ? 1 : 0;
                if (direction == 0)
                {
                    Kernel[i].Current += randomWeight * explorationGradients[i, 0];
                }
                else
                {
                    Kernel[i].Current -= randomWeight * explorationGradients[i, 1];
                }
                // bind it to the min/max
                if (Kernel[i].Current < Kernel[i].Start)
                {
                    Kernel[i].Current = Kernel[i].Start;
                }
                else if (Kernel[i].Current > Kernel[i].Stop)
                {
                    Kernel[i].Current = Kernel[i].Stop;
                }
            }
        }
    }
}