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
using System.Linq;
using System.Text;
using XTMF;
using TMG.Input;
using Datastructure;
using TMG.Estimation;
namespace TMG.NetworkEstimation
{
    public class CordonCountEstimation : IModelSystemTemplate
    {
        [RunParameter( "Input Directory", "../../Input", "The input directory for this model system." )]
        public string InputBaseDirectory { get; set; }

        public string OutputBaseDirectory { get; set; }

        [SubModelInformation( Required = true, Description = "A mapping between the truth data and the model data's names for stations." )]
        public FileLocation StationNameMapFile;

        [SubModelInformation( Required = true, Description = "A csv with the first column being the name the second being the value." )]
        public FileLocation TruthFile;

        [SubModelInformation( Required = true, Description = "A csv with the first column being the name the second being the value." )]
        public FileLocation ModelOutputFile;

        [RunParameter("Total Error Factor", 0f, "The factor applied to the sum of error.")]
        public float TotalErrorFactor;

        [RunParameter( "Mean Squared Error Factor", 0f, "The factor applied to the sum of each station's (error)^2." )]
        public float MeanSquareErrorFactor;

        [RunParameter( "Absolute Error Factor", 0f, "The factor applied to the sum of each station's Abs(error)." )]
        public float AbsoluteErrorFactor;

        private Dictionary<string, string> StationNameMap = new Dictionary<string, string>();

        private Dictionary<string, float> TruthValues = new Dictionary<string, float>();

        [RootModule]
        public IEstimationClientModelSystem Root;

        public bool ExitRequest()
        {
            return false;
        }

        public void Start()
        {
            LoadStationMap();
            LoadTruthData();
            float totalError = 0f;
            float meanSquareError = 0f;
            float absError = 0f;
            if (!EvaluateModelData(ref totalError, ref meanSquareError, ref absError) )
            {
                this.Root.RetrieveValue = () =>
                    this.TotalErrorFactor * totalError
                    + this.MeanSquareErrorFactor * meanSquareError
                    + this.AbsoluteErrorFactor * absError;
            }
            else
            {
            }
        }

        private bool EvaluateModelData(ref float totalError, ref float meanSquareError, ref float absError)
        {
            // model data needs to be loaded every time
            using ( var reader = new CsvReader( this.ModelOutputFile.GetFilePath() ) )
            {
                reader.LoadLine();
                while ( !reader.EndOfFile )
                {
                    var columns = reader.LoadLine();
                    if ( columns < 2 ) continue;
                    string modelStationName;
                    string truthName;
                    float modelStationValue;
                    reader.Get( out modelStationName, 0 );
                    reader.Get( out modelStationValue, 1 );
                    if ( this.StationNameMap.TryGetValue( modelStationName, out truthName ) )
                    {
                        float truthValue;
                        if ( this.TruthValues.TryGetValue( truthName, out truthValue ) )
                        {
                            var diff = modelStationValue - truthValue;
                            totalError += diff;
                            meanSquareError += diff * diff;
                            absError += diff < 0 ? -diff : diff;
                        }
                    }
                }
            }
            return true;
        }

        private void LoadTruthData()
        {
            if ( this.TruthValues.Count == 0 )
            {
                using ( var reader = new CsvReader( this.TruthFile.GetFilePath() ) )
                {
                    reader.LoadLine();
                    while ( !reader.EndOfFile )
                    {
                        var columns = reader.LoadLine();
                        if ( columns < 2 )
                        {
                            continue;
                        }
                        string stationName;
                        float stationValue;
                        reader.Get( out stationName, 0 );
                        reader.Get( out stationValue, 1 );
                        this.TruthValues.Add( stationName, stationValue );
                    }
                }
            }
        }

        private void LoadStationMap()
        {
            if ( this.StationNameMap.Count == 0 )
            {
                using ( CsvReader reader = new CsvReader( this.StationNameMapFile.GetFilePath() ) )
                {
                    // Burn header
                    reader.LoadLine();
                    // process data
                    while ( !reader.EndOfFile )
                    {
                        var columns = reader.LoadLine();
                        if ( columns < 2 )
                        {
                            continue;
                        }
                        string truthStationName, modelStationName;
                        reader.Get( out truthStationName, 0 );
                        reader.Get( out modelStationName, 1 );
                        this.StationNameMap.Add( modelStationName, truthStationName );
                    }
                }
            }
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
    }
}
