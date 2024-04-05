/*
    Copyright 2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;
using TMG.Input;
using Datastructure;
using TMG;
using TMG.Functions;
using System.IO;

namespace Tasha.Validation.Scheduler
{
    public sealed class SaveTripLengthFrequencyDistribution : ISelfContainedModule
    {
        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation(Required = false, Description = "The distance matrix to use (in metres), leave blank to use the zone system.")]
        public IDataSource<SparseTwinIndex<float>> DistanceMatrix;

        [SubModelInformation(Required = true, Description = "The trips to be stored.")]
        public IDataSource<SparseTwinIndex<float>> Demand;

        [RunParameter("Stride", 2.0f, "The step size in KM for each bin.")]
        public float Stride;

        [RunParameter("Bins", 100, "The number of bins after intrazonal to save data for. Distances farther than this will be added to an additional bin.")]
        public int Bins;

        [SubModelInformation(Required = true, Description = "The location to save the results to. (min,max,value)")]
        public FileLocation SaveTo;

        public void Start()
        {
            var distances = GetDistances();
            var demand = GetDemand();
            if(!ZoneSystemHelper.IsSameZoneSystem(demand, distances))
            {
                throw new XTMFRuntimeException(this, "The zone systems for the demand and distance matrices are not the same!");
            }
            float intrazonal = 0f;
            float pastBins = 0f;
            var flatDistances = distances.GetFlatData();
            var flatDemand = demand.GetFlatData();
            var bins = new float[Bins];
            var strideInMeters = Stride * 1000;
            // Store the demand into the correct bins
            for (int i = 0; i < flatDemand.Length; i++)
            {
                for (int j = 0; j < flatDemand[i].Length; j++)
                {
                    if(i != j)
                    {
                        var index = (int)(flatDistances[i][j] / strideInMeters);
                        if(index >= bins.Length)
                        {
                            pastBins += flatDemand[i][j];
                        }
                        else
                        {
                            bins[index] += flatDemand[i][j];
                        }
                    }
                    else
                    {
                        intrazonal += flatDemand[i][j];
                    }
                }
            }
            // Save the results to file
            try
            {
                using var writer = new StreamWriter(SaveTo);
                writer.WriteLine("Min,Max,Value");
                writer.Write("intrazonal,0,");
                writer.WriteLine(intrazonal);
                for (int i = 0; i < bins.Length; i++)
                {
                    writer.Write(i * Stride);
                    writer.Write(',');
                    writer.Write((i + 1) * Stride);
                    writer.Write(',');
                    writer.WriteLine(bins[i]);
                }
                writer.Write(Stride * bins.Length);
                writer.Write(",inf,");
                writer.WriteLine(pastBins);
            }
            catch(IOException e)
            {
                throw new XTMFRuntimeException(this, e, $"Unable to write to the file at location '{SaveTo.GetFilePath()}' {e.Message}");
            }
        }

        private SparseTwinIndex<float> GetDemand() => LoadFrom(Demand);

        private SparseTwinIndex<float> GetDistances() => DistanceMatrix != null ? LoadFrom(DistanceMatrix) : Root.ZoneSystem.Distances;

        private static SparseTwinIndex<float> LoadFrom(IDataSource<SparseTwinIndex<float>> source)
        {
            bool loaded = source.Loaded;
            if (!loaded)
            {
                source.LoadData();
            }
            var ret = source.GiveData();
            if (!loaded)
            {
                source.UnloadData();
            }
            return ret;
        }

        public bool RuntimeValidation(ref string error)
        {
            if(Bins <= 0)
            {
                error = "The number of bins must be greater than zero!";
                return false;
            }
            return true;
        }
    }
}
