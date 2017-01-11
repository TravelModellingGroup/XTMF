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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;
using Datastructure;
using TMG.Frameworks.Data.Processing.AST;
namespace TMG.Frameworks.Data.Processing
{
    [ModuleInformation(
        Description =
 @"This module is designed to execute optimized matrix math on the given data sources.  The available operations are add +, subtract -, multiply *, and divide /. 
You can also use brackets () to order the operations.  Math using literals will be optimized and no data source will be altered.  A valid expression could be 
'(A + B * C) / D' where you have the data sources A, B, C, and D defined with their Module names matching.  The result of this will be an OD matrix.  If only literals are used the size 
of the matrix will match the zone system otherwise the size of the matrices are loaded in from the data sources."
        )]
    public class ODMath : IDataSource<SparseTwinIndex<float>>
    {
        [RunParameter("Expression", "1", "The expression to evaluate for each OD cell.")]
        public string Expression;

        [SubModelInformation(Required = false, Description = "The matrices to refer to.")]
        public IDataSource[] DataSources;

        [DoNotAutomate]
        public ITravelDemandModel Root;

        private IConfiguration Config;

        public ODMath(IConfiguration config)
        {
            Config = config;
        }

        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public SparseTwinIndex<float> Data;

        public SparseTwinIndex<float> GiveData()
        {
            return Data;
        }

        public void LoadData()
        {
            var loadedSources = DataSources.Select(s => s.Loaded).ToArray();
            try
            {
                var result = ExpressionToExecute.Evaluate(DataSources);
                if (result.Error)
                {
                    throw new XTMFRuntimeException("In '" + Name + "' an exception during the execution of the expression occurred.\r\n" + result.ErrorMessage);
                }
                // check to see if the result is a scalar
                if (result.IsValue)
                {
                    if (Root != null)
                    {
                        var data = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
                        var flat = data.GetFlatData();
                        var row = flat[0];
                        var val = result.LiteralValue;
                        for (int i = 0; i < row.Length; i++)
                        {
                            row[i] = val;
                        }
                        for (int i = 1; i < flat.Length; i++)
                        {
                            Array.Copy(row, flat[i], row.Length);
                        }
                        Data = data;
                    }
                    else
                    {
                        throw new XTMFRuntimeException("In '" + Name + "' the result of the expression was a Scalar instead of a Matrix and there was no ITravelDemandModel in the ancestry to copy the zone system from!");
                    }
                }
                else if (result.IsVectorResult)
                {
                    throw new XTMFRuntimeException("In '" + Name + "' the result of the expression was a Vector instead of a matrix!");
                }
                else
                {
                    Data = result.ODData;
                }
            }
            finally
            {
                // unload the data sources that were loaded to evaluate the expression
                for (int i = 0; i < loadedSources.Length; i++)
                {
                    if (!loadedSources[i] && DataSources[i].Loaded)
                    {
                        DataSources[i].UnloadData();
                    }
                }
            }
            Loaded = true;
        }

        private void FindRoot()
        {
            var ancestry = TMG.Functions.ModelSystemReflection.BuildModelStructureChain(Config, this);
            for (int i = ancestry.Count - 1; i >= 0; i--)
            {
                var tdm = ancestry[i] as ITravelDemandModel;
                if (tdm != null)
                {
                    Root = tdm;
                    return;
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            FindRoot();
            if (!CompileAST(ref error))
            {
                error = $"In {Name} there was a compilation error for the expression.\r\n" + error;
                return false;
            }
            return true;
        }

        private AST.Expression ExpressionToExecute;

        private bool CompileAST(ref string error)
        {
            return Compiler.Compile(Expression, out ExpressionToExecute, ref error);
        }

        public void UnloadData()
        {
            Loaded = false;
            Data = null;
        }
    }

}
