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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMG.Input;
using XTMF;
namespace TMG.Emme.Utilities
{
    [ModuleInformation(Description =

@"This module is designed to compute the Level of Service variables between nodes in an EMME scenario."
)]
    // ReSharper disable once InconsistentNaming
    public class LOSBetweenPoints : IEmmeTool
    {

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("Centroid Range", "{1-9999}", typeof(RangeSet), "The range allowed for creating the temporary nodes.  Nodes in the zone system are ignored.")]
        public RangeSet CentroidRange;

        [RunParameter("Maximum Centroids", 3250, "The maximum number of centroids for your license size.  Size 13 in 4.2 is 3250.")]
        public int MaximumCentroids;

        [RunParameter("Scenario Number", 1, "The scenario number to interact with.")]
        public int Scenario;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = false, Description = "The assignment algorithm")]
        public IEmmeTool[] Assignment;

        [ModuleInformation(Description = "This is used to extract out a matrix from the results and to save it to file.")]
        public sealed class Matrix : IModule
        {
            [RootModule]
            public ITravelDemandModel Root;

            [SubModelInformation(Required = true, Description = "The source to load in the matrix for processing from.")]
            public IDataSource<SparseTwinIndex<float>> MatrixInput;

            [SubModelInformation(Required = true, Description = "The location to save the results to (od matrix csv).")]
            public FileLocation SaveTo;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }

            private float[][] Data;

            internal void InitializeData(List<int> nodesToExplore)
            {
                var numberOfNodesToExplore = nodesToExplore.Count;
                Data = new float[numberOfNodesToExplore][];
                for (int i = 0; i < Data.Length; i++)
                {
                    Data[i] = new float[numberOfNodesToExplore];
                }
            }

            internal void CollectResults(List<int> nodesToExplore, List<int> currentlyExploring, List<int> centroidNumbers)
            {
                if (MatrixInput.Loaded)
                {
                    MatrixInput.UnloadData();
                }
                MatrixInput.LoadData();
                var data = MatrixInput.GiveData();
                MatrixInput.UnloadData();
                var flatIndexes = centroidNumbers.Select(c => data.GetFlatIndex(c)).ToArray();
                // ensure that all of the indexes are properly allocated
                for (int i = 0; i < flatIndexes.Length && i < currentlyExploring.Count; i++)
                {
                    if (flatIndexes[i] < 0)
                    {
                        throw new XTMFRuntimeException(this, "In '" + Name + "' a centroid in our exploration set was not found in the data provided!");
                    }
                }
                var flatData = data.GetFlatData();
                for (int i = 0; i < currentlyExploring.Count; i++)
                {
                    for (int j = 0; j < currentlyExploring.Count; j++)
                    {
                        Data[currentlyExploring[i]][currentlyExploring[j]] = flatData[flatIndexes[i]][flatIndexes[j]];
                    }
                }
            }

            internal void SaveResults(List<int> nodesToExplore)
            {
                using (var writer = new StreamWriter(SaveTo))
                {
                    writer.Write("Origin\\Destination");
                    for (int i = 0; i < nodesToExplore.Count; i++)
                    {
                        writer.Write(',');
                        writer.Write(nodesToExplore[i]);
                    }
                    writer.WriteLine();
                    for (int i = 0; i < Data.Length; i++)
                    {
                        writer.Write(nodesToExplore[i]);
                        for (int j = 0; j < Data[i].Length; j++)
                        {
                            writer.Write(',');
                            writer.Write(Data[i][j]);
                        }
                        writer.WriteLine();
                    }
                }
                // once we have finished with the matrix, release the data
                Data = null;
            }
        }

        [SubModelInformation(Description = "The different data sources to use for the extraction.")]
        public Matrix[] Matricies;

        [SubModelInformation(Required = true, Description = "")]
        public FileLocation NodeFileLocation;

        public bool Execute(Controller controller)
        {
            Progress = 0.0f;
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");
            }
            List<int> nodesToExplore = GetNodesToExplore();
            List<int> newControids = GenerateCentroids();
            InitializeData(nodesToExplore);
            var exploration = ProduceRuns(nodesToExplore).ToList();
            for (int i = 0; i < exploration.Count; i++)
            {
                Progress = (float)i / exploration.Count;
                var currentlyExploring = exploration[i];
                AttachCentroids(mc, nodesToExplore, newControids, currentlyExploring);
                RunAssignment(controller);
                CollectData(nodesToExplore, currentlyExploring, newControids);
            }
            SaveResults(nodesToExplore);
            Progress = 1.0f;
            return true;
        }

        private List<int> GenerateCentroids()
        {
            var zoneNumbers = Root.ZoneSystem.ZoneArray.GetFlatData().Select(z => z.ZoneNumber).ToList();
            var toAdd = MaximumCentroids - zoneNumbers.Count;
            var ret = new List<int>();
            // Go through the range and add to our new centroid list all missing numbers until we have
            // can fill up to our maximum number of centroids.
            foreach (var set in CentroidRange)
            {
                for (int centroid = set.Start; centroid <= set.Stop; centroid++)
                {
                    if (!zoneNumbers.Contains(centroid))
                    {
                        ret.Add(centroid);
                        if (ret.Count >= toAdd)
                        {
                            return ret;
                        }
                    }
                }
            }
            throw new XTMFRuntimeException(this, "In '" + Name
                + "' we were unable to fill our available number of centroids given the range provided.");
        }

        private const string AttachCentroidToNodeTool = "tmg.XTMF_internal.attach_centroids_to_nodes";

        private void AttachCentroids(ModellerController controller, List<int> nodesToExplore, List<int> newControids,
            List<int> currentlyExploring)
        {
            //The goal is to execute our new tool in order to 
            controller.Run(this, AttachCentroidToNodeTool,
                string.Join(" ",
                Scenario.ToString(),
                "\"" + string.Join(";", currentlyExploring.Select(i => nodesToExplore[i].ToString())) + "\"",
                "\"" + string.Join(";", newControids.Select(i => i.ToString())) + "\""
               ));
        }

        private void InitializeData(List<int> nodesToExplore)
        {
            foreach (var matrix in Matricies)
            {
                matrix.InitializeData(nodesToExplore);
            }
        }

        private void CollectData(List<int> nodesToExplore, List<int> currentlyExploring, List<int> centroidMap)
        {
            foreach (var matrix in Matricies)
            {
                matrix.CollectResults(nodesToExplore, currentlyExploring, centroidMap);
            }
        }


        private void RunAssignment(Controller controller)
        {
            foreach (var assignment in Assignment)
            {
                assignment.Execute(controller);
            }
        }

        private void SaveResults(List<int> nodesToExplore)
        {
            foreach (var matrix in Matricies)
            {
                matrix.SaveResults(nodesToExplore);
            }
        }

        private List<int> GetNodesToExplore()
        {
            List<int> nodes = new List<int>();
            using (CsvReader reader = new CsvReader(NodeFileLocation))
            {
                // burn the header
                reader.LoadLine();
                while (reader.LoadLine(out int columns))
                {
                    if (columns > 0)
                    {
                        reader.Get(out int nodeToLoad, 0);
                        if (!nodes.Contains(nodeToLoad))
                        {
                            nodes.Add(nodeToLoad);
                        }
                    }
                }
            }
            return nodes;
        }

        private IEnumerable<List<int>> ProduceRuns(List<int> nodesToExplore)
        {
            var availableCentroids = MaximumCentroids - Root.ZoneSystem.ZoneArray.Count;
            //Execute Primary runs

            if (nodesToExplore.Count <= availableCentroids)
            {
                // in this case we can explore everything in one shot
                yield return BuildNodeMap(0, nodesToExplore.Count, 0, 0);
            }
            else
            {
                // half of the available nodes is our step size
                var stepSize = availableCentroids >> 1;
                int primarySteps = nodesToExplore.Count / stepSize; // int division is done intensionally to remove any remainders
                // in this case we need to break everything down into smaller parts so that we don't exceed the license size
                int startFirst, startSecond;
                for (int i = 0; i < primarySteps; i++)
                {
                    startFirst = stepSize * i;
                    for (int j = i + 1; j < primarySteps; j++)
                    {
                        startSecond = stepSize * j;
                        yield return BuildNodeMap(startFirst, startFirst + stepSize, startSecond, startSecond + stepSize);
                    }
                }
                //Process remainder
                var nodesRemaining = nodesToExplore.Count % stepSize;
                if (nodesRemaining > 0)
                {
                    startFirst = 0;
                    // if there is a remainder
                    var remainderStep = availableCentroids - nodesRemaining;
                    var remainderStart = nodesToExplore.Count - nodesRemaining;
                    while (startFirst < nodesToExplore.Count)
                    {
                        yield return BuildNodeMap(startFirst, Math.Min(startFirst + remainderStep, nodesToExplore.Count), remainderStart, nodesToExplore.Count);
                        startFirst += remainderStep;
                    }
                }
            }
        }

        private List<int> BuildNodeMap(int startFirst, int endFirst, int startSecond, int endSecond)
        {
            var map = new List<int>();
            for (int i = startFirst; i < endFirst; i++)
            {
                map.Add(i);
            }
            for (int j = startSecond; j < endSecond; j++)
            {
                map.Add(j);
            }
            return map;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
