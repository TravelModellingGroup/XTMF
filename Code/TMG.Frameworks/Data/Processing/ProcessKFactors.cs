using Datastructure;
using System;
using System.Linq;
using XTMF;

namespace TMG.Frameworks.Data.Processing;

[ModuleInformation(Description =
    "This module is used to apply factors to the given matrix (multiplication) to produce an adjusted matrix.")]
public sealed class ProcessKFactors : IDataSource<SparseTwinIndex<float>>
{
    [RootModule]
    public ITravelDemandModel Root;

    public bool Loaded {get;set;}

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

    private SparseTwinIndex<float> _data;

    [SubModelInformation(Required = true, Description = "The matrix to apply the K Factors to.")]
    public IDataSource<SparseTwinIndex<float>> InitialMatrix;

    [ModuleInformation(Description = "A K Factor to apply.")]
    public sealed class KFactor : IModule
    {
        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

        [RunParameter("Origin PDs", "1", typeof(RangeSet), "The range of planning district origins to apply this kfactor to.")]
        public RangeSet OriginPDs;

        [RunParameter("Destination PDs", "1", typeof(RangeSet), "The range of planning district destinations to apply this kfactor to.")]
        public RangeSet DestinationPDs;

        [RunParameter("Factor", 1.0f, "The factor to apply to zones within the origin and destination planning district ranges.")]
        public float Factor;

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

    [SubModelInformation(Required = false, Description = "The K-Factors to apply to the given matrix.")]
    public KFactor[] KFactors;

    public SparseTwinIndex<float> GiveData()
    {
        return _data;
    }

    public void LoadData()
    {
        var baseData = LoadInitialMatrix();
        var ret = baseData.CreateSimilarArray<float>();
        var pds = Root.ZoneSystem.ZoneArray.GetFlatData().Select(zone => zone.PlanningDistrict).ToArray();
        System.Threading.Tasks.Parallel.For(0, pds.Length, (int origin) =>
        {
            var kFactorsToApply = KFactors.Where(kf => kf.OriginPDs.Contains(pds[origin])).ToArray();
            var flatRetRow = ret.GetFlatData()[origin];
            var flatBaseRow = baseData.GetFlatData()[origin];
            // if there is nothing to do, continue on after copying the results.
            if(kFactorsToApply.Length == 0)
            {
                Array.Copy(flatBaseRow, 0, flatRetRow, 0, flatBaseRow.Length);
            }
            else
            {
                for (int destination = 0; destination < flatBaseRow.Length; destination++)
                {
                    var factor = 1.0f;
                    for (int k = 0; k < kFactorsToApply.Length; k++)
                    {
                        if(kFactorsToApply[k].DestinationPDs.Contains(pds[destination]))
                        {
                            factor *= kFactorsToApply[k].Factor;
                        }
                    }
                    flatRetRow[destination] = flatBaseRow[destination] * factor;
                }
            }
        });
        _data = ret;
        Loaded = true;
    }

    private SparseTwinIndex<float> LoadInitialMatrix()
    {
        var loaded = InitialMatrix.Loaded;
        if(!loaded)
        {
            InitialMatrix.LoadData();
        }
        var ret = InitialMatrix.GiveData();
        if(!loaded)
        {
            InitialMatrix.UnloadData();
        }
        return ret;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void UnloadData()
    {
        Loaded = false;
        _data = null;
    }
}
