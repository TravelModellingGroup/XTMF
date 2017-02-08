/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using TMG.Input;
using XTMF;
using Tasha.Common;
using System.IO;
using Datastructure;
using TMG;
using EmpStatus = TMG.TTSEmploymentStatus;
namespace Tasha.Validation.PopulationSynthesis
{
    public class SocialValidation : IPostHousehold
    {
        [RootModule]
        public ITashaRuntime Root;                

        [SubModelInformation(Required = true, Description = "Where do you want to save the Age Results. Must be in .CSV format.")]
        public FileLocation AgeResults;

        [SubModelInformation(Required = true, Description = "Where do you want to save the Employment/Occupation Results. Must be in .CSV format.")]
        public FileLocation OccEmpResults;

        [SubModelInformation(Required = true, Description = "Where do you want to save the Miscellaneous Results. Must be in .CSV format.")]
        public FileLocation MiscResults;

        //[RunParameter("Age Categories", "0-10,11-15,16-19,20-25,26-30,31-130",
        //    typeof(RangeSet), "The categories to break down age by.")]
        //public RangeSet AgeCategories;

        private float[][][] EmpOccResults = new float[100][][]; 
        private float[][] AgeByZone = new float[100][];
        private float[] GenderCounts = new float[2];
        private float[] LicenceCounts = new float[2];
        private float[] CarCounts = new float[5];
        private SparseArray<IZone> ZoneSystem;

        private TTSEmploymentStatus[] EmploymentCategories = {
            EmpStatus.FullTime,
            EmpStatus.WorkAtHome_FullTime,
            EmpStatus.WorkAtHome_PartTime,
            EmpStatus.NotEmployed,
            EmpStatus.PartTime,
            EmpStatus.Unknown            
        };

        private Occupation[] OccupationCategories =
        {
            Occupation.Office,
            Occupation.Manufacturing,
            Occupation.Professional,
            Occupation.Retail,
            Occupation.Farmer,
            Occupation.NotEmployed,
            Occupation.Unknown
        };        

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock (this)
            {
                foreach (var person in household.Persons)
                {                    
                    float expansionFactor = person.ExpansionFactor;
                    EmpOccResults[person.Age][Array.IndexOf(EmploymentCategories, person.EmploymentStatus)]
                        [Array.IndexOf(OccupationCategories, person.Occupation)] += expansionFactor;

                    if (person.Licence) { LicenceCounts[0] += expansionFactor; } else { LicenceCounts[1] += expansionFactor; }
                    if (person.Male) { GenderCounts[0] += expansionFactor; } else { GenderCounts[1] += expansionFactor; }                    

                    //var ageCategory = AgeByZone.ElementAtOrDefault(AgeCategories.IndexOf(person.Age));
                    //if (ageCategory != null)
                    //{
                    //    AgeByZone.ElementAtOrDefault(AgeCategories.IndexOf(person.Age))[homeZone] += expansionFactor;
                    //}
                }

                if (household.Vehicles.Length >= 4)
                {
                    CarCounts[4] += household.ExpansionFactor;
                }
                else
                {
                    CarCounts[household.Vehicles.Length] += household.ExpansionFactor;
                }
            }
        }        

        public void IterationFinished(int iteration)
        {
            using (StreamWriter writer = new StreamWriter(AgeResults))
            {
                writer.Write("Age, ");
                for (int i = 0; i < ZoneSystem.GetFlatData().Length; i++)
                {
                    writer.Write("{0}, ", ZoneSystem.GetFlatData()[i]);
                }
                writer.WriteLine();

                for (int j = 0; j < AgeByZone.Length; j++)
                {
                    writer.Write("{0},", j);                    
                    for (int k = 0; k < ZoneSystem.GetFlatData().Length; k++)
                    {                       
                        {
                            writer.Write("{0},", AgeByZone[j][k]);
                        }                                      
                    }
                    writer.WriteLine();
                }
            }

            using (StreamWriter writer = new StreamWriter(OccEmpResults))
            {
                writer.Write("Age, Employment, ");

                for (int i = 0; i < OccupationCategories.Length; i++)
                {
                    writer.Write("{0},", OccupationCategories[i]);
                }
                writer.WriteLine();

                for (int i = 0; i < EmpOccResults.Length; i++)
                {
                    for (int j = 0; j < EmploymentCategories.Length; j++)
                    {
                        writer.Write("{0},{1},", i, EmploymentCategories[j]);

                        for (int k = 0; k < OccupationCategories.Length; k++)
                        {
                            writer.Write("{0},", EmpOccResults[i][j][k]);                           
                        }
                        writer.WriteLine();
                    }
                }                
            }

            using (StreamWriter writer = new StreamWriter(MiscResults))
            {
                writer.WriteLine("Gender, Number of People");
                writer.WriteLine("Male, {0}", GenderCounts[0]);
                writer.WriteLine("Female, {0}", GenderCounts[1]);

                writer.WriteLine();

                writer.WriteLine("Licence, Number of People");
                writer.WriteLine("Yes, {0}", LicenceCounts[0]);
                writer.WriteLine("No, {0}", LicenceCounts[1]);

                writer.WriteLine();

                writer.WriteLine("Number of Cars, Number of Households");
                for (int i = 0; i < CarCounts.Length; i++)
                {
                    writer.WriteLine("{0}, {1}", i, CarCounts[i]);
                }
            }
                                       
        }
        public void IterationStarting(int iteration)
        {
            var zones = (ZoneSystem = Root.ZoneSystem.ZoneArray).GetFlatData();
            for (int i = 0; i < AgeByZone.Length; i++)
            {
                AgeByZone[i] = new float[zones.Length].ToArray();
            }

            for (int i = 0; i < EmpOccResults.Length; i++)
            {
                EmpOccResults[i] = new float[8][];
                for (int j = 0; j < EmpOccResults[i].Length; j++)
                {
                    EmpOccResults[i][j] = new float[8];
                }
            }                              
        }

        public void Load(int maxIterations)
        {
        }

        public string Name
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
            get;
            set;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
