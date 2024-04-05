/*
    Copyright 2014 James Vaughan for integration into XTMF.

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
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.Emme.Analysis;


public sealed class MatrixAnalysis : IEmmeTool, ISelfContainedModule
{
    [RootModule]
    public ITravelDemandModel Root;
    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public bool Execute(Controller controller)
    {
        Start();
        return true;
    }

    [RunParameter("Third Normalized Form", false, "Should the data be saved in third normalized form, if not it will be saved as a matrix.")]
    public bool ThirdNormalizedForm;

    private void SaveData(float[][] aggData, string[] aggregationHeaders)
    {

        using StreamWriter writer = new(OutputFile);
        if (ThirdNormalizedForm)
        {
            writer.WriteLine("From,To,Value");
            for (int i = 0; i < aggregationHeaders.Length; i++)
            {
                var row = aggData[i];
                for (int j = 0; j < aggregationHeaders.Length; j++)
                {
                    writer.Write(aggregationHeaders[i]);
                    writer.Write(',');
                    writer.Write(aggregationHeaders[j]);
                    writer.Write(',');
                    writer.WriteLine(row[j]);
                }
            }
        }
        else
        {
            SaveMatrix(aggData, aggregationHeaders, writer);
        }
    }

    private static void SaveMatrix(float[][] aggData, string[] aggregationHeaders, StreamWriter writer)
    {
        // write out the top line
        writer.Write("Origin\\Destination");
        for(int i = 0; i < aggregationHeaders.Length; i++)
        {
            writer.Write(',');
            writer.Write(aggregationHeaders[i]);
        }
        writer.WriteLine();
        for(int i = 0; i < aggregationHeaders.Length; i++)
        {
            writer.Write(aggregationHeaders[i]);
            var row = aggData[i];
            for(int j = 0; j < row.Length; j++)
            {
                writer.Write(',');
                writer.Write(row[j]);
            }
            writer.WriteLine();
        }
    }

    private void LoadMapping(out string[] aggregationHeaders, out List<int>[] zoneToAggregationMap)
    {
        var zoneSystem = Root.ZoneSystem.ZoneArray;
        List<string> headers = [];
        var map = new List<int>[zoneSystem.Count];
        using (CsvReader reader = new(AggregationFile))
        {
            // burn header
            reader.LoadLine(out int columns);
            // read the real data
            while (reader.LoadLine(out columns))
            {
                if(columns >= 2)
                {
                    reader.Get(out int zone, 0);
                    reader.Get(out string agg, 1);
                    zone = zoneSystem.GetFlatIndex(zone);
                    if(zone >= 0)
                    {
                        var index = GetAggIndex(agg, headers);
                        var prev = map[zone];
                        if(prev == null)
                        {
                            map[zone] = prev = new List<int>(2);
                        }
                        prev.Add(index);
                    }
                }
            }
        }
        aggregationHeaders = [.. headers];
        zoneToAggregationMap = map;
    }

    private static int GetAggIndex(string agg, List<string> headers)
    {
        int index;
        if((index = headers.IndexOf(agg)) >= 0)
        {
            return index;
        }
        headers.Add(agg);
        return headers.Count - 1;
    }

    [SubModelInformation(Required = true, Description = "The aggregation function to apply.")]
    public Aggregation AggregationToApply;


    public abstract class Aggregation : IModule
    {
        public abstract float[][] ApplyAggregation(float[][] data, List<int>[] zoneToHeaderMap, string[] headers);

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public float[][] BuildData(string[] headers)
        {
            var ret = new float[headers.Length][];
            for(int i = 0; i < ret.Length; i++)
            {
                ret[i] = new float[headers.Length];
            }
            return ret;
        }

        public virtual bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

    public sealed class Sum : Aggregation
    {
        public override float[][] ApplyAggregation(float[][] data, List<int>[] zoneToHeaderMap, string[] headers)
        {
            var ret = BuildData(headers);
            for(int i = 0; i < zoneToHeaderMap.Length; i++)
            {
                var iReferencedZones = zoneToHeaderMap[i];
                if(iReferencedZones != null)
                {
                    for(int j = 0; j < zoneToHeaderMap.Length; j++)
                    {
                        var jReferencedZones = zoneToHeaderMap[j];
                        if(jReferencedZones != null)
                        {
                            for(int o = 0; o < iReferencedZones.Count; o++)
                            {
                                for(int d = 0; d < jReferencedZones.Count; d++)
                                {
                                    ret[iReferencedZones[o]][jReferencedZones[d]] += data[i][j];
                                }
                            }
                        }
                    }
                }
            }
            return ret;
        }
    }

    public sealed class Average : Aggregation
    {
        public IResource WeightedValuesByZone;

        public override float[][] ApplyAggregation(float[][] data, List<int>[] zoneToHeaderMap, string[] headers)
        {
            var ret = BuildData(headers);
            var weightSum = BuildData(headers);
            var weights = WeightedValuesByZone.AcquireResource<SparseTwinIndex<float>>().GetFlatData();
            // build totals
            for(int i = 0; i < zoneToHeaderMap.Length; i++)
            {
                var iReferencedZones = zoneToHeaderMap[i];
                if(iReferencedZones != null)
                {
                    for(int j = 0; j < zoneToHeaderMap.Length; j++)
                    {
                        var jReferencedZones = zoneToHeaderMap[j];
                        if(jReferencedZones != null)
                        {
                            for(int o = 0; o < iReferencedZones.Count; o++)
                            {
                                for(int d = 0; d < jReferencedZones.Count; d++)
                                {
                                    ret[iReferencedZones[o]][jReferencedZones[d]] += data[i][j] * weights[i][j];
                                    weightSum[iReferencedZones[o]][jReferencedZones[d]] += weights[i][j];
                                }
                            }
                        }
                    }
                }
            }
            // average
            for(int i = 0; i < ret.Length; i++)
            {
                for(int j = 0; j < ret[i].Length; j++)
                {
                    if(weightSum[i][j] > 0)
                    {
                        ret[i][j] /= weightSum[i][j];
                    }
                    else
                    {
                        var totalTime = 0.0f;
                        var indexes = 0;
                        // if there are no trips coming to this agg OD cell we need to generate something still.
                        for(int o = 0; o < zoneToHeaderMap.Length; o++)
                        {
                            if(zoneToHeaderMap[o] != null && zoneToHeaderMap[o].Contains(i))
                            {
                                for(int d = 0; d < zoneToHeaderMap.Length; d++)
                                {
                                    if(zoneToHeaderMap[d] != null && zoneToHeaderMap[d].Contains(j))
                                    {
                                        totalTime += data[o][d];
                                        indexes++;
                                    }
                                }
                            }
                        }
                        ret[i][j] = totalTime / indexes;
                    }
                }
            }
            return ret;
        }

        public override bool RuntimeValidation(ref string error)
        {
            if(!WeightedValuesByZone.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the Weighted Values By Zone are not referencing a resource storing a SparseTwinIndex<float>!";
                return false;
            }
            return base.RuntimeValidation(ref error);
        }
    }


    public IResource AnalysisTarget;

    [SubModelInformation(Required = true, Description = "The that contains the Zone to Aggregation mapping (Zone,Map).")]
    public FileLocation AggregationFile;

    [SubModelInformation(Required = true, Description = "The location to save the analysis to.")]
    public FileLocation OutputFile;

    public bool RuntimeValidation(ref string error)
    {
        if(!AnalysisTarget.CheckResourceType<SparseTwinIndex<float>>())
        {
            error = "In '" + Name + "' the Analysis target was not of type SparseTwinIndex<float>!";
            return false;
        }
        return true;
    }

    public void Start()
    {
        LoadMapping(out string[] aggregationHeaders, out List<int>[] zoneToAggregationMap);
        var data = AnalysisTarget.AcquireResource<SparseTwinIndex<float>>();
        var aggData = AggregationToApply.ApplyAggregation(data.GetFlatData(), zoneToAggregationMap, aggregationHeaders);
        SaveData(aggData, aggregationHeaders);
    }
}
