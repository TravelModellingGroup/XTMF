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
using System.Xml;
using TMG.Emme;
using XTMF;

namespace TMG.NetworkEstimation;

public class NetworkAi : INetworkEstimationAI
{
    [SubModelInformation( Description = "The things to use for calculating the error.", Required = false )]
    public List<IErrorTally> ErrorTallies;

    [RunParameter( "Evaluation File", @"../ParameterEvaluation.csv", "The path to the file that stores the values of the tested parameters." )]
    public string EvaluationFile;

    [RunParameter( "Initial Parameters", "", "Leave this empty if you don't want to set the initial Parameters, otherwise the file name for the parameters" )]
    public string InitialParameterFile;

    [RunParameter( "Iterations From Best", 5, "Number of iterations we can go before testing how far we are from the best." )]
    public int IterationsFromBest;

    [RunParameter( "MABS Weight", 1f, "The weight applied to the mean absolute error in the evaluation function" )]
    public float MabsWeight;

    [RunParameter( "Momentum Residual", 0.1f, "The amount of momentum that continues on from the previous iteration." )]
    public float MomentumResidule;

    [RunParameter( "Momentum Threshold", 100f, "The threshold until there isn't enough momentum left in the system to continue if we have no momentum" )]
    public float MomentumThreshhold;

    [RunParameter( "Percentage Step Cap", 0.1f, "The maximum amount that a kernel step can take in 1 iteration" )]
    public float PercentageStepCap;

    [RunParameter( "Random Seed", 12345, "The random seed to use for this estimation." )]
    public int RandomSeed;

    [RunParameter( "Continue From Best", false, "Should we attempt to load the old values and find the one with the lowest error and continue from there?" )]
    public bool RerunFromLastRunParameters;

    [RunParameter( "RMSE Weight", 1f, "The weight applied to the root mean square error in the evaluation function" )]
    public float RmseWeight;

    [RunParameter( "Step Weight", 0.01f, "The amount that we will move our focus in each parameter dimension per iteration (multiplied against the gradient)." )]
    public float StepWeight;

    [RunParameter( "Total Error Weight", 1f, "The weight applied to the total error in the evaluation function" )]
    public float ErrorWeight;

    [RunParameter( "Total Iterations", 25, "The number of iterations to process." )]
    public int TotalIterations;

    [RunParameter( "Volatility Threshold", 100f, "The threshold until there isn't enough volatility left in the system to continue if we have no momentum." )]
    public float VolatilityThreshhold;

    [RunParameter( "Whisker Length", 0.1f, "The amount that we will search out to find orientation in each parameter dimension." )]
    public float WhiskerLength;

    private static char[] Comma = { ',' };

    private float BestRunError = float.MaxValue;

    private int CurrentIteration;

    private volatile bool Exit;

    private ParameterSetting[] Kernel;

    private float[] Momentum;

    private float[] MoveChoice;

    private int NumberOfExplorations;

    private float[] ParameterVolatility;

    private float[] PreviousMoveChoice;

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

    [RunParameter( "Advanced Analysis Error Function", true, "Should we use the advanced function instead of using the RMSE et al." )]
    public bool UseComplexErrorFunction { get; set; }

    public void CancelExploration()
    {
        Exit = true;
    }

    public float ComplexErrorFunction(ParameterSetting[] parameters, TransitLine[] transitLine, TransitLine[] predicted, float[] aggToTruth)
    {
        float sum = 0;
        //var regionError = this.ComputeRegionError( transitLine, predicted );
        //var lineByLine = this.ComputeLineError( transitLine, aggToTruth );
        foreach ( var tally in ErrorTallies )
        {
            sum += tally.ComputeError( parameters, transitLine, predicted );
        }
        return sum;
    }

    public float ErrorCombinationFunction(double rmse, double mabs, double terror)
    {
        return (float)( (
            RmseWeight * rmse
            + MabsWeight * mabs
            + ErrorWeight * terror ) );
    }

    public void Explore(ParameterSetting[] parameters, Action updateProgress, Func<ParameterSetting[], float> evaluationfunction)
    {
        Progress = 0;
        Random rand = new( ( ++NumberOfExplorations ) * ( RandomSeed ) );
        var numberOfParameters = parameters.Length;
        Kernel = parameters.Clone() as ParameterSetting[];
        ParameterVolatility = new float[numberOfParameters];
        Momentum = new float[numberOfParameters];
        MoveChoice = new float[numberOfParameters];
        PreviousMoveChoice = new float[numberOfParameters];
        GenerateRandomSeed( rand );
        float[,] explorationErrors = new float[numberOfParameters, 4];
        float[,] explorationGradients = new float[numberOfParameters, 4];
        ParameterSetting[,][] explorationPoints = new ParameterSetting[numberOfParameters, 4][];
        float bestSoFar = float.MaxValue;
        int numberOfIterationSinceBest = 0;

        for ( int i = 0; i < numberOfParameters; i++ )
        {
            for ( int j = 0; j < 4; j++ )
            {
                explorationPoints[i, j] = parameters.Clone() as ParameterSetting[];
            }
        }

        // Explore it for all of the pre-defined iterations
        for ( int iteration = 0; iteration < TotalIterations; iteration++ )
        {
            CurrentIteration = iteration;
            Progress = (float)iteration / TotalIterations;
            updateProgress();
            // figure out how good our point is
            if ( Exit )
            {
                break;
            }
            var kernelError = evaluationfunction( Kernel );
            if ( kernelError < bestSoFar )
            {
                bestSoFar = kernelError;
                numberOfIterationSinceBest = 0;
            }
            else if ( ( ++numberOfIterationSinceBest ) > IterationsFromBest )
            {
                break;
            }
            // Calculate all of the errors
            GenerateExplorationPoints( numberOfParameters, Kernel, explorationPoints, explorationErrors, evaluationfunction, updateProgress );
            // Calculate the gradients from the errors
            ComputeGradients( numberOfParameters, explorationErrors, explorationGradients, kernelError );
            ComputeVolatility( explorationGradients );
            if ( EarlyTermination() )
            {
                break;
            }
            MoveKernel( explorationGradients, rand );
            var moveChoiceTemp = PreviousMoveChoice;
            PreviousMoveChoice = MoveChoice;
            MoveChoice = moveChoiceTemp;
        }
        Progress = 1;
    }

    public bool RuntimeValidation(ref string error)
    {
        if ( MomentumResidule >= 1 )
        {
            error = "The momentum residule should be less than 1!";
            return false;
        }
        if ( MomentumResidule < 0 )
        {
            error = "The momentum residule can not be less than 0!";
            return false;
        }
        return true;
    }

    private static void ComputeGradients(int numberOfParameters, float[,] explorationErrors, float[,] explorationGradients, float kernelError)
    {
        for ( int i = 0; i < numberOfParameters; i++ )
        {
            explorationGradients[i, 0] = explorationErrors[i, 0] - kernelError;
            explorationGradients[i, 1] = explorationErrors[i, 1] - kernelError;
            explorationGradients[i, 2] = explorationErrors[i, 2] - kernelError;
            explorationGradients[i, 3] = explorationErrors[i, 3] - kernelError;
        }
    }

    private void ComputeVolatility(float[,] explorationGradients)
    {
        var dimensions = Kernel.Length;

        for ( int i = 0; i < dimensions; i++ )
        {
            var average = 0f;
            average += explorationGradients[i, 0];
            average += explorationGradients[i, 1];
            average += explorationGradients[i, 2];
            average += explorationGradients[i, 3];
            average /= 4;
            var absmeandiff = 0f;
            absmeandiff += Math.Abs( explorationGradients[i, 0] - average );
            absmeandiff += Math.Abs( explorationGradients[i, 1] - average );
            absmeandiff += Math.Abs( explorationGradients[i, 2] - average );
            absmeandiff += Math.Abs( explorationGradients[i, 3] - average );
            ParameterVolatility[i] = absmeandiff;
        }
        using StreamWriter writer = new("Volatility.csv", true);
        writer.Write(ParameterVolatility[0]);
        for (int i = 1; i < dimensions; i++)
        {
            writer.Write(',');
            writer.Write(ParameterVolatility[i]);
        }
        writer.WriteLine();
    }

    private bool EarlyTermination()
    {
        // check to see if we should be terminating early
        float totalVolaility = 0;
        float totalMomentum = 0;
        var dimensions = Kernel.Length;
        for ( int i = 0; i < dimensions; i++ )
        {
            totalVolaility += ParameterVolatility[i];
            totalMomentum += Math.Abs( Momentum[i] );
        }
        return totalVolaility < VolatilityThreshhold && totalMomentum < MomentumThreshhold;
    }

    private void GenerateExplorationPoints(int numberOfParameters, ParameterSetting[] kernel, ParameterSetting[,][] explorationPoints
        , float[,] explorationErrors, Func<ParameterSetting[], float> evaluationfunction, Action updateProgress)
    {
        for ( int i = 0; i < numberOfParameters; i++ )
        {
            for ( int j = 0; j < numberOfParameters; j++ )
            {
                if ( i != j )
                {
                    explorationPoints[i, 0][j].Current = kernel[j].Current;
                    explorationPoints[i, 1][j].Current = kernel[j].Current;
                    explorationPoints[i, 2][j].Current = kernel[j].Current;
                    explorationPoints[i, 3][j].Current = kernel[j].Current;
                }
                else
                {
                    for ( int k = 0; k < 4; k++ )
                    {
                        float currentWhisker;
                        switch ( k )
                        {
                            case 0:
                                currentWhisker = WhiskerLength * -2;
                                break;

                            case 1:
                                currentWhisker = -WhiskerLength;
                                break;

                            case 2:
                                currentWhisker = WhiskerLength;
                                break;

                            default:
                                currentWhisker = WhiskerLength * 2;
                                break;
                        }
                        explorationPoints[i, k][j].Current = kernel[j].Current + ( kernel[j].Stop - kernel[j].Start ) * currentWhisker;
                        if ( explorationPoints[i, k][j].Current < explorationPoints[i, k][j].Start )
                        {
                            explorationPoints[i, k][j].Current = explorationPoints[i, k][j].Start;
                        }
                        else if ( explorationPoints[i, k][j].Current > explorationPoints[i, k][j].Stop )
                        {
                            explorationPoints[i, k][j].Current = explorationPoints[i, k][j].Stop;
                        }
                    }
                }
            }
            for ( int k = 0; k < 4; k++ )
            {
                if ( Exit )
                {
                    break;
                }
                explorationErrors[i, k] = evaluationfunction( explorationPoints[i, k] );
                Progress = (float)CurrentIteration / TotalIterations
                    + ( 1.0f / TotalIterations ) * ( ( i * 4 + k ) / (float)( numberOfParameters * 4 ) );
                updateProgress();
            }
        }
    }

    private void GenerateRandomSeed(Random rand)
    {
        var dimensions = Kernel.Length;

        if ( RerunFromLastRunParameters && File.Exists( EvaluationFile ) )
        {
            // store the best values in the kernel
            using StreamReader reader = new(EvaluationFile);
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
        else if ( InitialParameterFile == null || InitialParameterFile == string.Empty || !File.Exists( InitialParameterFile ) )
        {
            for ( int i = 0; i < dimensions; i++ )
            {
                var value = ( rand.NextDouble() * ( Kernel[i].Stop - Kernel[i].Start ) ) + Kernel[i].Start;
                Kernel[i].Current = (float)value;
            }
        }
        else
        {
            XmlDocument doc = new();
            doc.Load( InitialParameterFile );
            var childNodes = doc["Root"]?.ChildNodes;
            if (childNodes != null)
            {
                foreach (XmlNode child in  childNodes)
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
        for ( int i = 0; i < dimensions; i++ )
        {
            var increasing = ( explorationGradients[i, 3] + explorationGradients[i, 2] ) / 2;
            var decreasing = ( explorationGradients[i, 1] + explorationGradients[i, 0] ) / 2;
            float change = randomWeight * ( increasing < decreasing ? Math.Abs( increasing ) : -Math.Abs( decreasing ) );
            change += ( Momentum[i] * MomentumResidule );
            if ( change < 0 )
            {
                if ( change > -PercentageStepCap )
                {
                    change = -PercentageStepCap;
                }
            }
            else if ( change > 0 )
            {
                if ( change > PercentageStepCap )
                {
                    change = PercentageStepCap;
                }
            }
            Momentum[i] = change;
            Kernel[i].Current += change;
            // bind it to the min/max
            if ( Kernel[i].Current < Kernel[i].Start )
            {
                Kernel[i].Current = Kernel[i].Start;
                Momentum[i] = 0;
            }
            else if ( Kernel[i].Current > Kernel[i].Stop )
            {
                Kernel[i].Current = Kernel[i].Stop;
                Momentum[i] = 0;
            }
        }
    }
}