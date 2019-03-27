/*
    Copyright 2019 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using TMG.Functions;
using XTMF;

namespace Tasha.PopulationSynthesis
{
    [ModuleInformation(Description = "Assign work zones from a 2D Resource Probability Distribution.")]
    public class AssignWorkZonesFrom2DResourceProbabilities : ICalculation<ITashaPerson, IZone>
    {
        [RootModule]
        public ITravelDemandModel Root;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

        public SparseArray<IZone> _zones;

        public sealed class AssignWorkZoneForOccEmp : IModule
        {

            [ParentModel]
            public AssignWorkZonesFrom2DResourceProbabilities Parent;

            public string Name { get; set; }

            public float Progress => 0f;

            public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

            private float[][] _workCDF;

            [SubModelInformation(Required = true, Description = "The aggregate PoRPoW model for this class.")]
            public IDataSource<SparseTwinIndex<float>> Distribution;

            public void Load()
            {
                var numberOfZones = Parent._zones.Count;
                _workCDF = new float[numberOfZones][];
                Distribution.LoadData();
                var model = Distribution.GiveData().GetFlatData();
                Distribution.UnloadData();
                Parallel.For(0, _workCDF.Length, (int i) =>
                {
                    var modelRow = model[i];
                    var row = _workCDF[i] = new float[modelRow.Length];
                    var total = VectorHelper.Sum(modelRow, 0, modelRow.Length);
                    var invTotal = 1.0f / total;
                    // make sure there is a value to avoid NaN's
                    if (!(float.IsNaN(invTotal) | float.IsInfinity(invTotal)))
                    {
                        var acc = 0.0f;
                        for (int j = 0; j < row.Length; j++)
                        {
                            acc += (row[j] = acc + modelRow[j] * invTotal);
                        }
                    }
                });
            }

            public void Unload()
            {
                _workCDF = null;
            }

            public int PickZone(Random r, int homeZone)
            {
                var pop = (float)r.NextDouble();
                var row = _workCDF[homeZone];
                Vector<float> vPop = new Vector<float>(pop);
                int i = 0;
                // find the first entry with a cdf that is greater than or equal to our pop.
                for (; i < row.Length - Vector<float>.Count; i+=Vector<float>.Count)
                {
                    if(Vector.LessThanOrEqualAny(vPop, new Vector<float>(row, i)))
                    {
                        for (int j = 0; j < Vector<float>.Count; j++)
                        {
                            if(pop <= row[i + j])
                            {
                                return i + j;
                            }
                        }
                    }
                }
                for(; i < row.Length; i++)
                {
                    if(pop <= row[i])
                    {
                        return i;
                    }
                }
                // If we ended up popping a value greater than everything find the last element with a value
                for (i = row.Length - 2; i >= 0; i--)
                {
                    if(row[i] < row[i + 1])
                    {
                        return i + 1;
                    }
                }
                // Check to see if the first zone has any probability, if not then we have an exception.
                if(row[0] <= 0.0f)
                {
                    throw new XTMFRuntimeException(this, $"We are trying to assign a work zone for a person living in" +
                    $" zone number { Parent._zones.GetFlatData()[homeZone].ZoneNumber}, however there are no ");
                }
                return 0;
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Required = true)]
        public AssignWorkZoneForOccEmp ProfessionalFullTime;
        [SubModelInformation(Required = true)]
        public AssignWorkZoneForOccEmp GeneralFullTime;
        [SubModelInformation(Required = true)]
        public AssignWorkZoneForOccEmp SalesFullTime;
        [SubModelInformation(Required = true)]
        public AssignWorkZoneForOccEmp ManufacturingFullTime;
        [SubModelInformation(Required = true)]
        public AssignWorkZoneForOccEmp ProfessionalPartTime;
        [SubModelInformation(Required = true)]
        public AssignWorkZoneForOccEmp GeneralPartTime;
        [SubModelInformation(Required = true)]
        public AssignWorkZoneForOccEmp SalesPartTime;
        [SubModelInformation(Required = true)]
        public AssignWorkZoneForOccEmp ManufacturingPartTime;


        public void Load()
        {
            _zones = Root.ZoneSystem.ZoneArray;
            Parallel.Invoke(
                () => ProfessionalFullTime.Load(),
                () => GeneralFullTime.Load(),
                () => SalesFullTime.Load(),
                () => ManufacturingFullTime.Load(),
                () => ProfessionalPartTime.Load(),
                () => GeneralPartTime.Load(),
                () => SalesPartTime.Load(),
                () => ManufacturingPartTime.Load());

        }

        [RunParameter("Random Seed", 45646132, "The random seed to use to fix the random number generator.")]
        public int RandomSeed;

        public IZone ProduceResult(ITashaPerson person)
        {
            ITashaHousehold household = person.Household;
            var random = new Random(RandomSeed * household.HouseholdId);
            int flatHomeZone = _zones.GetFlatIndex(household.HomeZone.ZoneNumber);
            if(person.EmploymentZone != null)
            {
                return person.EmploymentZone;
            }
            switch (person.EmploymentStatus)
            {
                case TTSEmploymentStatus.FullTime:
                    
                    switch (person.Occupation)
                    {
                        case Occupation.Professional:
                            return _zones.GetFlatData()[ProfessionalFullTime.PickZone(random, flatHomeZone)];
                        case Occupation.Office:
                            return _zones.GetFlatData()[GeneralFullTime.PickZone(random, flatHomeZone)];
                        case Occupation.Retail:
                            return _zones.GetFlatData()[SalesFullTime.PickZone(random, flatHomeZone)];
                        case Occupation.Manufacturing:
                            return _zones.GetFlatData()[ManufacturingFullTime.PickZone(random, flatHomeZone)];
                    }
                    break;
                case TTSEmploymentStatus.PartTime:
                    switch (person.Occupation)
                    {
                        case Occupation.Professional:
                            return _zones.GetFlatData()[ProfessionalPartTime.PickZone(random, flatHomeZone)];
                        case Occupation.Office:
                            return _zones.GetFlatData()[GeneralPartTime.PickZone(random, flatHomeZone)];
                        case Occupation.Retail:
                            return _zones.GetFlatData()[SalesPartTime.PickZone(random, flatHomeZone)];
                        case Occupation.Manufacturing:
                            return _zones.GetFlatData()[ManufacturingPartTime.PickZone(random, flatHomeZone)];
                    }
                    break;
            }
            return null;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Unload()
        {
            Parallel.Invoke(
                () => ProfessionalFullTime.Unload(),
                () => GeneralFullTime.Unload(),
                () => SalesFullTime.Unload(),
                () => ManufacturingFullTime.Unload(),
                () => ProfessionalPartTime.Unload(),
                () => GeneralPartTime.Unload(),
                () => SalesPartTime.Unload(),
                () => ManufacturingPartTime.Unload());
        }
    }
}
