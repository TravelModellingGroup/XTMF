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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.V2.Analysis
{
    [ModuleInformation(
        Description =
        @"The module is designed to be used with the V2 model in order to analyse the mobility choice model's rates."
        )]
    public class MobilityAnalysis : ISelfContainedModule
    {
        public string[] AgeTags;

        [RunParameter( "Age Tag String", "2,3,4,5", "The names of the different ages." )]
        public string AgeTagString;

        [RunParameter( "Mobility Cache FileName", "Cache/Mobility", typeof( FileFromOutputDirectory ),
            "The location of the mobility Cache Data to read." )]
        public FileFromOutputDirectory MobilityCacheFile;

        public string[] MobilityTags;

        [RunParameter( "Mobility Tag String", "NoCars,One Car No License,Two Cars No License,One Car With License,Two Cars With License",
            "The names of the different ages." )]
        public string MobilityTagString;

        [RunParameter( "Number of Occupations", 4, "The number of occupations in the model system." )]
        public int NumberOfOccupations;

        [RunParameter( "Report FileName", "Reports/MobilityReport.csv", typeof( FileFromOutputDirectory ),
            "The location of the mobility Cache Data to read." )]
        public FileFromOutputDirectory OutputFileName;

        [RootModule]
        public ITravelDemandModel Root;

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
            BuildTags( AgeTagString, out AgeTags );
            BuildTags( MobilityTagString, out MobilityTags );
            return true;
        }

        public void Start()
        {
            using ( StreamWriter writer = new StreamWriter( OutputFileName.GetFileName() ) )
            {
                writer.WriteLine( "Occupation,Age,MobilityCategory,Total" );
                for ( int i = 0; i < NumberOfOccupations; i++ )
                {
                    ProcessMobilityData( writer, i );
                }
            }
        }

        private static void BuildTags(string csString, out string[] splitString)
        {
            splitString = csString.Split( ',' );
        }

        private FileStream GetMobilityFileName(int occupationIndex)
        {
            var fileName = MobilityCacheFile.GetFileName() + ( occupationIndex + ".bin" );
            try
            {
                return File.OpenRead( fileName );
            }
            catch ( FileNotFoundException )
            {
                throw new XTMFRuntimeException( "In '" + Name + "' we were unable to load a file named '" + fileName + "'!" );
            }
        }

        private void LoadMobilityData(int occupationIndex, IZone[] zones, double[][] ageTotals)
        {
            using ( BinaryReader reader = new BinaryReader( GetMobilityFileName( occupationIndex ) ) )
            {
                var expectedFileSize = sizeof( float ) * AgeTags.Length * MobilityTags.Length * zones.Length * zones.Length;
                if ( reader.BaseStream.Length < expectedFileSize )
                {
                    throw new XTMFRuntimeException( "In '" + Name + "' we were expecting the mobility cache to be '"
                        + expectedFileSize + "' bytes long however it is only '" + reader.BaseStream.Length + "' bytes long!" );
                }
                float[] zoneTemp = new float[zones.Length];
                byte[] tempBuffer = new byte[zones.Length * sizeof( float )];
                // for each age
                for ( int age = 0; age < AgeTags.Length; age++ )
                {
                    var ageRow = ageTotals[age];
                    // for each origin
                    for ( int zonei = 0; zonei < zones.Length; zonei++ )
                    {
                        // for each mobility
                        for ( int mobility = 0; mobility < MobilityTags.Length; mobility++ )
                        {
                            // load in all of the destinations
                            reader.Read( tempBuffer, 0, tempBuffer.Length );
                            Buffer.BlockCopy( tempBuffer, 0, zoneTemp, 0, tempBuffer.Length );
                            double temp = 0;
                            for ( int i = 0; i < zoneTemp.Length; i++ )
                            {
                                temp += zoneTemp[i];
                            }
                            ageRow[mobility] += temp;
                        }
                    }
                }
            }
        }

        private void ProcessMobilityData(StreamWriter writer, int occupationIndex)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            double[][] ageTotals = new double[AgeTags.Length][];
            for ( int i = 0; i < ageTotals.Length; i++ )
            {
                ageTotals[i] = new double[MobilityTags.Length];
            }

            LoadMobilityData( occupationIndex, zones, ageTotals );
            for ( int age = 0; age < AgeTags.Length; age++ )
            {
                for ( int mobility = 0; mobility < MobilityTags.Length; mobility++ )
                {
                    writer.Write( occupationIndex );
                    writer.Write( ',' );
                    writer.Write( AgeTags[age] );
                    writer.Write( ',' );
                    writer.Write( MobilityTags[mobility] );
                    writer.Write( ',' );
                    writer.Write( ageTotals[age][mobility] );
                    writer.WriteLine();
                }
            }
        }
    }
}