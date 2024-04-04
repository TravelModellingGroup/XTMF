using Datastructure;
using System;
using System.Linq;
using XTMF;

namespace TMG.Frameworks.Data.Loading;


[ModuleInformation(Description = "This module provides some simple")]
public sealed class ZoneSystemMatrix : IDataSource<SparseTwinIndex<float>>
{

    [RootModule]
    public ITravelDemandModel Root;

    public enum MatrixType
    {
        StraightLineZoneDistance = 0,
        ManhattanZoneDistance = 1,
    }

    [RunParameter("Matrix Type", MatrixType.StraightLineZoneDistance, "The type of data from the zone system to fill the matrix with.")]
    public MatrixType Data;

    private SparseTwinIndex<float> _data = null;

    public SparseTwinIndex<float> GiveData()
    {
        return _data;
    }

    public bool Loaded => _data is not null;

    public void LoadData()
    {
        _data = Data switch
        {
            MatrixType.StraightLineZoneDistance => ComputeStraightLineDistance(),
            MatrixType.ManhattanZoneDistance => ComputeManhattanDistance(),
            _ => throw new XTMFRuntimeException(this, "Unknown Matrix Type!")
        };
    }

    private SparseTwinIndex<float> ComputeStraightLineDistance()
    {
        var zones = Root.ZoneSystem.ZoneArray;
        var ret = zones.CreateSquareTwinArray<float>();
        var zonePoints = zones.GetFlatData().Select(x => (x.X, x.Y)).ToArray();
        var flatData = ret.GetFlatData();

        for (var i = 0; i < flatData.Length; i++)
        {
            int j;
            // TODO: Write a vector version
            for (j = 0; j < flatData[i].Length; j++)
            {
                var dx = zonePoints[i].X - zonePoints[j].X;
                var dy = zonePoints[i].Y - zonePoints[j].Y;
                flatData[i][j] = MathF.Sqrt(dx * dx + dy * dy);
            }
        }
        return ret;
    }

    private SparseTwinIndex<float> ComputeManhattanDistance()
    {
        var zones = Root.ZoneSystem.ZoneArray;
        var ret = zones.CreateSquareTwinArray<float>();
        var zonePoints = zones.GetFlatData().Select(x => (x.X, x.Y)).ToArray();
        var flatData = ret.GetFlatData();

        for (var i = 0; i < flatData.Length; i++)
        {
            int j;
            // TODO: Write a vector version
            for (j = 0; j < flatData[i].Length; j++)
            {
                var dx = zonePoints[i].X - zonePoints[j].X;
                var dy = zonePoints[i].Y - zonePoints[j].Y;
                flatData[i][j] = MathF.Abs(dx) + MathF.Abs(dy);
            }
        }
        return ret;
    }

    public void UnloadData()
    {
        _data = null;
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
