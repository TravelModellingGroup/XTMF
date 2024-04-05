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
using Datastructure;
using TMG.Functions;
using TMG.GTAModel.DataUtility;
using XTMF;

namespace TMG.GTAModel;

[ModuleInformation(Description =
    @"This module is designed to distribute out the GTAModel version 3 home based other trips.  It is also compatible with the version 2 of GTAModel by specifying all of the employment parameters to be the same value for the regions.  The equation for the systematic utility is as follows:
<p>V_ij= (〖TravelTime〗_R 〖AutoTime〗_ij )</p>
<p>+ (〖PopulationParameter〗_R (ln⁡〖〖Population〗_j 〗 ))</p>
<p>+ (〖ProfessionalParameter〗_R (ln⁡〖〖ProfessionalEmployment〗_j 〗 ))</p>
<p>+ (〖GeneralParameter〗_R (ln⁡〖〖GeneralEmployment〗_j 〗 ))</p>
<p>+ (〖SalesParameter〗_R (ln⁡〖〖SalesEmployment〗_j 〗 ))</p>
<p>+(〖ManufacturingParameter〗_R (ln⁡〖〖Manufacturing〗_j 〗 ))</p>
<p>Where i is the origin zone and j is the destination zone.  R represents the region of the origin.</p>
")]
public class HBODistribution : IDemographicDistribution
{
    [RunParameter("Auto Network Name", "Auto", "The name of the auto network.")]
    public string AutoNetworkName;

    [RunParameter("Region Auto Parameters", "1,2,3,4,5", typeof(FloatList), "The region parameters for Auto Times.")]
    public FloatList RegionAutoParameter;

    [RunParameter("Region General Parameters", "1,2,3,4,5", typeof(FloatList), "The region parameters for the General Employment.")]
    public FloatList RegionEmploymentGeneralParameter;

    [RunParameter("Region Manufacturing Parameters", "1,2,3,4,5", typeof(FloatList), "The region parameters for the Manufacturing Employment.")]
    public FloatList RegionEmploymentManufacturingParameter;

    [RunParameter("Region Professional Parameters", "1,2,3,4,5", typeof(FloatList), "The region parameters for the Professional Employment.")]
    public FloatList RegionEmploymentProfessionalParameter;

    [RunParameter("Region Sales Parameters", "1,2,3,4,5", typeof(FloatList), "The region parameters for the Sales Employment.")]
    public FloatList RegionEmploymentSalesParameter;

    [RunParameter("Region Numbers", "1,2,3,4,5", typeof(NumberList), "The space to be reading region parameters in from.\r\nThis is used as an inverse lookup for the parameters.")]
    public NumberList RegionNumbers;

    [RunParameter("Region Population Parameters", "1,2,3,4,5", typeof(FloatList), "The region parameters for the Population.")]
    public FloatList RegionPopulationParameter;

    [RootModule]
    public IDemographic4StepModelSystemTemplate Root;

    [RunParameter("Simulation Time", "7:00AM", typeof(Time), "The time of day this will be simulating.")]
    public Time SimulationTime;

    private INetworkData NetworkData;

    public string Name
    {
        get;
        set;
    }

    public float Progress
    {
        get { return 0; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> productions, IEnumerable<SparseArray<float>> attractions, IEnumerable<IDemographicCategory> category)
    {
        using var ep = productions.GetEnumerator();
        using var ec = category.GetEnumerator();
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        float[] friction = null;
        while (ep.MoveNext() && ec.MoveNext())
        {
            friction = ComputeFriction(zones, ec.Current, friction);
            yield return SinglyConstrainedGravityModel.Process(ep.Current, friction);
        }
    }

    public bool RuntimeValidation(ref string error)
    {
        if (!LoadNetwork())
        {
            error = "In " + Name + " we were unable to find the network data '" + AutoNetworkName + "' to use as the auto network!";
            return false;
        }
        if (!CompareParameterCount(RegionAutoParameter))
        {
            error = "In " + Name + " the number of parameters for Auto does not match the number of regions!";
            return false;
        }
        if (!CompareParameterCount(RegionAutoParameter))
        {
            error = "In " + Name + " the number of parameters for Auto does not match the number of regions!";
            return false;
        }
        if (!CompareParameterCount(RegionPopulationParameter))
        {
            error = "In " + Name + " the number of parameters for Population does not match the number of regions!";
            return false;
        }
        if (!CompareParameterCount(RegionEmploymentProfessionalParameter))
        {
            error = "In " + Name + " the number of parameters for Professional Employment does not match the number of regions!";
            return false;
        }
        if (!CompareParameterCount(RegionEmploymentGeneralParameter))
        {
            error = "In " + Name + " the number of parameters for General Employment does not match the number of regions!";
            return false;
        }
        if (!CompareParameterCount(RegionEmploymentSalesParameter))
        {
            error = "In " + Name + " the number of parameters for Sales Employment does not match the number of regions!";
            return false;
        }
        if (!CompareParameterCount(RegionEmploymentManufacturingParameter))
        {
            error = "In " + Name + " the number of parameters for Manufacturing Employment does not match the number of regions!";
            return false;
        }
        return true;
    }

    private bool CompareParameterCount(FloatList data)
    {
        return RegionNumbers.Count == data.Count;
    }

    private float[] ComputeFriction(IZone[] zones, IDemographicCategory cat, float[] friction)
    {
        var numberOfZones = zones.Length;
        float[] ret = friction == null ? new float[numberOfZones * numberOfZones] : friction;
        // let it setup the modes so we can compute friction
        cat.InitializeDemographicCategory();
        Parallel.For(0, numberOfZones, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate (int i)
       {
           int index = i * numberOfZones;
           var origin = zones[i];
           if (!InverseLookup(zones[i].RegionNumber, out int regionIndex))
           {
               for (int j = 0; j < numberOfZones; j++)
               {
                   ret[index++] = 0;
               }
               return;
           }
           for (int j = 0; j < numberOfZones; j++)
           {
               var destination = zones[j];
               var autoTime = NetworkData.TravelTime(origin, destination, SimulationTime);
               var population = destination.Population;
               ret[index++] = (float)(RegionAutoParameter[regionIndex] * autoTime.ToMinutes()
                   // population
                   + RegionPopulationParameter[regionIndex] * Math.Log(population + 1)
                   // employment
                   + RegionEmploymentProfessionalParameter[regionIndex] * Math.Log(destination.ProfessionalEmployment + 1)
                   + RegionEmploymentGeneralParameter[regionIndex] * Math.Log(destination.GeneralEmployment + 1)
                   + RegionEmploymentSalesParameter[regionIndex] * Math.Log(destination.RetailEmployment + 1)
                   + RegionEmploymentManufacturingParameter[regionIndex] * Math.Log(destination.ManufacturingEmployment + 1));
           }
       });
        // Use the Log-Sum from the V's as the impedence function
        return ret;
    }

    private bool InverseLookup(int regionNumber, out int regionIndex)
    {
        return (regionIndex = RegionNumbers.IndexOf(regionNumber)) != -1;
    }

    private bool LoadNetwork()
    {
        foreach (var data in Root.NetworkData)
        {
            if (data.NetworkType == AutoNetworkName)
            {
                NetworkData = data;
                return true;
            }
        }
        return false;
    }
}