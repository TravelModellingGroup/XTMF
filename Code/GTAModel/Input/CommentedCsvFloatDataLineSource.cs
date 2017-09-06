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
using System.Linq;
using Datastructure;
using TMG.GTAModel.DataUtility;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    public class CommentedCsvFloatDataLineSource : IDataLineSource<float[]>
    {
        [RunParameter( "Column Numbers", "0,1,2,3", typeof( NumberList ), "A numbering of the columns in the order that they will be returned as." )]
        public NumberList ColumnNumbers;

        [Parameter( "Data Is Copied", false, "If data is copied set this be true to reduce memory usage, set to false if we need to allocate every time." )]
        public bool DataIsCopied;

        [RunParameter( "Input File", "Data.csv", typeof( FileFromInputDirectory ), "The .csv file, based on the model system's input directory to use." )]
        public FileFromInputDirectory InputFile;

        [RootModule]
        public IModelSystemTemplate Root;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public IEnumerable<float[]> Read()
        {
            if ( !InputFile.ContainsFileName() ) yield break;
            int maxColumn = ColumnNumbers.Max();
            var numberOfColumnNumbers = ColumnNumbers.Count;
            float[] data = new float[numberOfColumnNumbers];
            CommentedCsvReader reader;
            var fileName = InputFile.GetFileName( Root.InputBaseDirectory );
            try
            {
                reader = new CommentedCsvReader( fileName );
            }
            catch ( IOException e )
            {
                throw new XTMFRuntimeException(null, "In module '" + Name + "' we were unable to load the file '" + fileName + "', an error was reported\r\n'" + e.Message + "'" );
            }
            using ( reader )
            {
                // process the data
                while ( reader.NextLine() )
                {
                    // if the line is long enough
                    if ( reader.NumberOfCurrentCells >= maxColumn )
                    {
                        for ( int i = 0; i < numberOfColumnNumbers; i++ )
                        {
                            reader.Get( out data[i], ColumnNumbers[i] );
                        }
                        yield return data;
                        if ( !DataIsCopied )
                        {
                            data = new float[numberOfColumnNumbers];
                        }
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