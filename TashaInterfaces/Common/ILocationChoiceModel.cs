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
using TMG;
using XTMF;
using System;
namespace Tasha.Common;

public interface ILocationChoiceModel : IModule
{
    IZone GetLocationHomeBased(IEpisode episode, ITashaPerson person, Random random);

    IZone GetLocationHomeBased(Activity activity, IZone zone, Random random);

    IZone GetLocationWorkBased(IZone primaryWorkZone, ITashaPerson person, Random random);

    void LoadLocationChoiceCache();

    IZone GetLocation(IEpisode ep, Random random);

    /// <summary>
    /// This method will return the probabilities of selecting each zone.  The array will be reused upon the next call of the location choice
    /// model by the current thread.
    /// </summary>
    /// <param name="ep">The episode to find the probabilities of</param>
    /// <returns>An array of probabilities for the episode</returns>
    float[] GetLocationProbabilities(IEpisode ep);
}