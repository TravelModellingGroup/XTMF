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
namespace TMG.Estimation.AI
{
    public class LinearGridAI : IEstimationAI
    {
        [RunParameter( "SubSections", 10, "The number of different sections to break the space into." )]
        public int Subsections;

        [RunParameter( "Maximize", false, "Should we maximize the result or minimize it?" )]
        public bool Maximize;

        private ParameterSetting[] LocalSpaces;

        [RootModule]
        public IEstimationHost Root;

        public List<Job> CreateJobsForIteration()
        {
            var iteration = this.Root.CurrentIteration;
            if ( iteration == 0 )
            {
                this.SetupParameters( this.Root.Parameters );
            }
            var numberOfGaps = this.Subsections;
            var currentParameter = iteration % this.LocalSpaces.Length;
            var ret = new List<Job>();
            var min = this.LocalSpaces[currentParameter].Minimum;
            var max = this.LocalSpaces[currentParameter].Maximum;
            for ( int i = 0; i < numberOfGaps; i++ )
            {
                var job = this.CleanJob( this.LocalSpaces );
                job.Parameters[currentParameter].Current = ( ( (float)i / ( numberOfGaps - 1 ) ) * ( max - min ) ) + min;
                ret.Add( job );
            }
            return ret;
        }

        private Job CleanJob(ParameterSetting[] parameters)
        {
            var ret = new Job();
            ret.Processed = false;
            ret.ProcessedBy = null;
            ret.Value = float.NaN;
            ret.Processing = false;
            ret.Parameters = new ParameterSetting[parameters.Length];
            for ( int i = 0; i < ret.Parameters.Length; i++ )
            {
                ret.Parameters[i] = new ParameterSetting()
                {
                    Maximum = parameters[i].Maximum,
                    Minimum = parameters[i].Minimum,
                    Current = parameters[i].Current
                };
            }
            return ret;
        }

        private void SetupParameters(List<ParameterSetting> list)
        {
            var param = new ParameterSetting[list.Count];
            for ( int i = 0; i < param.Length; i++ )
            {
                var other = list[i];
                param[i] = new ParameterSetting();
                param[i].Minimum = other.Minimum;
                param[i].Maximum = other.Maximum;
                param[i].Current = ( other.Maximum + other.Minimum ) / 2;
            }
            this.LocalSpaces = param;
        }

        private int FindBest(List<Job> jobs)
        {
            int best = 0;
            if ( Maximize )
            {
                for ( int i = 1; i < jobs.Count; i++ )
                {
                    if ( jobs[i].Value > jobs[best].Value )
                    {
                        best = i;
                    }
                }
            }
            else
            {
                for ( int i = 1; i < jobs.Count; i++ )
                {
                    if ( jobs[i].Value < jobs[best].Value )
                    {
                        best = i;
                    }
                }
            }
            return best;
        }

        public void IterationComplete()
        {
            // update spaces
            var iteration = this.Root.CurrentIteration;
            var currentParameter = iteration % this.LocalSpaces.Length;
            var jobs = this.Root.CurrentJobs;
            int best = FindBest( jobs );
            int minimumIndex = best < 1 ? 0 : best - 1;
            int maximumIndex = best >= jobs.Count - 1 ? jobs.Count - 1 : best + 1;
            this.LocalSpaces[currentParameter].Minimum = jobs[minimumIndex].Parameters[currentParameter].Current;
            this.LocalSpaces[currentParameter].Maximum = jobs[maximumIndex].Parameters[currentParameter].Current;
            this.LocalSpaces[currentParameter].Current =
                ( this.LocalSpaces[currentParameter].Maximum + this.LocalSpaces[currentParameter].Minimum ) / 2;
        }

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

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
