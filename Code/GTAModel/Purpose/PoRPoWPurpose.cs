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
using Datastructure;
using TMG.Functions;
using TMG.ModeSplit;
using XTMF;

namespace TMG.GTAModel.Purpose
{
    // ReSharper disable once InconsistentNaming
    public class PoRPoWPurpose : PurposeBase, IDemographicCategoyPurpose, ISelfContainedModule
    {
        [SubModelInformation( Description = "Distribution", Required = true )]
        public IDemographicDistribution Distribution;

        [RunParameter( "Execute", true, "Should we execute this purpose?  Set to false if you are going to just use a cache." )]
        public bool Execute;

        [RunParameter( "Only Do Generation", false, "For testing the output of generation set this to true to skip running distribution." )]
        public bool OnlyDoGeneration;

        [RunParameter( "Save Result File Name", "", "The start of the name of the file (First Default = FrictionCache1.bin). If this is empty nothing will be saved." )]
        public string SaveResultFileName;

        private int CurrentNumber;

        private int LastIteration = -1;

        [SubModelInformation( Description = "Generation", Required = false )]
        public List<IDemographicCategoryGeneration> Categories { get; set; }

        public override float Progress
        {
            get { return Distribution.Progress; }
        }

        public override void Run()
        {
            if ( !Execute ) return;
            // we actually don't write our mode choice
            var numberOfCategories = Categories.Count;
            SparseArray<float>[] o = new SparseArray<float>[numberOfCategories];
            SparseArray<float>[] d = new SparseArray<float>[numberOfCategories];
            for ( int i = 0; i < o.Length; i++ )
            {
                o[i] = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
                d[i] = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
                Categories[i].Generate( o[i], d[i] );
            }
            // if we only need to run generation we are done
            if ( OnlyDoGeneration ) return;
            // we don't do mode choice
            foreach ( var distributionData in Distribution.Distribute( o, d, Categories ) )
            {
                var interative = ModeSplit as IInteractiveModeSplit;
                if ( interative != null )
                {
                    interative.EndInterativeModeSplit();
                }
                if ( !String.IsNullOrWhiteSpace( SaveResultFileName ) )
                {
                    SaveFriction( distributionData.GetFlatData() );
                }
            }
        }

        public void Start()
        {
            // to start, run
            Run();
        }

        private string GetFrictionFileName(string baseName)
        {
            if ( Root.CurrentIteration != LastIteration )
            {
                CurrentNumber = 0;
                LastIteration = Root.CurrentIteration;
            }
            return String.Concat( baseName, CurrentNumber++, ".bin" );
        }

        private void SaveFriction(float[][] ret)
        {
            try
            {
                var fileName = GetFrictionFileName( SaveResultFileName );
                var dirName = Path.GetDirectoryName( fileName );
                if (dirName == null)
                {
                    throw new XTMFRuntimeException($"We were unable to extract the directory name from the path '{fileName}'!");
                }
                if ( !Directory.Exists( dirName ) )
                {
                    Directory.CreateDirectory( dirName );
                }
                BinaryHelpers.ExecuteWriter( writer =>
                    {
                        for ( int i = 0; i < ret.Length; i++ )
                        {
                            for ( int j = 0; j < ret[i].Length; j++ )
                            {
                                writer.Write( ret[i][j] );
                            }
                        }
                    }, fileName );
            }
            catch ( IOException e )
            {
                throw new XTMFRuntimeException( "Unable to save distribution cache file!\r\n" + e.Message );
            }
        }
    }
}