/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Analysis
{
    [ModuleInformation( Description = "This module is designed to take many different files and produce a new file containing the summed value of each file, where each file is summed on an individual line." )]
    public class SumODData : ISelfContainedModule
    {
        [SubModelInformation( Description = "Files to process that contain OD Data.", Required = false )]
        public List<IReadODData<float>> ODDataInput;

        [RunParameter( "Ouput File Name", "Data.csv", typeof( FileFromOutputDirectory ), "The name of file to store the data into." )]
        public FileFromOutputDirectory OutputFile;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => null;

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            if ( !OutputFile.ContainsFileName() ) return;
            using var writer = new StreamWriter(OutputFile.GetFileName());
            writer.WriteLine("SourceName,Total");
            foreach (var dataSource in ODDataInput)
            {
                double total = 0f;
                foreach (var dataPoint in dataSource.Read())
                {
                    total += dataPoint.Data;
                }
                writer.Write(dataSource.Name);
                writer.Write(',');
                writer.WriteLine(total);
            }
        }
    }
}