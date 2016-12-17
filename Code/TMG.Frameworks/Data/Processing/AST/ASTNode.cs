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
using Datastructure;
using XTMF;

namespace TMG.Frameworks.Data.Processing.AST
{
    public abstract class ASTNode
    {
        /// <summary>
        /// The starting point of the node
        /// </summary>
        internal int Start;

        public ASTNode(int start)
        {
            Start = start;
        }

        public abstract ComputationResult Evaluate(IDataSource[] dataSources);

        internal abstract bool OptimizeAST(ref Expression ex, ref string error);
    }

    public class ComputationResult
    {
        public bool IsODResult { get { return ODData != null; } }

        public bool IsVectorResult { get { return VectorData != null; } }

        public bool Error { get { return ErrorMessage != null; } }

        public string ErrorMessage { get; private set; }

        public bool Accumulator { get; private set; }

        public enum VectorDirection
        {
            Unassigned,
            Horizontal,
            Vertical
        }

        public VectorDirection Direction { get; private set; }

        public SparseTwinIndex<float> ODData
        {
            get; private set;
        }

        public SparseArray<float> VectorData { get; private set; }

        public float LiteralValue
        {
            get; private set;
        }
        public bool IsValue { get { return !IsODResult && !IsVectorResult && !Error; } }

        public ComputationResult(float value)
        {
            LiteralValue = value;
        }

        public ComputationResult(SparseTwinIndex<float> data, bool accumulator)
        {
            ODData = data;
            Accumulator = accumulator;
        }

        public ComputationResult(SparseArray<float> data, bool accumulator, VectorDirection direction = VectorDirection.Unassigned)
        {
            VectorData = data;
            Accumulator = accumulator;
            Direction = direction;
        }

        public ComputationResult(ComputationResult res, VectorDirection direction)
        {
            ODData = res.ODData;
            LiteralValue = res.LiteralValue;
            VectorData = res.VectorData;
            Direction = direction;
        }

        public ComputationResult(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }
}
