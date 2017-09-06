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
using Datastructure;
using System;
using System.Linq;
using XTMF;
namespace TMG.Frameworks.Data.Loading
{
    [ModuleInformation(Description =
@"This module is designed to get the network information from a travel demand model and store it into a single matrix."
        )]
    public class LoadINetworkDataMatrix : IDataSource<SparseTwinIndex<float>>
    {
        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        private SparseTwinIndex<float> Data;

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("Network Name", "", "The name of the network to load from.")]
        public string Network;

        [RunParameter("Data Type To Load", nameof(NetworkDataType.Time), typeof(NetworkDataType), "The type of data to load from the network.")]
        public NetworkDataType TypeToLoad;

        [RunParameter("Time", "7:00AM", typeof(Time), "The time of day to get the data for.")]
        public Time TimeToLoad;

        public enum NetworkDataType
        {
            Time,
            Cost
        }

        public SparseTwinIndex<float> GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
            var network = Root.NetworkData.FirstOrDefault(n => n.NetworkType == Network);
            if (network == null)
            {
                throw new XTMFRuntimeException(this, $"In {Name} we were unable to find a network with the name '{Network}'");
            }
            if (!network.Loaded)
            {
                network.LoadData();
            }
            var data = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            switch (TypeToLoad)
            {
                case NetworkDataType.Time:
                    LoadTimes(data, network);
                    break;
                case NetworkDataType.Cost:
                    LoadCosts(data, network);
                    break;
                default:
                    throw new 
                        XTMFRuntimeException(this, $"In {Name} we were unable to identify the type of data to load found type {Enum.GetName(typeof(NetworkDataType), TypeToLoad)}!");

            }
            Data = data;
            Loaded = true;
        }

        private void LoadTimes(SparseTwinIndex<float> data, INetworkData network)
        {
            var flatData = data.GetFlatData();
            var time = TimeToLoad;
            for (int i = 0; i < flatData.Length; i++)
            {
                var row = flatData[i];
                for (int j = 0; j < row.Length; j++)
                {
                    row[j] = network.TravelTime(i, j, time).ToMinutes();
                }
            }
        }

        private void LoadCosts(SparseTwinIndex<float> data, INetworkData network)
        {
            var flatData = data.GetFlatData();
            var time = TimeToLoad;
            for (int i = 0; i < flatData.Length; i++)
            {
                var row = flatData[i];
                for (int j = 0; j < row.Length; j++)
                {
                    row[j] = network.TravelCost(i, j, time);
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {
            Loaded = false;
            Data = null;
        }
    }

}
