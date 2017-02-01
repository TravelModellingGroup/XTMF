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
using Datastructure;
using TMG.GTAModel.DataUtility;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel
{
    public class StaticAssignment : IAssignment
    {
        [SubModelInformation( Description = "The static data sources to assign.", Required = false )]
        public List<IReadODData<float>> DataSources;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        [RunParameter( "Demographic Index Selection", "1", typeof( NumberList ), "A list of demographic parameters to use, in order for the data sources.  There must be the same number of mode choice selections as there are Data Sources!" )]
        public NumberList SelectedDemographicChoices;

        [RunParameter( "Mode Choice Selection", "1", typeof( NumberList ), "A list of mode choice parameter selections to use, in order for the data sources.  There must be the same number of mode choice selections as there are Data Sources!" )]
        public NumberList SelectedModeChoices;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public IEnumerable<SparseTwinIndex<float>> Assign()
        {
            int length = DataSources.Count;
            for ( int i = 0; i < length; i++ )
            {
                yield return BuildOD( DataSources[i], SelectedModeChoices[i], SelectedDemographicChoices[i] );
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( SelectedModeChoices.Count != DataSources.Count )
            {
                error = "In " + Name + " the number of mode choice parameter set options is not the same as the number of data sources!";
                return false;
            }
            return true;
        }

        private SparseTwinIndex<float> BuildOD(IReadODData<float> dataSource, int modeChoiceIndex, int demographicIndex)
        {
            // Setup the mode choice to have the right parameters
            Root.ModeParameterDatabase.ApplyParameterSet( modeChoiceIndex, demographicIndex );
            // build a matrix to store
            var ret = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            // there is no point trying to do this in parallel since most likely everything is being streamed off of disk anyways
            foreach ( var dataPoint in dataSource.Read() )
            {
                // no point trying to use the flat structure since we are dealing with entries from sparse space
                ret[dataPoint.O, dataPoint.D] = dataPoint.Data;
            }
            return ret;
        }
    }
}