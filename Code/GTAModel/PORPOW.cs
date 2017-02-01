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
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using XTMF;

namespace TMG.GTAModel
{
    public class PORPOW : ISelfContainedModule
    {
        [SubModelInformation(Description = "Occupation Generation", Required = false)]
        public List<IDemographicCategoryGeneration> Categories;

        [RunParameter("Accuracy Epsilon", 0.8f, "The epsilon value used for the gravity distribution.")]
        public float Epsilon;

        [RunParameter("Impedance Parameter", -1f, "The factor applied to the utility calculation to generate the friction.")]
        public float ImpedianceParameter;

        [RunParameter("Max Iterations", 300, "The maximum number of iterations for computing the Work Location.")]
        public int MaxIterations;

        [RunParameter("Random Seed", 12345, "The random seed used for assigning work places to people.")]
        public int RandomSeed;

        [RootModule]
        public IDemographic4StepModelSystemTemplate Root;

        [RunParameter("Save Results", false, "Should we save the results?")]
        public bool SaveAssignedPopulation;

        [RunParameter("Save File Name", "", "What should we call the file that we save the assigned population into?")]
        public string SaveFileName;

        [RunParameter("Simulation Time", "7:00", typeof(Time), "The time of day the simulation will be for.")]
        public Time SimulationTime;

        private int CurrentOccupationIndex;

        private SparseArray<IZone> ZoneArray;

        [DoNotAutomate]
        public IDistribution Distribution
        {
            get;
            set;
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
            if (Categories == null || Categories.Count == 0)
            {
                error = "The PoR-PoW Model does not have any categories to process!";
                return false;
            }
            if (SaveAssignedPopulation)
            {
                if (String.IsNullOrWhiteSpace(SaveFileName))
                {
                    error = "A file name needs to be selected when trying to save the work assigned population!";
                    return false;
                }
                if (!Uri.IsWellFormedUriString(SaveFileName, UriKind.RelativeOrAbsolute))
                {
                    error = String.Concat('"', SaveFileName, "\" is not a valid file name!");
                    return false;
                }
            }
            return true;
        }

        public void Start()
        {
            // if we haven't run before calculate everything here
            InitializeFlows();
            if (SaveAssignedPopulation)
            {
                Root.Population.Save(SaveFileName);
            }
        }

        /// <summary>
        /// Assign workers to zones
        /// </summary>
        /// <param name="workplaceDistribution"></param>
        /// <param name="occupation"></param>
        private void AssignToWorkers(SparseTwinIndex<float> workplaceDistribution, IDemographicCategoryGeneration cat)
        {
            /*
             * -> For each zone
             * 1) Load the population
             * 2) Count the number of people
             * 3) Count the number of jobs for the zone
             * 4) Compute the ratio of people to jobs and Balance it by normalizing @ population level
             * 5) Shuffle the people to avoid bias
             * 6) Apply the random split algorithm from the Population Synthesis to finish it off
             */
            var zoneIndexes = ZoneArray.ValidIndexies().ToArray();
            var flatZones = ZoneArray.GetFlatData();
            var numberOfZones = zoneIndexes.Length;
            var flatWorkplaceDistribution = workplaceDistribution.GetFlatData();
            var flatPopulation = Root.Population.Population.GetFlatData();
            try
            {
                Parallel.For(0, numberOfZones, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    delegate {
                        return new Assignment { dist = ZoneArray.CreateSimilarArray<float>(), indexes = null };
                    },
                delegate (int z, ParallelLoopState unused, Assignment assign)
                {
                    var dist = assign.dist;
                    var indexes = assign.indexes;
                    var flatDist = dist.GetFlatData();
                    var distributionForZone = flatWorkplaceDistribution[z];
                    Random rand = new Random((RandomSeed * z) * (CurrentOccupationIndex * numberOfZones));
                    IZone zoneI = flatZones[z];
                    var zonePop = flatPopulation[z];
                    int popLength = zonePop.Length;
                    if (indexes == null || indexes.Length < popLength)
                    {
                        indexes = new int[(int)(popLength * 1.5)];
                        assign.indexes = indexes;
                    }

                    int totalPeopleInCat = 0;
                    // 1+2) learn who is qualified for this distribution
                    for (int i = 0; i < popLength; i++)
                    {
                        var person = zonePop[i];
                        if (cat.IsContained(person))
                        {
                            indexes[totalPeopleInCat] = i;
                            totalPeopleInCat++;
                        }
                    }
                    // 3) Count how many jobs are expected to come from this zone
                    double totalJobsFromThisOrigin = 0;
                    for (int i = 0; i < numberOfZones; i++)
                    {
                        totalJobsFromThisOrigin += (flatDist[i] = distributionForZone[i]);
                    }
                    if (totalJobsFromThisOrigin == 0)
                    {
                        return assign;
                    }
                    // 4) Calculate the ratio of people who work to the number of jobs so we can balance it again
                    float normalizationFactor = 1 / (float)totalJobsFromThisOrigin;
                    for (int i = 0; i < numberOfZones; i++)
                    {
                        flatDist[i] = flatDist[i] * normalizationFactor;
                    }

                    // 5) card sort algo
                    for (int i = totalPeopleInCat - 1; i > 0; i--)
                    {
                        var swapIndex = rand.Next(i);
                        var temp = indexes[i];
                        indexes[i] = indexes[swapIndex];
                        indexes[swapIndex] = temp;
                    }
                    // 6) Apply the random split algorithm from the Population Synthesis to finish it off
                    var flatResult = SplitAndClear(totalPeopleInCat, dist, rand);
                    int offset = 0;
                    for (int i = 0; i < numberOfZones; i++)
                    {
                        var ammount = flatResult[i];
                        for (int j = 0; j < ammount; j++)
                        {
                            if (offset + j >= indexes.Length ||
                                indexes[offset + j] > zonePop.Length)
                            {
                                throw new XTMFRuntimeException("We tried to assign to a person that does not exist!");
                            }
                            zonePop[indexes[offset + j]].WorkZone = flatZones[i];
                        }
                        offset += ammount;
                    }
                    return assign;
                }, delegate { });
            }
            catch (AggregateException e)
            {
                throw new XTMFRuntimeException(e.InnerException.Message + "\r\n" + e.InnerException.StackTrace);
            }
        }

        private float[] ComputeFriction(IZone[] zones, IDemographicCategoryGeneration cat, float[] friction)
        {
            var numberOfZones = zones.Length;
            float[] ret = friction == null ? new float[numberOfZones * numberOfZones] : friction;
            var rootModes = Root.Modes;
            var numberOfModes = rootModes.Count;
            var minFrictionInc = (float)Math.Exp(-10);
            // let it setup the modes so we can compute friction
            cat.InitializeDemographicCategory();
            try
            {
                Parallel.For(0, numberOfZones, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate (int i)
               {
                   int index = i * numberOfZones;
                   var origin = zones[i];
                   int vIndex = i * numberOfZones * numberOfModes;
                   for (int j = 0; j < numberOfZones; j++)
                   {
                       double c = 0f;
                       var destination = zones[j];
                       int feasibleModes = 0;
                       for (int mIndex = 0; mIndex < numberOfModes; mIndex++)
                       {
                           var mode = rootModes[mIndex];
                           if (!mode.Feasible(zones[i], zones[j], SimulationTime))
                           {
                               vIndex++;
                               continue;
                           }
                           var inc = mode.CalculateV(zones[i], zones[j], SimulationTime);
                           if (!(double.IsInfinity(inc) | double.IsNaN(inc)))
                           {
                               feasibleModes++;
                               c += inc >= 0 ? 1.0 : Math.Exp(inc);
                           }
                       }
                       if (feasibleModes == 0)
                       {
                           throw new XTMFRuntimeException("There was no valid mode to travel between " + zones[i].ZoneNumber + " and " + zones[j].ZoneNumber);
                       }
                       ret[index++] = (float)Math.Exp(ImpedianceParameter * Math.Log(c));
                   }
               });
            }
            catch (AggregateException e)
            {
                throw e.InnerException;
            }
            // Use the Log-Sum from the V's as the impedence function
            return ret;
        }

        private double ImpedenceFunction(int o, int d)
        {
            double c = 0f;
            var modes = Root.Modes;
            var numberOfModes = modes.Count;
            var origin = ZoneArray[o];
            var destination = ZoneArray[d];
            for (int i = 0; i < numberOfModes; i++)
            {
                var inc = modes[i].CalculateV(origin, destination, SimulationTime);
                c += Math.Exp(inc > 0 ? 0 : inc);
            }
            // Use the Log-Sum from the V's as the impedence function
            return Math.Exp(ImpedianceParameter * Math.Log(c));
        }

        private void InitializeFlows()
        {
            Progress = 0;
            // we are going to need to split based on this information
            ZoneArray = Root.ZoneSystem.ZoneArray;
            var occupations = Root.Demographics.OccupationCategories;
            var validZones = ZoneArray.ValidIndexies().ToArray();
            var numberOfZones = validZones.Length;
            //[Occupation][O , D]
            var distribution = occupations.CreateSimilarArray<SparseTwinIndex<float>>();
            //Generate the place of work place of residence OD's
            SparseArray<float> O = ZoneArray.CreateSimilarArray<float>();
            SparseArray<float> D = ZoneArray.CreateSimilarArray<float>();
            var occupationIndexes = occupations.ValidIndexies().ToArray();
            var numCat = Categories.Count;
            // Start burning that CPU
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            SparseTwinIndex<float> workplaceDistribution = null;
            SparseTwinIndex<float> prevWorkplaceDistribution = null;
            float[] friction = null;
            float[] nextFriction = null;
            for (int i = 0; i < numCat; i++)
            {
                CurrentOccupationIndex = i;
                Task assignToPopulation = null;
                if (i > 0)
                {
                    assignToPopulation = new Task(delegate
                    {
                           if (prevWorkplaceDistribution != null)
                           {
                               // We actually are assigning to the previous category with this data so we need i - 1
                               AssignToWorkers(prevWorkplaceDistribution, Categories[i - 1]);
                               prevWorkplaceDistribution = null;
                           }
                       });
                    assignToPopulation.Start();
                }
                Task computeNextFriction = null;
                if (i + 1 < numCat)
                {
                    computeNextFriction = new Task(delegate {
                       nextFriction = ComputeFriction(ZoneArray.GetFlatData(), Categories[i + 1], nextFriction);
                   });
                    computeNextFriction.Start();
                }

                Categories[i].Generate(O, D);
                GravityModel gravityModel = new GravityModel(ImpedenceFunction, (progress => Progress = (progress / numCat) + ((float)i / numCat)), Epsilon, MaxIterations);
                workplaceDistribution = gravityModel.ProcessFlow(O, D, validZones);
                Progress = ((float)(i + 1) / numCat);
                if (assignToPopulation != null)
                {
                    try
                    {
                        assignToPopulation.Wait();
                        assignToPopulation.Dispose();
                        assignToPopulation = null;
                    }
                    catch (AggregateException e)
                    {
                        throw new XTMFRuntimeException(e.InnerException.Message + "\r\n" + e.InnerException.StackTrace);
                    }
                }
                if (computeNextFriction != null)
                {
                    try
                    {
                        computeNextFriction.Wait();
                        computeNextFriction.Dispose();
                        computeNextFriction = null;
                    }
                    catch (AggregateException e)
                    {
                        throw new XTMFRuntimeException(e.InnerException.Message + "\r\n" + e.InnerException.StackTrace);
                    }
                }
                prevWorkplaceDistribution = workplaceDistribution;
                var frictionTemp = friction;
                friction = nextFriction;
                nextFriction = friction;
            }
            friction = null;
            nextFriction = null;
            prevWorkplaceDistribution = null;
            AssignToWorkers(workplaceDistribution, Categories[numCat - 1]);
            workplaceDistribution = null;
            // ok now we can relax
            Thread.CurrentThread.Priority = ThreadPriority.Normal;
            GC.Collect();
        }

        private int[] SplitAndClear(int pop, SparseArray<float> splitPercentages, Random rand)
        {
            var flatSplitPercentages = splitPercentages.GetFlatData();
            var length = flatSplitPercentages.Length;
            var flatRet = new int[length];
            var flatRemainder = new float[length];
            float remainderTotal = 0;
            int total = 0;
            for (int i = 0; i < length; i++)
            {
                float element = (flatSplitPercentages[i] * pop);
                total += (flatRet[i] = (int)Math.Floor(element));
                flatRemainder[i] = element - flatRet[i];
            }
            int notAssigned = pop - total;
            // Make sure that we do not over assign
            remainderTotal = notAssigned;
            for (int i = 0; i < notAssigned; i++)
            {
                var randPop = rand.NextDouble() * remainderTotal;
                float ammountToReduce = 0;
                int j = 0;
                for (; j < length; j++)
                {
                    randPop -= (ammountToReduce = flatRemainder[j]);
                    if (randPop <= 0)
                    {
                        remainderTotal -= ammountToReduce;
                        flatRemainder[j] = 0;
                        flatRet[j] += 1;
                        break;
                    }
                }
                if (j == length)
                {
                    for (j = 0; j < length; j++)
                    {
                        if (flatRemainder[j] >= 0)
                        {
                            remainderTotal -= flatRemainder[j];
                            flatRemainder[j] = 0;
                            flatRet[j] += 1;
                            break;
                        }
                    }
                }
            }
            return flatRet;
        }

        private struct Assignment
        {
            internal SparseArray<float> dist;
            internal int[] indexes;
        }
    }
}