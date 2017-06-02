/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using TMG.Functions;

namespace TMG.Frameworks.Data.Processing
{
    [ModuleInformation(Description = "This module provides an easy way to compute a gravity model given production, attraction, and the friction.")]
    public sealed class GravityModel2D : IDataSource<SparseTwinIndex<float>>
    {
        public bool Loaded
        {
            get { return Data != null; }
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private SparseTwinIndex<float> Data;

        public IDataSource<SparseArray<float>> Production;

        public IDataSource<SparseArray<float>> Attraction;

        public IDataSource<SparseTwinIndex<float>> Friction;

        [RunParameter("Maximum Error", 0.05f, "The maximum error allowed for each attraction.")]
        public float MaximumError;

        [RunParameter("Max Iterations", 100, "The maximum iterations that will be executed.  Even if the maximum error is no")]
        public int MaxIterations;

        [RunParameter("Unload production", true, "Should we unload production if we load it?")]
        public bool UnloadProduction;

        [RunParameter("Unload attraction", true, "Should we unload attraction if we load it?")]
        public bool UnloadAttraction;

        [RunParameter("Unload friction", true, "Should we unload friction if we load it?")]
        public bool UnloadFriction;

        public enum Balance
        {
            NoBalancing,
            AverageProductionAttraction,
            MatchToProduction,
            MatchToAttraction
        }

        [RunParameter("Production Attraction Balancing", nameof(Balance.MatchToProduction), typeof(Balance), "The function to use in order to balance the production and attraction going into the gravity model.")]
        public Balance BalanceFunction;

        public SparseTwinIndex<float> GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
            var production = GetData(Production, UnloadProduction);
            var attraction = GetData(Attraction, UnloadAttraction);
            var friction = GetData(Friction, UnloadFriction);
            ApplyBalance(ref production, ref attraction);
            var model = new GravityModel(friction, (p) => Progress = p, MaximumError, MaxIterations);
            Data = model.ProcessFlow(production, attraction, production.ValidIndexArray());
        }

        private void ApplyBalance(ref SparseArray<float> production, ref SparseArray<float> attraction)
        {
            switch (BalanceFunction)
            {
                case Balance.NoBalancing:
                    // nothing to do
                    return;
                case Balance.MatchToProduction:
                    MatchAttractionToProduction(ref production, ref attraction);
                    break;
                case Balance.MatchToAttraction:
                    // assign backwards
                    MatchAttractionToProduction(ref attraction, ref production);
                    break;
                case Balance.AverageProductionAttraction:
                    AverageAttractionAndProduction(ref production, ref attraction);
                    break;
            }
        }

        private void AverageAttractionAndProduction(ref SparseArray<float> production, ref SparseArray<float> attraction)
        {
            var newAttraction = attraction.CreateSimilarArray<float>();
            var newProduction = production.CreateSimilarArray<float>();
            var flatAttraction = attraction.GetFlatData();
            var flatProduction = production.GetFlatData();
            var newFlatProduction = newProduction.GetFlatData();
            var newFlatAttraction = newAttraction.GetFlatData();
            var productionTotal = VectorHelper.Sum(flatProduction, 0, flatProduction.Length);
            var attractionTotal = VectorHelper.Sum(flatAttraction, 0, flatAttraction.Length);
            var totalAverage = productionTotal + attractionTotal / 2.0f;
            var ratio = totalAverage / attractionTotal;
            if (float.IsNaN(ratio) || float.IsInfinity(ratio))
            {
                ratio = 0.0f;
            }
            VectorHelper.Multiply(newFlatAttraction, flatAttraction, ratio);
            ratio = totalAverage / productionTotal;
            if (float.IsNaN(ratio) || float.IsInfinity(ratio))
            {
                ratio = 0.0f;
            }
            VectorHelper.Multiply(newFlatProduction, flatProduction, 1.0f/ratio);
            attraction = newAttraction;
            production = newProduction;
        }

        private void MatchAttractionToProduction(ref SparseArray<float> production, ref SparseArray<float> attraction)
        {
            var newAttraction = attraction.CreateSimilarArray<float>();
            var flatAttraction = attraction.GetFlatData();
            var flatProduction = production.GetFlatData();
            var newFlatAttraction = newAttraction.GetFlatData();
            var ratio = VectorHelper.Sum(flatProduction, 0, flatProduction.Length) / VectorHelper.Sum(flatAttraction, 0, flatAttraction.Length);
            if (float.IsNaN(ratio) || float.IsInfinity(ratio))
            {
                ratio = 0.0f;
            }
            VectorHelper.Multiply(newFlatAttraction, flatAttraction, ratio);
            attraction = newAttraction;
        }

        private static T GetData<T>(IDataSource<T> dataSource, bool unloadIfLoad)
        {
            bool loaded = false;
            if (!dataSource.Loaded)
            {
                dataSource.LoadData();
                loaded = true;
            }
            var ret = dataSource.GiveData();
            if(loaded && unloadIfLoad)
            {
                dataSource.UnloadData();
            }
            return ret;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Data = null;
        }
    }

}
