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
using System.Text;
using Datastructure;
using XTMF;

namespace DistroCacheMaker
{
    /// <summary>
    ///
    /// </summary>
    public class DistroMaker : IModelSystemTemplate
    {
        [RunParameter( "#Adult Distributions", 6, "The number of different categories for the number of adults." )]
        public int AdultDistributions;

        [RunParameter( "Adult Distribution Output", "AdultDistributions.zfc", "The output file for adult distributions" )]
        public string AdultDistributionsZFC;

        [RunParameter( "NAD Distribution", "SchedulerData/nad_dist.txt", "The frequency for start times for each distribution" )]
        public string adultIn;

        [RunParameter( "Duration Frequency Distribution", "SchedulerData/dur_fre_dist.txt", "The frequency for duration for each distribution" )]
        public string DurationDistribution;

        [RunParameter( "Frequency Distribution", "SchedulerData/fre_dist.txt", "The frequency for each distribution" )]
        public string Frequency;

        [RunParameter( "Frequency Output", "FrequencyDistributions.zfc", "The output file for the frequency distributions" )]
        public string FrequencyLevelsZFC;

        [RunParameter( "Generate If Exists", false, "Regenerate the data even if it already exists?" )]
        public bool GenerateIfExists;

        [RunParameter( "MaxFrequency", 10, "The highest frequency number" )]
        public int HighFrequency;

        [RunParameter( "Adults Fequencies", 9, "The number of adult frequencies" )]
        public int NumberOfAdultFrequencies;

        [RunParameter( "NumberOfDistributions", 262, "The number of distributions" )]
        public int NumberOfDistributions;

        [RootModule]
        public IModelSystemTemplate Root;

        [RunParameter( "Start Time Frequency Distribution", "SchedulerData/sta_fre_dist.txt", "The frequency for start time for each distribution" )]
        public string StartTimeFrequency;

        [RunParameter( "StartTimeQuantums", 96, "The number of different time units in a day" )]
        public int StartTimeQuantums;

        private static Tuple<byte, byte, byte> Colour = new Tuple<byte, byte, byte>( 50, 150, 50 );

        [RunParameter( "Base Input Directory", "../../Input", "Ignore if not the root of the model system. The base input directory for this model system." )]
        public string InputBaseDirectory
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        [RunParameter( "Base Output Directory", "../../Output", "Ignore if not the root of the model system.  The base output directory for this model system." )]
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

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="args"></param>
        public void Start()
        {
            CreateFrequencyDistroCache();
            CreateAdultFrequencyDistroCache();
        }

        private void CreateAdultFrequencyDistroCache()
        {
            if ( !this.GenerateIfExists && File.Exists( this.GetFullPath( this.AdultDistributionsZFC ) ) ) return;
            string temp = Path.GetTempFileName();
            using ( StreamReader reader = new StreamReader( this.GetFullPath( this.adultIn ) ) )
            {
                using ( StreamWriter writer = new StreamWriter( temp ) )
                {
                    //0...num of distrubtions
                    for ( int i = 0; i < this.AdultDistributions; i++ )
                    {
                        StringBuilder sb = new StringBuilder( 1000000 );
                        //convert the data to one line

                        sb.Append( i );
                        for ( int j = 0; j < this.NumberOfAdultFrequencies; j++ )
                        {
                            string line = reader.ReadLine();
                            //skip the first 3 values (they are implied) based on index
                            string[] values = line.Split( ',' );
                            sb.Append( "," );
                            sb.Append( values[3] );
                        }
                        writer.WriteLine( sb );
                    }
                }
            }
            //  ZoneCreator.CsvToZFC(temp, Zone.GetNumberOfZones, 4 * numInternalZones, TashaConfiguration.GetInputFile(directory, "LocationChoiceModelWorkCache"), false);
            if ( File.Exists( this.GetFullPath( this.AdultDistributionsZFC ) ) )
            {
                try
                {
                    File.Delete( this.GetFullPath( this.AdultDistributionsZFC ) );
                }
                catch
                { }
            }
            SparseZoneCreator zc = new SparseZoneCreator( this.AdultDistributions, this.NumberOfAdultFrequencies );
            zc.LoadCSV( temp, false );
            zc.Save( this.GetFullPath( AdultDistributionsZFC ) );
            File.Delete( temp );
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="frequency"></param>
        /// <param name="startTimeFrequency"></param>
        /// <param name="startTime"></param>
        /// <param name="outFrequencyLevels"></param>
        /// <param name="numberOfDistributions"></param>
        /// <param name="highestFrequency"></param>
        /// <param name="startTimeQuantums"></param>
        private void CreateFrequencyDistroCache()
        {
            if ( !this.GenerateIfExists && File.Exists( this.GetFullPath( this.FrequencyLevelsZFC ) ) )
            {
                return;
            }
            string temp = Path.GetTempFileName();
            StreamReader readFrequency;
            StreamReader readStartTimeFrequency;
            StreamReader readStartTime;
            StreamWriter writer;
            using ( writer = new StreamWriter( temp ) )
            using ( readStartTime = new StreamReader( this.GetFullPath( this.DurationDistribution ) ) )
            using ( readStartTimeFrequency = new StreamReader( this.GetFullPath( this.StartTimeFrequency ) ) )
            using ( readFrequency = new StreamReader( this.GetFullPath( this.Frequency ) ) )
            {
                // get rid of the headers
                RemoveHeaders( readFrequency, readStartTime, readStartTimeFrequency );

                StringBuilder line;
                for ( int dist = 0; dist < this.NumberOfDistributions; dist++ )
                {
                    line = new StringBuilder( 20000 );
                    line.Append( dist );
                    line.Append( ',' );

                    WriteFrequencies( line, readFrequency );
                    WriteDurations( line, readStartTime );
                    WriteStartTimeFrequencies( line, readStartTimeFrequency );
                    writer.WriteLine( line.ToString( 0, line.Length - 1 ) );
                }
            }

            var numberOfFrequencies = this.HighFrequency + 1; // it was inclusive
            var numberOfSTF = numberOfFrequencies * this.StartTimeQuantums;
            // there are actually StartTimeQuantums + 1 durations
            var numberOfDurations = ( this.StartTimeQuantums ) * ( this.StartTimeQuantums + 1 );

            int types = numberOfFrequencies + numberOfSTF + numberOfDurations;
            if ( File.Exists( this.FrequencyLevelsZFC ) )
            {
                try
                {
                    File.Delete( this.FrequencyLevelsZFC );
                }
                catch
                { }
            }
            SparseZoneCreator zc = new SparseZoneCreator( this.NumberOfDistributions, types );
            zc.LoadCSV( temp, false );
            zc.Save( this.GetFullPath( this.FrequencyLevelsZFC ) );
            File.Delete( temp );
        }

        private string GetFullPath(string localPath)
        {
            if ( !Path.IsPathRooted( localPath ) )
            {
                return Path.Combine( this.Root.InputBaseDirectory, localPath );
            }
            return localPath;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="readFrequency"></param>
        /// <param name="readStartTime"></param>
        /// <param name="readStartTimeFrequency"></param>
        private void RemoveHeaders(StreamReader readFrequency, StreamReader readStartTime, StreamReader readStartTimeFrequency)
        {
            readFrequency.ReadLine();
            readStartTime.ReadLine();
            readStartTimeFrequency.ReadLine();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="line"></param>
        /// <param name="durationTime"></param>
        private void WriteDurations(StringBuilder line, StreamReader durationTime)
        {
            for ( int startTime = 0; startTime < StartTimeQuantums; startTime++ )
            {
                for ( int duration = 0; duration <= StartTimeQuantums; duration++ )
                {
                    string[] durationSplit = durationTime.ReadLine().Split( ',' );
                    if ( durationSplit.Length <= 3 )
                    {
                        throw new XTMFRuntimeException( "Invalid duration line!" );
                    }
                    line.Append( durationSplit[3] );
                    line.Append( ',' );
                }
            }
        }

        private void WriteFrequencies(StringBuilder line, StreamReader readFrequency)
        {
            //fre_dist3
            for ( int i = 0; i <= HighFrequency; i++ )
            {
                string[] frequencySplit = readFrequency.ReadLine().Split( ',' );
                line.Append( frequencySplit[2] );
                line.Append( ',' );
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="line"></param>
        /// <param name="readFrequency"></param>
        /// <param name="readStartTimeFrequency"></param>
        private void WriteStartTimeFrequencies(StringBuilder line, StreamReader readStartTimeFrequency)
        {
            for ( int fre = 0; fre <= HighFrequency; fre++ )
            {
                for ( int time = 0; time < StartTimeQuantums; time++ )
                {
                    string[] freqStartSplit = readStartTimeFrequency.ReadLine().Split( ',' );
                    line.Append( freqStartSplit[3] );
                    line.Append( ',' );
                }
                //TODO: verify correctness (Nik is guessing)
                //readStartTimeFrequency.ReadLine();
            }
        }
    }
}