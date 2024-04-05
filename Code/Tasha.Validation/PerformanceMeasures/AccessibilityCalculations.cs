/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.IO;
using TMG.Input;
using Datastructure;
using TMG;
using TMG.DataUtility;
using Tasha.Common;
using XTMF;


namespace Tasha.Validation.PerformanceMeasures;

public class AccessibilityCalculations : ISelfContainedModule
{
    [RootModule]
    public ITashaRuntime Root;
    
    [RunParameter("Population Zones to Analyze", "1-1000", typeof(RangeSet), "The zones that you want to do the accessibility calculations for")]
    public RangeSet PopZoneRange;

    [RunParameter("Employment Zones to Analyze", "1-9999", typeof(RangeSet), "Which employment zones do you want to do accessibility calculations for")]
    public RangeSet EmpZoneRange;

    [SubModelInformation(Required = true, Description = "File containing the NIA data")]
    public IResource NIAData;

    [SubModelInformation(Required = true, Description = "File containing the employment data")]
    public IResource EmploymentData;

    [SubModelInformation(Required = true, Description = "The auto time matrix")]
    public IResource AutoTimeMatrix;

    [SubModelInformation(Required = true, Description = "The transit IVTT matrix")]
    public IResource TransitIVTTMatrix; 

    [SubModelInformation(Required = true, Description = "The resource that will add all three transit time matrices")]
    public IResource TotalTransitTimeMatrix;

    [RunParameter("Accessibility Times to Analyze", "10,15,20,30,45,60,90", typeof(NumberList), "A comma separated list of accessibility times to execute this against.")]
    public NumberList AccessibilityTimes;

    [SubModelInformation(Required = true, Description = "Results file in .CSV format ")]
    public FileLocation ResultsFile;

    Dictionary<int, float> AutoAccessibilityResults = [];
    Dictionary<int, float> TransitIVTTAccessibilityResults = [];
    Dictionary<int, float> TransitAccessibilityResults = [];

    public void Start()
    {
        var zoneSystem = Root.ZoneSystem.ZoneArray;
        var zones = zoneSystem.GetFlatData();
        var niApop = NIAData.AcquireResource<SparseArray<float>>().GetFlatData();
        var employmentByZone = EmploymentData.AcquireResource<SparseArray<float>>().GetFlatData();            
        var autoTimes = AutoTimeMatrix.AcquireResource<SparseTwinIndex<float>>().GetFlatData();
        var transitIVTT = TransitIVTTMatrix.AcquireResource<SparseTwinIndex<float>>().GetFlatData();
        var totalTransitTimes = TotalTransitTimeMatrix.AcquireResource<SparseTwinIndex<float>>().GetFlatData();            

        float[] zonePopulation = (from z in Root.ZoneSystem.ZoneArray.GetFlatData()                                           
                                       select (float)z.Population).ToArray();

        float analyzedpopulationSum = (from z in Root.ZoneSystem.ZoneArray.GetFlatData()
                                       where PopZoneRange.Contains(z.ZoneNumber)
                                        select z.Population).Sum();

        float employmentSum = (from z in Root.ZoneSystem.ZoneArray.GetFlatData()
                               where EmpZoneRange.Contains(z.ZoneNumber)
                               select employmentByZone[zoneSystem.GetFlatIndex(z.ZoneNumber)]).Sum();

        float niAsum = niApop.Sum();
        var normalDenominator = 1.0f / (analyzedpopulationSum * employmentSum);
        var niaDenominator = 1.0f / (niAsum * employmentSum);

        using StreamWriter writer = new(ResultsFile);
        CalculateAccessibility(zones, employmentByZone, autoTimes, transitIVTT, totalTransitTimes, zonePopulation, false);
        writer.WriteLine("Analyzed Population Accessibility");
        WriteToFile(normalDenominator, writer);
        writer.WriteLine();

        AutoAccessibilityResults.Clear();
        TransitIVTTAccessibilityResults.Clear();
        TransitAccessibilityResults.Clear();

        CalculateAccessibility(zones, employmentByZone, autoTimes, transitIVTT, totalTransitTimes, niApop, true);
        writer.WriteLine("NIA Zone Accessibility");
        WriteToFile(niaDenominator, writer);

        AutoAccessibilityResults.Clear();
        TransitIVTTAccessibilityResults.Clear();
        TransitAccessibilityResults.Clear();
    }

    private void WriteToFile(float denominator, StreamWriter writer)
    {
        writer.WriteLine("Auto Accessibility");
        writer.WriteLine("Time(mins), Percentage Accessible");
        foreach (var pair in AutoAccessibilityResults)
        {
            var percentageAccessible = AutoAccessibilityResults[pair.Key] * denominator;
            writer.WriteLine("{0},{1}", pair.Key, percentageAccessible);
        }

        writer.WriteLine("Transit IVTT Accessibility");
        writer.WriteLine("Time(mins), Percentage Accessible");
        foreach (var pair in TransitIVTTAccessibilityResults)
        {
            var percentageAccessible = TransitIVTTAccessibilityResults[pair.Key] * denominator;
            writer.WriteLine("{0},{1}", pair.Key, percentageAccessible);
        }

        writer.WriteLine("Total Transit Time Accessibility");
        writer.WriteLine("Time(mins), Percentage Accessible");
        foreach (var pair in TransitAccessibilityResults)
        {
            var percentageAccessible = TransitAccessibilityResults[pair.Key] * denominator;
            writer.WriteLine("{0},{1}", pair.Key, percentageAccessible);
        }
    }

    private void CalculateAccessibility(IZone[] zones, float[] employmentByZone, float[][] autoTimes, float[][] transitIVTT, 
        float[][] totalTransitTimes, float[] zonePopulation, bool niaCalc)
    {
        float accessiblePopulation;

        foreach (var accessTime in AccessibilityTimes)
        {
            AddToResults(0, accessTime, AutoAccessibilityResults);
            AddToResults(0, accessTime, TransitIVTTAccessibilityResults);
            AddToResults(0, accessTime, TransitAccessibilityResults);
            for (int i = 0; i < zonePopulation.Length; i++)
            {
                if (PopZoneRange.Contains(zones[i].ZoneNumber) || niaCalc)
                {
                    for (int j = 0; j < employmentByZone.Length; j++)
                    {
                        if (EmpZoneRange.Contains(zones[j].ZoneNumber))
                        {
                            if (autoTimes[i][j] < accessTime)
                            {
                                accessiblePopulation = (zonePopulation[i] * employmentByZone[j]);
                                AddToResults(accessiblePopulation, accessTime, AutoAccessibilityResults);
                            }
                            if (transitIVTT[i][j] < accessTime)
                            {
                                accessiblePopulation = zonePopulation[i] * employmentByZone[j];
                                AddToResults(accessiblePopulation, accessTime, TransitIVTTAccessibilityResults);
                            }
                            if (totalTransitTimes[i][j] < accessTime)
                            {
                                accessiblePopulation = zonePopulation[i] * employmentByZone[j];
                                AddToResults(accessiblePopulation, accessTime, TransitAccessibilityResults);
                            }
                        }
                    }
                }
            }
        }
    }


    public void AddToResults(float population, int accessTime, Dictionary<int, float> results)
    {
        if(results.ContainsKey(accessTime))
        {
            results[accessTime] += population;
        }
        else 
        {
            results.Add(accessTime, population);
        }
    }

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
        get { return new Tuple<byte, byte, byte>(120, 25, 100); }
    }

    public bool RuntimeValidation(ref string error)
    {
        if (!EmploymentData.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the ODEmployment was not of type SparseArray<float>!";
            return false;
        }

        else if (!AutoTimeMatrix.CheckResourceType<SparseTwinIndex<float>>())
        {
            error = "In '" + Name + "' the AutoTimeMatrix was not of type SparseTwinIndex<float>!";
            return false;
        }

        else if (!TransitIVTTMatrix.CheckResourceType<SparseTwinIndex<float>>())
        {
            error = "In '" + Name + "' the AutoTimeMatrix was not of type SparseTwinIndex<float>!";
            return false;
        }

        else if (!NIAData.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the NIAData was not of type SparseTwinIndex<float>!";
            return false;
        }

        return true;  
    }
}
