﻿/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
namespace TMG.Frameworks.Data.Synthesis.Gibbs;


public class GibbsSampler : ISelfContainedModule
{

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    [SubModelInformation(Description = "The pools to generate.")]
    public Pool[] Pools;

    [SubModelInformation(Description = "Aggregations to apply between pools")]
    public Aggregation[] Aggregations;

    public void Start()
    {
        // just generate things for now, later we will need to do recombination
        foreach(var pool in Pools)
        {
            pool.GeneratePool();
        }
        // once all of the pools have been computed combine the pools
        foreach(var aggregation in Aggregations)
        {
            aggregation.Execute();
        }
    }
}
