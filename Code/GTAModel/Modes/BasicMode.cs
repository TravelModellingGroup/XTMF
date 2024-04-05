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

namespace TMG.GTAModel.Modes;

[ModuleInformation( Description =
    @"The basic mode is an attempt of creating a generic mode that can represent 
all GTAModel modes that involve using INetworkData and ITripComponentData sources.  
This mode is not designed to handle walk or bike modes.  Other more specific modes can 
be based off of this module."
    )]
public class BasicMode : IMode
{
    [RunParameter( "Adjacent Zone", 0f, "The additive constant for traveling between adjacent zones." )]
    public float AdjacentZone;

    [RunParameter( "AgeConstant1", 0.0f, "An additive constant for persons for different ages." )]
    public float AgeConstant1;

    [RunParameter( "AgeConstant2", 0.0f, "An additive constant for persons for different ages." )]
    public float AgeConstant2;

    [RunParameter( "AgeConstant3", 0.0f, "An additive constant for persons for different ages." )]
    public float AgeConstant3;

    [RunParameter( "AgeConstant4", 0.0f, "An additive constant for persons for different ages." )]
    public float AgeConstant4;

    [RunParameter( "Boarding", 0.0f, "The boarding penalty in minutes." )]
    public float Boarding;

    [RunParameter( "Positive IVTT", false, "Should this mode check to see if the in vehicle travel time time is greater than zero in order to be feasible?" )]
    public bool CheckPositiveIVTT;

    [RunParameter( "Positive Walk", false, "Should this mode check to see if the walk time is greater than zero in order to be feasible?" )]
    public bool CheckPositiveWalk;

    [RunParameter( "Constant", 0.0f, "The base constant term of the utility calculation." )]
    public float Constant;

    [RunParameter( "Destination Employment Density", 0.0f, "The weight to use for the employment density of the destination zone." )]
    public float DestinationEmploymentDensity;

    [RunParameter( "Destination Population Density", 0.0f, "The weight to use for the population density of the destination zone." )]
    public float DestinationPopulationDensity;

    [RunParameter( "Distance", 0f, "The weight to add based on the straight line distance between zones(KM)." )]
    public float Distance;

    [RunParameter( "In-VehicleTravelTime Weight", 0.0f, "The utility for each minute traveling in the vehicle." )]
    public float IVTT;

    [RunParameter( "Multiple-Vehicle Household", 0.0f, "An additive constant for households that contain a multiple vehicles." )]
    public float MultipleVehicleHousehold;

    [RunParameter( "No Driver's License", 0.0f, "An additive constant for persons who have no driver's license." )]
    public float NoDriversLicense;

    [RunParameter( "Origin Employment Density", 0.0f, "The weight to use for the employment density of the origin zone." )]
    public float OriginEmploymentDensity;

    [RunParameter( "Origin Population Density", 0.0f, "The weight to use for the population density of the origin zone." )]
    public float OriginPopulationDensity;

    [RunParameter( "Parking Cost", 0.0f, "The weight given to the parking cost to calculate the utility." )]
    public float Parking;

    [RunParameter( "Part-Time Work", 0.0f, "An additive constant for persons who are part time workers." )]
    public float PartTime;

    [RootModule]
    public ITravelDemandModel Root;

    [RunParameter( "Short Distance", 0f, "If the trip is smaller than this value (meters) then the Small Distance Constant will be applied to the utility." )]
    public float ShortDistance;

    [RunParameter( "Short Distance Constant", 0f, "This value will be applied if the trip is shorter than the short distance variable" )]
    public float ShortDistanceConstant;

    [RunParameter( "Single-Vehicle Household", 0.0f, "An additive constant for households that contain a single vehicle." )]
    public float SingleVehicleHousehold;

    [RunParameter( "Travel Cost", 0.0f, "The weight given to the travel cost to calculate the utility." )]
    public float TravelCost;

    [RunParameter( "Wait Time Weight", 0.0f, "The utility for each minute waiting.  This is only used for network data sources that support walk time components." )]
    public float Wait;

    [RunParameter( "Walk Time Weight", 0.0f, "The utility for each minute walking.  This is only used for network data sources that support walk time components." )]
    public float Walk;

    [DoNotAutomate]
    protected ITripComponentData AdvancedNetworkData;

    [DoNotAutomate]
    protected INetworkData NetworkData;

    [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
    public float CurrentlyFeasible { get; set; }

    [RunParameter( "ModeName", "Auto", "The name of the mode." )]
    public string ModeName { get; set; }

    public string Name
    {
        get;
        set;
    }

    [RunParameter( "Network Name", "Auto", "The name of the network data to use." )]
    public string NetworkType { get; set; }

    [Parameter( "NonPersonalVehicle", false, "Is this mode using a non-personal vehicle?" )]
    public bool NonPersonalVehicle { get; set; }

    public float Progress
    {
        get { return 0; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public virtual float CalculateV(IZone originZone, IZone destinationZone, Time time)
    {
        var zoneArray = Root.ZoneSystem.ZoneArray;
        var origin = zoneArray.GetFlatIndex( originZone.ZoneNumber );
        var destination = zoneArray.GetFlatIndex( destinationZone.ZoneNumber );
        // initialize to the combined constant value for the mode
        float v = Constant + PartTime + NoDriversLicense + SingleVehicleHousehold + MultipleVehicleHousehold
        + AgeConstant1 + AgeConstant2 + AgeConstant3 + AgeConstant4
        + Parking * destinationZone.ParkingCost
        + GetDensityV( originZone, destinationZone );
        // if we need to calculate distance
        var zoneDistance = Root.ZoneSystem.Distances.GetFlatData()[origin][destination];

        // if the trip is smaller than the short distance
        if ( zoneDistance <= ShortDistance )
        {
            v += ShortDistanceConstant;
        }

        v += Distance * ( zoneDistance / 1000f );
        // check what kind of network data we are working with to see if we can use subcomponents
        if ( AdvancedNetworkData == null )
        {
            NetworkData.GetAllData(origin, destination, time, out float ivtt, out float cost);
            v += IVTT * ivtt + TravelCost * cost;
        }
        else
        {
            AdvancedNetworkData.GetAllData( origin, destination, time, out float ivtt, out float walk, out float wait, out float boarding, out float cost );
            v += IVTT * ivtt
            + Walk * walk
            + ( ( walk > 0 ) & ( ivtt <= 0 ) ? AdjacentZone : 0f )
            + Wait * wait
            + Boarding * boarding
            + TravelCost * cost;
        }
        return v;
    }

    public float Cost(IZone origin, IZone destination, Time time)
    {
        return NetworkData.TravelCost( origin, destination, time );
    }

    public bool Feasible(IZone originZone, IZone destinationZone, Time time)
    {
        var zoneArray = Root.ZoneSystem.ZoneArray;
        var origin = zoneArray.GetFlatIndex( originZone.ZoneNumber );
        var destination = zoneArray.GetFlatIndex( destinationZone.ZoneNumber );
        if(CurrentlyFeasible <= 0)
        {
            return false;
        }
        if(AdvancedNetworkData == null)
        {
            return NetworkData.ValidOd(origin, destination, time) && (!CheckPositiveIVTT || NetworkData.TravelTime(origin, destination, time).ToMinutes() > 0);
        }

        AdvancedNetworkData.GetAllData(origin, destination, time, out float ivtt, out float walk, out float wait, out float boarding, out float cost);
        return AdvancedNetworkData.ValidOd(origin, destination, time)
               && ((!CheckPositiveIVTT || ivtt > 0))
               && ((!CheckPositiveWalk || walk > 0));
    }

    public virtual bool RuntimeValidation(ref string error)
    {
        // Load in the network data
        LoadNetworkData();
        if ( NetworkData == null )
        {
            error = "In '" + Name + "' we were unable to find any network data called '" + NetworkType + "'!";
            return false;
        }
        return true;
    }

    public Time TravelTime(IZone origin, IZone destination, Time time)
    {
        return NetworkData.TravelTime( origin, destination, time );
    }

    private float GetDensityV(IZone origin, IZone destination)
    {
        // convert the area to KM^2
        return (float)(
            Math.Log( ( origin.Population / ( origin.InternalArea / 1000f ) ) + 1 ) * OriginPopulationDensity
            + Math.Log( ( destination.Employment / ( destination.InternalArea / 1000f ) ) + 1 ) * DestinationEmploymentDensity );
    }

    /// <summary>
    /// Find and Load in the network data
    /// </summary>
    private void LoadNetworkData()
    {
        foreach ( var dataSource in Root.NetworkData )
        {
            if ( dataSource.NetworkType == NetworkType )
            {
                NetworkData = dataSource;
                if (dataSource is ITripComponentData advancedData)
                {
                    AdvancedNetworkData = advancedData;
                }
                break;
            }
        }
    }
}