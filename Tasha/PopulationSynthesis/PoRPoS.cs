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
using Tasha.Common;
using XTMF;
using TMG;
using TMG.Input;
using Datastructure;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.IO;
using TMG.Functions;

namespace Tasha.PopulationSynthesis
{
    [ModuleInformation(Description =
        @"This module is designed to implement the GTAModel V4 PoRPoS model.")]
    public sealed class PoRPoS : IPreIteration
    {
        [ModuleInformation(Description =
            @"This module is designed to setup a resource with a matrix containing home to school linkages.")]
        public sealed class PoRPoSAgeGroup : IModule
        {
            [RootModule]
            public ITravelDemandModel Root;

            [SubModelInformation(Required = true, Description = "The distribution for the base year.")]
            public IDataSource<SparseTwinIndex<float>> BaseYearData;

            [SubModelInformation(Required = true, Description = "The amount of students living in each zone.")]
            public IDataSource<SparseArray<float>> StudentsByZone;

            [SubModelInformation(Required = true, Description = "The location where the data will be stored.")]
            public IResource Results;

            [SubModelInformation(Required = false, Description = "Optionally save the results of this age group to file (csv).")]
            public FileLocation SaveResults;

            [SubModelInformation(Required = false, Description = "The location to save the factors for testing.")]
            public FileLocation SaveFactors;

            internal void LoadData()
            {
                this.BaseYearData.LoadData();
                this.StudentsByZone.LoadData();
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
                if ( !this.Results.CheckResourceType<SparseTwinIndex<float>>() )
                {
                    error = "In '" + this.Name + "' the results resource is not of type SparseTwinIndex<float>!";
                    return false;
                }
                return true;
            }

            internal void Execute()
            {
                Console.WriteLine( "Processing '" + this.Name + "'" );
                var results = this.Results.AcquireResource<SparseTwinIndex<float>>();
                var data = results.GetFlatData();
                float[] o = this.StudentsByZone.GiveData().GetFlatData();
                var baseData = this.BaseYearData.GiveData().GetFlatData();
                var factors = new ConcurrentDictionary<int, float>();
                var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
                Parallel.For( 0, baseData.Length, (int i) =>
                    {
                        var baseYearRow = baseData[i];
                        float baseYearStudents = VectorHelper.Sum(baseYearRow, 0, baseYearRow.Length);
                        var resultRow = data[i];
                        if ( baseYearStudents > 0 )
                        {
                            var factor = o[i] / baseYearStudents;
                            factors[i] = factor;
                            VectorHelper.Multiply(resultRow, 0, baseYearRow, 0, factor, baseYearRow.Length);
                        }
                        else
                        {
                            // if there were no students then we need to ensure the results are zero
                            Array.Clear(resultRow, 0, resultRow.Length);
                            if ( o[i] == 0.0f )
                            {
                                factors[i] = 1.0f;
                            }
                            else
                            {
                                factors[i] = float.PositiveInfinity;
                            }
                        }
                    } );
                if ( this.SaveResults != null )
                {
                    TMG.Functions.SaveData.SaveMatrix( results, this.SaveResults.GetFilePath() );
                }
                if ( this.SaveFactors != null )
                {
                    using (var writer = new StreamWriter( this.SaveFactors ))
                    {
                        writer.WriteLine( "Zone,Factor" );
                        foreach ( var zoneFactor in from element in factors
                                                    orderby element.Key ascending
                                                    select new { Zone = zones[element.Key].ZoneNumber, Factor = element.Value } )
                        {
                            writer.Write( zoneFactor.Zone );
                            writer.Write( ',' );
                            writer.WriteLine( zoneFactor.Factor );
                        }
                    }
                }
            }
        }

        [SubModelInformation(Required = false, Description = "The different age groups to represent in our model.")]
        public PoRPoSAgeGroup[] AgeGroups;

        public void Execute(int iterationNumber, int totalIterations)
        {
            var ageGroups = this.AgeGroups;
            Console.WriteLine( "Starting Place of Residence place of School" );
            for ( int i = 0; i < ageGroups.Length; i++ )
            {
                ageGroups[i].LoadData();
                ageGroups[i].Execute();
            }
            Console.WriteLine( "Finished Place of Residence place of School" );
        }

        public void Load(int totalIterations)
        {
            var ageGroups = this.AgeGroups;
            for ( int i = 0; i < ageGroups.Length; i++ )
            {
                ageGroups[i].LoadData();
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
            return true;
        }
    }
}
