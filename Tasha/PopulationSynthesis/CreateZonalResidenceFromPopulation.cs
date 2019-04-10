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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.PopulationSynthesis
{
    [ModuleInformation(Description = "Used to generate GTAModel V4.0+ Zonal Residence Vectors from the streamed households.")]
    public sealed class CreateZonalResidenceFromPopulation : IPostHousehold
    {
        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

        private SparseArray<IZone> _zones;

        [RunParameter("Exclude Zones", "6000-9999", typeof(RangeSet), "The set of employment zones to ignore people whom are employed within.")]
        public RangeSet ExcludeZones;

        [RootModule]
        public ITashaRuntime Root;

        /// <summary>
        /// [Occ/Emp][FlatZone]
        /// </summary>
        private float[][] _data;

        [SubModelInformation(Required = true, Description = "The location to save the ZonalResidence to.")]
        public FileLocation ProfessionalFullTime;
        [SubModelInformation(Required = true, Description = "The location to save the ZonalResidence to.")]
        public FileLocation ProfessionalPartTime;
        [SubModelInformation(Required = true, Description = "The location to save the ZonalResidence to.")]
        public FileLocation GeneralFullTime;
        [SubModelInformation(Required = true, Description = "The location to save the ZonalResidence to.")]
        public FileLocation GeneralPartTime;
        [SubModelInformation(Required = true, Description = "The location to save the ZonalResidence to.")]
        public FileLocation SalesFullTime;
        [SubModelInformation(Required = true, Description = "The location to save the ZonalResidence to.")]
        public FileLocation SalesPartTime;
        [SubModelInformation(Required = true, Description = "The location to save the ZonalResidence to.")]
        public FileLocation ManufacturingFullTime;
        [SubModelInformation(Required = true, Description = "The location to save the ZonalResidence to.")]
        public FileLocation ManufacturingPartTime;


        public void Execute(ITashaHousehold household, int iteration)
        {
            var flatHomeZone = _zones.GetFlatIndex(household.HomeZone.ZoneNumber);
            foreach(var person in household.Persons)
            {
                int occ = GetOccIndex(person);
                int emp = GetEmpIndex(person);
                if(occ >= 0 & emp >= 0 && (person.EmploymentZone == null || !ExcludeZones.Contains(person.EmploymentZone.ZoneNumber)))
                {
                    _data[occ * 2 + emp][flatHomeZone] += person.ExpansionFactor;
                }
            }
        }

        private int GetEmpIndex(ITashaPerson person)
        {
            switch(person.EmploymentStatus)
            {
                case TTSEmploymentStatus.FullTime:
                    return 0;
                case TTSEmploymentStatus.PartTime:
                    return 1;
                default:
                    return -1;
            }
        }

        private int GetOccIndex(ITashaPerson person)
        {
            switch(person.Occupation)
            {
                case Occupation.Professional:
                    return 0;
                case Occupation.Office:
                    return 1;
                case Occupation.Retail:
                    return 2;
                case Occupation.Manufacturing:
                    return 3;
                default:
                    return -1;
            }
        }

        public void IterationFinished(int iteration)
        {
            // save the results
            Save(ProfessionalFullTime, _data[0]);
            Save(ProfessionalPartTime, _data[1]);
            Save(GeneralFullTime, _data[2]);
            Save(GeneralPartTime, _data[3]);
            Save(SalesFullTime, _data[4]);
            Save(SalesPartTime, _data[5]);
            Save(ManufacturingFullTime, _data[6]);
            Save(ManufacturingPartTime, _data[7]);
        }

        private void Save(FileLocation location, float[] data)
        {
            var zones = _zones.GetFlatData();
            try
            {
                using (var writer = new StreamWriter(location))
                {
                    writer.WriteLine("Zone,Workers");
                    for (int i = 0; i < data.Length; i++)
                    {
                        writer.Write(zones[i].ZoneNumber);
                        writer.Write(',');
                        writer.WriteLine(data[i]);
                    }
                }
            }
            catch(IOException e)
            {
                throw new XTMFRuntimeException(this, e, $"Unable to write to file \"{location.ToString()}\"!");
            }
        }

        public void IterationStarting(int iteration)
        {
            _zones = Root.ZoneSystem.ZoneArray;
            _data = new float[8][];
            for (int i = 0; i < _data.Length; i++)
            {
                _data[i] = new float[_zones.Count];
            }
        }

        public void Load(int maxIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            if(Root.Parallel)
            {
                error = "Please run this model system in Serial, not in parallel.";
                return false;
            }
            return true;
        }
    }
}
