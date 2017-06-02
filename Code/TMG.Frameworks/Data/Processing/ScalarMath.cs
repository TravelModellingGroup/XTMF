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
using System.Linq;
using XTMF;
using TMG.Frameworks.Data.Processing.AST;
namespace TMG.Frameworks.Data.Processing
{
    [ModuleInformation(
        Description =
 @"This module is designed to execute optimized matrix math on the given data sources.  The available operations are add +, subtract -, multiply *, and divide /. 
You can also use brackets () to order the operations.  Math using literals will be optimized and no data source will be altered.  A valid expression could be 
'(A + B * C) / D' where you have the data sources A, B, C, and D defined with their Module names matching.  The result of this will be a scalar."
        )]
    public class ScalarMath : IDataSource<float>
    {
        [RunParameter("Expression", "1", "The expression to evaluate for each OD cell.")]
        public string Expression;

        [SubModelInformation(Required = false, Description = "The matrices to refer to.")]
        public IDataSource[] DataSources;

        public bool Loaded
        {
            get; set;
        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public float Data;

        public float GiveData()
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
                    Data = result.LiteralValue;
                }
                else if (result.IsVectorResult)
                {
                    throw new XTMFRuntimeException("In '" + Name + "' the result of the expression was a Vector instead of a scalar!");
                }
                else
                {
                    throw new XTMFRuntimeException("In '" + Name + "' the result of the expression was a Matrix instead of a scalar!");
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

        public bool RuntimeValidation(ref string error)
        {
            if (!CompileAst(ref error))
            {
                return false;
            }
            return true;
        }

        private Expression ExpressionToExecute;

        private bool CompileAst(ref string error)
        {
            return Compiler.Compile(Expression, out ExpressionToExecute, ref error);
        }

        public void UnloadData()
        {
            Loaded = false;
            Data = 0.0f;
        }
    }

}
