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
using XTMF;
using TMG.Input;
namespace TMG.Emme.Utilities;

[ModuleInformation(
    Description = "This module is designed to facilitate the conversion of different matrix formats into the EMME 4+ binary matrix format."
    )]
public class ConvertMatrixToBinary : ISelfContainedModule
{
    [SubModelInformation(Required = true, Description = "The location to save the binary matrix.")]
    public FileLocation OutputLocation;

    [SubModelInformation(Description = "The source to load the data in from.", Required = true)]
    public IReadODData<float> SourceData;

    [RootModule]
    public ITravelDemandModel Root;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void Start()
    {
        if(!Root.ZoneSystem.Loaded)
        {
            Root.ZoneSystem.LoadData();
        }
        var tempMatrix = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
        foreach(var point in SourceData.Read())
        {
            if(tempMatrix.ContainsIndex(point.O, point.D))
            {
                tempMatrix[point.O, point.D] = point.Data;
            }
        }
        new EmmeMatrix(Root.ZoneSystem.ZoneArray,tempMatrix.GetFlatData()).Save(OutputLocation, false);
    }
}
