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

namespace Tasha.XTMFModeChoice;

public sealed class PossibleTripChainSolution
{
    public byte[] PickedModes;

    private Action<Random, ITripChain>[] _tourData;

    public float U;

    private float _systematicUtility;    

    internal PossibleTripChainSolution(ModeChoiceTripData[] baseTripData, byte[] solution, Action<Random, ITripChain>[] tourData, float tourDependentUtility)
    {
        var modes = new byte[solution.Length];
        for (int i = 0; i < modes.Length; i++)
        {
            modes[i] = solution[i];
            _systematicUtility += baseTripData[i].V[solution[i]];
        }
        _tourData = tourData;
        _systematicUtility += tourDependentUtility;
        PickedModes = modes;
        RegenerateU(baseTripData);
    }

    internal void PickSolution(Random random, ITripChain chain)
    {
        if (_tourData is null) return;
        for (int i = 0; i < _tourData.Length; i++)
        {
            _tourData[i]?.Invoke(random, chain);
        }
    }

    internal void RegenerateU(ModeChoiceTripData[] tripData)
    {
        float errorTotal = 0;
        for (int i = 0; i < tripData.Length; i++)
        {
            var pickedMode = PickedModes[i];
            errorTotal += tripData[i].Error[pickedMode];
        }
        U = _systematicUtility + errorTotal;
    }
}