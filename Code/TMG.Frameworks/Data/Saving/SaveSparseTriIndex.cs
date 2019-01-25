/*
    Copyright 2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Saving
{
    public sealed class SaveSparseTriIndex : ISelfContainedModule
    {
        public string Name { get; set; }
        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        [SubModelInformation(Required = true, Description = "The datasource to save")]
        public IDataSource<SparseTriIndex<float>> DataSource;

        [SubModelInformation(Required = true, Description = "The directory to save each layer to.")]
        public FileLocation SaveDirectory;

        [RunParameter("FileName Prefix", "Layer-", "The file will be saved as [FileName Prefix][LayerNumber].[extension]")]
        public string FileNamePrefix;

        [RunParameter("Unload Data", true, "Should we unload the data after saving it if we loaded it?")]
        public bool UnloadData;

        public enum FileFormats
        {
            MTX = 0,
            CSVThirdNormalized = 1,
            CSVSquare = 2
        }

        [RunParameter("File Format", "MTX", typeof(FileFormats), "The file format to save as.")]
        public FileFormats FileFormat;

        public void Start()
        {
            var weLoaded = false;
            if (!DataSource.Loaded)
            {
                weLoaded = true;
                DataSource.LoadData();
            }
            var data = DataSource.GiveData();
            Save(data);
            if (weLoaded && UnloadData)
            {
                DataSource.UnloadData();
            }
        }

        private void Save(SparseTriIndex<float> data)
        {
            var layerIndex = data.ValidIndexes().ToArray();
            System.Threading.Tasks.Parallel.For(0, layerIndex.Length, (int i) =>
            {
                Save(layerIndex[i], data.GetFlatData()[i], data);
            });
        }

        private void Save(int layerIndex, float[][] flatData, SparseTriIndex<float> data)
        {
            var origins = data.ValidIndexes(layerIndex).ToArray();
            // if there is no data there is nothing to save!
            if (origins.Length <= 0)
            {
                return;
            }
            var destinations = data.ValidIndexes(origins[0]).ToArray();
            switch (FileFormat)
            {
                case FileFormats.MTX:
                    SaveMTX(layerIndex, flatData, origins, destinations);
                    return;
                case FileFormats.CSVThirdNormalized:
                    SaveThirdNormalizedCSV(layerIndex, flatData, origins, destinations);
                    return;
                case FileFormats.CSVSquare:
                    SaveSquareMatrixToCSV(layerIndex, flatData, origins, destinations);
                    return;
                default:
                    throw new XTMFRuntimeException(this, "Unknown file format!");
            }
        }

        private void SaveSquareMatrixToCSV(int layerIndex, float[][] flatData, int[] origins, int[] destinations)
        {
            string buildRow(float[] data, int rowNumber)
            {
                StringBuilder b = new StringBuilder();
                b.Append(rowNumber);
                for (int i = 0; i < data.Length; i++)
                {
                    b.Append(',');
                    b.Append(data[i]);
                }
                return b.ToString();
            }
            using (var writer = new StreamWriter(BuildFileName(layerIndex)))
            {
                writer.Write("origin\\destination");
                for (int j = 0; j < destinations.Length; j++)
                {
                    writer.Write(',');
                    writer.Write(destinations[j]);
                }
                writer.WriteLine();
                // write the main body
                foreach (var row in flatData.AsParallel().AsOrdered().Select((r, i) => buildRow(r, origins[i])))
                {
                    writer.WriteLine(row);
                }
            }
        }

        private void SaveThirdNormalizedCSV(int layerIndex, float[][] flatData, int[] origins, int[] destinations)
        {
            using (var writer = new StreamWriter(BuildFileName(layerIndex)))
            {
                writer.WriteLine("Origin,Destination,Value");
                for (int i = 0; i < flatData.Length; i++)
                {
                    for (int j = 0; j < flatData[i].Length; j++)
                    {
                        writer.Write(origins[i]);
                        writer.Write(',');
                        writer.Write(destinations[j]);
                        writer.Write(',');
                        writer.WriteLine(flatData[i][j]);
                    }
                }
            }
        }

        private void SaveMTX(int layerIndex, float[][] flatData, int[] origins, int[] destinations)
        {
            new Emme.EmmeMatrix(origins, flatData).Save(BuildFileName(layerIndex), false);
        }

        private string BuildFileName(int layerIndex)
        {
            string ret = null;
            switch (FileFormat)
            {
                case FileFormats.MTX:
                    ret = Path.Combine(SaveDirectory, FileNamePrefix + layerIndex + ".mtx");
                    break;
                case FileFormats.CSVThirdNormalized:
                    ret = Path.Combine(SaveDirectory, FileNamePrefix + layerIndex + ".csv");
                    break;
                case FileFormats.CSVSquare:
                    ret = Path.Combine(SaveDirectory, FileNamePrefix + layerIndex + ".csv");
                    break;
                default:
                    throw new XTMFRuntimeException(this, "Unknown file format!");
            }
            // make sure the path exists
            DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(ret));
            if(!dir.Exists)
            {
                dir.Create();
            }
            return ret;
        }

        public bool RuntimeValidation(ref string error)
        {
            if(FileFormat == FileFormats.CSVThirdNormalized)
            {
                error = $"In {Name}, currently the Third Normalized file format is not supported.";
                return false;
            }
            return true;
        }
    }
}
