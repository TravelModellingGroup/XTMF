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

namespace TMG.NetworkEstimation
{
    public class NetworkAI : INetworkEstimationAI
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
        public float MABSWeight;

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
        public float RMSEWeight;

        [RunParameter( "Step Weight", 0.01f, "The amount that we will move our focus in each parameter dimension per iteration (multiplied against the gradient)." )]
        public float StepWeight;

        [RunParameter( "Total Error Weight", 1f, "The weight applied to the total error in the evaluation function" )]
        public float TErrorWeight;

        [RunParameter( "Total Iterations", 25, "The number of iterations to process." )]
        public int TotalIterations;

        [RunParameter( "Volatility Threshold", 100f, "The threshold until there isn't enough volatility left in the system to continue if we have no momentum." )]
        public float VolatilityThreshhold;

        [RunParameter( "Whisker Length", 0.1f, "The amount that we will search out to find orientation in each parameter dimension." )]
        public float WhiskerLength;

        private static char[] Comma = new char[] { ',' };

        private float BestRunError = float.MaxValue;

        private int CurrentIteration;

        private volatile bool Exit = false;

        private ParameterSetting[] Kernel;

        private float[] Momentum;

        private float[] MoveChoice;

        private int NumberOfExplorations;

        private float[] ParameterVolatility;

        private float[] PreviousMoveChoice;

        private float[] PreviousVolatility;

        public NetworkAI()
        {
        }

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
            this.Exit = true;
        }

        public float ComplexErrorFunction(ParameterSetting[] parameters, TransitLine[] transitLine, TransitLine[] predicted, float[] aggToTruth)
        {
            float sum = 0;
            //var regionError = this.ComputeRegionError( transitLine, predicted );
            //var lineByLine = this.ComputeLineError( transitLine, aggToTruth );
            foreach ( var tally in this.ErrorTallies )
            {
                sum += tally.ComputeError( parameters, transitLine, predicted );
            }
            return sum;
        }

        public float ErrorCombinationFunction(double rmse, double mabs, double terror)
        {
            return (float)( (
                this.RMSEWeight * rmse
                + this.MABSWeight * mabs
                + this.TErrorWeight * terror ) );
        }

        public void Explore(ParameterSetting[] parameters, Action UpdateProgress, Func<ParameterSetting[], float> evaluationfunction)
        {
            this.Progress = 0;
            Random rand = new Random( ( ++this.NumberOfExplorations ) * ( this.RandomSeed ) );
            var numberOfParameters = parameters.Length;
            this.Kernel = parameters.Clone() as ParameterSetting[];
            this.ParameterVolatility = new float[numberOfParameters];
            this.PreviousVolatility = new float[numberOfParameters];
            this.Momentum = new float[numberOfParameters];
            this.MoveChoice = new float[numberOfParameters];
            this.PreviousMoveChoice = new float[numberOfParameters];
            this.GenerateRandomSeed( rand );
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
            for ( int iteration = 0; iteration < this.TotalIterations; iteration++ )
            {
                this.CurrentIteration = iteration;
                this.Progress = (float)iteration / this.TotalIterations;
                UpdateProgress();
                // figure out how good our point is
                if ( this.Exit )
                {
                    break;
                }
                var kernelError = evaluationfunction( this.Kernel );
                if ( kernelError < bestSoFar )
                {
                    bestSoFar = kernelError;
                    numberOfIterationSinceBest = 0;
                }
                else if ( ( ++numberOfIterationSinceBest ) > this.IterationsFromBest )
                {
                    break;
                }
                // Calculate all of the errors
                GenerateExplorationPoints( numberOfParameters, this.Kernel, explorationPoints, explorationErrors, evaluationfunction, UpdateProgress );
                // Calculate the gradients from the errors
                ComputeGradients( numberOfParameters, explorationErrors, explorationGradients, kernelError );
                ComputeVolatility( explorationGradients, kernelError );
                if ( EarlyTermination() )
                {
                    break;
                }
                MoveKernel( explorationGradients, rand );
                var moveChoiceTemp = PreviousMoveChoice;
                PreviousMoveChoice = MoveChoice;
                MoveChoice = moveChoiceTemp;
            }
            this.Progress = 1;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( this.MomentumResidule >= 1 )
            {
                error = "The momentum residule should be less than 1!";
                return false;
            }
            else if ( this.MomentumResidule < 0 )
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

        private void ComputeVolatility(float[,] explorationGradients, float kernelError)
        {
            var dimensions = this.Kernel.Length;

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
                this.ParameterVolatility[i] = absmeandiff;
            }
            using ( StreamWriter writer = new StreamWriter( "Volatility.csv", true ) )
            {
                writer.Write( this.ParameterVolatility[0] );
                for ( int i = 1; i < dimensions; i++ )
                {
                    writer.Write( ',' );
                    writer.Write( this.ParameterVolatility[i] );
                }
                writer.WriteLine();
            }
        }

        private bool EarlyTermination()
        {
            // check to see if we should be terminating early
            float totalVolaility = 0;
            float totalMomentum = 0;
            var dimensions = this.Kernel.Length;
            for ( int i = 0; i < dimensions; i++ )
            {
                totalVolaility += this.ParameterVolatility[i];
                totalMomentum += Math.Abs( this.Momentum[i] );
            }
            return totalVolaility < this.VolatilityThreshhold && totalMomentum < this.MomentumThreshhold;
        }

        private void GenerateExplorationPoints(int numberOfParameters, ParameterSetting[] kernel, ParameterSetting[,][] explorationPoints
            , float[,] explorationErrors, Func<ParameterSetting[], float> evaluationfunction, Action UpdateProgress)
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
                                    currentWhisker = this.WhiskerLength * -2;
                                    break;

                                case 1:
                                    currentWhisker = -this.WhiskerLength;
                                    break;

                                case 2:
                                    currentWhisker = this.WhiskerLength;
                                    break;

                                default:
                                    currentWhisker = this.WhiskerLength * 2;
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
                    if ( this.Exit )
                    {
                        break;
                    }
                    explorationErrors[i, k] = evaluationfunction( explorationPoints[i, k] );
                    this.Progress = (float)( (float)this.CurrentIteration / this.TotalIterations )
                        + ( 1.0f / this.TotalIterations ) * ( ( i * 4 + k ) / (float)( numberOfParameters * 4 ) );
                    UpdateProgress();
                }
            }
        }

        private void GenerateRandomSeed(Random rand)
        {
            var dimensions = this.Kernel.Length;

            if ( RerunFromLastRunParameters && File.Exists( this.EvaluationFile ) )
            {
                // store the best values in the kernel
                using ( StreamReader reader = new StreamReader( this.EvaluationFile ) )
                {
                    string line;
                    // Burn the header
                    reader.ReadLine();
                    while ( ( line = reader.ReadLine() ) != null )
                    {
                        string[] split = line.Split( Comma );
                        // make sure our 3 metrics are here
                        if ( split != null && split.Length == dimensions + 4 )
                        {
                            int offset = split.Length - 3;
                            float RMSE = float.Parse( split[offset + 0] );
                            float MSE = float.Parse( split[offset + 1] );
                            float TError = float.Parse( split[offset + 2] );
                            float value = this.ErrorCombinationFunction( RMSE, MSE, TError );
                            if ( value < this.BestRunError )
                            {
                                this.BestRunError = value;
                                for ( int i = 0; i < this.Kernel.Length; i++ )
                                {
                                    this.Kernel[i].Current = float.Parse( split[i] );
                                }
                            }
                        }
                    }
                }
            }
            else if ( this.InitialParameterFile == null || this.InitialParameterFile == String.Empty || !File.Exists( this.InitialParameterFile ) )
            {
                for ( int i = 0; i < dimensions; i++ )
                {
                    var value = ( rand.NextDouble() * ( this.Kernel[i].Stop - this.Kernel[i].Start ) ) + this.Kernel[i].Start;
                    this.Kernel[i].Current = (float)value;
                }
            }
            else
            {
                XmlDocument doc = new XmlDocument();
                doc.Load( this.InitialParameterFile );

                foreach ( XmlNode child in doc["Root"].ChildNodes )
                {
                    if ( child.Name == "Parameter" )
                    {
                        var pName = child.Attributes["Name"].InnerText;
                        for ( int i = 0; i < this.Kernel.Length; i++ )
                        {
                            if ( this.Kernel[i].ParameterName == pName )
                            {
                                this.Kernel[i].Current = float.Parse( child.Attributes["Value"].InnerText );
                                break;
                            }
                        }
                    }
                }
            }
            // Reset the best run error so we properly add in the new value (boarding file output from the macro)
            this.BestRunError = float.MaxValue;
        }

        private void MoveKernel(float[,] explorationGradients, Random r)
        {
            var dimensions = this.Kernel.Length;
            var randomWeight = (float)r.NextDouble() * this.StepWeight;
            for ( int i = 0; i < dimensions; i++ )
            {
                var increasing = ( explorationGradients[i, 3] + explorationGradients[i, 2] ) / 2;
                var decreasing = ( explorationGradients[i, 1] + explorationGradients[i, 0] ) / 2;
                float change = randomWeight * ( increasing < decreasing ? Math.Abs( increasing ) : -Math.Abs( decreasing ) );
                change += ( this.Momentum[i] * this.MomentumResidule );
                if ( change < 0 )
                {
                    if ( change > -this.PercentageStepCap )
                    {
                        change = -this.PercentageStepCap;
                    }
                }
                else if ( change > 0 )
                {
                    if ( change > this.PercentageStepCap )
                    {
                        change = this.PercentageStepCap;
                    }
                }
                this.Momentum[i] = change;
                this.Kernel[i].Current += change;
                // bind it to the min/max
                if ( this.Kernel[i].Current < this.Kernel[i].Start )
                {
                    this.Kernel[i].Current = this.Kernel[i].Start;
                    this.Momentum[i] = 0;
                }
                else if ( this.Kernel[i].Current > this.Kernel[i].Stop )
                {
                    this.Kernel[i].Current = this.Kernel[i].Stop;
                    this.Momentum[i] = 0;
                }
            }
        }
    }
}