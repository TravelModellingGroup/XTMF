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
using System.IO;
using System.Diagnostics;

namespace James.UTDM
{
    public class FourStep : I4StepModel
    {
        [SubModelInformation(Description = "The module that will load the zone system", Required=true)]
        public IZoneSystem ZoneSystem
        {
            get;
            set;
        }

        [SubModelInformation(Description = "The model that assigns onto the networks", Required = true)]
        public INetworkAssignment NetworkAssignment { get; set; }

        [SubModelInformation(Description = "The Network information that will be fed for assignment", Required = false)]
        public IList<INetworkData> NetworkData
        {
            get;
            set;
        }

        [SubModelInformation(Description="The different categories to process", Required=false)]
        public List<IPurpose> Purpose { get; set; }

        public string Name { get; set; }

        [RunParameter("TotalIterations", 3, "The number of iterations to go.")]
        public int TotalIterations
        {
            get;
            set;
        }

        [SubModelInformation(Description = "The modes contained in this mode split.", Required = false)]
        public List<IModeChoiceNode> Modes
        {
            get;
            set;
        }

        private string Status;

        Func<float> GetProgress = null;

        private int CurrentCategory;

        public void Start()
        {
            this.CurrentIteration = -1;
            this.ZoneSystem.LoadData();

            this.Status = "Running Initial Network Assignment";
            this.GetProgress = (() => this.NetworkAssignment.Progress);
            this.NetworkAssignment.RunInitialAssignments();
            

            for (this.CurrentIteration = 0; this.CurrentIteration < this.TotalIterations; this.CurrentIteration++)
            {
                this.Status = "Processing iteration " + this.CurrentIteration;

                this.CurrentCategory = -1;
                foreach (var data in this.NetworkData)
                {
                    data.LoadData();
                }
                for (int i = 0; i < this.Purpose.Count; i++)
                {
                    this.Purpose[(this.CurrentCategory = i)].Run();
                }
                foreach (var data in this.NetworkData)
                {
                    data.UnloadData();
                }

                this.NetworkAssignment.RunNetworkAssignment();
            }

            this.NetworkAssignment.RunPostAssignments();
        }

        [RunParameter("Input Base Directory", "../../Input", "The base input directory")]
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

        public float Progress
        {
            get
            {
                var progressFunction = this.GetProgress;
                if (progressFunction != null)
                {
                    return progressFunction();
                }
                return 0f;
            }

        }

        /*
        public float Progress
        {

            get
            {
                float percentOfIteration = 1.0f / TotalIterations;
                if (this.CurrentIteration == -1) return 0;
                if (this.CurrentCategory == -1)
                {
                    return (percentOfIteration * CurrentIteration);
                }
                return (percentOfIteration * CurrentIteration) + (percentOfIteration * this.Purpose[CurrentCategory].Progress)
                    * ((CurrentCategory + 1) / (float)this.Purpose.Count);
            }
        }*/

        private static Tuple<byte, byte, byte> Green = new Tuple<byte, byte, byte>(75, 125, 75);

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return Green; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public int CurrentIteration { get; set; }


        public bool ExitRequest()
        {
            return false;
        }

        public override string ToString()
        {
            return this.Status;
        }
    }
}
