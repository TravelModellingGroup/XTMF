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

    public class Conditional : XTMF.IModule
    {
        [RootModule]
        public Pool Root;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public DataModule<string>[] ConditionalColumns;

        public float[] Probability;

        private int[] ColumnIndex;

        private int[] IndexMultiplier;

        [SubModelInformation(Required = true, Description = "A CSV file with each conditional attribute's value followed by the destination attribute value and probability [0,1].")]
        public FileLocation ConditionalSource;

        public void LoadConditionalsData()
        {
            GenerateBackendData();
            var prob = Probability;
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
                        reader.Get(out prob[GetIndex(currentIndex)], currentIndex.Length);
                    }
                }
            }
            if (!any)
            {
                throw new XTMFRuntimeException($@"In {Name} we did not load any conditionals from the file '{ConditionalSource}'!  
This could be because the data does not have the expected number of columns ({expectedColumns}) as interpreted by the given attributes.");
            }
        }

        private void GenerateBackendData()
        {
            int[] valuesByColumn = new int[ColumnIndex.Length];
            for (int i = 0; i < ColumnIndex.Length; i++)
            {
                valuesByColumn[i] = Root.Attributes[ColumnIndex[i]].PossibleValues.Length;
            }
            Probability = new float[valuesByColumn.Aggregate(1, (f, s) => f * s)];
            // reuse the memory
            CreateIndexMultipliers(valuesByColumn);
        }

        public int GetIndex(int[] indices)
        {
            var multipliers = IndexMultiplier;
            int ret = 0;
            for (int i = 0; i < indices.Length; i++)
            {
                ret = (ret * multipliers[i]) + indices[i];
            }
            return ret;
        }

        public float GetProbability(int[] indices)
        {
            return Probability[GetIndex(indices)];
        }

        private void CreateIndexMultipliers(int[] valuesPerColumn)
        {
            var multiplier = 1;
            IndexMultiplier = new int[valuesPerColumn.Length];
            for (int i = 0; i < IndexMultiplier.Length; i++)
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

        public bool RuntimeValidation(ref string error)
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
    }
}
