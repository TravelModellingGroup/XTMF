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
using TMG.Functions;
using XTMF;

namespace TMG.Frameworks.Data.Processing.AST;

public sealed class Negate : MonoExpression
{
    public Negate(int start) : base(start)
    {
    }

    internal override bool OptimizeAst(ref Expression ex, ref string error)
    {
        // Optimize our children first
        if (!InnerExpression.OptimizeAst(ref InnerExpression, ref error))
        {
            return false;
        }
        // optimize the case that we are a negative literal
        if (ex is Literal l)
        {
            ex = new Literal(Start, -l.Value);
        }
        return true;
    }

    public override ComputationResult Evaluate(IDataSource[] dataSources)
    {
        var inner = InnerExpression.Evaluate(dataSources);
        if(inner.IsValue)
        {
            return new ComputationResult(-inner.LiteralValue);
        }
        else if(inner.IsVectorResult)
        {
            var ret = inner.Accumulator ? inner.VectorData : inner.VectorData.CreateSimilarArray<float>();
            VectorHelper.Negate(ret.GetFlatData(), inner.VectorData.GetFlatData());
            return new ComputationResult(ret, true, inner.Direction);
        }
        else
        {
            var ret = inner.Accumulator ? inner.OdData : inner.OdData.CreateSimilarArray<float>();
            var flatRet = ret.GetFlatData();
            var flatInner = inner.OdData.GetFlatData();
            for (int i = 0; i < flatRet.Length; i++)
            {
                VectorHelper.Negate(flatRet[i], flatInner[i]);
            }
            return new ComputationResult(ret, true);
        }
    }
}
