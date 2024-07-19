/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.Estimation.Utilities;


[ModuleInformation(Description = "Provides a way to return a scalar as the fitness for the Estimation Client Model System.")]
public sealed class ReportFitnessFromScalar : ISelfContainedModule
{
    [RootModule]
    public IEstimationClientModelSystem Root;

    [SubModelInformation(Required = true, Description = "The fitness value to return to the estimation host.")]
    public IDataSource<float> Fitness;

    public void Start()
    {
        // Force the data to be loaded every time.
        Fitness.LoadData();
        var value = Fitness.GiveData();
        Root.RetrieveValue = () => value;
        Fitness.UnloadData();
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

}
