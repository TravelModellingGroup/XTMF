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
using TMG.Input;
using XTMF;
namespace TMG.Frameworks.Data.Synthesis.Gibbs
{

    public class Conditional : IDataSource
    {
        [RootModule]
        public Pool Root;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public DataModule<string>[] ConditionalColumns;

        public float[] CDF;

        protected int[] ColumnIndex;

        private int[] IndexMultiplier;

        private int AttributeLength;

        [SubModelInformation(Required = true, Description = "A CSV file with each conditional attribute's value followed by the destination attribute value and probability [0,1].")]
        public FileLocation ConditionalSource;

        protected bool Loaded = false;

        public virtual bool RequiresReloadingPerZone { get { return false; } }

        bool IDataSource.Loaded
        {
            get { return Loaded; }
        }

        public virtual void LoadConditionalsData(int currentZone)
        {
            if (!Loaded)
            {
                var prob = GenerateBackendData();
                int expectedColumns = ColumnIndex.Length + 1;
                var currentIndex = new int[expectedColumns - 1];
                bool any = false;
                using (var reader = new CsvReader(ConditionalSource))
                {
                    int columns;
                    reader.LoadLine();
                    while (reader.LoadLine(out columns))
                    {
                        if (columns >= expectedColumns)
                        {
                            any = true;
                            for (int i = 0; i < currentIndex.Length; i++)
                            {
                                reader.Get(out currentIndex[i], i);
                            }
                            var probIndex = GetIndex(currentIndex);
                            if (probIndex < prob.Length)
                            {
                                reader.Get(out prob[probIndex], currentIndex.Length);
                            }
                            else
                            {
                                throw new XTMFRuntimeException($"In '{Name}' we found an invalid index to assign to {probIndex} but the max index was only {prob.Length}!");
                            }
                        }
                    }
                }
                CDF = ConvertToCDF(prob);
                if (!any)
                {
                    throw new XTMFRuntimeException($@"In {Name} we did not load any conditionals from the file '{ConditionalSource.GetFilePath()}'!  
This could be because the data does not have the expected number of columns ({expectedColumns}) as interpreted by the given attributes.");
                }
                Loaded = true;
            }
        }

        protected float[] ConvertToCDF(float[] prob)
        {
            var stride = AttributeLength;
            for (int i = 0; i < prob.Length; i += stride)
            {
                var tally = 0.0f;
                for (int j = 0; j < stride; j++)
                {
                    tally += prob[i + j];
                    prob[i + j] = tally;
                }
            }
            return prob;
        }

        internal void Apply(int[] currentResult, float pop)
        {
            var columns = ColumnIndex;
            var multipliers = IndexMultiplier;
            var cdf = CDF;
            int startIndex = 0;
            var length = AttributeLength;
            for (int i = 0; i < columns.Length - 1; i++)
            {
                startIndex += multipliers[i] * currentResult[columns[i]];
            }
            for (int i = 0; i < length; i++)
            {
                if (pop <= cdf[startIndex + i])
                {
                    currentResult[columns[columns.Length - 1]] = i;
                    break;
                }
            }
        }

        protected float[] GenerateBackendData()
        {
            int[] valuesByColumn = new int[ColumnIndex.Length];
            for (int i = 0; i < ColumnIndex.Length; i++)
            {
                valuesByColumn[i] = Root.Attributes[ColumnIndex[i]].PossibleValues.Length;
            }
            var ret = new float[valuesByColumn.Aggregate(1, (f, s) => f * s)];
            AttributeLength = valuesByColumn[valuesByColumn.Length - 1];
            // reuse the memory
            CreateIndexMultipliers(valuesByColumn);
            return ret;
        }

        protected int GetIndex(int[] indices)
        {
            var multipliers = IndexMultiplier;
            int ret = 0;
            for (int i = 0; i < indices.Length; i++)
            {
                ret += multipliers[i] * indices[i];
            }
            return ret;
        }

        private float GetProbability(int[] indices)
        {
            return CDF[GetIndex(indices)];
        }

        private void CreateIndexMultipliers(int[] valuesPerColumn)
        {
            var multiplier = 1;
            IndexMultiplier = new int[valuesPerColumn.Length];
            for (int i = IndexMultiplier.Length - 1; i >= 0; i--)
            {
                IndexMultiplier[i] = multiplier;
                multiplier *= valuesPerColumn[i];
            }
        }

        private int GetAttributeIndex(string name)
        {
            var at = Root.Attributes;
            for (int i = 0; i < at.Length; i++)
            {
                if (at[i].Name == name)
                {
                    return i;
                }
            }
            return -1;
        }

        public virtual bool RuntimeValidation(ref string error)
        {
            ColumnIndex = new int[ConditionalColumns.Length];
            for (int i = 0; i < ConditionalColumns.Length; i++)
            {
                var index = GetAttributeIndex(ConditionalColumns[i].Data);
                ColumnIndex[i] = index;
                if (index < 0)
                {
                    error = $"In '{Name}' we were unable to find an attribute named {ConditionalColumns[i].Data}.";
                    return false;
                }
            }
            return true;
        }

        public void LoadData()
        {
            throw new NotImplementedException();
        }

        public void UnloadData()
        {
            Loaded = false;
        }
    }
}
