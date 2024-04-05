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
using TMG;
using TMG.Input;
using XTMF;
using TMG.Functions;
using Datastructure;
using System.IO;
namespace Tasha.DataExtraction;

public class GatherOccupationByEmpStatByPD : ISelfContainedModule
{

    [SubModelInformation( Required = true, Description = "A resource containing the zone system." )]
    public IResource ZoneSystem;

    [SubModelInformation( Required = true, Description = "A resource containing a connection to the database." )]
    public IResource DatabaseConnection;

    [RunParameter( "TTSYear", 2011, "Which TTSYear should we use?" )]
    public int TTSYear;

    [RunParameter( "Zone System", 2006, "Which zone system should we load?" )]
    public int ZoneSystemNumber;

    [SubModelInformation( Required = true, Description = "The location to save the results as a .csv file." )]
    public FileLocation OutputFile;

    public void Start()
    {
        var connection = DatabaseConnection.AcquireResource<IDbConnection>();
        var zones = ZoneSystem.AcquireResource<IZoneSystem>().ZoneArray;
        using var command = connection.CreateCommand();
        // Gather the data
        var fullTime = Execute('F', command, zones);
        var partTime = Execute('P', command, zones);
        // normalize the data
        Normalize(fullTime);
        Normalize(partTime);
        // save the data
        SaveData(fullTime, partTime);
    }

    /// <summary>
    /// Normalize the sparse array across rows.
    /// </summary>
    /// <param name="employmentData">The data to normalizes</param>
    private void Normalize(SparseArray<float[]> employmentData)
    {
        var data = employmentData.GetFlatData();
        for ( int i = 0; i < data.Length; i++ )
        {
            var row = data[i];
            if ( row != null )
            {
                Normalize( row );
            }
        }
    }

    /// <summary>
    /// Normalize the row
    /// </summary>
    /// <param name="row">The row to normalize, must not be null</param>
    private void Normalize(float[] row)
    {
        var total = 0.0f;
        for ( int i = 0; i < row.Length; i++ )
        {
            total += row[i];
        }
        if ( total > 0 )
        {
            for ( int i = 0; i < row.Length; i++ )
            {
                row[i] /= total;
            }
        }
    }

    /// <summary>
    /// Save both full time and part time data to file.
    /// </summary>
    /// <param name="fullTime">The full time data</param>
    /// <param name="partTime">The part time data</param>
    private void SaveData(SparseArray<float[]> fullTime, SparseArray<float[]> partTime)
    {
        using var writer = new StreamWriter(OutputFile);
        writer.WriteLine("EmpStat,PD,Occ,Probability");
        WriteData(writer, fullTime, '1');
        WriteData(writer, partTime, '2');
    }

    /// <summary>
    /// Save the data from the given split data to the given file as CSV.
    /// EmpStat,
    /// </summary>
    /// <param name="writer">The stream to write to.</param>
    /// <param name="splitData">The data to use</param>
    /// <param name="empCode">The empStat code to dump</param>
    private void WriteData(StreamWriter writer, SparseArray<float[]> splitData, char empCode)
    {
        var data = splitData.GetFlatData();
        for ( int i = 0; i < data.Length; i++ )
        {
            var row = splitData[i];
            if ( row != null )
            {
                // buffer as much of the header ahead of time to help performance
                var pdStr = string.Concat( empCode, ",", splitData.GetFlatIndex( i ), "," );
                for ( int j = 0; j < row.Length; j++ )
                {
                    writer.Write( pdStr );
                    writer.Write( j + 1 );
                    writer.Write( ',' );
                    writer.WriteLine( row[j] );
                }
            }
        }
    }

    /// <summary>
    /// Gather the data for each PD from the database
    /// </summary>
    /// <param name="employmentStatusChar">The employment code we want to look at</param>
    /// <param name="command">The command / connection to use</param>
    /// <param name="zones">The zone system to extract PD's for</param>
    /// <returns>The occupation data stored per PD</returns>
    private SparseArray<float[]> Execute(char employmentStatusChar, IDbCommand command, SparseArray<IZone> zones)
    {
        var pds = ZoneSystemHelper.CreatePdArray<float[]>( zones );
        command.CommandText = @"
SELECT Persons.EmploymentStatus, PlanningDistrict.PD, Persons.Occupation, SUM(Persons.ExpansionFactor) AS ExpandedPersons
FROM (((Households INNER JOIN Persons ON Households.TTSYear = Persons.TTSYear AND Households.HouseholdId = Persons.HouseholdId)
	INNER JOIN HouseholdZones ON Households.TTSYear = HouseholdZones.TTSYear AND Households.HouseholdId = HouseholdZones.HouseholdId)
	INNER JOIN PlanningDistrict ON HouseholdZones.ZoneSystem = PlanningDistrict.ZoneSystem AND HouseholdZones.Zone = PlanningDistrict.Zone)
" + "WHERE Households.TTSYear = " + TTSYear + " AND HouseholdZones.ZoneSystem = " + ZoneSystemNumber + " AND (Persons.EmploymentStatus = '" + employmentStatusChar + "' )"
    + @" AND Persons.Occupation <> '9'
GROUP BY Persons.EmploymentStatus, PlanningDistrict.PD, Persons.Occupation
ORDER BY Persons.EmploymentStatus ASC, PlanningDistrict.PD ASC, Persons.Occupation ASC;
";
        using ( var reader = command.ExecuteReader() )
        {
            while ( reader.Read() )
            {
                var pd = reader.GetInt32( 1 );
                var occ = reader.GetString( 2 );
                bool exists = true;
                var data = pds[pd];
                if ( data == null )
                {
                    if ( pds.ContainsIndex( pd ) )
                    {
                        exists = false;
                        data = new float[4];
                    }
                    else
                    {
                        continue;
                    }
                }
                int index;
                switch ( occ )
                {
                    case "P":
                        index = 0;
                        break;
                    case "G":
                        index = 1;
                        break;
                    case "S":
                        index = 2;
                        break;
                    case "M":
                        index = 3;
                        break;
                    default:
                        continue;
                }
                data[index] = (float)reader.GetDouble( 3 );
                if ( !exists )
                {
                    pds[pd] = data;
                }
            }
        }
        return pds;
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
        return "Gathering Occupation Data for each occpation by planning district.";
    }
}
