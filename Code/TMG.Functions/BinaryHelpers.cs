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
using System.IO;
namespace TMG.Functions
{
    public static class BinaryHelpers
    {
        public static void ExecuteWriter(Action<BinaryWriter> toRun, string fileName)
        {
            Stream file = null;
            try
            {
                file = File.OpenWrite(fileName);
                using (var writer = new BinaryWriter(file))
                {
                    file = null;
                    toRun(writer);
                }
            }
            finally
            {
                file?.Dispose();
            }
        }

        public static void ExecuteReader(Action<BinaryReader> toRun, string fileName)
        {
            Stream file = null;
            try
            {
                file = File.OpenRead(fileName);
                using (var reader = new BinaryReader(file))
                {
                    file = null;
                    toRun(reader);
                }
            }
            finally
            {
                file?.Dispose();
            }
        }
    }
}
