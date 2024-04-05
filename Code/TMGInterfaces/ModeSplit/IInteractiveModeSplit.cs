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
namespace TMG.ModeSplit;

public interface IInteractiveModeSplit : IMultiModeSplit
{
    /// <summary>
    /// Compute with the current set of parameters a particular OD pair.
    /// This value will be saved and used for the mode split.
    /// </summary>
    /// <param name="o">The origin zone</param>
    /// <param name="d">The destination zone</param>
    /// <returns>The sum of the utility of the top layer to the power of E</returns>
    float ComputeUtility(IZone o, IZone d);

    /// <summary>
    /// Use this function to kill an already running iterative mode split.
    /// This function is not required to be called unless the mode split
    /// is never actually executed in order to reclaim memory.
    /// </summary>
    void EndInterativeModeSplit();

    /// <summary>
    /// Initialize the mode choice to go into inter-interactive mode
    /// </summary>
    /// <param name="numberOfInteractiveCategories">This will be used for updating progress.
    /// This will be the total number of distributions for the purpose.</param>
    void StartNewInteractiveModeSplit(int numberOfInteractiveCategories);
}