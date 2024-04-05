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
using System.Threading.Tasks;
using XTMF;

namespace TMG.GTAModel;

[ModuleInformation(Description=
    @"DirectModeAggregationTally provides a way to aggregate the OD demand from multiple 
purposes and modes for building an aggregate assignment, such as building demand for EMME/3. 
Leaving any of the options blank will select all of the given type (purposes or modes)." )]
public class DirectModeAggregationTally : IModeAggregationTally
{
    [RunParameter( "Modes", "Auto", "Ex \"Auto,Taxi,Passenger\" the modes you want to process." )]
    public string ModeNames;

    [RunParameter( "Purposes", "", "Leave blank to do all purposes, otherwise Ex. \"A,B,D,E\" if you want to exclude C." )]
    public string PurposeNames;

    [RootModule]
    public I4StepModel Root;

    /// <summary>
    /// The mode indexes to process
    /// </summary>
    protected int[] ModeIndexes;

    /// <summary>
    /// The purpose indexes to process
    /// </summary>
    protected int[] PurposeIndexes;

    public string Name
    {
        get;
        set;
    }

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get;
        set;
    }

    public virtual void IncludeTally(float[][] currentTally)
    {
        var purposes = Root.Purpose;
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        var numberOfZones = zones.Length;
        for ( int purp = 0; purp < PurposeIndexes.Length; purp++ )
        {
            var purpose = purposes[PurposeIndexes[purp]];
            for ( int m = 0; m < ModeIndexes.Length; m++ )
            {
                var data = GetResult( purpose.Flows, ModeIndexes[m] );
                // if there is no data continue on to the next mode
                if ( data == null ) continue;
                Parallel.For( 0, numberOfZones, delegate(int o)
                {
                    if ( data[o] == null ) return;
                    for ( int d = 0; d < numberOfZones; d++ )
                    {
                        currentTally[o][d] += data[o][d];
                    }
                } );
            }
        }
    }

    public virtual bool RuntimeValidation(ref string error)
    {
        if ( !ProcessModeNames( ref error ) )
        {
            return false;
        }
        if ( !ProcessPurposeNames( ref error ) )
        {
            return false;
        }
        return true;
    }

    protected IModeChoiceNode GetMode(int index)
    {
        var modes = Root.Modes;
        var length = modes.Count;
        int current = 0;
        for ( int i = 0; i < length; i++ )
        {
            var m = GetMode( ref current, index, modes[i] );
            if ( m != null )
            {
                return m;
            }
        }
        return null;
    }

    protected float[][] GetResult(List<TreeData<float[][]>> list, int modeIndex)
    {
        var length = list.Count;
        int current = 0;
        for ( int i = 0; i < length; i++ )
        {
            float[][] temp = GetResult( list[i], modeIndex, ref current );
            if ( temp != null )
            {
                return temp;
            }
        }
        return null;
    }

    protected float[][] GetResult(TreeData<float[][]> node, int modeIndex, ref int current)
    {
        if ( modeIndex == current )
        {
            return node.Result;
        }
        current++;
        if ( node.Children != null )
        {
            for ( int i = 0; i < node.Children.Length; i++ )
            {
                float[][] temp = GetResult( node.Children[i], modeIndex, ref current );
                if ( temp != null )
                {
                    return temp;
                }
            }
        }
        return null;
    }

    private IModeChoiceNode GetMode(ref int current, int index, IModeChoiceNode mode)
    {
        if ( current == index )
        {
            return mode;
        }
        current++;
        if (mode is IModeCategory cat)
        {
            var length = cat.Children.Count;
            for (int i = 0; i < length; i++)
            {
                var m = GetMode(ref current, index, cat.Children[i]);
                if (m != null)
                {
                    return m;
                }
            }
        }
        return null;
    }

    private int GetModeIndex(string trimmed)
    {
        var modes = Root.Modes;
        var length = modes.Count;
        int index = 0;
        for ( int i = 0; i < length; i++ )
        {
            if ( GetModeIndex( trimmed, modes[i], ref index ) )
            {
                return index;
            }
        }
        return -1;
    }

    private bool GetModeIndex(string trimmed, IModeChoiceNode node, ref int index)
    {
        if ( node.ModeName == trimmed )
        {
            return true;
        }
        index++;
        if (node is IModeCategory cat)
        {
            var length = cat.Children.Count;
            for (int i = 0; i < length; i++)
            {
                if (GetModeIndex(trimmed, cat.Children[i], ref index))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool ProcessModeNames(ref string error)
    {
        List<int> care = [];
        if ( String.IsNullOrWhiteSpace( ModeNames ) )
        {
            // if nothing is given return the top level
            var length = Root.Modes.Count;
            for ( int i = 0; i < length; i++ )
            {
                care.Add( GetModeIndex( Root.Modes[i].ModeName ) );
            }
            return true;
        }
        string[] parts = ModeNames.Split( ',' );
        foreach ( var part in parts )
        {
            var trimmed = part.Trim();
            int index = GetModeIndex( trimmed );

            if ( index == -1 )
            {
                error = "In " + Name + "We were unable to find a mode with the name \"" + trimmed + "\"!";
                return false;
            }
            care.Add( index );
        }
        ModeIndexes = care.ToArray();
        return true;
    }

    private bool ProcessPurposeNames(ref string error)
    {
        if ( String.IsNullOrWhiteSpace( PurposeNames ) )
        {
            var length = Root.Purpose.Count;
            PurposeIndexes = new int[length];
            for ( int i = 0; i < length; i++ )
            {
                PurposeIndexes[i] = i;
            }
        }
        else
        {
            string[] parts = PurposeNames.Split( ',' );
            List<int> care = [];
            var purposes = Root.Purpose;
            var numberOfPurposes = purposes.Count;
            foreach ( var part in parts )
            {
                var trimmed = part.Trim();
                bool found = false;
                for ( int i = 0; i < numberOfPurposes; i++ )
                {
                    if ( purposes[i].PurposeName == trimmed )
                    {
                        care.Add( i );
                        found = true;
                        break;
                    }
                }
                if ( !found )
                {
                    error = "We were unable to find a purpose with the name \"" + trimmed + "\"!";
                    return false;
                }
            }
            PurposeIndexes = care.ToArray();
        }
        return true;
    }
}