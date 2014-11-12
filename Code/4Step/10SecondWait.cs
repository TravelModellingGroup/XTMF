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
using System.Threading;
using System.IO;
using System.Windows.Forms;
using TMG.Functions;
using XTMF;

namespace James.UTDM
{
    public class TenSecondWait : IModelSystemTemplate
    {
        [RunParameter( "Wait time", "10 seconds", typeof( Time ), "The time to run" )]
        public Time TimeToWait;

        volatile bool Exit = false;

        [RunParameter( "Input Directory", "../../Input", "The directory that the input is located at." )]
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
            this.Exit = true;
            return true;
        }

        //public TMG.Input.IReadODData<float> Reader;

        public void Start()
        {
            var seconds = this.TimeToWait.ToMinutes() * 60f;
            for (int i = 0; !this.Exit && i < seconds; i++)
            {
                this.Progress = i / seconds;
                Console.WriteLine( "Waiting {0}", i );
                // sleep for 1/2 a a second
                Thread.Sleep(1000);
            }
            /*foreach(var element in this.Reader.Read())
            {
                this.Progress = this.Reader.Progress;
            }*/
            /*bool bob = Compression.CompressFile( "../../DurhamInput/PORPOS/hbsbase1.txt" );
            if ( bob )
            {
                MessageBox.Show( "Success" );
            }
            else
            {
                MessageBox.Show( "Sadface" );
            }*/
            this.Progress = 1;
        }

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

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
