/*
    Copyright 2015-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using XTMF;
using TMG;
using TMG.Input;
namespace Tasha.Data
{
    [ModuleInformation(Description = "This module provides a way of storing a single floating point number.  It also provides the interface to set its value.")]
    public class FloatData : ISetableDataSource<float>
    {
        [RunParameter("Initial Value", 0.0f, "The value to initially set this data source to.")]
        public float InitialValue { get { return Data; } set { Data = value; } }

        private float Data;

        public float GiveData()
        {
            return this.Data;
        }

        public bool Loaded
        {
            get; set;
        }

        public void LoadData()
        {
            Data = InitialValue;
            Loaded = true;
        }

        public void UnloadData()
        {
            Data = InitialValue;
            Loaded = false;
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

        public void SetData(float newValue)
        {
            Data = newValue;
        }
    }
}
