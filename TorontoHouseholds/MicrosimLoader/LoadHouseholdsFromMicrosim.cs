/*
    Copyright 2021 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;
using TMG.Input;
using System.Collections;

namespace TMG.Tasha.MicrosimLoader
{
    [ModuleInformation(Description = "This module is designed to load the Households, Persons, and Trips from Microsim and pass them through the model.")]
    public class LoadHouseholdsFromMicrosim : IDataLoader<ITashaHousehold>, IDisposable
    {
        [RootModule]
        public ITashaRuntime Root;

        public bool OutOfData => false;

        public int Count { get; }

        public object SyncRoot => null;

        public bool IsSynchronized => false;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(5,150,50);

        private ITashaHousehold[] _households;

        public void CopyTo(ITashaHousehold[] array, int index)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            
        }

        public IEnumerator<ITashaHousehold> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public void LoadData()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public bool RuntimeValidation(ref string error)
        {
            throw new NotImplementedException();
        }

        public ITashaHousehold[] ToArray()
        {
            throw new NotImplementedException();
        }

        public bool TryAdd(ITashaHousehold item)
        {
            throw new NotImplementedException();
        }

        public bool TryTake(out ITashaHousehold item)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
