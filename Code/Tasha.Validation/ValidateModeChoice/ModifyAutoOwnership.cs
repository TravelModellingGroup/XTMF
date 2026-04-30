/*
    Copyright 2025 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG;
using XTMF;

namespace Tasha.Validation.ValidateModeChoice;

[ModuleInformation(Description = "")]
public sealed class ModifyAutoOwnership : ICalculation<ITashaHousehold, int>
{

    [RunParameter("Random Seed", 1354645, "A seed to feed into the random number generator.")]
    public int RandomSeed;

    [RunParameter("Minimum Value", 0, "The minimum value that is allowed to be returned.")]
    public int MinimumValue;

    [RunParameter("Maximum Value", 4, "The maximum value that is allowed to be returned.")]
    public int MaximumValue;

    [SubModelInformation(Required = true, Description = "The calculation to modify.")]
    public ICalculation<ITashaHousehold, int> AutoOwnershipModel;

    [RunParameter("Expected Change", 0.0f, "A value in [-1,1] mapping to a probability to increase or decrease the result.")]
    public float ExpectedChange;

    public void Load()
    {
        AutoOwnershipModel.Load();
    }

    public int ProduceResult(ITashaHousehold data)
    {
        Random r = new Random(RandomSeed * data.HouseholdId);
        var initialResult = AutoOwnershipModel.ProduceResult(data);
        // Now randomly change the result
        int modifiedValue;
        var pop = r.NextDouble();
        if (pop < Math.Abs(ExpectedChange))
        {
            modifiedValue = ExpectedChange > 0 ? initialResult + 1 : initialResult - 1;
        }
        else
        {
            modifiedValue = initialResult;
        }
        return Math.Clamp(modifiedValue, MinimumValue, MaximumValue);
    }

    public void Unload()
    {
        AutoOwnershipModel.Unload();
    }

    public string Name { get; set; }

    public float Progress => AutoOwnershipModel.Progress;

    public Tuple<byte, byte, byte> ProgressColour => new (50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        if (MathF.Abs(ExpectedChange) > 1.0f)
        {
            error = "Expected Change must be in the range [-1,1]";
            return false;
        }
        if (RandomSeed == 0)
        {
            error = "Random Seed cannot be 0";
            return false;
        }
        return true;
    }
}
