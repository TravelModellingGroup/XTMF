/*
    Copyright 2020 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using System.IO;
using Tasha.Common;
using TMG.Input;
using XTMF;
using TMG;

namespace Tasha.Utilities
{
    public sealed class SaveHouseholdsToCSV : IPostHousehold, IDisposable
    {
        [RootModule]
        public ITashaRuntime Root;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

        [SubModelInformation(Required = true, Description = "The location to save the household information to.")]
        public FileLocation SaveTo;
        private bool disposedValue;

        private StreamWriter _writer;

        public void Execute(ITashaHousehold household, int iteration)
        {
            _writer.WriteLine($"{household.HouseholdId},{household.HomeZone.ZoneNumber},{household.ExpansionFactor}," +
                $"{DwellingTypeToStr(household.DwellingType)},{household.Persons.Length},{household.Vehicles.Length},{household.IncomeClass}");
        }

        private string DwellingTypeToStr(DwellingType dwellingType)
        {
            switch(dwellingType)
            {
                case DwellingType.Apartment:
                    return "2";
                case DwellingType.House:
                    return "1";
                case DwellingType.Townhouse:
                    return "3";
                default:
                    return "9";
            }
        }

        public void IterationFinished(int iteration)
        {
            _writer?.Dispose();
            _writer = null;
        }

        public void IterationStarting(int iteration)
        {
            _writer?.Dispose();
            _writer = new StreamWriter(SaveTo);
            _writer.WriteLine("HouseholdID,Zone,ExpansionFactor,DwellingType,NumberOfPersons,NumberOfVehicles,Income");
        }

        public void Load(int maxIterations)
        {
            
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _writer?.Dispose();
                    _writer = null;
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
