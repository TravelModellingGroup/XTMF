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
using System.Text;
using System.Xml;
using TMG.Emme;
using XTMF;
using XTMF.Networking;

namespace TMG.NetworkEstimation
{
    public class GeneticNetworkEstimationClient : I4StepModel
    {
        [SubModelInformation( Description = "The AI that will be used to evaluate the given configuration", Required = true )]
        public INetworkEstimationAI AI;

        public IClient Client;

        [RunParameter( "Emme Column Size", 6, "The amount of data that we can fit into a column for emme." )]
        public int EmmeColumnSize;

        [RunParameter( "EMME To TTS", @"../../Input/EMMEToTTS.csv", "CSV file to link EMME Lines to the TTS data." )]
        public string EMMEToTTSFile;

        [RunParameter( "Emme Input Output", @"D:\EMMENetworks\Test_Transit-1\Database\cache\scalars.311", "The name of the file the macro loads" )]
        public string MacroInputFile;

        [RunParameter( "Emme Macro Output", @"D:\EMMENetworks\Test_Transit-1\Database\cache\boardings_predicted.621", "The name of the file the macro creates" )]
        public string MacroOutputFile;

        [RunParameter( "Parameter Instructions", "../../Input/ParameterInstructions.xml", "Describes which and how the parameters will be estimated." )]
        public string ParameterInstructions;

        [RunParameter( "TruthFile", @"../../Input/TransitLineTruth.csv", "The name of the file the macro creates" )]
        public string TruthFile;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 50, 150, 50 );

        private static char[] Comma = new char[] { ',' };

        private static int SummeryNumber = 0;

        private float BestRunError = float.MaxValue;

        private bool Exit = false;

        private ParameterSetting[] Parameters;

        private MessageQueue<Job> ParametersToProcess;

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
        public IList<INetworkData> NetworkData
        {
            get;
            set;
        }

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
            get { return _ProgressColour; }
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
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( this.Client == null )
            {
                error = "The Genetic Network Estimation Client needs to be run as a remote client.";
                return false;
            }
            return true;
        }

        public void Start()
        {
            using ( this.ParametersToProcess = new MessageQueue<Job>() )
            {
                this.InitializeClient();
                while ( !this.Exit )
                {
                    var job = this.ParametersToProcess.GetMessageOrTimeout( 200 );
                    if ( job != null )
                    {
                        // Process the system, and then return the result back to the server
                        float result = this.ProcessParameters( job );
                        this.Client.SendCustomMessage( new ProcessedResult() { Generation = job.Generation, Index = job.Index, Result = result }, 0 );
                    }
                }
            }
            this.Exit = true;
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

        private void InitializeClient()
        {
            this.LoadInstructions();
            this.InitializeAssignment();
            this.Client.RegisterCustomSender( 0, SendResultToHost );
            this.Client.RegisterCustomReceiver( 1, ReceiveNewParameters );
            this.Client.RegisterCustomMessageHandler( 1, QueueProcessing );
            // send the message to start the chain reaction of processing
            this.Client.SendCustomMessage( new ProcessedResult() { Index = -1 }, 0 );
        }

        private void LoadInstructions()
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

        private float ProcessParameters(Job job)
        {
            // Step 1, figure out our parameters
            var length = job.Parameters.Length;
            for ( int i = 0; i < length; i++ )
            {
                this.Parameters[i].Current = job.Parameters[i];
            }
            this.SetupInputFiles( this.Parameters );
            this.NetworkAssignment.RunNetworkAssignment();
            return this.ProcessResults( this.Parameters );
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
            var value = this.AI.UseComplexErrorFunction ? this.AI.ComplexErrorFunction( this.Parameters, this.Truth, predicted, aggToTruth ) : this.AI.ErrorCombinationFunction( rmse, mabs, terror );
            if ( value < this.BestRunError )
            {
                SaveBordingData( aggToTruth, Orphans );
                this.BestRunError = value;
            }
            return value;
        }

        private void QueueProcessing(object o)
        {
            var job = o as Job;
            this.ParametersToProcess.Add( job );
        }

        private object ReceiveNewParameters(Stream s)
        {
            Job job = new Job(); ;
            BinaryReader reader = new BinaryReader( s );
            job.Generation = reader.ReadInt32();
            job.Index = reader.ReadInt32();
            var length = reader.ReadInt32();
            job.Parameters = new float[length];
            for ( int i = 0; i < length; i++ )
            {
                job.Parameters[i] = reader.ReadSingle();
            }
            reader = null;
            return job;
        }

        private void SaveBordingData(float[] aggToTruth, List<KeyValuePair<string, float>> Orphans)
        {
            File.Copy( this.MacroOutputFile, "Best-" + Path.GetFileName( this.MacroOutputFile ), true );
            PrintSummery( aggToTruth, Orphans );
        }

        private void SendResultToHost(object o, Stream s)
        {
            BinaryWriter writer = new BinaryWriter( s );
            var res = o as ProcessedResult;
            writer.Write( res.Generation );
            writer.Write( res.Index );
            if ( res.Index != -1 )
            {
                writer.Write( res.Result );
            }
            writer = null;
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
                    writer.WriteLine( String.Format( " all all: {0}", this.ToEmmeFloat( p.Current ) ) );
                }
            }
        }

        /// <summary>
        /// Process floats to work with emme
        /// </summary>
        /// <param name="p">The float you want to send</param>
        /// <returns>A limited precision non scientific number in a string</returns>
        private string ToEmmeFloat(float p)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append( (int)p );
            p = p - (int)p;
            if ( p > 0 )
            {
                var integerSize = builder.Length;
                builder.Append( '.' );
                for ( int i = integerSize; i < this.EmmeColumnSize; i++ )
                {
                    p = p * 10;
                    builder.Append( (int)p );
                    p = p - (int)p;
                    if ( p == 0 )
                    {
                        break;
                    }
                }
            }
            return builder.ToString();
        }

        private class Job
        {
            internal int Generation;
            internal int Index;
            internal float[] Parameters;
        }

        private class ProcessedResult
        {
            internal int Generation;
            internal int Index;
            internal float Result;
        }
    }
}