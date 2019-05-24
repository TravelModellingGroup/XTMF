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
using Tasha.Common;

namespace Tasha.XTMFModeChoice
{
    public sealed class PossibleTripChainSolution
    {
        public float U;

        private float TourDependentUtility;

        private ModeChoiceTripData[] BaseData;

        private TourData TourData;

        internal PossibleTripChainSolution(ModeChoiceTripData[] baseTripData, byte[] solution, TourData tourData)
        {
            BaseData = baseTripData;
            var modes = new byte[solution.Length];
            for (int i = 0; i < modes.Length; i++)
            {
                modes[i] = solution[i];
            }
            if ( tourData != null )
            {
                TourData = tourData;
                TourDependentUtility = TourData.TourUtilityModifiers;
            }
            PickedModes = modes;
            RegenerateU();
        }

        internal void PickSolution(Random random, ITripChain chain)
        {
            if ( TourData == null ) return;
            var onSolution = TourData.OnSolution;
            for ( int i = 0; i < onSolution.Length; i++ )
            {
                onSolution[i]?.Invoke(random, chain);
            }
        }

        public byte[] PickedModes;

        public void RegenerateU()
        {
            float total = 0;
            for ( int i = 0; i < BaseData.Length; i++ )
            {
                total += BaseData[i].V[PickedModes[i]] + BaseData[i].Error[PickedModes[i]];
            }
            U = total + TourDependentUtility;
        }
    }
}