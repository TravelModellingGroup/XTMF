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
using TMG.Emme;
using XTMF;

namespace TMG.NetworkEstimation
{
    public class CombinationTally : IErrorTally
    {
        [SubModelInformation( Description = "The first tally to compute.", Required = true )]
        public IErrorTally FirstTally;

        [RunParameter( "First Weight", 0.03f, "The weighting for the line tally." )]
        public float FirstWeight;

        [SubModelInformation( Description = "The second tally to compute.", Required = true )]
        public IErrorTally SecondTally;

        [RunParameter( "Second Weight", 0.06f, "The weighting for the region tally." )]
        public float SecondWeight;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public float ComputeError(ParameterSetting[] parameters, TransitLine[] truth, TransitLine[] predicted)
        {
            var first = this.FirstTally.ComputeError( parameters, truth, predicted );
            var second = this.SecondTally.ComputeError( parameters, truth, predicted );
            return ( first * FirstWeight ) + ( second * SecondWeight );
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}