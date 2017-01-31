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
using Datastructure;
using System;
using System.Collections.Generic;
using TMG.Input;
using XTMF;
namespace TMG.Frameworks.Data.Loading
{
    [ModuleInformation(Description =
@"This module will stream ODData<float> from a CSV file.  If there are two columns of data it will store it as Origin,Data.  If there three or more it
 will be stored as Origin,Destination,Data.")]
    // ReSharper disable once InconsistentNaming
    public class LoadODDataFromCSV : IReadODData<float>
    {
        [SubModelInformation(Required = true, Description = "The location to read the data from.")]
        public FileLocation LoadFrom;

        [RunParameter("Contains Header", true, "Set this to true if there is a header line in the CSV File.")]
        public bool ContainsHeader;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public IEnumerable<ODData<float>> Read()
        {
            using (var reader = new CsvReader(LoadFrom, true))
            {
                if (ContainsHeader)
                {
                    reader.LoadLine();
                }
                int columns;
                while (reader.LoadLine(out columns))
                {
                    if (columns >= 2)
                    {
                        ODData<float> data = new ODData<float>();
                        reader.Get(out data.O, 0);
                        if (columns >= 3)
                        {
                            reader.Get(out data.D, 1);
                            reader.Get(out data.Data, 2);
                        }
                        else
                        {
                            reader.Get(out data.Data, 1);
                        }
                        yield return data;
                    }
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
