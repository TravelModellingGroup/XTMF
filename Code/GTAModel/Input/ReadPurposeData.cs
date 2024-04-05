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
using System.Collections.Generic;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input;

public class ReadPurposeData : IReadODData<float>
{
    [RootModule]
    public I4StepModel Root;

    [RunParameter("Purpose Name", "External", "The name of the purpose to read from..")]
    public string Purpose;

    public IEnumerable<ODData<float>> Read()
    {
        IPurpose purpose = GetPurpose();
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        float[] ret = new float[zones.Length * zones.Length];
        LoadData(ret, purpose);
        var odData = new ODData<float>();
        for (int i = 0; i < zones.Length; i++)
        {
            odData.O = zones[i].ZoneNumber;
            for (int j = 0; j < zones.Length; j++)
            {
                odData.D = zones[j].ZoneNumber;
                odData.Data = ret[i * zones.Length + j];
                yield return odData;
            }
        }
    }

    private void LoadData(float[] ret, IPurpose purpose)
    {
        var data = purpose.Flows;
        if (data == null) return;
        for (int i = 0; i < data.Count; i++)
        {
            LoadData(ret, data[i]);
        }
    }

    private void LoadData(float[] ret, TreeData<float[][]> data)
    {
        if (data.Children != null)
        {
            // if we have children just process them
            for (int i = 0; i < data.Children.Length; i++)
            {
                LoadData(ret, data.Children[i]);
            }
        }
        else
        {
            var grid = data.Result;
            if (grid == null)
            {
                return;
            }
            for (int i = 0; i < grid.Length; i++)
            {
                var row = grid[i];
                if (row != null)
                {
                    for (int j = 0; j < row.Length; j++)
                    {
                        ret[i * grid.Length + j] += row[j];
                    }
                }
            }

        }
    }

    private IPurpose GetPurpose()
    {
        var purposes = Root.Purpose;
        for (int i = 0; i < purposes.Count; i++)
        {
            if (purposes[i].PurposeName == Purpose)
            {
                return purposes[i];
            }
        }
        throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to find a purpose named '" + Purpose + "'!");
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
