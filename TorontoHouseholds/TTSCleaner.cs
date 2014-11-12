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
using System.Data;
using System.Data.OleDb;
using System.IO;
using Datastructure;
using XTMF;
using Tasha.Common;
namespace TMG.Tasha
{
    [ModuleInformation(
        Description = "This module is used to provide a Clean list of households from a TTS Database. " +
                        "As an input, the module loads an .mdb file which has the TTS records. " +
                        "The procedure than removes 'bad' households. The procedure judges household " +
                        "based on the following cases: \n1. Invalid Household Zones \n2. Invalid destination " +
                        "or origin zones \n3. Invalid employment or school zones \n4. No Return Trip \n5. Invalid " +
                        "purposes (such as Home-Home or don't start at home). As an output, the module " +
                        "produces a list of clean households. "
         )]
    public class TTSCleaner : IModelSystemTemplate
    {
        [RunParameter( "Unknown Zone#", 9999, "The zone number representing data to assume for home zone" )]
        public int AssumeHomeZone;

        [RunParameter( "Exclude External Trips", false, "Exclude the households that have trips that end up in external zones." )]
        public bool ExcludeExternalTrips;

        [RunParameter( "External Ranges", "", typeof( RangeSet ), "The ranges of zones to count as external and exclude." )]
        public RangeSet ExternalRanges;

        [RunParameter( "First Trip in Trip Chain", 1, "What number represents the first trip in a trip chain in your records" )]
        public int FirstTrip;

        [RunParameter( "Home Purpose", "H", "How do you represent a Home Purpose in your Trip records" )]
        public string HomePurpose;

        [RunParameter( "Good Households File Name", "GoodHouseholds.csv", "The filename to save all of the good households to." )]
        public string OutputName;

        private string Status = "Initializing";

        [RunParameter( "SQL Connection String", "", "The connection string to use if you are not using an access database." )]
        public string SQLConnectionString;

        [RunParameter( "Employment Status Column Name", "EmploymentStatus", "The name of the employment status column." )]
        public string EmploymentStatusColumn;
        [RunParameter( "Student Status Column Name", "StudentStatus", "The name of the student status column." )]
        public string StudentStatusColumn;
        [RunParameter( "Occupation Column Name", "Occupation", "The name of the occupation column." )]
        public string OccupationColumn;

        [RunParameter( "Access Database File", "Trips.mdb", "The database file" )]
        public string DataBaseFile { get; set; }

        [RunParameter( "Destination Column Name", "gta01_dest", "The column name representing Destination Zone" )]
        public string DesinationColumn { get; set; }

        [RunParameter( "Employment Zone Column Name", "gta01_emp", "The column name representing Employment Zone" )]
        public string EmploymentColumn { get; set; }

        [RunParameter( "Household Column Name", "hhld_num", "The column name representing Household Numbers" )]
        public string HhldColumn { get; set; }

        [RunParameter( "The Households Table", "Hhld01", "The name of the Household Table" )]
        public string HhldTable { get; set; }

        [RunParameter( "Household Zone Column", "gta01_hhld", "The column name representing Household Zone" )]
        public string HhldZoneColumn { get; set; }

        [RunParameter( "Input Base Directory", "Input", "The base directory for input." )]
        public string InputBaseDirectory { get; set; }

        public string Name
        {
            get;
            set;
        }

        [RunParameter( "Origin Column Name", "gta01_orig", "The column name representing Origin Zone" )]
        public string OriginColumn { get; set; }

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        [RunParameter( "Person Column Name", "pers_num", "The column name representing Persons" )]
        public string PersonColumn { get; set; }

        [RunParameter( "The Persons Table", "Pers01", "The name of the Persons Table" )]
        public string PersTable { get; set; }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        [RunParameter( "Purpose Destination Column", "purp_dest", "The column name representing Purpose Destination" )]
        public string PurposeDColumn { get; set; }

        [RunParameter( "Purpose Origin Column", "purp_orig", "The column name representing Purpose Origin" )]
        public string PurposeOColumn { get; set; }

        [RunParameter( "School Zone Column Name", "gta01_sch", "The column name representing School Zones" )]
        public string SchoolColumn { get; set; }

        [RunParameter( "Trip Column Name", "trip_num", "The column name representing Number of Trips" )]
        public string TripColumn { get; set; }

        [RunParameter( "The Trips Table", "Trips01", "The name of the Trips Table" )]
        public string TripsTable { get; set; }

        [SubModelInformation( Description = "The model that will load all of our zones", Required = true )]
        public IZoneSystem ZoneSystem { get; set; }

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            ZoneSystem.LoadData();
            Dictionary<int, int> BadHouseholds = new Dictionary<int, int>();
            SortedList<int, int> hhlds = null;
            try
            {
                using ( IDbConnection connection = GetConnection() )
                {
                    if ( connection.State == ConnectionState.Closed )
                    {
                        connection.Open();
                    }
                    using ( var command = connection.CreateCommand() )
                    {
                        this.Status = "Removing Bad HHLD Zones";
                        hhlds = RemoveBadHHLDZones( BadHouseholds, command );
                        this.Status = "Removing Unknown Person Attributes";
                        RemoveBadPersonAttributes( BadHouseholds, command );
                        this.Status = "Removing Bad Destinations";
                        RemoveBadDestinationOrigins( BadHouseholds, command );
                        this.Status = "Removing Bad Work Zones";
                        RemoveBadEmploymentSchoolZones( BadHouseholds, command );
                        this.Status = "Removing No Return Trip";
                        RemoveNoReturnTrips( BadHouseholds, command, hhlds );
                        this.Status = "Removing Bad Purpose Origins";
                        RemoveBadPurposeOrigins( BadHouseholds, command );
                    }
                }
            }
            catch ( InvalidOperationException )
            {
                if ( Environment.Is64BitProcess )
                {
                    throw new XTMFRuntimeException( "We were unable to open a connection to access, please make sure the path is correct and you are running at 64bit version of Microsoft Access.\r\nIf you do not have a 64bit version of access please update XTMF into 32Bit mode." );
                }
                else
                {
                    throw new XTMFRuntimeException( "We were unable to open a connection to access, please make sure the path is correct and you are running at 32bit version of Microsoft Access.\r\nIf you do not have a 32bit version of access please update XTMF into 64Bit mode." );
                }
            }
            catch ( OleDbException e )
            {
                throw new XTMFRuntimeException( e.Message );
            }
            this.Status = "Saving Good HHLDs";
            this.Progress = 0;
            try
            {
                using ( StreamWriter writer = new StreamWriter( this.OutputName ) )
                {
                    writer.WriteLine( "Hhld_id" );
                    var length = (float)hhlds.Count;
                    int count = 0;
                    foreach ( var id in hhlds.Keys )
                    {
                        if ( !BadHouseholds.ContainsKey( id ) )
                        {
                            writer.WriteLine( id );
                        }
                        this.Progress = count++ / length;
                    }
                }
            }
            catch ( IOException )
            {
                throw new XTMFRuntimeException( "We encountered a problem trying to save the good households, please ensure the path is correct." );
            }
        }

        private IDbConnection GetConnection()
        {
            if ( String.IsNullOrWhiteSpace( this.DataBaseFile ) )
            {
                return new System.Data.SqlClient.SqlConnection( this.SQLConnectionString );
            }
            else
            {
                return new OleDbConnection( "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + Path.GetFullPath( this.GetFullPath( DataBaseFile ) ) + ";Persist Security Info=False;" );
            }
        }

        public override string ToString()
        {
            return this.Status;
        }

        private static void AddBadHousehold(Dictionary<int, int> BadHouseholds, int Hhld)
        {
            if ( !BadHouseholds.ContainsKey( Hhld ) )
            {
                BadHouseholds.Add( Hhld, Hhld );
            }
        }

        private static int Read32(int column, IDataReader reader)
        {
            int zone;
            try
            {
                zone = reader.GetInt32( column );
            }
            catch
            {
                try
                {
                    zone = reader.GetInt16( column );
                }
                catch
                {
                    try
                    {
                        zone = (int)reader.GetInt64( column );
                    }
                    catch
                    {
                        object o = reader.GetValue( 1 );
                        string str = o.ToString();
                        if ( !int.TryParse( str, out zone ) )
                        {
                            throw new XTMFRuntimeException( "Unable to read a trip number called number called \"" + str + "\"" );
                        }
                    }
                }
            }
            return zone;
        }

        private string GetFullPath(string localPath)
        {
            var fullPath = localPath;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( this.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        private static void AddParameters<T>(IDbCommand command, string name, T value, DbType type)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            param.DbType = type;
        }

        private void RemoveBadPersonAttributes(Dictionary<int, int> BadHouseholds, IDbCommand command)
        {
            this.Progress = 0;
            float total = 0;
            command.CommandText = "SELECT COUNT(*) FROM @PersonTable;";
            AddParameters( command, "@PersonTable", this.PersTable, DbType.String );
            total = (int)command.ExecuteScalar();

            command.CommandText = @"SELECT [@PersonTable].[@HouseholdColumn], [@PersonTable].[@PersonColumn],
[@PersonTable].[@EmploymentStatusColumn], [@PersonTable].[@StudentStatusColumn], [@PersonTable].[@OccupationColumn] FROM [@PersonTable];";
            AddParameters( command, "@HouseholdColumn", this.HhldColumn, DbType.String );
            AddParameters( command, "@PersonColumn", this.PersonColumn, DbType.String );
            AddParameters( command, "@EmploymentStatusColumn", this.EmploymentStatusColumn, DbType.String );
            AddParameters( command, "@StudentStatusColumn", this.StudentStatusColumn, DbType.String );
            AddParameters( command, "@OccupationColumn", this.OccupationColumn, DbType.String );
            using ( var reader = command.ExecuteReader() )
            {
                int current = 0;
                while ( reader.Read() )
                {

                    var Hhld = reader.GetInt32( 0 );
                    var employmentStatus = (TTSEmploymentStatus)( reader.GetString( 2 )[0] );
                    var studentStatus = (StudentStatus)( reader.GetString( 3 )[0] );
                    var occuation = (Occupation)( reader.GetString( 4 )[0] );
                    if ( employmentStatus == TTSEmploymentStatus.Unknown | studentStatus == StudentStatus.Unknown | occuation == Occupation.Unknown )
                    {
                        AddBadHousehold( BadHouseholds, Hhld );
                    }
                    Progress = current++ / total;
                }
            }
            command.Parameters.Clear();
        }

        private void RemoveBadDestinationOrigins(Dictionary<int, int> BadHouseholds, IDbCommand command)
        {
            this.Progress = 0;
            float total = 0;
            command.CommandText = "SELECT COUNT(*) FROM [@TripsTable];";
            AddParameters( command, "@TripsTable", TripsTable, DbType.String );
            total = (int)command.ExecuteScalar();

            command.CommandText = "SELECT [@TripsTable].[@HhldColumn], [@TripsTable].[@OriginColumn], [@TripsTable].[@DesinationColumn] FROM [@TripsTable];";
            AddParameters( command, @"HhldColumn", HhldColumn, DbType.String );
            AddParameters( command, @"OriginColumn", OriginColumn, DbType.String );
            AddParameters( command, @"DesinationColumn", DesinationColumn, DbType.String );
            using ( var reader = command.ExecuteReader() )
            {
                int current = 0;
                while ( reader.Read() )
                {
                    var Hhld = reader.GetInt32( 0 );
                    int Orig = Read32( 1, reader );
                    int Dest = Read32( 2, reader );

                    if ( !( ZoneSystem.ZoneArray.ContainsIndex( Orig ) && ZoneSystem.ZoneArray.ContainsIndex( Dest ) )
                        || ( this.ExcludeExternalTrips & this.ExternalRanges.Contains( Dest ) ) )
                    {
                        AddBadHousehold( BadHouseholds, Hhld );
                    }
                    Progress = current++ / total;
                }
            }
            command.Parameters.Clear();
        }

        private void RemoveBadEmploymentSchoolZones(Dictionary<int, int> BadHouseholds, IDbCommand command)
        {
            this.Progress = 0;
            command.CommandText = "SELECT COUNT(*) FROM [@PersTable";
            AddParameters( command, "@PersTable", PersTable, DbType.String );
            float total = (int)command.ExecuteScalar();
            command.CommandText = "SELECT [@PersTable].[@HhldColumn], [@PersTable].[@EmploymentColumn], [@PersTable].[@SchoolColumn] FROM [@PersTable];";
            //, PersTable, HhldColumn, EmploymentColumn, SchoolColumn );
            AddParameters( command, "@HhldColumn", HhldColumn, DbType.String );
            AddParameters( command, "@EmploymentColumn", EmploymentColumn, DbType.String );
            AddParameters( command, "@SchoolColumn", SchoolColumn, DbType.String );

            using ( var reader = command.ExecuteReader() )
            {
                int current = 0;
                while ( reader.Read() )
                {
                    var Hhld = reader.GetInt32( 0 );
                    var Emp = reader.GetInt32( 1 );
                    var Sch = reader.GetInt32( 2 );

                    if ( ( ( Emp != 0 && Emp != this.AssumeHomeZone && Emp != this.ZoneSystem.RoamingZoneNumber )
                        && !ZoneSystem.ZoneArray.ContainsIndex( Emp ) )
                            || ( ( Sch != 0 && Sch != this.AssumeHomeZone && Sch != this.ZoneSystem.RoamingZoneNumber )
                            && !ZoneSystem.ZoneArray.ContainsIndex( Sch ) )
                        )
                    {
                        AddBadHousehold( BadHouseholds, Hhld );
                    }
                    Progress = current++ / total;
                }
            }
            command.Parameters.Clear();
        }

        private SortedList<int, int> RemoveBadHHLDZones(Dictionary<int, int> BadHouseholds, IDbCommand command)
        {
            var ret = new SortedList<int, int>();
            this.Progress = 0;
            float total = 0;
            command.CommandText = "SELECT COUNT(*) FROM @HhldTable;";
            AddParameters( command, "@HhldTable", HhldTable, DbType.String );
            total = (int)command.ExecuteScalar();
            command.CommandText = "SELECT [@HhldTable].[@HhldColumn], [@HhldTable].[@HhldZoneColumn] FROM [@HhldTable];";
            AddParameters( command, "@HhldColumn", HhldColumn, DbType.String );
            AddParameters( command, "@HhldZoneColumn", HhldZoneColumn, DbType.String );
            int current = 0;
            using ( var reader = command.ExecuteReader() )
            {
                while ( reader.Read() )
                {
                    var Hhld = reader.GetInt32( 0 );
                    int zone = Read32( 1, reader );

                    if ( !ZoneSystem.ZoneArray.ContainsIndex( zone ) )
                    {
                        BadHouseholds.Add( Hhld, Hhld );
                    }
                    ret.Add( Hhld, zone );
                    Progress = current++ / total;
                }
            }
            command.Parameters.Clear();
            return ret;
        }

        private void RemoveBadPurposeOrigins(Dictionary<int, int> BadHouseholds, IDbCommand command)
        {
            this.Progress = 0;
            float total = 0;
            command.CommandText = "SELECT COUNT(*) FROM [@TripsTable];";
            AddParameters( command, "@TripsTable", TripsTable, DbType.String );
            total = (int)command.ExecuteScalar();
            command.CommandText = @"SELECT [@TripsTable].[@HhldColumn], [@TripsTable].[@TripColumn],
[@TripsTable].[@PurposeOColumn], [@TripsTable].[@PurposeDColumn] FROM [@TripsTable];";
            AddParameters( command, "@HhldColumn", HhldColumn, DbType.String );
            AddParameters( command, "@TripColumn", TripColumn, DbType.String );
            AddParameters( command, "@PurposeOColumn", PurposeOColumn, DbType.String );
            AddParameters( command, "@PurposeDColumn", PurposeDColumn, DbType.String );
            using ( var reader = command.ExecuteReader() )
            {
                int current = 0;
                while ( reader.Read() )
                {
                    var Hhld = reader.GetInt32( 0 );
                    int TripNumber = Read32( 1, reader );

                    var PurpOrig = reader.GetString( 2 );
                    var PurpDest = reader.GetString( 3 );
                    if ( TripNumber == 1 && PurpOrig != HomePurpose )
                    {
                        AddBadHousehold( BadHouseholds, Hhld );
                    }
                    else if ( PurpOrig == HomePurpose && PurpDest == HomePurpose )
                    {
                        AddBadHousehold( BadHouseholds, Hhld );
                    }
                    Progress = current++ / total;
                }
            }
            command.Parameters.Clear();
        }

        private void RemoveNoReturnTrips(Dictionary<int, int> BadHouseholds, IDbCommand command, SortedList<int, int> hhlds)
        {
            this.Progress = 0;
            float total = 0;
            command.CommandText = "SELECT COUNT(*) FROM [@TripsTable]";
            AddParameters( command, "@TripsTable", TripsTable, DbType.String );
            total = (int)command.ExecuteScalar();
            command.CommandText = @"SELECT [@TripsTable].[@HhldColumn], [@TripsTable].[@PersonColumn], [@TripsTable].[@TripColumn], [@TripsTable].[@OriginColumn],
[@TripsTable].[@DesinationColumn], [@TripsTable].[@PurposeDColumn] FROM [@TripsTable]
ORDER BY [@TripsTable].[@HhldColumn], [@TripsTable].[@PersonColumn], [@TripsTable].[@TripColumn] ASC;";
            AddParameters( command, "@HhldColumn", HhldColumn, DbType.String );
            AddParameters( command, "@PersonColumn", PersonColumn, DbType.String );
            AddParameters( command, "@TripColumn", TripColumn, DbType.String );
            AddParameters( command, "@OriginColumn", OriginColumn, DbType.String );
            AddParameters( command, "@DesinationColumn", DesinationColumn, DbType.String );
            AddParameters( command, "@PurposeDColumn", PurposeDColumn, DbType.String );

            using ( var reader = command.ExecuteReader() )
            {
                int current = 0;
                int LastHhld = -1;
                int LastPers = -1;
                int LastDest = -1;
                int prevHhldZone = -1;
                int PrevTripNum = -1;
                string prevPurposeDest = null;
                var zoneArray = ZoneSystem.ZoneArray;
                while ( reader.Read() )
                {
                    var Hhld = reader.GetInt32( 0 );
                    var CurrentPers = Read32( 1, reader );

                    var CurrentTripNum = Read32( 2, reader );
                    var origin = Read32( 3, reader );
                    var Dest = Read32( 4, reader );
                    var hhldZone = hhlds[Hhld];
                    var PurpDest = reader.GetString( 5 );
                    if ( LastHhld != -1 )
                    {
                        if ( ( LastHhld != Hhld ) | ( LastPers != CurrentPers ) )
                        {
                            if ( prevHhldZone != LastDest || PrevTripNum == FirstTrip || prevPurposeDest != HomePurpose )
                            {
                                AddBadHousehold( BadHouseholds, LastHhld );
                            }
                        }
                    }
                    if ( ( LastHhld != Hhld ) | ( LastPers != CurrentPers ) )
                    {
                        if ( hhldZone != origin )
                        {
                            AddBadHousehold( BadHouseholds, Hhld );
                        }
                    }
                    prevHhldZone = hhldZone;
                    PrevTripNum = CurrentTripNum;
                    LastHhld = Hhld;
                    LastPers = CurrentPers;
                    LastDest = Dest;
                    prevPurposeDest = PurpDest;
                    Progress = current++ / total;
                }

                if ( !ZoneSystem.ZoneArray.ContainsIndex( LastDest ) || prevHhldZone != LastDest || prevPurposeDest != HomePurpose )
                {
                    AddBadHousehold( BadHouseholds, LastHhld );
                }
            }
            command.Parameters.Clear();
        }
    }
}