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
using System.IO;
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    public class ReadEmme4BinaryMatrix : IReadODData<float>
    {

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation(Required = true, Description = "The file to read from.")]
        public FileLocation InputFile;

        public string Name { get; set; }

        public float Progress
        {
            get
            {
                return 0f;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return null;
            }
        }

        public IEnumerable<ODData<float>> Read()
        {
            if (!File.Exists(InputFile))
            {
                throw new XTMFRuntimeException(this, $"Unable to read an EMME Binary Matrix located at {InputFile.GetFilePath()}");
            }
            using (BinaryReader reader = TMG.Functions.BinaryHelpers.CreateReader(this, InputFile))
            {
                EmmeMatrix matrix = new EmmeMatrix(reader);
                if (!matrix.IsValidHeader())
                {
                    throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to load the matrix '" + InputFile + "'");
                }
                ODData<float> result = new ODData<float>();
                int pos = 0;
                var indexes = matrix.Indexes;
                var originIndexes = indexes[0];
                var destinationIndexes = indexes[1];
                switch (matrix.Type)
                {
                    case EmmeMatrix.DataType.Float:
                        {
                            var data = matrix.FloatData;
                            for (int i = 0; i < originIndexes.Length; i++)
                            {
                                result.O = originIndexes[i];
                                for (int j = 0; j < destinationIndexes.Length; j++)
                                {
                                    result.D = destinationIndexes[j];
                                    result.Data = data[pos++];
                                    yield return result;
                                }
                            }
                        }
                        break;
                    case EmmeMatrix.DataType.Double:
                        {
                            var data = matrix.DoubleData;
                            for (int i = 0; i < originIndexes.Length; i++)
                            {
                                result.O = originIndexes[i];
                                for (int j = 0; j < destinationIndexes.Length; j++)
                                {
                                    result.D = destinationIndexes[j];
                                    result.Data = (float)data[pos++];
                                    yield return result;
                                }
                            }
                        }
                        break;
                    case EmmeMatrix.DataType.SignedInteger:
                        {
                            var data = matrix.SignedIntData;
                            for (int i = 0; i < originIndexes.Length; i++)
                            {
                                result.O = originIndexes[i];
                                for (int j = 0; j < destinationIndexes.Length; j++)
                                {
                                    result.D = destinationIndexes[j];
                                    result.Data = data[pos++];
                                    yield return result;
                                }
                            }
                        }
                        break;
                    case EmmeMatrix.DataType.UnsignedInteger:
                        {
                            var data = matrix.UnsignedIntData;
                            for (int i = 0; i < originIndexes.Length; i++)
                            {
                                result.O = originIndexes[i];
                                for (int j = 0; j < destinationIndexes.Length; j++)
                                {
                                    result.D = destinationIndexes[j];
                                    result.Data = data[pos++];
                                    yield return result;
                                }
                            }
                        }
                        break;
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
