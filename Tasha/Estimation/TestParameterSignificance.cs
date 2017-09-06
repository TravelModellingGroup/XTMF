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
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Tasha.Common;
using TMG.Input;
using XTMF;
using Tasha.XTMFModeChoice;

namespace Tasha.Estimation
{
    public class TestParameterSignificance : IPostHousehold
    {
        [SubModelInformation( Required = true, Description = "The output from estimation." )]
        public FileLocation EstimationResult;

        [RunParameter( "Mode Parameter Row", 1, "Which 0 indexed row should we read for the parameters to test.  1 would be the row after the header." )]
        public int ModeParameterFileRow;

        [RunParameter( "Observed Mode Tag", "ObservedMode", "The name of the data to lookup to get the observed mode." )]
        public string ObservedMode;

        [SubModelInformation( Required = true, Description = "The parameter file used for estimation." )]
        public FileLocation ParameterFile;

        [RootModule]
        public ITashaRuntime Root;

        [SubModelInformation( Required = true, Description = "The file to save output of the significance test." )]
        public FileLocation SignificanceResultFile;

        private IConfiguration Config;

        private IModelSystemStructure TashaMSS;

        public TestParameterSignificance(IConfiguration config)
        {
            Config = config;
        }

        private bool FindTasha(IModelSystemStructure mst, ref IModelSystemStructure modelSystemStructure)
        {
            if ( mst.Module == Root )
            {
                modelSystemStructure = mst;
                return true;
            }
            if ( mst.Children != null )
            {
                foreach ( var child in mst.Children )
                {
                    if ( FindTasha( child, ref modelSystemStructure ) )
                    {
                        return true;
                    }
                }
            }
            // Then we didn't find it in this tree
            return false;
        }

        private double BaseFitness;
        private double BaseRandomFitness;
        private double Fitness;
        private ParameterSetting[] Parameters;
        private int TotalIterations;

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            var householdFitness = EvaluateHousehold( household );
            if ( iteration == 0 )
            {
                var randomFitness = EvalateRandomFitness( household );
                lock ( this )
                {
                    Fitness += householdFitness;
                    BaseRandomFitness += randomFitness;
                }
            }
            else
            {
                lock ( this )
                {
                    Fitness += householdFitness;
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            // check to see if we are in the base case
            if ( iteration == 0 )
            {
                BaseFitness = Fitness;
                using ( StreamWriter writer = new StreamWriter( SignificanceResultFile.GetFilePath() ) )
                {
                    writer.WriteLine( "Parameter,Rho^2,Difference" );
                    writer.Write( "Base," );
                    writer.Write( 1.0 - ( BaseFitness / BaseRandomFitness ) );
                    writer.Write( ',' );
                    writer.WriteLine( '0' );
                }
            }
            else
            {
                var baseRho = ( 1.0 - ( BaseFitness / BaseRandomFitness ) );
                var ourRho = ( 1.0 - ( Fitness / BaseRandomFitness ) );
                using ( StreamWriter writer = new StreamWriter( SignificanceResultFile.GetFilePath(), true ) )
                {
                    writer.Write( Parameters[iteration - 1].Names[0] );
                    writer.Write( ',' );
                    writer.Write( ourRho );
                    writer.Write( ',' );
                    writer.WriteLine( baseRho - ourRho );
                }
            }
            // Reset rho
            Fitness = 0.0;
        }

        public void Load(int maxIterations)
        {
            TotalIterations = maxIterations;
            LoadParameterFile();
            LoadEstimationResult();
            DoubleCheckRightNumberOfParameters();
        }

        public bool RuntimeValidation(ref string error)
        {
            IModelSystemStructure tashaStructure = null;
            foreach ( var mst in Config.ProjectRepository.ActiveProject.ModelSystemStructure )
            {
                if ( FindTasha( mst, ref tashaStructure ) )
                {
                    foreach ( var child in tashaStructure.Children )
                    {
                        TashaMSS = child;
                        break;
                    }
                    break;
                }
            }
            if ( TashaMSS == null )
            {
                error = "In '" + Name + "' we were unable to find the Client Model System!";
                return false;
            }
            return true;
        }

        public void IterationStarting(int iteration)
        {
            InitializeParameterValues( iteration );
            AssignParameters();
        }

        private void AssignParameters()
        {
            for ( int i = 0; i < Parameters.Length; i++ )
            {
                for ( int j = 0; j < Parameters[i].Names.Length; j++ )
                {
                    AssignValue( Parameters[i].Names[j], Parameters[i].Current );
                }
            }
        }

        private void AssignValue(string parameterName, float value)
        {
            string[] parts = SplitNameToParts( parameterName );
            AssignValue( parts, 0, TashaMSS, value );
        }

        private void AssignValue(string[] parts, int currentIndex, IModelSystemStructure currentStructure, float value)
        {
            if ( currentIndex == parts.Length - 1 )
            {
                AssignValue( parts[currentIndex], currentStructure, value );
                return;
            }
            if ( currentStructure.Children == null ) return;
            for ( int i = 0; i < currentStructure.Children.Count; i++ )
            {
                if ( currentStructure.Children[i].Name == parts[currentIndex] )
                {
                    AssignValue( parts, currentIndex + 1, currentStructure.Children[i], value );
                    return;
                }
            }
        }

        private void AssignValue(string variableName, IModelSystemStructure currentStructure, float value)
        {
            var parameters = currentStructure.Parameters.Parameters;
            for ( int i = 0; i < parameters.Count; i++ )
            {
                if ( parameters[i].Name == variableName )
                {
                    var type = currentStructure.Module.GetType();
                    if ( parameters[i].OnField )
                    {
                        var field = type.GetField( parameters[i].VariableName );
                        field.SetValue( currentStructure.Module, value );
                        return;
                    }
                    else
                    {
                        var field = type.GetProperty( parameters[i].VariableName );
                        field.SetValue( currentStructure.Module, value, null );
                        return;
                    }
                }
            }
            throw new XTMFRuntimeException(this,
                String.Format("In '{0}' we were unable to find a variable named '{1}' in '{2}'.",
                Name, variableName, currentStructure.Name) );
        }


        private string[] SplitNameToParts(string parameterName)
        {
            List<string> parts = new List<string>();
            var stringLength = parameterName.Length;
            StringBuilder builder = new StringBuilder();
            for ( int i = 0; i < stringLength; i++ )
            {
                switch ( parameterName[i] )
                {
                    case '.':
                        parts.Add( builder.ToString() );
                        builder.Clear();
                        break;
                    case '\\':
                        if ( i + 1 < stringLength )
                        {
                            if ( parameterName[i + 1] == '.' )
                            {
                                builder.Append( '.' );
                                i += 2;
                            }
                            else if ( parameterName[i + 1] == '\\' )
                            {
                                builder.Append( '\\' );
                            }
                        }
                        break;
                    default:
                        builder.Append( parameterName[i] );
                        break;
                }
            }
            parts.Add( builder.ToString() );
            return parts.ToArray();
        }

        private void DoubleCheckRightNumberOfParameters()
        {
            if ( Parameters.Length + 1 != TotalIterations )
            {
                throw new XTMFRuntimeException(this, "In '" + Name + "' we were expecting " + ( Parameters.Length + 1 )
                + " iterations!" );
            }
        }

        private double EvalateRandomFitness(ITashaHousehold household)
        {
            double fitness = 0.0;
            var householdData = (ModeChoiceHouseholdData)household["ModeChoiceData"];
            for ( int i = 0; i < householdData.PersonData.Length; i++ )
            {
                for ( int j = 0; j < householdData.PersonData[i].TripChainData.Length; j++ )
                {
                    for ( int k = 0; k < householdData.PersonData[i].TripChainData[j].TripData.Length; k++ )
                    {
                        fitness += EvaluateRandomFitness( householdData.PersonData[i].TripChainData[j].TripData[k],
                            householdData.PersonData[i].TripChainData[j].TripChain.Trips[k].ModesChosen.Length );
                    }
                }
            }
            return fitness;
        }

        private double EvaluateHousehold(ITashaHousehold household)
        {
            double fitness = 0;
            foreach ( var p in household.Persons )
            {
                foreach ( var chain in p.TripChains )
                {
                    foreach ( var trip in chain.Trips )
                    {
                        var value = Math.Log( ( EvaluateTrip( trip ) + 1 ) / ( trip.ModesChosen.Length + 1 ) );
                        fitness += value;
                    }
                }
            }
            return fitness;
        }

        private double EvaluateRandomFitness(ModeChoiceTripData tripData, int householdIterations)
        {
            int feasibleModes = 0;
            for ( int i = 0; i < tripData.Feasible.Length; i++ )
            {
                if ( tripData.Feasible[i] )
                {
                    feasibleModes++;
                }
            }
            if ( feasibleModes == 0 ) feasibleModes = 1;
            return Math.Log( ( ( ( householdIterations / (double)feasibleModes ) ) + 1.0 )
                / ( householdIterations + 1.0 ) );
        }
        private double EvaluateTrip(ITrip trip)
        {
            int correct = 0;
            var observedMode = trip[ObservedMode];
            foreach ( var choice in trip.ModesChosen )
            {
                if ( choice == observedMode )
                {
                    correct++;
                }
            }
            return correct;
        }

        private void InitializeParameterValues(int iteration)
        {
            var localParameters = Parameters;
            for ( int i = 0; i < localParameters.Length; i++ )
            {
                localParameters[i].Current = localParameters[i].InitialValue;
            }
            if ( iteration > 0 )
            {
                localParameters[iteration - 1].Current = 0;
            }
        }

        private void LoadEstimationResult()
        {
            var modeParameterFile = EstimationResult.GetFilePath();
            using ( StreamReader reader = new StreamReader( modeParameterFile ) )
            {
                // First read the header, we will need that data to store in the mode parameters
                var headerLine = reader.ReadLine();
                if ( headerLine == null )
                {
                    throw new XTMFRuntimeException(this, "The file \"" + modeParameterFile + "\" does not contain any data to load parameters from!" );
                }
                string[] header = headerLine.Split( ',' );
                for ( int i = 1; ( i < ModeParameterFileRow ) && ( reader.ReadLine() != null ); i++ )
                {
                    // do nothing
                }
                string line = reader.ReadLine();
                if ( line == null )
                {
                    throw new XTMFRuntimeException(this, "We were unable to find a row#" + ModeParameterFileRow + " in the data set at \"" + modeParameterFile + "\"" );
                }
                var parameters = line.Split( ',' );
                var localParameters = Parameters;
                var numberOfParameters = header.Length;
                for ( int i = 0; i < numberOfParameters; i++ )
                {
                    var endOfMode = header[i].IndexOf( '.' );
                    if ( endOfMode < 0 )
                    {
                        continue;
                    }
                    bool found = false;
                    // find the matching mode
                    for ( int j = 0; j < localParameters.Length; j++ )
                    {
                        for ( int k = 0; k < localParameters[j].Names.Length; k++ )
                        {
                            if ( localParameters[j].Names[k] == header[i] )
                            {
                                if ( !float.TryParse( parameters[i], out localParameters[j].Current ) )
                                {
                                    throw new XTMFRuntimeException(this, "In '" + Name
                                        + "' we were unable to read in the parameter, '" + parameters[i]
                                        + "' in order to assign it as a value." );
                                }
                                localParameters[j].InitialValue = localParameters[j].Current;
                                found = true;
                            }
                        }
                    }
                    if ( !found )
                    {
                        throw new XTMFRuntimeException(this, "In '" + Name
                                        + "' we were unable to match a parameter called '" + header[i] + "'" );
                    }
                }
            }
        }

        private void LoadParameterFile()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load( ParameterFile.GetFilePath() );
            List<ParameterSetting> parameters = new List<ParameterSetting>();
            var children = doc["Root"]?.ChildNodes;
            if (children != null)
            {
                foreach (XmlNode child in  children)
                {
                    if (child.Name == "Parameter")
                    {
                        ParameterSetting current = new ParameterSetting();
                        if (child.HasChildNodes)
                        {
                            var nodes = child.ChildNodes;
                            current.Names = new string[nodes.Count];
                            for (int i = 0; i < nodes.Count; i++)
                            {
                                XmlNode name = nodes[i];
                                var parameterPath = name.Attributes?["ParameterPath"].InnerText;
                                current.Names[i] = parameterPath;
                            }
                        }
                        else
                        {
                            var parameterPath = child.Attributes?["ParameterPath"].InnerText;
                            current.Names = new[] {parameterPath};
                        }
                        parameters.Add(current);
                    }
                }
            }
            Parameters = parameters.ToArray();
        }

        protected struct ParameterSetting
        {
            internal float Current;
            internal float InitialValue;
            internal string[] Names;
        }
    }
}