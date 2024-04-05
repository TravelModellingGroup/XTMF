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
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.V2.Generation
{
    public class SchoolGeneration : IDemographicCategoryGeneration
    {
        [Parameter( "Age", 0, "The age category that this generation will be generating for." )]
        public int Age;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        [RunParameter( "Save Production To File", "", typeof( FileFromOutputDirectory ), "Leave this blank to not save, otherwise enter in the file name." )]
        public FileFromOutputDirectory SaveProduction;

        internal SparseTriIndex<float> StudentDailyRates;
        internal SparseTriIndex<float> StudentTimeOfDayRates;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Generate(SparseArray<float> production, SparseArray<float> attractions)
        {
            var ages = Root.Demographics.AgeRates;
            var studentRates = Root.Demographics.SchoolRates.GetFlatData();
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var prod = production.GetFlatData();

            for ( int i = 0; i < zones.Length; i++ )
            {
                // this is only null for externals
                var studentRatesForZone = studentRates[i];
                if ( studentRatesForZone == null )
                {
                    // if it is an external ignore
                    prod[i] = 0f;
                }
                else
                {
                    // otherwise compute the production
                    var pd = zones[i].PlanningDistrict;
                    prod[i] = zones[i].Population * ages[zones[i].ZoneNumber, Age] * studentRatesForZone[Age, 0] *
                        StudentDailyRates[pd, 0, Age] * StudentTimeOfDayRates[pd, 0, Age];
                }
            }

            SaveProductionToDisk( zones, prod );
        }

        public void InitializeDemographicCategory()
        {
            // we don't need to do any mode choice initialization since this will have a fixed mode split
        }

        public bool IsContained(IPerson person)
        {
            return person.StudentStatus > 0;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void SaveProductionToDisk(IZone[] zones, float[] prod)
        {
            if ( SaveProduction.ContainsFileName() )
            {
                var first = !File.Exists( SaveProduction.GetFileName() );
                using StreamWriter writer = new(SaveProduction.GetFileName(), true);
                if (first)
                {
                    writer.WriteLine("Age,Zone,Production");
                }

                for (int i = 0; i < zones.Length; i++)
                {
                    writer.Write(Age);
                    writer.Write(',');
                    writer.Write(zones[i].ZoneNumber);
                    writer.Write(',');
                    writer.Write(prod[i]);
                    writer.WriteLine();
                }
            }
        }
    }
}