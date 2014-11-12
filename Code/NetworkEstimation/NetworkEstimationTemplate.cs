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
using System.Linq;
using System.Xml;
using TMG.Emme;
using XTMF;
using XTMF.Networking;

namespace TMG.NetworkEstimation
{
    public class NetworkEstimationTemplate : I4StepModel
    {
        public IClient Client;

        [RunParameter( "EMME To TTS", @"../../Input/TTSToEMME.csv", "CSV file to link EMME Lines to the TTS data." )]
        public string EMMEToTTSFile;

        [SubModelInformation( Description = "The AI to use for estimation", Required = true )]
        public INetworkEstimationAI EstimationAI;

        [RunParameter( "Evaluation File", @"../ParameterEvaluation.csv", "The file where the parameters and their evaluated fitness are stored." )]
        public string EvaluationFile;

        [RunParameter( "Emme Input Output", @"C:\Users\James\Documents\Project\scalars.311", "The name of the file the macro Loads" )]
        public string MacroInputFile;

        [RunParameter( "Emme Macro Output", @"C:\Users\James\Documents\Project\output.621", "The name of the file the macro creates" )]
        public string MacroOutputFile;

        [RunParameter( "Number Of Runs", 80, "The Number of runs to do." )]
        public int NumberOfRuns;

        [RunParameter( "Parameter Instructions", "../../Input/ParameterInstructions.xml", "Describes which and how the parameters will be estimated." )]
        public string ParameterInstructions;

        [RunParameter( "ResultPort", 12345, "The Custom Port to use for sending back the results" )]
        public int ResultPort;

        [RunParameter( "TruthFile", @"../../Input/TransitLineTruth.csv", "The file that contains the boardings on transit lines." )]
        public string TruthFile;

        private static Tuple<byte, byte, byte> Colour = new Tuple<byte, byte, byte>( 100, 200, 100 );

        private static char[] Comma = new char[] { ',' };

        private static int SummeryNumber = 0;

        private float BestRunError = float.MaxValue;

        private volatile bool Exit = false;

        private bool FirstRun = false;

        private ParameterSetting[] Parameters;

        private TransitLine[] Truth;

        public int CurrentIteration
        {
            get;
            set;
        }

        public string InputBaseDirectory
        {
            get;
            set;
        }

        [DoNotAutomate]
        public List<IModeChoiceNode> Modes
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        [SubModelInformation( Description = "The network model that we want to estimate", Required = true )]
        public INetworkAssignment NetworkAssignment { get; set; }

        [DoNotAutomate]
        public IList<INetworkData> NetworkData { get { return null; } }

        public string OutputBaseDirectory
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
            get { return Colour; }
        }

        [DoNotAutomate]
        public List<IPurpose> Purpose
        {
            get;
            set;
        }

        public int TotalIterations
        {
            get;
            set;
        }

        [DoNotAutomate]
        public IZoneSystem ZoneSystem
        {
            get;
            set;
        }

        public bool ExitRequest()
        {
            this.Exit = true;
            this.EstimationAI.CancelExploration();
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( !File.Exists( this.ParameterInstructions ) )
            {
                error = "The file \"" + this.ParameterInstructions + "\" was not found!";
                return false;
            }
            return true;
        }

        public void Start()
        {
            InitializeAssignment();
            LoadParameterInstructions();
            this.FirstRun = true;
            if ( this.Client != null )
            {
                this.Client.RegisterCustomSender( this.ResultPort, new Action<object, Stream>( delegate(object o, Stream s)
                    {
                        var results = o as float[];
                        if ( results == null ) return;
                        var length = results.Length;
                        BinaryWriter writer = new System.IO.BinaryWriter( s );
                        for ( int i = 0; i < length; i++ )
                        {
                            writer.Write( results[i] );
                        }
                        writer = null;
                    } ) );
            }
            for ( int run = 0; run < this.NumberOfRuns; run++ )
            {
                float currentPoint = (float)run / this.NumberOfRuns;
                float inverse = 1f / this.NumberOfRuns;
                this.Progress = 0;
                this.EstimationAI.Explore( this.Parameters, () => this.Progress = currentPoint + ( inverse * this.EstimationAI.Progress ), this.EvaluteParameters );
                this.Progress = currentPoint + inverse;
                if ( this.Exit ) break;
            }
        }

        private float EvaluteParameters(ParameterSetting[] parameters)
        {
            SetupInputFiles( parameters );
            this.NetworkAssignment.RunNetworkAssignment();
            return ProcessResults( parameters );
        }

        private void InitializeAssignment()
        {
            // Get all of the initial ground truth data
            List<TransitLine> truthList = new List<TransitLine>();
            // On the first pass go through all of the data and store the records of the TTS boardings
            using ( StreamReader reader = new StreamReader( this.TruthFile ) )
            {
                string line;
                while ( ( line = reader.ReadLine() ) != null )
                {
                    var split = line.Split( Comma, StringSplitOptions.RemoveEmptyEntries );
                    TransitLine current = new TransitLine();
                    string currentName;
                    current.ID = new string[] { ( currentName = split[1] ) };
                    current.Bordings = float.Parse( split[0] );
                    if ( split.Length > 2 )
                    {
                        current.Mode = split[2][0];
                    }
                    else
                    {
                        current.Mode = 'b';
                    }
                    // Check to make sure that there isn't another ID with this name already
                    int count = truthList.Count;
                    for ( int j = 0; j < count; j++ )
                    {
                        if ( truthList[j].ID[0] == currentName )
                        {
                            throw new XTMFRuntimeException( String.Format( "The TTS record {0} at line {1} has a duplicate entry on line {2}", currentName, j + 1, count + 1 ) );
                        }
                    }
                    truthList.Add( current );
                }
            }
            // now on the second pass go through and find all of the EMME Links that connect to the TTS data
            var truthEntries = truthList.Count;
            List<string>[] nameLinks = new List<string>[truthEntries];
            using ( StreamReader reader = new StreamReader( this.EMMEToTTSFile ) )
            {
                string line;
                while ( ( line = reader.ReadLine() ) != null )
                {
                    var split = line.Split( Comma, StringSplitOptions.RemoveEmptyEntries );
                    string ttsName = split[0];
                    string emmeName = split[1];
                    for ( int i = 0; i < truthEntries; i++ )
                    {
                        if ( truthList[i].ID[0] == ttsName )
                        {
                            List<string> ourList;
                            if ( ( ourList = nameLinks[i] ) == null )
                            {
                                nameLinks[i] = ourList = new List<string>();
                            }
                            ourList.Add( emmeName );
                            break;
                        }
                    }
                }
            }
            // Now on the third pass we go through and apply all of the EMME ID's
            for ( int i = 0; i < truthEntries; i++ )
            {
                List<string> nameList;
                if ( ( nameList = nameLinks[i] ) == null )
                {
                    throw new XTMFRuntimeException( String.Format( "The TTS record {0} has no EMME Links associated with it.  Aborting.", truthList[i].ID[0] ) );
                }
                else
                {
                    var temp = truthList[i];
                    temp.ID = nameList.ToArray();
                    truthList[i] = temp;
                }
            }
            this.Truth = truthList.ToArray();
            truthList = null;
        }

        private void LoadParameterInstructions()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load( this.ParameterInstructions );
            List<ParameterSetting> parameters = new List<ParameterSetting>();
            foreach ( XmlNode child in doc["Root"].ChildNodes )
            {
                if ( child.Name == "Parameter" )
                {
                    ParameterSetting current = new ParameterSetting();
                    current.ParameterName = child.Attributes["Name"].InnerText;
                    current.MSNumber = int.Parse( child.Attributes["MS"].InnerText );
                    current.Start = float.Parse( child.Attributes["Start"].InnerText );
                    current.Stop = float.Parse( child.Attributes["Stop"].InnerText );
                    current.Current = current.Start;
                    parameters.Add( current );
                }
            }
            this.Parameters = parameters.ToArray();
        }

        private void PrintSummery(float[] aggToTruth, List<KeyValuePair<string, float>> Orphans)
        {
            using ( StreamWriter writer = new StreamWriter( "LineSummery" + ( SummeryNumber++ ) + ".csv" ) )
            {
                writer.WriteLine( "Truth,Predicted,Error,Error^2,EmmeLines" );
                for ( int i = 0; i < aggToTruth.Length; i++ )
                {
                    float error = aggToTruth[i] - this.Truth[i].Bordings;
                    writer.Write( this.Truth[i].Bordings );
                    writer.Write( ',' );
                    writer.Write( aggToTruth[i] );
                    writer.Write( ',' );
                    writer.Write( error );
                    writer.Write( ',' );
                    writer.Write( error * error );
                    for ( int j = 0; j < this.Truth[i].ID.Length; j++ )
                    {
                        writer.Write( ',' );
                        writer.Write( this.Truth[i].ID[j] );
                    }
                    writer.WriteLine();
                }
                writer.WriteLine();
                writer.WriteLine();
                writer.WriteLine( "Orphans" );
                foreach ( var orphan in Orphans )
                {
                    writer.Write( orphan.Value );
                    writer.Write( ',' );
                    writer.WriteLine( orphan.Key );
                }
            }
        }

        private float ProcessResults(ParameterSetting[] param)
        {
            TransitLines currentLines = new TransitLines( this.MacroOutputFile );
            var predicted = currentLines.Lines;
            var numberOfLines = predicted.Length;
            double rmse = 0;
            double mabs = 0;
            double terror = 0;
            float[] aggToTruth = new float[this.Truth.Length];
            List<KeyValuePair<string, float>> Orphans = new List<KeyValuePair<string, float>>();
            for ( int i = 0; i < numberOfLines; i++ )
            {
                int index = -1;
                bool orphan = true;
                for ( int j = 0; j < this.Truth.Length; j++ )
                {
                    bool found = false;
                    foreach ( var line in predicted[i].ID )
                    {
                        if ( this.Truth[j].ID.Contains( line ) )
                        {
                            index = j;
                            found = true;
                            break;
                        }
                    }
                    if ( found )
                    {
                        orphan = false;
                        aggToTruth[j] += predicted[i].Bordings;
                        break;
                    }
                }
                if ( orphan )
                {
                    Orphans.Add( new KeyValuePair<string, float>( predicted[i].ID[0], predicted[i].Bordings ) );
                }
            }

            for ( int i = 0; i < this.Truth.Length; i++ )
            {
                var error = aggToTruth[i] - this.Truth[i].Bordings;
                rmse += error * error;
                mabs += Math.Abs( error );
                terror += error;
            }
            var value = this.EstimationAI.UseComplexErrorFunction ? this.EstimationAI.ComplexErrorFunction( this.Parameters, this.Truth, predicted, aggToTruth ) : this.EstimationAI.ErrorCombinationFunction( rmse, mabs, terror );
            if ( value < this.BestRunError )
            {
                SaveBordingData( aggToTruth, Orphans );
                this.BestRunError = value;
            }
            SaveEvaluation( param, value, rmse, mabs, terror );
            return value;
        }

        private void SaveBordingData(float[] aggToTruth, List<KeyValuePair<string, float>> Orphans)
        {
            File.Copy( this.MacroOutputFile, "Best-" + Path.GetFileName( this.MacroOutputFile ), true );
            PrintSummery( aggToTruth, Orphans );
        }

        private void SaveEvaluation(ParameterSetting[] param, float value, double rmse, double mabs, double terror)
        {
            if ( this.Client != null )
            {
                var paramLength = param.Length;
                float[] results = new float[paramLength + 4];
                for ( int i = 0; i < paramLength; i++ )
                {
                    results[i] = param[i].Current;
                }
                results[paramLength] = value;
                results[paramLength + 1] = (float)rmse;
                results[paramLength + 2] = (float)mabs;
                results[paramLength + 3] = (float)terror;
                this.Client.SendCustomMessage( results, this.ResultPort );
                results = null;
            }
            bool exists = File.Exists( this.EvaluationFile );
            using ( StreamWriter writer = new StreamWriter( this.EvaluationFile, true ) )
            {
                if ( !exists )
                {
                    writer.Write( param[0].ParameterName );
                    for ( int i = 1; i < param.Length; i++ )
                    {
                        writer.Write( ',' );
                        writer.Write( param[i].ParameterName );
                    }
                    writer.Write( ',' );
                    writer.Write( "Value" );
                    writer.Write( ',' );
                    writer.Write( "rmse" );
                    writer.Write( ',' );
                    writer.Write( "mabs" );
                    writer.Write( ',' );
                    writer.WriteLine( "terror" );
                }
                if ( this.FirstRun && exists )
                {
                    writer.WriteLine();
                }
                this.FirstRun = false;
                writer.Write( param[0].Current );
                for ( int i = 1; i < param.Length; i++ )
                {
                    writer.Write( ',' );
                    writer.Write( param[i].Current );
                }
                writer.Write( ',' );
                writer.Write( value );
                writer.Write( ',' );
                writer.Write( rmse );
                writer.Write( ',' );
                writer.Write( mabs );
                writer.Write( ',' );
                writer.WriteLine( terror );
            }
        }

        private void SetupInputFiles(ParameterSetting[] param)
        {
            /*
             * t matrices
             * m ms[MS:##] [NAME]
             *  all all: [VALUE]
             */
            using ( StreamWriter writer = new StreamWriter( this.MacroInputFile ) )
            {
                writer.WriteLine( "t matrices" );
                foreach ( var p in param )
                {
                    writer.WriteLine( String.Format( "m ms{0} {1}", p.MSNumber, p.ParameterName ) );
                    writer.WriteLine( String.Format( " all all: {0}", p.Current ) );
                }
            }
        }
    }
}