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
using Tasha.Common;

namespace Tasha.ModeChoice
{
    /// <summary>
    ///
    /// </summary>
    internal sealed class ModeSet
    {
        public double U;
        internal static ModeChoice ModeChoice;
        private static ConcurrentDictionary<int, ConcurrentBag<ModeSet>> ModeSetPool = new ConcurrentDictionary<int, ConcurrentBag<ModeSet>>();

        /// <summary>
        /// Create a new Mode Set
        /// </summary>
        /// <param name="chain">The trip chain this mode is for</param>
        private ModeSet(ITripChain chain)
        {
            Chain = chain;
            Length = chain.Trips.Count;
            ChosenMode = new ITashaMode[Length];
        }

        private ModeSet(ModeSet copyMe, double u)
        {
            Chain = copyMe.Chain;
            Length = copyMe.Length;
            ChosenMode = new ITashaMode[Length];
            Array.Copy(copyMe.ChosenMode, ChosenMode, copyMe.Length);
            U = u;
        }

        /// <summary>
        /// The modes for this Mode Set
        /// </summary>
        public ITashaMode[] ChosenMode { get; set; }

        /// <summary>
        /// How many Trips we represent for this Mode Set
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// The chain this mode set belongs to
        /// </summary>
        private ITripChain Chain { get; set; }

        /// <summary>
        /// Gets an enumeration of the ModeSets for the trip chain
        /// </summary>
        /// <param name="chain">The trip chain to look at</param>
        /// <returns>An enumeration of all of the mode sets for this chain</returns>
        public static IEnumerable<ModeSet> GetModeSets(ITripChain chain)
        {
            return (List<ModeSet>)chain["ModeSets"];
        }

        /// <summary>
        /// This code initializes the modeset for a trip chain
        /// </summary>
        /// <param name="chain">The chain to init mode sets for</param>
        public static void InitModeSets(ITripChain chain)
        {
            chain.Attach("ModeSets", new List<ModeSet>(chain.Trips.Count));
        }

        /// <summary>
        /// Stores this mode set to the trip chain
        /// </summary>
        /// <param name="chain">The chain to attach this set to</param>
        /// <param name="u"></param>
        public void Store(ITripChain chain, double u)
        {
            List<ModeSet> set = (List<ModeSet>)chain["ModeSets"];
            set.Add(new ModeSet(this, u));
        }

        internal static ModeSet Make(ITripChain chain)
        {
            ModeSet newModeSet;
            var chainLength = chain.Trips.Count;
            ConcurrentBag<ModeSet> ourBag;
            if (!ModeSetPool.TryGetValue(chainLength, out ourBag))
            {
                ModeSetPool[chainLength] = new ConcurrentBag<ModeSet>();
                return new ModeSet(chain);
            }
            if (ourBag.TryTake(out newModeSet))
            {
                newModeSet.Chain = chain;
                return newModeSet;
            }
            else
            {
                return new ModeSet(chain);
            }
        }

        internal static ModeSet Make(ModeSet set, double newU)
        {
            ModeSet newModeSet;
            var chainLength = set.Length;
            ConcurrentBag<ModeSet> ourBag;
            if (!ModeSetPool.TryGetValue(chainLength, out ourBag))
            {
                ModeSetPool[chainLength] = new ConcurrentBag<ModeSet>();
                return new ModeSet(set, newU);
            }
            if (ourBag.TryTake(out newModeSet))
            {
                for (int i = 0; i < chainLength; i++)
                {
                    newModeSet.ChosenMode[i] = set.ChosenMode[i];
                }
                newModeSet.U = newU;
                newModeSet.Chain = set.Chain;
                return newModeSet;
            }
            else
            {
                return new ModeSet(set, newU) { Chain = set.Chain };
            }
        }

        internal static void ReleaseModeSets(ITripChain tripChain)
        {
            var sets = GetModeSets(tripChain);
            if (sets != null)
            {
                foreach (var set in sets)
                {
                    var length = set.Length;
                    set.Chain = null;
                    ModeSetPool[length].Add(set);
                }
            }
        }

        internal void RecalculateU()
        {
            int tripPlace = 0;
            double newU = 0;
            var numberOfModes = ModeChoice.NonSharedModes.Count;
            if (Chain == null || Chain.Trips == null)
            {
                return;
            }
            foreach (var trip in Chain.Trips)
            {
                var data = ModeData.Get(trip);
                for (int mode = 0; mode < numberOfModes; mode++)
                {
                    if (ModeChoice.NonSharedModes[mode] == ChosenMode[tripPlace])
                    {
                        newU += data.U(mode);
                        break;
                    }
                }
                tripPlace++;
            }
            U = newU;
        }
    }
}