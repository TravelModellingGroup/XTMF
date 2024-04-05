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
using XTMF;
using TMG;
using TMG.Input;
using System.Data;
using Datastructure;

namespace Tasha.DataExtraction
{
    [ModuleInformation( Description =
        "This module is designed to go into a database and extract the population by zone."
        )]
    public class GatherPopulationByZone : ISelfContainedModule
    {
        [SubModelInformation( Required = true, Description = "A resource containing the zone system." )]
        public IResource ZoneSystem;

        [SubModelInformation( Required = true, Description = "A resource containing a connection to the database." )]
        public IResource DatabaseConnection;

        [SubModelInformation( Required = true, Description = "The location to save the results to in csv format." )]
        public FileLocation OutputFile;

        [RunParameter( "Zone System", 2006, "Which zone system should we load?" )]
        public int ZoneSystemNumber;

        [RunParameter( "TTSYear", 2011, "Which TTSYear should we use?" )]
        public int TTSYear;

        [RunParameter( "TTSYear Column", "TTSYear", "The name of the column containing what TTS year it is." )]
        public string TTSYearColumn;

        [RunParameter( "Person's Table", "Persons", "The name of the Person's table." )]
        public string PersonsTable;

        [RunParameter( "Expansion Factor Column Name", "ExpansionFactor", "The name of the expansion factor column." )]
        public string ExpansionFactorColumnName;

        [RunParameter( "Home Zone Table Name", "HouseholdZones", "The name of the table that links zones to household ID's" )]
        public string HomeZoneTableName;

        [RunParameter( "Zone Number Column", "Zone", "The name of the column that gives the zone number for the household." )]
        public string ZoneNumberColumn;

        [RunParameter( "Zone System Column", "ZoneSystem", "The name of the column that identifies the zone system." )]
        public string ZoneSystemColumn;

        [RunParameter( "HouseholdID Column", "HouseholdID", "The name of the column that represents the household's id." )]
        public string HouseholdIDColumn;

        public void Start()
        {
            float[] population;
            var zoneSystem = ZoneSystem.AcquireResource<IZoneSystem>();
            var connection = DatabaseConnection.AcquireResource<IDbConnection>();
            using ( var command = connection.CreateCommand() )
            {
                population = ExtractPopulation( command, zoneSystem.ZoneArray );
            }

            WritePopulation( population, zoneSystem.ZoneArray );
        }

        private void WritePopulation(float[] population, SparseArray<IZone> zones)
        {
            var flatZones = zones.GetFlatData();
            using var writer = new StreamWriter(OutputFile.GetFilePath());
            writer.WriteLine("Zone,Population");
            for (int i = 0; i < population.Length; i++)
            {
                writer.Write(flatZones[i].ZoneNumber);
                writer.Write(',');
                writer.WriteLine(population[i]);
            }
        }

        private float[] ExtractPopulation(IDbCommand command, SparseArray<IZone> zones)
        {
            float[] populationInZone = new float[zones.GetFlatData().Length];
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
                    var index = zones.GetFlatIndex( zoneNumber );
                    if ( index >= 0 )
                    {
                        populationInZone[index] = (float)reader.GetDouble( 1 );
                    }
                }
            }
            return populationInZone;
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
            if ( !DatabaseConnection.CheckResourceType( typeof( IDbConnection ) ) )
            {
                error = "In '" + Name + "' the database connection resource does not contain a database connection!\r\n"
                    + " Instead it contains '" + DatabaseConnection.GetResourceType() + "'!";
                return false;
            }
            if ( !ZoneSystem.CheckResourceType( typeof( IZoneSystem ) ) )
            {
                error = "In '" + Name + "' the zone system resource does not contain a zone system!\r\n"
                    + " Instead it contains '" + ZoneSystem.GetResourceType() + "'!";
                return false;
            }
            return true;
        }

        public override string ToString()
        {
            return "Gathering Population by zone.";
        }
    }
}
