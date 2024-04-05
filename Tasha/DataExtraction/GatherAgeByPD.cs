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
using System.Data;
using XTMF;
using TMG;
using TMG.Input;
using Datastructure;
using System.IO;
namespace Tasha.DataExtraction;

public class GatherAgeByPD : ISelfContainedModule
{
    [SubModelInformation( Required = true, Description = "A resource containing the zone system." )]
    public IResource ZoneSystem;

    [SubModelInformation( Required = true, Description = "A resource containing a connection to the database." )]
    public IResource DatabaseConnection;

    [SubModelInformation( Required = true, Description = "The name of the file to save to in csv format." )]
    public FileLocation OutputFileName;

    [RunParameter( "Age Sets", "0-10,11-15,16-18,19-25,26-30,31-100", typeof( RangeSet ), "The different age categories to break the population into." )]
    public RangeSet AgeSets;

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

    [RunParameter( "Age Column", "Age", "The name of the column that represents the age of the person." )]
    public string AgeColumn;

    public void Start()
    {
        var zones = ZoneSystem.AcquireResource<IZoneSystem>().ZoneArray;
        var numberOfZones = zones.GetFlatData().Length;
        var connection = DatabaseConnection.AcquireResource<IDbConnection>();
        float[][] populationByAge;
        using ( var command = connection.CreateCommand() )
        {
            populationByAge = new float[AgeSets.Count][];
            FillInPopulationByZone( zones, numberOfZones, command, populationByAge );
        }
        WriteOutData( populationByAge, zones );
    }

    private void WriteOutData(float[][] populationByAge, SparseArray<IZone> zones)
    {
        var pdData = new SparseArray<float>[AgeSets.Count];
        BuildPlanningDistrictData( populationByAge, zones, pdData );
        NormalizeData( pdData );
        SaveData( pdData );
    }

    private void SaveData(SparseArray<float>[] pdData)
    {
        var pdIndexes = pdData[0].ValidIndexArray();
        using var writer = new StreamWriter(OutputFileName.GetFilePath());
        writer.WriteLine("PD,AgeCategory,ExpandedPopulation");
        for (int i = 0; i < pdData.Length; i++)
        {
            var pdArray = pdData[i];
            for (int j = 0; j < pdIndexes.Length; j++)
            {
                writer.Write(pdIndexes[j]);
                writer.Write(',');
                writer.Write(i);
                writer.Write(',');
                writer.WriteLine(pdArray[pdIndexes[j]]);
            }
        }
    }

    private static void BuildPlanningDistrictData(float[][] populationByAge, SparseArray<IZone> zones, SparseArray<float>[] pdData)
    {
        pdData[0] = TMG.Functions.ZoneSystemHelper.CreatePdArray<float>( zones );
        for ( int i = 1; i < pdData.Length; i++ )
        {
            pdData[i] = pdData[0].CreateSimilarArray<float>();
        }
        var flatZones = zones.GetFlatData();
        for ( int i = 0; i < populationByAge.Length; i++ )
        {
            //the first step is to clear out the data
            var array = populationByAge[i];
            var pdArray = pdData[i];
            for ( int j = 0; j < array.Length; j++ )
            {
                pdArray[flatZones[j].PlanningDistrict] += array[j];
            }
        }
    }

    private static void NormalizeData(SparseArray<float>[] pdData)
    {
        var numberOfPD = pdData[0].GetFlatData().Length;
        for ( int i = 0; i < numberOfPD; i++ )
        {
            var total = 0.0f;
            for ( int j = 0; j < pdData.Length; j++ )
            {
                total += pdData[j].GetFlatData()[i];
            }
            var factor = 1 / total;
            if ( float.IsNaN( factor ) | float.IsInfinity( factor ) )
            {
                continue;
            }
            for ( int j = 0; j < pdData.Length; j++ )
            {
                pdData[j].GetFlatData()[i] *= factor;
            }
        }
    }

    private void FillInPopulationByZone(SparseArray<IZone> zones, int numberOfZones, IDbCommand command, float[][] populationByAge)
    {
        for ( int i = 0; i < AgeSets.Count; i++ )
        {
            populationByAge[i] = new float[numberOfZones];
            command.CommandText =
            String.Format( @"SELECT [{3}].[{0}], SUM([{2}].[{1}])
FROM [{2}] INNER JOIN [{3}] ON
[{2}].[{4}] = [{3}].[{4}] AND [{2}].[{5}] = [{3}].[{5}] 
WHERE [{2}].[{5}] = {6} AND [{3}].[{7}] = {8} AND [{2}].[{9}] >= {10} AND [{2}].[{9}] <= {11}
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
                    ZoneSystemNumber,
                //9
                    AgeColumn,
                //10
                    AgeSets[i].Start,
                //11
                AgeSets[i].Stop );
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var zone = reader.GetInt32(0);
                var index = zones.GetFlatIndex(zone);
                if (index >= 0)
                {
                    populationByAge[i][index] = (float)reader.GetDouble(1);
                }
            }
        }
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
        if ( !DatabaseConnection.CheckResourceType<IDbConnection>() )
        {
            error = "In '" + Name + "' the database connection resource does not contain a database connection!\r\n"
                + " Instead it contains '" + DatabaseConnection.GetResourceType() + "'!";
            return false;
        }
        if ( !ZoneSystem.CheckResourceType<IZoneSystem>() )
        {
            error = "In '" + Name + "' the zone system resource does not contain a zone system!\r\n"
                + " Instead it contains '" + ZoneSystem.GetResourceType() + "'!";
            return false;
        }
        return true;
    }

    public override string ToString()
    {
        return "Getting Age Rates";
    }
}
