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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;
using TMG;
using System.IO;
using TMG.Input;
using System.Threading;
using System.Threading.Tasks;
namespace James.UTDM
{
    public class DemoMSTLive : IModelSystemTemplate
    {
        [RunParameter( "Input Directory", "../../Input", "The directory that contains our input." )]
        public string InputBaseDirectory { get; set; }

        public string OutputBaseDirectory { get; set; }

        [SubModelInformation( Required = true, Description = "No Description" )]
        public IModule Bob;

        [SubModelInformation( Required = true, Description = "The location to save our data." )]
        public FileLocation OutputFileLocation;

        [RunParameter( "Start", 13, "The start integer" )]
        public int Start1;
        [RunParameter( "End", 15, "The end integer" )]
        public int End;


        public bool ExitRequest()
        {
            return false;
        }

        public void Start()
        {
            //( true ) ? "IfTrue" : "IfFalse";
            // this can hold at most 100 things before blocking
            var toProcess = new BlockingCollection<string>( 100 );
            int processed = 0;
            Task myWriter = Task.Factory.StartNew( () =>
                {
                    using ( var writer = new StreamWriter( this.OutputFileLocation.GetFilePath(), true ) )
                    {
                        foreach ( var toWrite in toProcess.GetConsumingEnumerable() )
                        {
                            writer.WriteLine( toWrite );
                            processed++;
                        }
                    }
                } );
            this._Progress = () => (float)( processed ) / ( End - Start1 );
            Parallel.For( Start1, End + 1, (int i) =>
                {
                    toProcess.Add( i + ( ( i % 2 == 0 ) ? " is even" : " is odd" ) );
                } );
            toProcess.CompleteAdding();
            myWriter.Wait();
        }

        public string Name { get; set; }

        //private Func<float> _Progress = () => 0f;

        //private Func<float> _Progress = delegate()
        //{
        //    return 0f;
        //};

        private Func<float> _Progress = Bob1;

        private static float Bob1()
        {
            return 0f;
        }

        public float Progress
        {
            get
            {
                return this._Progress();
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( Start1 <= End )
            {
                return true;
            }
            else
            {
                error = "In '" + this.Name + "' Start1 is greater than End";
                return false;
            }
        }

        public override string ToString()
        {
            return "Funny Name";
        }
    }
}
