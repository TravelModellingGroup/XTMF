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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Tasha.Common;
using TMG.Input;
using XTMF;
namespace Tasha.Validation.ModeChoice
{
    public class ModeSplit : IPostHousehold
    {
        [RootModule]
        public ITashaRuntime Root;

        [SubModelInformation(Required = true, Description = "The location to save the mode splits to.")]
        public FileLocation OutputFileLocation;

        public string Name { get; set; }

        public float Progress
        {
            get
            {
                return 0f;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return null;
            }
        }

        ITashaMode[] Modes;
        float[] Counts;
        SpinLock WriteLock = new SpinLock(false);

        public void Execute(ITashaHousehold household, int iteration)
        {
            var persons = household.Persons;
            bool taken = false;
            WriteLock.Enter(ref taken);
            for(int i = 0; i < persons.Length; i++)
            {
                var expanionFactor = persons[i].ExpansionFactor;
                var tripChains = persons[i].TripChains;
                for(int j = 0; j < tripChains.Count; j++)
                {
                    var tripChain = tripChains[j].Trips;
                    for(int k = 0; k < tripChain.Count; k++)
                    {
                        var mode = tripChain[k].Mode;
                        for(int l = 0; l < Modes.Length; l++)
                        {
                            if(Modes[l] == mode)
                            {
                                Counts[l] += expanionFactor;
                                break;
                            }
                        }
                    }
                }
            }
            if(taken) WriteLock.Exit(true);
        }

        public void IterationFinished(int iteration)
        {
            using(var writer = new StreamWriter(OutputFileLocation, true))
            {
                writer.Write("Iteration: ");
                writer.WriteLine(iteration);
                writer.WriteLine("Mode,ExpandedTrips");
                for(int i = 0; i < Modes.Length; i++)
                {
                    writer.Write(Modes[i].ModeName);
                    writer.Write(',');
                    writer.WriteLine(Counts[i]);
                }
            }
            for(int i = 0; i < Counts.Length; i++)
            {
                Counts[i] = 0.0f;
            }
        }

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }


        public void IterationStarting(int iteration)
        {
            Modes = Root.AllModes.ToArray();
            Counts = new float[Modes.Length];
        }
    }
}
