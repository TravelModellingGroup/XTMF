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
    public class Fail : IModelSystemTemplate
    {
        [RunParameter( "Runtime Exception", true, "Should we cause a runtime exception or a runtime validation exception?" )]
        public bool RuntimeException;

        [RunParameter( "Deep Fail", false, "Should we fail with a large stack trace?" )]
        public bool RecuriveFail;

        public enum TestEnum
        {
            Valid,
            AlsoValid
        }

        [RunParameter("Test Enumerations", TestEnum.Valid, "Test enumerations (Valid,AlsoValid)")]
        public TestEnum TestEnumParameter;

        public string InputBaseDirectory
        {
            get;
            set;
        }

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
            if ( RecuriveFail )
            {
                DeepFail( 10 );
            }
            throw new XTMFRuntimeException( "Here is an XTMF runtime exception!" );
        }

        public int DeepFail(int level)
        {
            if ( level <= 0 )
            {
                throw new XTMFRuntimeException( "Here is a deep XTMF runtime exception!" );
            }
            return DeepFail( level - 1 ) + DeepFail( level - 2 );
        }

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

        public bool RuntimeValidation(ref string error)
        {
            if ( !RuntimeException )
            {
                error = "Here is a runtime validation exception!";
                return false;
            }
            return true;
        }
    }
}
