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
using System.Linq;
using System.Text;
using XTMF;
using TMG;
using TMG.Input;
using Datastructure;
using System.Data;
using System.IO;
namespace Tasha.DataExtraction
{
    [ModuleInformation(Description=
        @"This module is designed to quickly extract the student participation and base year matrix for a given TTSYear.")]
    public class ExtractBaseYearStudentLinkages : ISelfContainedModule
    {
        [RunParameter( "Min Age", 11, "The minimum age for us to include in the matrix." )]
        public int MinAge;

        [RunParameter( "Max Age", 11, "The maximum age for us to include in the matrix, inclusive." )]
        public int MaxAge;

        [SubModelInformation( Required = true, Description = "A resource containing the zone system." )]
        public IResource ZoneSystem;

        [SubModelInformation( Required = true, Description = "A resource containing a connection to the database." )]
        public IResource DatabaseConnection;

        [SubModelInformation( Required = true, Description = "The name of the base year file to save to in csv format." )]
        public FileLocation OutputFileName;

        [SubModelInformation( Required = true, Description = "The name of the file to save the participation rates to." )]
        public FileLocation StudentRateOutput;

        [RunParameter( "Zone System", 2006, "Which zone system should we load?" )]
        public int ZoneSystemNumber;

        [RunParameter( "TTSYear", 2011, "Which TTSYear should we use?" )]
        public int TTSYear;

        [RunParameter( "TTSYear Column", "TTSYear", "The name of the column containing what TTS year it is." )]
        public string TTSYearColumn;

        [RunParameter( "Person's Table", "Persons", "The name of the Person's table." )]
        public string PersonsTable;

        [RunParameter( "Person ID Column", "PersonNumber", "The name of the column that contains the person ID." )]
        public string PersonsIDColumn;

        [RunParameter( "Expansion Factor Column Name", "ExpansionFactor", "The name of the expansion factor column." )]
        public string ExpansionFactorColumnName;

        [RunParameter( "Home Zone Table Name", "HouseholdZones", "The name of the table that links zones to household ID's" )]
        public string HomeZoneTableName;

        [RunParameter( "School Zone Table Name", "SchoolZones", "The name of the table that links zones to household ID's" )]
        public string SchoolZoneTableName;

        [RunParameter( "Zone Number Column", "Zone", "The name of the column that gives the zone number for the household." )]
        public string ZoneNumberColumn;

        [RunParameter( "Zone System Column", "ZoneSystem", "The name of the column that identifies the zone system." )]
        public string ZoneSystemColumn;

        [RunParameter( "HouseholdID Column", "HouseholdID", "The name of the column that represents the household's id." )]
        public string HouseholdIDColumn;

        [RunParameter( "Age Column", "Age", "The name of the column that represents the age of the person." )]
        public string AgeColumn;


        public void Start()
        {
            var connection = this.DatabaseConnection.AquireResource<IDbConnection>();
            var zones = this.ZoneSystem.AquireResource<IZoneSystem>();
            using ( var command = connection.CreateCommand() )
            {
                AddParameter( command, "@TTSYear", TTSYear, DbType.Int32 );
                AddParameter( command, "@ZoneSystemNumber", ZoneSystemNumber, DbType.Int32 );
                AddParameter( command, "@MinimumAge", this.MinAge, DbType.Int32 );
                AddParameter( command, "@MaximumAge", this.MaxAge, DbType.Int32 );

                command.CommandText = String.Format(
                    @"SELECT [{0}].[{1}], [{3}].[SchoolZone], SUM([{2}].[{4}])
FROM ([{2}] INNER JOIN [{0}] ON
[{2}].[{5}] = [{0}].[{5}] 
    AND [{2}].[{6}] = [{0}].[{6}])
INNER JOIN [{3}] ON
[{3}].[{6}] = [{2}].[{6}] AND [{3}].[{5}] = [{2}].[{5}]
    AND [{3}].[{8}] = [{2}].[{8}]
WHERE [{2}].[{6}] = @TTSYear AND
[{3}].[{7}] = @ZoneSystemNumber AND [{0}].[{7}] = @ZoneSystemNumber AND
[{2}].[{9}] >= @MinimumAge AND [{2}].[{9}] <= @MaximumAge
GROUP BY [{0}].[{1}], [{3}].[SchoolZone];",
                    //0
                                                                                this.HomeZoneTableName,
                    //1
                                                                                this.ZoneNumberColumn,
                    //2
                                                                                this.PersonsTable,
                    //3
                                                                                this.SchoolZoneTableName,
                    //4
                                                                                this.ExpansionFactorColumnName,
                    //5
                                                                                this.HouseholdIDColumn,
                    //6
                                                                                this.TTSYearColumn,
                    //7
                                                                                this.ZoneSystemColumn,
                                                                                this.PersonsIDColumn,
                    //9
                                                                                this.AgeColumn
                                                                                );
                var zoneArray = zones.ZoneArray;
                var flatZones = zoneArray.GetFlatData();
                var result = zoneArray.CreateSquareTwinArray<float>();
                using ( var reader = command.ExecuteReader() )
                {
                    while ( reader.Read() )
                    {
                        var homeZone = reader.GetInt32( 0 );
                        var schoolZones = reader.GetInt32( 1 );
                        var exp = reader.GetDouble( 2 );
                        if ( result.ContainsIndex( homeZone, schoolZones ) )
                        {
                            result[homeZone, schoolZones] = (float)exp;
                        }
                    }
                }
                var flatResult = result.GetFlatData();
                using ( var writer = new StreamWriter( this.OutputFileName.GetFilePath() ) )
                {
                    writer.WriteLine( "HomeZone,SchoolZone,People" );
                    for ( int i = 0; i < flatZones.Length; i++ )
                    {
                        var row = flatResult[i];
                        var iAsString = flatZones[i].ZoneNumber.ToString();
                        for ( int j = 0; j < flatZones.Length; j++ )
                        {
                            if ( row[j] > 0 )
                            {
                                writer.Write( iAsString );
                                writer.Write( ',' );
                                writer.Write( flatZones[j].ZoneNumber );
                                writer.Write( ',' );
                                writer.WriteLine( row[j] );
                            }
                        }
                    }
                }
                float[] populationInZone = new float[flatZones.Length];
                //Build SQL request
                command.CommandText =
                    String.Format( @"SELECT [{3}].[{0}], SUM([{2}].[{1}])
FROM [{2}] INNER JOIN [{3}] ON
[{2}].[{4}] = [{3}].[{4}] AND [{2}].[{5}] = [{3}].[{5}] 
WHERE [{2}].[{5}] = {6} AND [{3}].[{7}] = {8}
GROUP BY [{3}].[{0}];",
                    //0
                            ZoneNumberColumn,
                    //1
                            ExpansionFactorColumnName,
                    //2
                            PersonsTable,
                    //3
                            HomeZoneTableName,
                    //4
                            HouseholdIDColumn,
                    //5
                            TTSYearColumn,
                    //6
                            TTSYear,
                    //7
                            ZoneSystemColumn,
                    //8
                            ZoneSystemNumber );
                // process data
                using ( var reader = command.ExecuteReader() )
                {
                    while ( reader.Read() )
                    {
                        // if the zone is in our zone system add them to it
                        var zoneNumber = reader.GetInt32( 0 );
                        var index = zoneArray.GetFlatIndex( zoneNumber );
                        if ( index >= 0 )
                        {
                            populationInZone[index] = (float)reader.GetDouble( 1 );
                        }
                    }
                }
                var pdStudents = TMG.Functions.ZoneSystemHelper.CreatePDArray<float>( zoneArray );
                var pdPopulation = pdStudents.CreateSimilarArray<float>();
                for ( int i = 0; i < flatResult.Length; i++ )
                {
                    var pd = flatZones[i].PlanningDistrict;
                    pdStudents[pd] = pdStudents[pd] + flatResult[i].Sum();
                    pdPopulation[pd] = pdPopulation[pd] + populationInZone[i];
                }
                using ( var writer = new StreamWriter( this.StudentRateOutput.GetFilePath() ) )
                {
                    var flatPdStudents = pdStudents.GetFlatData();
                    var flatPdPopulation = pdPopulation.GetFlatData();
                    var indexes = pdPopulation.ValidIndexArray();
                    writer.WriteLine( "PD,StudentRate" );
                    for ( int i = 0; i < indexes.Length; i++ )
                    {
                        var pop = flatPdPopulation[i];
                        writer.Write( indexes[i] );
                        writer.Write( ',' );
                        if ( pop <= 0 )
                        {
                            writer.WriteLine( '0' );
                        }
                        else
                        {
                            writer.WriteLine( flatPdStudents[i] / pop );
                        }
                    }
                }
            }
        }

        private static void AddParameter<T>(IDbCommand command, string name, T value, DbType type)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            param.DbType = type;
            command.Parameters.Add( param );
        }

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
            if ( !this.DatabaseConnection.CheckResourceType<IDbConnection>() )
            {
                error = "In '" + this.Name + "' the database connection resource does not contain a database connection!\r\n"
                    + " Instead it contains '" + this.DatabaseConnection.GetResourceType() + "'!";
                return false;
            }
            if ( !this.ZoneSystem.CheckResourceType<IZoneSystem>() )
            {
                error = "In '" + this.Name + "' the zone system resource does not contain a zone system!\r\n"
                    + " Instead it contains '" + this.ZoneSystem.GetResourceType() + "'!";
                return false;
            }
            return true;
        }

        public override string ToString()
        {
            return "Extracting base year's student matrix";
        }
    }
}
