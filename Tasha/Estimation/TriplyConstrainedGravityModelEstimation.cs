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
using System.Text;
using System.Threading.Tasks;
using TMG.Estimation;
using XTMF;
using Datastructure;
using TMG.Functions;
using TMG;

namespace Tasha.Estimation
{

    public class TriplyConstrainedGravityModelEstimation : ITravelDemandModel
    {
        [RootModule]
        public IEstimationClientModelSystem Root;

        [RunParameter("Input Directory", "../../Input", "The directory containing the input files for this run relative to the run directory or using an absolute path.")]
        public string InputBaseDirectory { get; set; }


        public string Name { get; set; }

        public string OutputBaseDirectory { get; set; }


        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = false, Description = "The network data for estimation")]
        public IList<INetworkData> NetworkData
        {
            get; set;
        }

        [SubModelInformation(Required = true, Description = "The zone system for the estimation.")]
        public IZoneSystem ZoneSystem
        {
            get; set;
        }


        public IResource TruthData;
        public IResource ModelData;

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            if(!TruthData.CheckResourceType<SparseTriIndex<float>>())
            {
                error = "In '" + Name + "' TruthData is not a SparseTriIndex<float>!";
                return false;
            }
            if(!ModelData.CheckResourceType<SparseTriIndex<float>>())
            {
                error = "In '" + Name + "' ModelData is not a SparseTriIndex<float>!";
                return false;
            }
            return true;
        }

        bool first = true;
        /// <summary>
        /// The truth for each category
        /// </summary>
        float[] TotalTruth;
        public void Start()
        {
            if(!ZoneSystem.Loaded)
            {
                ZoneSystem.LoadData();
            }
            var truth = TruthData.AquireResource<SparseTriIndex<float>>().GetFlatData();
            if(first)
            {
                TotalTruth = truth.Select(category => category.Sum(row => VectorHelper.VectorSum(row, 0, row.Length))).ToArray();
                for(int category = 0; category < TotalTruth.Length; category++)
                {
                    //normalize the truth data
                    Parallel.For(0, truth[category].Length, (int i) =>
                    {
                        float[] truthRow = truth[category][i];
                        VectorHelper.VectorMultiply(truthRow, 0, truthRow, 0, 1.0f / TotalTruth[category], truthRow.Length);
                    });
                }
                for(int i = 0; i < NetworkData.Count; i++)
                {
                    NetworkData[i].LoadData();
                }
                first = false;
            }
            var model = ModelData.AquireResource<SparseTriIndex<float>>().GetFlatData();
            ModelData.ReleaseResource();
            // Normalize the model data
            float[] modelTotalByCategory = model.Select(cateogry => cateogry.Sum(row => VectorHelper.VectorSum(row, 0, row.Length))).ToArray();
            for(int category = 0; category < modelTotalByCategory.Length; category++)
            {
                //normalize the truth data
                Parallel.For(0, model[category].Length, (int i) =>
                    {
                        float[] modelRow = model[category][i];
                        VectorHelper.VectorMultiply(modelRow, 0, modelRow, 0, 1.0f / modelTotalByCategory[category], modelRow.Length);
                    });
            }
            float fitness = 0.0f;
            for(int category = 0; category < truth.Length; category++)
            {
                Parallel.For(0, truth[category].Length, 
                    ()=>
                {
                    return new float[truth[category].Length];
                },
                (int i, ParallelLoopState _, float[] errorForHomeZone) =>
                {
                    var truthVector = truth[category][i];
                    var modelRow = model[category][i];
                    for(int j = 0; j < truthVector.Length; j++)
                    {
                        errorForHomeZone[i] += truthVector[j] * (float)Math.Log(modelRow[j] + 1.0e-10f);
                    }
                    return errorForHomeZone;
                },
                (float[] errorData)=>
                {
                    var sumOfError = errorData.Sum();
                    lock (this)
                    {
                        fitness += sumOfError;
                    }
                });

            }
            Root.RetrieveValue = () => TotalTruth.Sum() * fitness;
        }
    }

}
