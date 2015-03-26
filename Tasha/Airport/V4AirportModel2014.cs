/*
    Copyright 2014 James Vaughan for integration into XTMF.

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
using TMG.Input;
using Tasha.Common;
using Datastructure;

namespace Tasha.Airport
{

    [ModuleInformation(Description =
@"This module is designed to provide a 2014 calibrated model for trips into and out of Pearson airport for the use by the GTAA.  In addition this module is hoped to provide a better
prediction of current airport travel behaviour than the GTAModel V2.5 airport model that was included in GTAModel V4.00.  This module will likely be included in GTAModel V4.01+.
")]
    public class V4AirportModel2014 : IPreIteration, IPostIteration
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        /// <summary>
        /// This is our link into the Tasha based model system so we can get access to the network data
        /// </summary>
        [RootModule]
        public ITashaRuntime Root;

        /// <summary>
        /// The transit network that will be used for computing the travel times for the model.
        /// </summary>
        private ITripComponentData TransitNetwork;

        /// <summary>
        /// The auto network that will be used for computing the travel times for the model.
        /// </summary>
        private INetworkData AutoNetwork;

        /// <summary>
        /// A local cache of the zone system for quick access
        /// </summary>
        private SparseArray<IZone> ZoneSystem;

        public void Execute(int iterationNumber, int totalIterations)
        {
            LoadPearsonZoneNumber();
            var modeSplits = ComputeModeSplits();
            if(AutoProbabilities != null)
            {
                SaveAuto(modeSplits);
            }
            if(TransitProbabilities != null)
            {
                SaveTransit(modeSplits);
            }
            if(ComputeDistribution)
            {
                var distributions = ComputeDistributions(modeSplits);
                if(DistributionProbabilities != null)
                {
                    SaveDistributions(distributions);
                }
            }
        }

        /// <summary>
        /// Save the auto probabilities to the resource
        /// </summary>
        /// <param name="modeSplits">The mode splits</param>
        private void SaveAuto(ModeSplitUtilities[] modeSplits)
        {
            var data = AutoProbabilities.AquireResource<SparseArray<float>>().GetFlatData();
            for(int i = 0; i < modeSplits.Length; i++)
            {
                data[i] = modeSplits[i].Auto;
            }
        }

        /// <summary>
        /// Save the transit probabilities to resource
        /// </summary>
        /// <param name="modeSplits">The mode splits</param>
        private void SaveTransit(ModeSplitUtilities[] modeSplits)
        {
            var data = TransitProbabilities.AquireResource<SparseArray<float>>().GetFlatData();
            for(int i = 0; i < modeSplits.Length; i++)
            {
                data[i] = modeSplits[i].Transit;
            }
        }

        /// <summary>
        /// Save the distributions to resource
        /// </summary>
        /// <param name="distributions">The distributions</param>
        private void SaveDistributions(float[] distributions)
        {
            var data = DistributionProbabilities.AquireResource<SparseArray<float>>().GetFlatData();
            for(int i = 0; i < distributions.Length; i++)
            {
                data[i] = distributions[i];
            }
        }

        /// <summary>
        /// Compute the mode splits for each zone
        /// </summary>
        /// <returns>The results from the mode choice model</returns>
        private ModeSplitUtilities[] ComputeModeSplits()
        {
            var zones = ZoneSystem.GetFlatData();
            var utilities = new ModeSplitUtilities[zones.Length];
            for(int i = 0; i < utilities.Length; i++)
            {
                ComputeModeSplitForZone(i, utilities);
            }
            return utilities;
        }

        /// <summary>
        /// The struct binds together the probabilities of Auto, Transit, and the logsum for each mode split.
        /// </summary>
        private struct ModeSplitUtilities
        {
            internal float Auto;
            internal float Transit;
            internal float Logsum;
        }

        /// <summary>
        /// Convert the Pearson zone number into the flat spatial index for faster compute times.
        /// This will also load the zone system into our local cache
        /// </summary>
        private void LoadPearsonZoneNumber()
        {
            // first we are going to load in the zone system from the model system for quick reference since it is a property and would be a virtual
            // call every time we needed access to it, which will be often.
            ZoneSystem = Root.ZoneSystem.ZoneArray;
            PearsonFlatZoneNumber = ZoneSystem.GetFlatIndex(PearsonZoneNumber);
            if(PearsonZoneNumber < 0)
            {
                throw new XTMFRuntimeException("In '" + Name + "' the zone number used for Pearson is not inside of the zone system!");
            }
        }

        [RunParameter("Pearson Zone Number", 3709, "The zone number for Pearson airport.")]
        public int PearsonZoneNumber;

        private int PearsonFlatZoneNumber;

        [RunParameter("Mode Split Auto Time Factor", 0.0f, "The factor to apply to the auto times in order to compute the mode split.")]
        public float BetaAutoTime;

        [RunParameter("Mode Split Auto Cost Factor", 0.0f, "The factor to apply to the auto costs in order to compute the mode split.")]
        public float BetaAutoCost;

        [RunParameter("Mode Split Transit Time Factor", 0.0f, "The factor to apply to the transit times in order to compute the mode split.")]
        public float BetaTransitTime;

        [RunParameter("Mode Split Transit Constant", 0.0f, "The transit alternative constant to apply for the transit mode for mode split.")]
        public float BetaTransitConstant;

        [RunParameter("Mode Split Transit Fare Factor", 0.0f, "The factor to apply to the transit fare in order to compute the mode split.")]
        public float BetaTransitFare;

        [RunParameter("Time of Day", "7:00 AM", typeof(Time), "The time of day to use for accessing the network level of services.")]
        public Time TimeOfDay;

        [SubModelInformation(Required = false, Description = "The optional location to save the auto probabilities, used for estimation.")]
        public IResource AutoProbabilities;

        [SubModelInformation(Required = false, Description = "The optional location to save the transit probabilities, used for estimation.")]
        public IResource TransitProbabilities;

        [SubModelInformation(Required = false, Description = "The optional location to save the distribution probabilities.")]
        public IResource DistributionProbabilities;

        /// <summary>
        /// Computes and stores the mode split for a zone going to Pearson.
        /// This will also compute the logsum for each zone.
        /// </summary>
        /// <param name="zone">The flat indexed zone to compute for</param>
        /// <param name="utilities">The array to store the results in</param>
        private void ComputeModeSplitForZone(int zone, ModeSplitUtilities[] utilities)
        {
            // first we need to get the data from the network
            float aivtt, acost, tivtt, twalk, twait, _railTime, tfare;
            AutoNetwork.GetAllData(zone, PearsonFlatZoneNumber, TimeOfDay, out aivtt, out acost);
            bool transit = TransitNetwork.GetAllData(zone, PearsonFlatZoneNumber, TimeOfDay, out tivtt, out twalk, out twait, out _railTime, out tfare);
            // Second compute the utilities for each mode
            utilities[zone].Auto = (float)Math.Exp(BetaAutoTime * aivtt + BetaAutoCost * acost);
            if(transit && (tivtt > 0 | twalk > 0))
            {
                utilities[zone].Transit = (float)Math.Exp(BetaTransitConstant + BetaTransitTime * (tivtt + twalk + twait) + BetaTransitFare * tfare);
                var total = utilities[zone].Auto + utilities[zone].Transit;
                utilities[zone].Logsum = (float)Math.Log(total);
                utilities[zone].Auto /= total;
                utilities[zone].Transit /= total;
            }
            else
            {
                utilities[zone].Logsum = (float)Math.Log(utilities[zone].Auto);
                utilities[zone].Auto /= 1f;
                utilities[zone].Transit = 0f;
            }

        }

        [RunParameter("Distribution Log of Population Factor", 0.0f, "The factor to apply to the log of the population of a zone for distribution.")]
        public float BetaLogPopulation;

        [RunParameter("Distribution Log of Employment Factor", 0.0f, "The factor to apply to the log of the employment of a zone for distribution.")]
        public float BetaLogEmployment;

        [RunParameter("Distribution Logsum Factor", 0.0f, "The factor to apply to the logsum of the mode split utilities for zone distribution.")]
        public float BetaLogsum;

        [SubModelInformation(Required = true, Description = "A link to the total employment per zone.")]
        public IResource Employment;

        [RunParameter("Compute Distribution", true, "Should we compute the distribution model as well as the mode split model?")]
        public bool ComputeDistribution;

        [RunParameter("Employment has Log Applied", false, "Set this to true of the resource being used already has the employment totals with log applied to them by zone.")]
        public bool EmploymentHasLogApplied;

        /// <summary>
        /// Compute the distributions by zone.
        /// </summary>
        /// <param name="modeSplits">The mode splits from their previously computed model.</param>
        /// <returns>The probability of a trip being from each zone.</returns>
        private float[] ComputeDistributions(ModeSplitUtilities[] modeSplits)
        {
            var zones = ZoneSystem.GetFlatData();
            var distributions = new float[zones.Length];
            // get our input data
            var employment = GetEmploymentData();
            var population = GetPopulationData();
            // compute the utilities
            for(int i = 0; i < modeSplits.Length; i++)
            {
                // make sure they are real zones [this will exclude subways]
                if(employment[i] + population[i] < 0.1f)
                {
                    distributions[i] = 0.0f;
                }
                else
                {
                    distributions[i] = (float)Math.Exp(BetaLogPopulation * population[i] + BetaLogEmployment * employment[i] + BetaLogsum * modeSplits[i].Logsum);
                }
            }
            // get the reciprocal of the total utility
            var sumOfDistributions = distributions.Sum();
            if(sumOfDistributions <= 0)
            {
                throw new XTMFRuntimeException("In '" + Name + "' there was a total of 0 for the distributions!");
            }
            var total = 1.0f / sumOfDistributions;
            if(float.IsNaN(total))
            {
            }
            // use the reciprocal of the total to convert the utilities into probabilities
            for(int i = 0; i < distributions.Length; i++)
            {
                distributions[i] *= total;
            }
            return distributions;
        }

        /// <summary>
        /// Returns the log of population for each zone.
        /// </summary>
        /// <returns>Log of Population</returns>
        private float[] GetPopulationData()
        {
            return ZoneSystem.GetFlatData().Select(zone => (float)Math.Log(zone.Population + 1)).ToArray();
        }

        /// <summary>
        /// Get the log of employment per zone.
        /// </summary>
        /// <returns>Log of Employment by zone</returns>
        private float[] GetEmploymentData()
        {
            if(EmploymentHasLogApplied)
            {
                return Employment.AquireResource<SparseArray<float>>().GetFlatData().Select(x => x).ToArray();
            }
            else
            {
                return Employment.AquireResource<SparseArray<float>>().GetFlatData().Select(x => (float)Math.Log(x + 1)).ToArray();
            }
        }

        public void Load(int totalIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            if(AutoProbabilities != null && !AutoProbabilities.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the auto probability resource specified is not a SparseArray<float> resource!";
                return false;
            }
            if(TransitProbabilities != null && !TransitProbabilities.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the transit probability resource specified is not a SparseArray<float> resource!";
                return false;
            }
            if(DistributionProbabilities != null && !DistributionProbabilities.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the distribution probability resource specified is not a SparseArray<float> resource!";
                return false;
            }
            if(!LoadTransitNetworks(ref error))
            {
                return false;
            }
            return true;
        }

        [RunParameter("Auto Network Name", "Auto", "The name of the auto network to use.")]
        public string AutoNetworkName;

        [RunParameter("Transit Network kName", "Transit", "The name of the transit network to use.")]
        public string TransitNetworkName;

        private bool GetFromName<T>(out T type, string name) where T : class, INetworkData
        {
            foreach(var network in Root.NetworkData)
            {
                if(network.NetworkType == name)
                {
                    type = network as T;
                    if(type != null)
                    {
                        return true;
                    }
                }
            }
            type = null;
            return false;
        }

        private bool LoadTransitNetworks(ref string error)
        {
            if(!GetFromName<INetworkData>(out AutoNetwork, AutoNetworkName))
            {
                error = "In '" + Name + "' we were unable to find an auto network named '" + AutoNetworkName + "'!";
            }
            if(!GetFromName<ITripComponentData>(out TransitNetwork, TransitNetworkName))
            {
                error = "In '" + Name + "' we were unable to find an transit network named '" + TransitNetworkName + "'!";
            }
            return true;
        }

        public void Load(IConfiguration config, int totalIterations)
        {

        }
    }

}
