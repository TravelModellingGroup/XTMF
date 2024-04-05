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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datastructure;
using XTMF;

namespace TMG.GTAModel;

// ReSharper disable once InconsistentNaming
public class PoRPoSGeneration : DemographicCategoryGeneration
{
    [RunParameter("Generation FileName", "", "The name of the file to save to, this will append the file. Leave blank to not save.")]
    public string GenerationOutputFileName;

    [SubModelInformation(Description = "Used to gather the daily generation rates", Required = true)]
    public IDataSource<SparseTriIndex<float>> LoadDailyRates;

    [SubModelInformation(Description = "Used to gather the period generation rates", Required = true)]
    public IDataSource<SparseTriIndex<float>> LoadTimeOfDayRates;

    [RunParameter("Planning Districts", true, "Is the data using planning districts?")]
    public bool UsesPlanningDistricts;

    internal SparseTriIndex<float> DailyRates;

    internal bool LoadData = true;
    internal SparseTriIndex<float> TimeOfDayRates;

    override public void Generate(SparseArray<float> production, SparseArray<float> attractions)
    {
        if ( LoadData )
        {
            if ( DailyRates == null )
            {
                LoadDailyRates.LoadData();
                DailyRates = LoadDailyRates.GiveData();
            }
            if ( TimeOfDayRates == null )
            {
                LoadTimeOfDayRates.LoadData();
                TimeOfDayRates = LoadTimeOfDayRates.GiveData();
            }
        }
        var flatProduction = production.GetFlatData();
        var numberOfIndexes = flatProduction.Length;

        // Compute the Production
        ComputeProduction( flatProduction, numberOfIndexes );
        float totalProduction = flatProduction.Sum();
        WriteGenerationCSV( totalProduction );
        //The PoRPoS Model does NOT include having an attraction component.  The distribution will handle this case.
        if ( LoadData )
        {
            DailyRates = null;
            TimeOfDayRates = null;
        }
    }

    private float EmployedMobilityProbability(int mobility, int emp, int occ, SparseTriIndex<float> ncars, int age, SparseTwinIndex<float> dlicRate)
    {
        switch ( mobility )
        {
            case 0:
                return ( 1 - dlicRate[age, emp] ) * ncars[0, occ, 0];
            case 1:
                return ( 1 - dlicRate[age, emp] ) * ncars[0, occ, 1];
            case 2:
                return ( 1 - dlicRate[age, emp] ) * ncars[0, occ, 2];
            case 3:
                return ( dlicRate[age, emp] * ncars[1, occ, 0] );
            case 4:
                return dlicRate[age, emp] * ncars[1, occ, 1];
            case 5:
                return dlicRate[age, emp] * ncars[1, occ, 2];
            default:
                throw new XTMFRuntimeException(this, "Unknown mobility type '" + mobility + "'!" );
        }
    }

    private float UnemployedMobilityProbability(int mobility, SparseTriIndex<float> ncars, int age, SparseTwinIndex<float> dlicRate)
    {
        switch ( mobility )
        {
            case 0:
                return ( 1 - dlicRate[age, 0] ) * ncars[0, age, 0];
            case 1:
                return ( 1 - dlicRate[age, 0] ) * ncars[0, age, 1];
            case 2:
                return ( 1 - dlicRate[age, 0] ) * ncars[0, age, 2];
            case 3:
                return dlicRate[age, 0] * ncars[1, age, 0];
            case 4:
                return dlicRate[age, 0] * ncars[1, age, 1];
            case 5:
                return dlicRate[age, 0] * ncars[1, age, 2];
            default:
                throw new XTMFRuntimeException(this, "Unknown mobility type '" + mobility + "'!" );
        }
    }

    private float ApplyMobilityProbability(int mobility, int emp, int occ, SparseTriIndex<float> ncars, int age, SparseTwinIndex<float> dlicRate)
    {
        // make sure the age is greater than 0
        age = ( age == 0 ? 1 : age );
        // now we have slightly different forms depending if the person is a worker or not
        if ( emp == 0 )
        {
            return UnemployedMobilityProbability( mobility, ncars, age, dlicRate );
        }
        return EmployedMobilityProbability( mobility, emp, occ, ncars, age, dlicRate );
    }

    private void ComputeProduction(float[] flatProduction, int numberOfZones)
    {
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        Parallel.For( 0, numberOfZones, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            delegate (int i)
        {
            if ( zones[i].Population <= 0 | zones[i].RegionNumber == 0 )
            {
                return;
            }
            float temp = 0f;
            var ppDataSpacialIndex = UsesPlanningDistricts ? zones[i].PlanningDistrict : zones[i].ZoneNumber;
            var ageRates = Root.Demographics.AgeRates;
            var empRates = Root.Demographics.EmploymentStatusRates.GetFlatData()[i];
            var occRates = Root.Demographics.OccupationRates.GetFlatData()[i];
            var dlicRate = Root.Demographics.DriversLicenseRates.GetFlatData()[i];
            var ncars = Root.Demographics.WorkerVehicleRates.GetFlatData()[i];
            var unempCars = Root.Demographics.NonWorkerVehicleRates.GetFlatData()[i];
            var studentRates = Root.Demographics.SchoolRates.GetFlatData()[i];
            foreach ( var aSet in AgeCategoryRange )
            {
                for ( int age = aSet.Start; age <= aSet.Stop; age++ )
                {
                    // The data is actually 2D, we just dereference to 0 for easier module access
                    var ppGenerationRate = DailyRates[ppDataSpacialIndex, age, 0] * TimeOfDayRates[ppDataSpacialIndex, age, 0];
                    foreach ( var empSet in EmploymentStatusCategory )
                    {
                        for ( int emp = empSet.Start; emp <= empSet.Stop; emp++ )
                        {
                            var studentFactor = studentRates[age, emp];
                            if ( emp == 0 )
                            {
                                temp += GenerateForNonWorkingStudent( zones, i, ageRates, empRates, dlicRate, unempCars, age, ppGenerationRate, emp, studentFactor );
                            }
                            else
                            {
                                temp += GenerateForWorkingStudent( zones, i, ageRates, empRates, occRates, dlicRate, ncars, age, ppGenerationRate, emp, studentFactor );
                            }
                        }
                    }
                }
            }
            flatProduction[i] = temp;
        } );
    }

    private float GenerateForNonWorkingStudent(IZone[] zones, int zoneIndex, SparseTwinIndex<float> ageRates, SparseTwinIndex<float> empRates, SparseTwinIndex<float> dlicRate, SparseTriIndex<float> unempCars, int age, float ppGenerationRate, int emp, float studentFactor)
    {
        float temp = 0f;
        var nonMobilityCategoryRate = ageRates[zones[zoneIndex].ZoneNumber, age]
                    * empRates[age, emp]
                    * studentFactor;
        foreach ( var mobilitySet in Mobility )
        {
            for ( int mob = mobilitySet.Start; mob <= mobilitySet.Stop; mob++ )
            {
                // calculate the probability of a person of this type for this zone
                var catFactor = nonMobilityCategoryRate * ApplyMobilityProbability( mob, emp, 0, unempCars, age, dlicRate );
                // now apply that to
                temp += zones[zoneIndex].Population * catFactor *
                    ppGenerationRate;
            }
        }
        return temp;
    }

    private float GenerateForWorkingStudent(IZone[] zones, int zoneIndex, SparseTwinIndex<float> ageRates, SparseTwinIndex<float> empRates, SparseTriIndex<float> occRates, SparseTwinIndex<float> dlicRate, SparseTriIndex<float> ncars, int age, float ppGenerationRate, int emp, float studentFactor)
    {
        float temp = 0;
        var ageEmpRate = ageRates[zones[zoneIndex].ZoneNumber, age]
                            * empRates[age, emp] * studentFactor;
        foreach ( var occSet in OccupationCategory )
        {
            for ( int occ = occSet.Start; occ <= occSet.Stop; occ++ )
            {
                var nonMobilityRate = ageEmpRate * occRates[age, emp, occ];
                foreach ( var mobilitySet in Mobility )
                {
                    for ( int mob = mobilitySet.Start; mob <= mobilitySet.Stop; mob++ )
                    {
                        // calculate the probability of a person of this type for this zone
                        var catFactor = nonMobilityRate * ApplyMobilityProbability( mob, emp, occ, ncars, age, dlicRate );
                        // now apply that to
                        temp += zones[zoneIndex].Population * catFactor *
                            ppGenerationRate;
                    }
                }
            }
        }
        return temp;
    }

    private void WriteGenerationCSV(float totalProduction)
    {
        if ( !String.IsNullOrEmpty( GenerationOutputFileName ) )
        {
            bool first = !File.Exists( GenerationOutputFileName );
            // if the file name exists try to write to it, appending
            using StreamWriter writer = new(GenerationOutputFileName, true);
            if (first)
            {
                writer.WriteLine("Age,Employment,Occupation,Mobility,Total");
            }
            writer.Write(AgeCategoryRange.ToString());
            writer.Write(',');
            writer.Write(EmploymentStatusCategory.ToString());
            writer.Write(',');
            writer.Write(OccupationCategory.ToString());
            writer.Write(',');
            writer.Write(Mobility.ToString());
            writer.Write(',');
            writer.WriteLine(totalProduction);
        }
    }
}