/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.Frameworks.Data.Loading
{
    [ModuleInformation(Description = "This module will force the data source to be loaded, and if it has already been previously, reload.")]
    public class LoadDataSource<T> : ISelfContainedModule
    {

        [RunParameter("Reload", true, "Reload the data source if it has already been loaded?")]
        public bool Reload;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = true, Description = "The data source to load/reload.")]
        public IDataSource<T> ToLoad;

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            if(Reload && ToLoad.Loaded)
            {
                ToLoad.UnloadData();
            }
            ToLoad.LoadData();
        }
    }

}
