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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation(Description=@"This module provides a loader for a population. 
This module requires the root module in the model system to be of type ‘ITravelDemandModel’.")]
    // ReSharper disable once InconsistentNaming
    public class GTAPopulation : IPopulation
    {
        [RunParameter( "Pop File Name", "SyntheticPopulation.csv", "The name of the file to use for loading/saving the population." )]
        public string FileName;

        [RootModule]
        public ITravelDemandModel Root;

        public string Name
        {
            get;
            set;
        }

        public SparseArray<IPerson[]> Population
        {
            get;
            set;
        }

        public float Progress
        {
            get; private set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Load()
        {
            Progress = 0;
            var fileName = GetFileName( FileName );
            if ( !File.Exists( fileName ) )
            {
                throw new XTMFRuntimeException(this, String.Format( "The file {0} was not found when trying to load the population!", fileName ) );
            }
            SparseArray<IPerson[]> pop = Root.ZoneSystem.ZoneArray.CreateSimilarArray<IPerson[]>();
            SparseArray<Household[]> households = Root.ZoneSystem.ZoneArray.CreateSimilarArray<Household[]>();
            var flatHouseholds = households.GetFlatData();
            var flatZones = Root.ZoneSystem.ZoneArray.GetFlatData();
            Dictionary<int, List<IPerson>> tempPop = new( flatZones.Length );
            Parallel.For( 0, flatZones.Length, delegate(int i)
            {
                IZone zone = flatZones[i];
                flatHouseholds[i] = new[]
                    {   new Household { Cars = 0, Zone = zone },
                        new Household { Cars = 1, Zone = zone },
                        new Household { Cars = 2, Zone = zone }
                    };
            } );
            using ( CommentedCsvReader reader = new( fileName ) )
            {
                int i = 0;
                var baseStream = reader.BaseStream;
                while ( reader.NextLine() )
                {
                    if ( reader.NumberOfCurrentCells > 9 )
                    {
                        reader.Get(out int zone, 0);
                        reader.Get( out int age, 1 );
                        reader.Get( out int cars, 2 );
                        reader.Get( out int employmentStatus, 5 );
                        reader.Get( out int studentStatus, 6 );
                        reader.Get( out int occupation, 7 );
                        reader.Get( out int driversLicense, 8 );
                        reader.Get( out float expansionFactor, 9 );
                        if (!tempPop.TryGetValue(zone, out List<IPerson> zoneData))
                        {
                            zoneData = tempPop[zone] = new List<IPerson>(10);
                        }
                        zoneData.Add( new Person
                        {
                            Age = age,
                            DriversLicense = driversLicense > 0,
                            EmploymentStatus = employmentStatus,
                            ExpansionFactor = expansionFactor,
                            Household = households[zone][cars],
                            Occupation = occupation,
                            StudentStatus = studentStatus
                        } );
                        if ( i >= 4000 )
                        {
                            i = 0;
                            Progress = (float)baseStream.Position / baseStream.Length;
                        }
                        i++;
                    }
                }
                Progress = 1;
            }
            SetupPopulation( pop, tempPop, flatZones );
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( String.IsNullOrWhiteSpace( FileName ) )
            {
                error = "The population file name was never assigned!";
                return false;
            }
            return true;
        }

        public void Save()
        {
            Output( Root.ZoneSystem.ZoneArray.ValidIndexArray(), GetFileName( FileName ), Population );
        }

        public void Save(string fileName)
        {
            Output( Root.ZoneSystem.ZoneArray.ValidIndexArray(), fileName, Population );
        }

        private string GetFileName(string fileName)
        {
            if ( !Path.IsPathRooted( fileName ) )
            {
                return Path.Combine( Root.InputBaseDirectory, fileName );
            }
            return fileName;
        }

        private void Output(int[] validZones, string fileName, SparseArray<IPerson[]> population)
        {
            var dir = Path.GetDirectoryName( fileName );
            if ( !String.IsNullOrEmpty( dir ) && !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
            FileStream fs = null;
            try
            {
                fs = new FileStream( fileName, FileMode.Create, FileAccess.Write, FileShare.None, 0x4000, true );
                using StreamWriter writer = new(fs);
                fs = null;
                // Print out all of the people
                var length = validZones.Length;
                writer.WriteLine("Zone,Age,Cars,SchoolZone,WorkZone,EmploymentStatus,StudentStatus,Occupation,DriversLicense,ExpansionFactor");
                WriteParallel(validZones, population, writer, length);
            }
            finally
            {
                fs?.Dispose();
            }
        }

        private void SetupPopulation(SparseArray<IPerson[]> pop, Dictionary<int, List<IPerson>> tempPop, IZone[] flatZones)
        {
            var flatPop = pop.GetFlatData();
            Parallel.For( 0, flatZones.Length, delegate(int i)
            {
                if (tempPop.TryGetValue(flatZones[i].ZoneNumber, out List<IPerson> temp))
                {
                    flatPop[i] = temp.ToArray();
                }
                else
                {
                    flatPop[i] = new IPerson[0];
                }
            } );
            Population = pop;
        }

        private void WriteParallel(int[] validZones, SparseArray<IPerson[]> population, StreamWriter writer, int length)
        {
            var output = population.CreateSimilarArray<StringBuilder>();
            var flatOutput = output.GetFlatData();
            var flatPop = population.GetFlatData();
            int current = 0;
            Parallel.For( 0, length,
                delegate(int i)
                {
                    var popInZone = flatPop[i];
                    if ( popInZone == null ) return;
                    StringBuilder builder = new();
                    foreach ( var person in popInZone )
                    {
                        builder.Append( validZones[i] );
                        if ( person == null )
                        {
                            builder.AppendLine( ", PERSON MISSING!" );
                            continue;
                        }
                        builder.Append( ',' );
                        builder.Append( person.Age );
                        builder.Append( ',' );
                        builder.Append( person.Household != null ? person.Household.Cars : 0 );
                        builder.Append( ',' );
                        builder.Append( person.SchoolZone != null ? person.SchoolZone.ZoneNumber : -1 );
                        builder.Append( ',' );
                        builder.Append( person.WorkZone != null ? person.WorkZone.ZoneNumber : -1 );
                        builder.Append( ',' );
                        builder.Append( person.EmploymentStatus );
                        builder.Append( ',' );
                        builder.Append( person.StudentStatus );
                        builder.Append( ',' );
                        builder.Append( person.Occupation );
                        builder.Append( ',' );
                        builder.Append( person.DriversLicense ? 1 : 0 );
                        builder.Append( ',' );
                        builder.Append( person.ExpansionFactor );
                        builder.AppendLine();
                    }
                    flatOutput[i] = builder;
                    Interlocked.Increment( ref current );
                    Progress = (float)current / length;
                } );

            for ( int i = 0; i < length; i++ )
            {
                var sbuilder = flatOutput[i];
                if ( sbuilder != null )
                {
                    writer.Write( sbuilder.ToString() );
                }
            }
            Progress = 1;
        }
    }
}