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
using System.Collections.Generic;
using Tasha.Common;
using TMG;

namespace Tasha.Internal;

internal class TashaPerson : Attachable, ITashaPerson
{
    public TashaPerson()
    {
        TripChains = new List<ITripChain>( 2 );
        AuxTripChains = new List<ITripChain>( 1 );
    }

    /// <summary>
    /// Is this person an adult
    /// </summary>
    public bool Adult
    {
        get { return Age >= 18; }
    }

    /// <summary>
    /// How old is this person
    /// </summary>
    public int Age { get; internal set; }

    public List<ITripChain> AuxTripChains
    {
        get;
        set;
    }

    public bool Child
    {
        get { return ( Age >= 0 && Age <= 10 ); }
    }

    public TTSEmploymentStatus EmploymentStatus
    {
        get;
        internal set;
    }

    public IZone EmploymentZone
    {
        get;
        internal set;
    }

    public float ExpansionFactor { get; set; }

    public bool Female
    {
        get;
        internal set;
    }

    public bool FreeParking
    {
        get;
        internal set;
    }

    public ITashaHousehold Household
    {
        get;
        internal set;
    }

    public int Id
    {
        get;
        internal set;
    }

    public bool Licence
    {
        get;
        internal set;
    }

    public bool Male
    {
        get { return !Female; }

        internal set
        {
            Female = !value;
        }
    }

    public Occupation Occupation
    {
        get;
        internal set;
    }

    public IList<IPersonIterationData> PersonIterationData
    {
        get;
        set;
    }

    public IZone SchoolZone
    {
        get;
        set;
    }

    public StudentStatus StudentStatus
    {
        get;
        internal set;
    }

    public TransitPass TransitPass
    {
        get;
        internal set;
    }

    public List<ITripChain> TripChains
    {
        get;
        internal set;
    }

    public bool YoungAdult
    {
        get { return ( Age >= 16 && Age <= 19 ); }
    }

    /// <summary>
    /// Is this person a Youth
    /// </summary>
    public bool Youth
    {
        get { return ( Age >= 11 && Age <= 15 ); }
    }

    public ITashaPerson Clone()
    {
        return (ITashaPerson)MemberwiseClone();
    }

    public void Recycle()
    {
        Release();
        foreach ( var chain in TripChains )
        {
            chain.Recycle();
        }
        foreach ( var chain in AuxTripChains )
        {
            chain.Recycle();
        }
        TripChains.Clear();
        AuxTripChains.Clear();
    }
}