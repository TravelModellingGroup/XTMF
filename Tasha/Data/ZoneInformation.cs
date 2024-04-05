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
using Datastructure;
using XTMF;
using TMG;
using TMG.Input;
namespace Tasha.Data;

[ModuleInformation(Description =
    @"This module is designed to take in information from the reader
and store it into a SparseArray<float> that is of the same type as the model system's zone system.
Data outside of the zones that are defined is trimmed off. Only the Origin from the Reader will be used, destination will be ignored.")]
public class ZoneInformation : IDataSource<SparseArray<float>>
{
    [RunParameter("Continue After Invalid ODPair", false, "Should the model system continue after an invalid value has been read in?")]
    public bool ContinueAfterInvalidODPair;

    [SubModelInformation(Required = false, Description = "Origin will be will be used to store data.")]
    public IReadODData<float> Reader;

    [RootModule]
    public ITravelDemandModel Root;

    private SparseArray<float> Data;

    public SparseArray<float> GiveData()
    {
        return Data;
    }

    public bool Loaded
    {
        get { return Data != null; }
    }

    public void LoadData()
    {
        var data = Root.ZoneSystem?.ZoneArray?.CreateSimilarArray<float>();
        if (data == null)
        {
            if (Root.ZoneSystem == null)
            {
                throw new XTMFRuntimeException(this, "There is no zone system defined!");
            }
            if(Root.ZoneSystem.Loaded)
            {
                throw new XTMFRuntimeException(this, "The zone system was loaded however, there was no zone array to be accessed.");
            }
            // Load the zone system
            Root.ZoneSystem.LoadData();
            data = Root.ZoneSystem.ZoneArray?.CreateSimilarArray<float>();
            if(data == null)
            {
                throw new XTMFRuntimeException(this, "Even after reloading the zone system, there was no zone array to be accessed.");
            }
        }
        if (Reader != null)
        {
            foreach (var point in Reader.Read())
            {
                if (data.ContainsIndex(point.O))
                {
                    data[point.O] = point.Data;
                }
                else if (!ContinueAfterInvalidODPair)
                {
                    throw new XTMFRuntimeException(this, $"An invalid data point was attempted to be read in for zone {point.O} with the value of {point.Data}!");
                }
            }
        }
        Data = data;
    }

    public void UnloadData()
    {
        Data = null;
    }

    public string Name { get; set; }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
