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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMG;
using TMG.Input;
using XTMF;
using Datastructure;
using TMG.GTAModel.DataUtility;
namespace TMG.GTAModel.Purpose
{
    public class ExternalPurpose : PurposeBase
    {
        [SubModelInformation( Required = true, Description = "The base Year's distribution.(Origin,Destination,Mode,Data)" )]
        public IDataSource<SparseTriIndex<float>> ExternalBaseYearDistribution;

        [SubModelInformation( Required = true, Description = "The base year's population for external zones." )]
        public IDataSource<SparseArray<float>> ExternalBaseYearPopulation;

        [SubModelInformation( Required = true, Description = "The base year's employment for external zones." )]
        public IDataSource<SparseArray<float>> ExternalBaseYearEmployment;

        [Parameter( "OriginalModeNumbers", "1,2,3,6,7", typeof( NumberList ), "The mode indexes from the original model." )]
        public NumberList OriginalModeNumbers;

        [Parameter( "NewLeafNumbers", "1,2,4,8,7", typeof( NumberList ), "The mode's leaf indexes (1 based) for the new model's." )]
        public NumberList NewLeafNumbers;


        public override float Progress { get { return 0f; } }

        public override void Run()
        {
            // We only need to run in the first iteration
            if ( this.Root.CurrentIteration == 0 )
            {
                this.Flows = TMG.Functions.MirrorModeTree.CreateMirroredTree<float[][]>( this.Root.Modes );
                // Gather the data, process, then unload the data
                this.ExternalBaseYearDistribution.LoadData();
                this.ExternalBaseYearPopulation.LoadData();
                this.ExternalBaseYearEmployment.LoadData();
                var distribution = this.ExternalBaseYearDistribution.GiveData();
                var population = this.ExternalBaseYearPopulation.GiveData();
                var employment = this.ExternalBaseYearEmployment.GiveData();
                ProcessExternalModel( distribution, population, employment );
                this.ExternalBaseYearDistribution.UnloadData();
                this.ExternalBaseYearPopulation.UnloadData();
                this.ExternalBaseYearEmployment.UnloadData();
            }
        }

        private void ProcessExternalModel(SparseTriIndex<float> distribution, SparseArray<float> population, SparseArray<float> employment)
        {
            ProcessEI( distribution, population );
            ProcessIE( distribution, employment );
            ProcessEE( distribution, population, employment );
        }

        private void ProcessEE(SparseTriIndex<float> distribution, SparseArray<float> population, SparseArray<float> employment)
        {
            var indexes = population.ValidIndexArray();
            Parallel.For( 0, indexes.Length, delegate(int i)
            {
                var zones = this.Root.ZoneSystem.ZoneArray;
                var origin = indexes[i];
                var originTotal = population[origin] + employment[origin];
                var originZone = zones[origin];
                // The factor to apply to the distribution to map it to the results
                var factor = originTotal / ( originZone.Population + originZone.Employment );
                if ( originTotal <= 0 | originZone.RegionNumber > 0 | float.IsInfinity( factor ) )
                {
                    return;
                }
                var originIndex = zones.GetFlatIndex( origin );
                var numberOfZones = zones.GetFlatData().Length;
                foreach ( var destination in distribution.ValidIndexes( origin ) )
                {
                    var destinationIndex = zones.GetFlatIndex( destination );
                    // do not process EE trips
                    if ( zones[destination].RegionNumber != 0 )
                    {
                        continue;
                    }
                    foreach ( var mode in distribution.ValidIndexes( origin, destination ) )
                    {
                        var ammount = distribution[origin, destination, mode] * factor;
                        AddData( mode, originIndex, destinationIndex, ammount, numberOfZones );
                    }
                }
            } );
        }

        private void ProcessIE(SparseTriIndex<float> distribution, SparseArray<float> employment)
        {
            var indexes = employment.ValidIndexArray();
            Parallel.For( 0, indexes.Length, delegate(int i)
            {
                var zones = this.Root.ZoneSystem.ZoneArray;
                var destination = indexes[i];
                var destinationEmployment = employment[destination];
                var destinationZone = zones[destination];
                var factor = destinationEmployment / destinationZone.Employment;
                if ( destinationEmployment <= 0 | destinationZone.RegionNumber > 0 | float.IsInfinity( factor ) )
                {
                    return;
                }
                var destinationIndex = zones.GetFlatIndex( destination );
                // The factor to apply to the distribution to map it to the results
                var numberOfZones = zones.GetFlatData().Length;
                foreach ( var origin in distribution.ValidIndexes() )
                {
                    var originIndex = zones.GetFlatIndex( origin );
                    // do not process EE trips
                    if ( zones[origin].RegionNumber == 0 )
                    {
                        continue;
                    }
                    foreach ( var mode in distribution.ValidIndexes( origin, destination ) )
                    {
                        var ammount = distribution[origin, destination, mode] * factor;
                        if ( ammount <= 0 )
                        {
                            continue;
                        }
                        AddData( mode, originIndex, destinationIndex, ammount, numberOfZones );
                    }
                }
            } );
        }

        private void ProcessEI(SparseTriIndex<float> distribution, SparseArray<float> population)
        {
            var indexes = population.ValidIndexArray();
            Parallel.For( 0, indexes.Length, delegate(int i)
            {
                var zones = this.Root.ZoneSystem.ZoneArray;
                var zoneNumber = indexes[i];
                var originPopulation = population[zoneNumber];
                var originZone = zones[zoneNumber];
                var factor = originPopulation / originZone.Population;
                if ( originPopulation <= 0 | originZone.RegionNumber > 0 | float.IsInfinity( factor ) )
                {
                    return;
                }
                var originIndex = zones.GetFlatIndex( zoneNumber );
                // The factor to apply to the distribution to map it to the results

                var numberOfZones = zones.GetFlatData().Length;
                foreach ( var destination in distribution.ValidIndexes( zoneNumber ) )
                {
                    var destinationIndex = zones.GetFlatIndex( destination );
                    // do not process EE trips
                    if ( zones[destination].RegionNumber == 0 )
                    {
                        continue;
                    }
                    foreach ( var mode in distribution.ValidIndexes( zoneNumber, destination ) )
                    {
                        var ammount = distribution[zoneNumber, destination, mode] * factor;
                        AddData( mode, originIndex, destinationIndex, ammount, numberOfZones );
                    }
                }
            } );
        }

        private int RemapMode(int mode)
        {
            var index = this.OriginalModeNumbers.IndexOf( mode );
            if ( index > -1 )
            {
                return this.NewLeafNumbers[index];
            }
            throw new XTMFRuntimeException( "In '" + this.Name 
                + "' we were unable to map a mode with index '" + mode + "' to any new leaf mode." );
        }

        private void AddData(int mode, int originIndex, int destinationIndex, float ammount, int numberOfZones)
        {
            TreeData<float[][]> modeData = TMG.Functions.MirrorModeTree.GetLeafNodeWithIndex( this.Flows, RemapMode( mode ) - 1 );
            // ensure there is data for us to store a result
            if ( modeData.Result == null )
            {
                lock ( modeData )
                {
                    if ( modeData.Result == null )
                    {
                        modeData.Result = new float[numberOfZones][];
                    }
                }
            }
            if ( modeData.Result[originIndex] == null )
            {
                lock ( modeData )
                {
                    System.Threading.Thread.MemoryBarrier();
                    if ( modeData.Result[originIndex] == null )
                    {
                        modeData.Result[originIndex] = new float[numberOfZones];
                    }
                }
            }
            modeData.Result[originIndex][destinationIndex] = ammount;
        }

        public override bool RuntimeValidation(ref string error)
        {
            if ( this.OriginalModeNumbers.Count != this.NewLeafNumbers.Count )
            {
                error = "In '" + this.Name + "' the number of parameters in OriginalModeNumbers must be the same as in NewLeafNumbers!";
                return false;
            }
            return base.RuntimeValidation( ref error );
        }
    }
}
