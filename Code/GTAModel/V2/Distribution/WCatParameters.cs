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
using TMG.GTAModel.Modes.UtilityComponents;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.V2.Distribution;

public class WCatParameters : IModule
{
    [SubModelInformation( Description = "The list of constants to apply.", Required = false )]
    public List<SpatialDiscriminationConstantUtilityComponent> Constants;

    [SubModelInformation( Description = "Loads the parameters from disk.", Required = true )]
    public IDataLineSource<float[]> LoadWCatParamaterFile;

    private float[][] Parameters;

    public float LSum { get; set; }

    public string Name { get; set; }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public float CalculateConstantV(IZone origin, IZone destination, Time time)
    {
        float total = 0f;
        for ( int i = 0; i < Constants.Count; i++ )
        {
            total += Constants[i].CalculateV( origin, destination, time );
        }
        // Same is actually multiplied!
        return (float)( Math.Exp( total ) );
    }

    public void LoadData()
    {
        // Load our data
        List<float[]> p = [.. LoadWCatParamaterFile.Read()];
        Parameters = [.. p];
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    /// <summary>
    /// Set
    /// </summary>
    /// <param name="wcat">(Occupation - 1) * 5 + mobility</param>
    public void SetDemographicCategory(int wcat)
    {
        if ( Parameters == null )
        {
            throw new XTMFRuntimeException(this, Name + " needs to be loaded before accessing its data!" );
        }
        if ( Parameters.Length <= wcat )
        {
            throw new XTMFRuntimeException(this, Name + " was accessed for a wcat#" + wcat + " where only " + Parameters.Length + " are available!" );
        }
        if ( Parameters.Length < 0 )
        {
            throw new XTMFRuntimeException(this, Name + " was accessed for a wcat#" + wcat + ".  Only positive wcats are acceptable." );
        }
        AssignSet( wcat );
    }

    public void UnloadData()
    {
        // unload all of the data
        Parameters = null;
    }

    private void AssignSet(int wcat)
    {
        LSum = Parameters[wcat][0];
        for ( int i = 0; i < Constants.Count; i++ )
        {
            Constants[i].Constant = Parameters[wcat][i + 1];
        }
    }
}