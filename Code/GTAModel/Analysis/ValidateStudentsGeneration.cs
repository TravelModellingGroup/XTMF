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
using System.IO;
using Datastructure;
namespace TMG.GTAModel.Analysis
{
    public class ValidateStudentsGeneration : ISelfContainedModule
    {
        [RootModule]
        public IDemographicsModelSystemTemplate Root;

        [SubModelInformation( Required = true, Description = "Where to save the analysis. (CSV)" )]
        public FileLocation SaveTo;

        [SubModelInformation( Description = "Used to gather the daily generation rates", Required = true )]
        public IDataSource<SparseTriIndex<float>> LoadDailyRates;

        [SubModelInformation( Description = "Used to gather the period generation rates", Required = true )]
        public IDataSource<SparseTriIndex<float>> LoadTimeOfDayRates;

        public void Start()
        {
            this.Progress = 0f;
            this.LoadDailyRates.LoadData();
            this.LoadTimeOfDayRates.LoadData();
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            var ageRates = this.Root.Demographics.AgeRates.GetFlatData();
            var ageCategories = this.Root.Demographics.AgeCategories.GetFlatData();
            var studentRates = this.Root.Demographics.SchoolRates.GetFlatData();
            var employmentRates = this.Root.Demographics.EmploymentStatusRates.GetFlatData();
            var employmentCategories = this.Root.Demographics.EmploymentStatus.GetFlatData();
            var dailyRates = this.LoadDailyRates.GiveData();
            var timeOfDayRates = this.LoadTimeOfDayRates.GiveData();
            using ( var writer = new StreamWriter( this.SaveTo.GetFilePath() ) )
            {
                writer.WriteLine( "Zone,AgeCategory,EmpStat,Persons" );
                for ( int i = 0; i < ageRates.Length; i++ )
                {
                    var ageRate = ageRates[i];
                    var studentRate = studentRates[i].GetFlatData();
                    var pop = zones[i].Population;
                    var zoneNumber = zones[i].ZoneNumber;
                    var pd = zones[i].PlanningDistrict;
                    var empRate = employmentRates[i].GetFlatData();
                    for ( int age = 0; age < ageRate.Length; age++ )
                    {
                        var agePop = pop * ageRate[age];
                        var stuEmpRate = studentRate[age];
                        var generationRate = dailyRates[pd, age, 0] * timeOfDayRates[pd, age, 0];
                        for ( int emp = 0; emp < stuEmpRate.Length; emp++ )
                        {
                            writer.Write( zoneNumber );
                            writer.Write( ',' );
                            writer.Write( ageCategories[age] );
                            writer.Write( ',' );
                            writer.Write( employmentCategories[emp] );
                            writer.Write( ',' );
                            writer.WriteLine( stuEmpRate[emp] * agePop * empRate[age][emp] * generationRate);
                        }
                    }
                    // Update our progress
                    this.Progress = (float)i / zones.Length;
                }
            }
            this.LoadDailyRates.UnloadData();
            this.LoadTimeOfDayRates.UnloadData();
            this.Progress = 1f;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return null; } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
