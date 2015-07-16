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
using TMG;
namespace Tasha.DataExtraction
{
    [ModuleInformation(Description=
        @"This module is used for executing a series of self contained modules (<em>XTMF.ISelfContainedModule</em>) that can share resources.
Modules will be executed in the presented order.  Progress and status message are passed through to the currently running modules.  Progress
will assume that each module will execute with approximately the same amount of time, thus progress space is evenly distributed.  It will also
add in the current progress reported by the currently executing module."
        )]
    public class ExecuteWithResources : IModelSystemTemplate, IResourceSource
    {
        private Func<float> ProgressLogic;
        private Func<string> StatusLogic;

        [SubModelInformation( Required = false, Description = "Resources to be used by the model system." )]
        public List<IResource> Resources
        {
            get;
            set;
        }

        [SubModelInformation( Required = false, Description = "The models to run with the given resources." )]
        public List<ISelfContainedModule> ToRun;

        [RunParameter("Input Directory", "../../V4Input", "The path to the input directory for this model system.")]
        public string InputBaseDirectory { get; set; }

        public string OutputBaseDirectory { get; set; }

        bool Exit;

        [RunParameter("Release", true, "Should we release the resources after finishing?")]
        public bool Release;

        public bool ExitRequest()
        {
            this.Exit = true;
            for ( int i = 0; i < this.ToRun.Count; i++ )
            {
                // make an attempt to exit early if possible
                try
                {
                    var mst = this.ToRun[i] as IModelSystemTemplate;
                    if ( mst != null )
                    {
                        mst.ExitRequest();
                    }
                }
                catch
                {
                }
            }
            return true;
        }

        public void Start()
        {
            this.Exit = false;
            ExecuteToRun();
            if(Release)
            {
                ReleaseResources();
            }
        }

        private void ReleaseResources()
        {
            for ( int j = 0; j < this.Resources.Count; j++ )
            {
                this.Resources[j].ReleaseResource();
            }
        }

        private void ExecuteToRun()
        {
            int i = 0;
            // assign the progress logic
            this.ProgressLogic = () =>
            {
                if ( i < this.ToRun.Count )
                {
                    return ( ( this.ToRun[i].Progress / this.ToRun.Count ) + (float)i / this.ToRun.Count );
                }
                return 1f;
            };
            // assign the status logic
            this.StatusLogic = () =>
            {
                if ( i < this.ToRun.Count )
                {
                    if ( this.Exit )
                    {
                        return "Exiting after: " + this.ToRun[i].ToString();
                    }
                    else
                    {
                        return this.ToRun[i].ToString();
                    }
                }
                return "Done";
            };
            for ( ; i < this.ToRun.Count; i++ )
            {
                this.ToRun[i].Start();
                if ( this.Exit )
                {
                    break;
                }
            }
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return this.ProgressLogic == null ? 0f : this.ProgressLogic(); }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public override string ToString()
        {
            return this.StatusLogic == null ? "Loading" : this.StatusLogic();
        }
    }
}
