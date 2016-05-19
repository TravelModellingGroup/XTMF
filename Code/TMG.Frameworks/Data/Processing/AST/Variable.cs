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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;

namespace TMG.Frameworks.Data.Processing.AST
{
    public class Variable : Value
    {
        public readonly string Name;

        public Variable(int start, string name) : base(start)
        {
            Name = name;
        }

        public override ComputationResult Evaluate(IDataSource[] dataSources)
        {
            var source = dataSources.FirstOrDefault(d => d.Name == Name);
            if (source == null)
            {
                return new ComputationResult("Unable to find a data source named '" + Name + "'!");
            }
            if (!source.Loaded)
            {
                source.LoadData();
            }
            var odSource = source as IDataSource<SparseTwinIndex<float>>;
            if (odSource != null)
            {
                return new ComputationResult(odSource.GiveData(), false);
            }
            var vectorSource = source as IDataSource<SparseArray<float>>;
            if (vectorSource != null)
            {
                return new ComputationResult(vectorSource.GiveData(), false);
            }
            var valueSource = source as IDataSource<float>;
            if (valueSource != null)
            {
                return new ComputationResult(valueSource.GiveData());
            }
            return new ComputationResult("The data source '" + Name + "' was not of a valid resource type!");
        }
    }
}
