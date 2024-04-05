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
using System.IO;
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using TMG.ModeSplit;
using XTMF;
using Range = Datastructure.Range;
// ReSharper disable AccessToModifiedClosure
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel.V2.Distribution;

public class ProbabilityDistribution : IDemographicDistribution
{
    [SubModelInformation( Description = "Used to gather the daily generation rates", Required = true )]
    public List<IDataSource<SparseTriIndex<float>>> LoadDailyRatesList;

    [SubModelInformation( Description = "Used to gather the period generation rates", Required = true )]
    public List<IDataSource<SparseTriIndex<float>>> LoadTimeOfDayRatesList;

    [RunParameter( "Mobility Cache", "Mobility", "The starting file path for the mobility cache data.  If the file path is 'Data/Mobility0.bin', set it to 'Data/Mobility'." )]
    public string MobilityCacheFile;

    [Parameter( "Mobility Range", "0-4", typeof( RangeSet ), "The different mobilities to execute against." )]
    public RangeSet MobilityRanges;

    [Parameter( "Categories Per Occupation", 20, "The number of categories that will be used before we switch occupations.  This should be (Mobility Types * Working Age Categories * Employment Status Categories)." )]
    public int NumberOfCategoriesPerOccupation;

    [ParentModel]
    public IPurpose Parent;

    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation( Required = false, Description = "Implement this if you wish to save the distribution results to disk." )]
    public ISaveODDataSeries<float> SaveDistributionSeries;

    [RunParameter( "Work Cache", "Work", "The starting file path for the work PoRPoW model data.  If the file path is 'Data/Work0.bin', set it to 'Data/Work'." )]
    public string WorkCacheFile;

    [DoNotAutomate]
    protected IInteractiveModeSplit ModeSplit;

    private IDataSource<SparseTriIndex<float>> CurrentDailyRates;

    private IDataSource<SparseTriIndex<float>> CurrentTimeOfDayRates;

    /// <summary>
    /// For Each Age [Origin * (NumberOfZones * MobilityTypes) + MobilityType * Number Of Zones + Destination]
    /// </summary>
    private float[] MobilityCache;

    private int NumberOfGenerations;

    public string Name { get; set; }

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
        if ( SaveDistributionSeries != null )
        {
            SaveDistributionSeries.Reset();
        }
        IDemographicCategory cat;
        var ret = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
        var linkages = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
        using (var catEnum = category.GetEnumerator())
        using (var productionEnum = productions.GetEnumerator())
        {
            int catIndex = 0;
            var rangeSet = new RangeSet(new[] {0});
            // just multiply by the number of occupations in v2 since this is a throw away module
            NumberOfGenerations = NumberOfCategoriesPerOccupation * 4;
            while (catEnum.MoveNext() & productionEnum.MoveNext())
            {
                cat = catEnum.Current;
                var ageRate = productionEnum.Current;
                Parallel.Invoke(
                    () => SetupMobilityInformation(catIndex),
                    () => LoadLinkages(catIndex, linkages.GetFlatData())
                );
                foreach (var mobilitySet in MobilityRanges)
                {
                    for (int mobility = mobilitySet.Start; mobility <= mobilitySet.Stop; mobility++)
                    {
                        var realCat = cat as DemographicCategoryGeneration;
                        if (realCat == null)
                        {
                            throw new XTMFRuntimeException(this, "In '" + Name +
                                                           "' it is required that all generates be of the type DemographicCategoryGeneration!");
                        }
                        rangeSet[0] = new Range(mobility, mobility);
                        realCat.Mobility = rangeSet;
                        DistributePopulation(realCat, ageRate.GetFlatData(), linkages.GetFlatData(), catIndex,
                            ret.GetFlatData());
                        Task save = null;
                        if (SaveDistributionSeries != null)
                        {
                            save = Task.Factory.StartNew(() =>
                            {
                                SaveDistribution(ret);
                            });
                        }
                        yield return ret;
                        if (save != null)
                        {
                            save.Wait();
                        }
                        catIndex++;
                    }
                }
            }
        }
        // Free up our memory
        MobilityCache = null;
        UnloadRates();
    }

    public bool RuntimeValidation(ref string error)
    {
        ModeSplit = Parent.ModeSplit as IInteractiveModeSplit;
        if ( ModeSplit == null )
        {
            error = "In '" + Name
                + "' it is required that the parent module uses an IInteractive Mode Choice module!  Please contact your model system provider for assistance.";
            return false;
        }
        return true;
    }

    private static void FillBuffer(BinaryReader reader, byte[] temp)
    {
        var count = 0;
        while ( count < temp.Length )
        {
            count += reader.Read( temp, count, temp.Length - count );
        }
    }

    private void DistributePopulation(DemographicCategoryGeneration cat, float[] ageProbability, float[][] assignedPersons, int catIndex, float[][] ret)
    {
        var timeOfDayRate = CurrentTimeOfDayRates.GiveData();
        var dailyRates = CurrentDailyRates.GiveData();
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        var numberOfMobilityTypes = GetMobilityTypeTotal();
        var currentMobilityType = catIndex % numberOfMobilityTypes;
        ModeSplit.StartNewInteractiveModeSplit( NumberOfGenerations );
        // Setup the mode choice function, since each zone is the same, we only need to do this once
        cat.InitializeDemographicCategory();
        var age = cat.AgeCategoryRange[0].Start;
        //ageOfffset = foreach each OD Pair, for each mobility category multiplied by the age - 2 (workrs start at 2).
        var ageOffset = ( 5 * zones.Length * zones.Length ) * ( age - 2 );
        // for each origin zone
        Parallel.For( 0, zones.Length, i =>
            {
                var pdI = zones[i].PlanningDistrict;
                // Offset of the origin
                var iOffset = i * zones.Length * numberOfMobilityTypes;
                // offset by the age category plus the mobility category offset
                iOffset += ageOffset + currentMobilityType * zones.Length;
                // for each destination
                var row = ret[i];
                var assignedPersonRow = assignedPersons[i];
                if ( ageProbability[i] == 0 )
                {
                    for ( int j = 0; j < zones.Length; j++ )
                    {
                        row[j] = 0;
                    }
                }
                else
                {
                    int currentJIndex = 0;
                    for ( int j = 0; j < zones.Length; j++ )
                    {
                        if ( assignedPersonRow[j] <= 0 )
                        {
                            row[j] = 0;
                        }
                        else if ( j == i )
                        {
                            throw new XTMFRuntimeException(this, "We found assigned persons for an intra-zonal work trip!" );
                        }
                        else
                        {
                            var pdJ = zones[j].PlanningDistrict;
                            // compute the mode choice
                            var util = ModeSplit.ComputeUtility( zones[i], zones[j] );
                            if ( float.IsNaN( util ) | float.IsInfinity( util ) )
                            {
                                throw new XTMFRuntimeException(this, "We came across an invalid utility! Age = " + age + " origin = " + zones[i].ZoneNumber + " destination = " + zones[j].ZoneNumber
                                    + " demographic category = '" + cat.ToString() + "'" );
                            }
                            // Process how many persons go here
                            // probabilities contains the probability of being in that age category already
                            var rate = timeOfDayRate[pdI, pdJ, age] * dailyRates[pdI, pdJ, age];
                            var persons = ageProbability[i] * assignedPersonRow[j];
                            if ( ( rate == 0 ) & ( persons > 0 ) )
                            {
                                throw new XTMFRuntimeException(this, "We lost generation for " + zones[i].ZoneNumber + " to " + zones[j].ZoneNumber + ". " + persons
                                    + " persons in category " + catIndex + "." );
                            }
                            row[j] = persons * rate * MobilityCache[iOffset + currentJIndex];
                        }
                        currentJIndex++;
                    }
                }
            } );
    }

    private int GetMobilityTypeTotal()
    {
        int total = 0;
        foreach ( var range in MobilityRanges )
        {
            total += range.Stop - range.Start + 1;
        }
        return total;
    }

    private void LoadLinkages(int catIndex, float[][] linkageStoreage)
    {
        if ( catIndex % NumberOfCategoriesPerOccupation == 0 )
        {
            var loadIn = catIndex / NumberOfCategoriesPerOccupation;
            var file = WorkCacheFile + loadIn + ".bin";
            using BinaryReader reader = new(File.OpenRead(file));
            var length = (int)reader.BaseStream.Length;
            byte[] temp = new byte[length];
            FillBuffer(reader, temp);
            int count = 0;
            for (int i = 0; i < linkageStoreage.Length; i++)
            {
                var row = linkageStoreage[i];
                var rowLength = row.Length * sizeof(float);
                Buffer.BlockCopy(temp, count, row, 0, rowLength);
                count += rowLength;
            }
        }
    }

    private void SaveDistribution(SparseTwinIndex<float> sparseRet)
    {
        SaveDistributionSeries.SaveMatrix( sparseRet.GetFlatData() );
    }

    private void SetupMobilityInformation(int catIndex)
    {
        if ( catIndex % NumberOfCategoriesPerOccupation == 0 )
        {
            // time for the next one
            var loadIn = catIndex / NumberOfCategoriesPerOccupation;
            var file = MobilityCacheFile + loadIn + ".bin";
            using ( BinaryReader reader = new( File.OpenRead( file ) ) )
            {
                var size = (int)reader.BaseStream.Length / 4;
                MobilityCache = new float[size];
                byte[] temp = new byte[size * sizeof( float )];
                FillBuffer( reader, temp );
                Buffer.BlockCopy( temp, 0, MobilityCache, 0, temp.Length );
            }
            UnloadRates();
            CurrentDailyRates = LoadDailyRatesList[loadIn];
            CurrentTimeOfDayRates = LoadTimeOfDayRatesList[loadIn];
            CurrentDailyRates.LoadData();
            CurrentTimeOfDayRates.LoadData();
        }
    }

    private void UnloadRates()
    {
        if ( CurrentTimeOfDayRates != null )
        {
            CurrentDailyRates.UnloadData();
            CurrentTimeOfDayRates.UnloadData();
        }
    }
}