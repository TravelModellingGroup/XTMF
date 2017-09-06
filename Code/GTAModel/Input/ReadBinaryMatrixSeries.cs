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
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    public class ReadBinaryMatrixSeries : IDataSource<SparseTwinIndex<float>>
    {
        [RunParameter( "Input File Format", "BinaryData%X.bin", "The file series to be read in and sumed.  The %X will be replaced by the index number" )]
        public FileFromOutputDirectory InputFileBase;

        [RunParameter( "Number of Files", 0, "The index of the files to start at." )]
        public int NumberOfFiles;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Starting Index", 0, "The index of the files to start at." )]
        public int StartingIndex;

        private SparseTwinIndex<float> Data;

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public SparseTwinIndex<float> GiveData()
        {
            return Data;
        }

        public bool Loaded
        {
            get { return Data != null; }
        }

        public void LoadData()
        {
            var ret = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            var flatRet = ret.GetFlatData();
            for ( int index = StartingIndex; index < StartingIndex + NumberOfFiles; index++ )
            {
                LoadData( index, flatRet );
            }
            Data = ret;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Data = null;
        }

        private static void FillBuffer(BinaryReader reader, byte[] temp)
        {
            var count = 0;
            while ( count < temp.Length )
            {
                count += reader.Read( temp, count, temp.Length - count );
            }
        }

        private void LoadData(int index, float[][] flatRet)
        {
            var fileNameWithIndexing = InputFileBase.GetFileName();
            int indexOfInsert = fileNameWithIndexing.IndexOf( "%X", StringComparison.InvariantCulture );
            if ( indexOfInsert == -1 )
            {
                throw new XTMFRuntimeException(this, "In '" + Name
                    + "' the parameter 'Input File Format' does not contain a substitution '%X' in order to progress through the series!  Please update the parameter to include the substitution." );
            }
            var fileName = fileNameWithIndexing.Insert( indexOfInsert, index.ToString() ).Replace( "%X", "" );
            var toProcess = new BlockingCollection<ProcessOrder>( 1 );
            Task processingTask = new Task(
                () =>
                {
                    var floatTemp = new float[flatRet.Length];
                    foreach ( var set in toProcess.GetConsumingEnumerable() )
                    {
                        Buffer.BlockCopy( set.RawData, 0, floatTemp, 0, set.RawData.Length );
                        var row = flatRet[set.RowIndex];
                        for ( int j = 0; j < floatTemp.Length; j++ )
                        {
                            row[j] += floatTemp[j];
                        }
                    }
                } );
            processingTask.Start();
            try
            {
                using ( var reader = new BinaryReader( File.Open( fileName, FileMode.Open ) ) )
                {
                    for ( int i = 0; i < flatRet.Length; i++ )
                    {
                        var temp = new byte[flatRet.Length * sizeof( float )];
                        FillBuffer( reader, temp );
                        toProcess.Add( new ProcessOrder { RawData = temp, RowIndex = i } );
                    }
                }
            }
            catch ( FileNotFoundException )
            {
                throw new XTMFRuntimeException(this, "In '" + Name + "' the file '" + Path.GetFullPath( fileName ) + "' was not found!" );
            }
            toProcess.CompleteAdding();
            processingTask.Wait();
        }

        private struct ProcessOrder
        {
            internal byte[] RawData;
            internal int RowIndex;
        }
    }
}