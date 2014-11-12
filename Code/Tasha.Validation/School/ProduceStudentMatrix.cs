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
using TMG.Functions;
using System.Threading.Tasks;
using System.Collections.Concurrent;
namespace Tasha.Validation.School
{
    [ModuleInformation(
        Description = @"This module is designed to capture the assignment of student's from household zone to school zone.  This can then be used to validate PoRPoS models.  Built for GTAModelV4.0+.
<br/>
The matrix will be saved in the OD square csv format."
        )]
    public sealed class ProduceStudentMatrix : IPostHousehold, IDisposable
    {

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation( Required = true, Description = "The location to save the file in csv OD matrix format." )]
        public FileLocation Output;

        [RunParameter( "Ages", "0-11", typeof( RangeSet ), "The range of ages to gather together to form the matrix." )]
        public RangeSet Ages;

        private SparseTwinIndex<float> ExpandedStudents;

        private Task SaveTask;

        private BlockingCollection<Assignment> ToSave;

        private struct Assignment
        {
            internal readonly int HouseholdZone;
            internal readonly int SchoolZone;
            internal readonly float Expanded;

            internal Assignment(int householdZone, int schoolZone, float expanded)
            {
                this.HouseholdZone = householdZone;
                this.SchoolZone = schoolZone;
                this.Expanded = expanded;
            }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            var householdZone = household.HomeZone.ZoneNumber;
            var persons = household.Persons;
            lock ( this )
            {
                for ( int i = 0; i < persons.Length; i++ )
                {
                    var schoolZone = persons[i].SchoolZone;
                    if ( schoolZone != null && this.Ages.Contains( persons[i].Age ) )
                    {
                        this.ToSave.Add( new Assignment( householdZone, schoolZone.ZoneNumber, persons[i].ExpansionFactor ) );
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            this.ToSave.CompleteAdding();
            this.SaveTask.Wait();
            TMG.Functions.SaveData.SaveMatrix( this.ExpandedStudents, Output );
        }

        public void Load(int maxIterations)
        {

        }

        ~ProduceStudentMatrix()
        {
            this.Dispose();
        }

        public void IterationStarting(int iteration)
        {
            this.ExpandedStudents = this.Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            this.ToSave = new BlockingCollection<Assignment>();
            this.SaveTask = Task.Factory.StartNew( () =>
                {
                    var students = this.ExpandedStudents;
                    foreach ( var assignment in this.ToSave.GetConsumingEnumerable() )
                    {
                        students[assignment.HouseholdZone, assignment.SchoolZone] += assignment.Expanded;
                    }
                }, TaskCreationOptions.LongRunning );
        }

        public void Dispose()
        {
            if ( this.ToSave != null )
            {
                this.ToSave.CompleteAdding();
                this.ToSave = null;
            }
            this.SaveTask = null;
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
