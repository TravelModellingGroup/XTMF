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

namespace James.UTDM
{
    public class ChainMST : IModelSystemTemplate
    {
        [SubModelInformation( Description = "The other MST's to run", Required = false )]
        public List<IModelSystemTemplate> SubMST;

        [RunParameter( "Input Directory", "../../Input", "The Input Directory" )]
        public string InputBaseDirectory
        {
            get;
            set;
        }

        [DoNotAutomate]
        private IModelSystemTemplate CurrentlyExecuting;

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        public bool ExitRequest()
        {
            return false;
        }

        public void Start()
        {
            foreach ( var mst in this.SubMST )
            {
                this.CurrentlyExecuting = mst;
                mst.Start();
            }
            this.CurrentlyExecuting = null;
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return CurrentlyExecuting != null ? this.CurrentlyExecuting.Progress : 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return CurrentlyExecuting != null ? this.CurrentlyExecuting.ProgressColour : new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public override string ToString()
        {
            return CurrentlyExecuting != null ? this.CurrentlyExecuting.ToString() : "Executing a model system chain.";
        }
    }
}
