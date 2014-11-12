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
using Tasha.Common;
using XTMF;

namespace Tasha.Validation
{
    public class RandomTashaSeeds : IModelSystemTemplate
    {
        [RunParameter( "Iterations", 100, "Number of iterations to run." )]
        public int Iterations;

        [RunParameter( "RandomSeed", 12345, "The random seed to generate the random seeds" )]
        public int RandomSeed;

        [SubModelInformation( Description = "The tasha to run", Required = true )]
        public ITashaRuntime Tasha;

        private int CurrentIteration;

        public string InputBaseDirectory
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        public float Progress
        {
            get
            {
                return ( this.CurrentIteration / (float)this.Iterations ) + ( 1 / (float)this.Iterations ) * this.Tasha.Progress;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            Random r = new Random( this.RandomSeed );
            for ( int i = 0; i < Iterations; i++ )
            {
                this.CurrentIteration = i;
                this.Tasha.RandomSeed = r.Next();
                this.Tasha.Start();
            }
        }

        public override string ToString()
        {
            return this.Tasha.ToString();
        }
    }
}