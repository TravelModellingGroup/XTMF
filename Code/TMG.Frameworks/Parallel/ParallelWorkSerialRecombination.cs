/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThreadParallel = System.Threading.Tasks.Parallel;
namespace TMG.Frameworks.Parallel
{
    /// <summary>
    /// This is used for helping work through different parallel programming problems
    /// where there is a section of code that can be run in parallel but the results
    /// need to be aggregated in order to ensure reproducibility.
    /// </summary>
    /// <typeparam name="Data"></typeparam>
    /// <typeparam name="Intermediate"></typeparam>
    public static class ParallelWorkSerialRecombination<Data, Intermediate>
    {
        private struct TaggedBaseData
        {
            internal Data Data;
            internal int TaskNumber;

            public TaggedBaseData(Data d, int taskNumber)
            {
                Data = d;
                TaskNumber = taskNumber;
            }
        }

        private struct TaggedIntermediate
        {
            internal Intermediate ProcessedData;
            internal int TaskNumber;

            public TaggedIntermediate(Intermediate d, int taskNumber)
            {
                ProcessedData = d;
                TaskNumber = taskNumber;
            }
        }

        /// <summary>
        /// Processed Partition
        /// </summary>
        private struct ProcessedPartition
        {
            internal IEnumerable<Intermediate> ProcessedData;
            internal int TaskNumber;

            public ProcessedPartition(IEnumerable<Intermediate> d, int taskNumber)
            {
                ProcessedData = d;
                TaskNumber = taskNumber;
            }
        }

        public static void ComputeInParallel(IEnumerable<Data> baseData, Func<Data, Intermediate> parallelWork, Action<Intermediate> recombination, int numberOfPartitions)
        {
            var intermediateResults = new BlockingCollection<ProcessedPartition>();
            ThreadParallel.Invoke(() =>
            {
                try
                {
                    var partitionSize = (int)Math.Ceiling(baseData.Count() / (float)numberOfPartitions);
                    ThreadParallel.ForEach(baseData.Select((d, i) => new TaggedBaseData(d, i)).GroupBy(d => d.TaskNumber / partitionSize), (IGrouping<int, TaggedBaseData> g) =>
                    {
                        intermediateResults.Add(new ProcessedPartition(g.Select(d => parallelWork(d.Data)), g.Key));
                    });
                }
                finally
                {
                    intermediateResults.CompleteAdding();
                }
            }, () =>
            {
                int expecting = 0;
                var backlog = new Dictionary<int, IEnumerable<Intermediate>>();
                foreach (var group in intermediateResults.GetConsumingEnumerable())
                {
                    if (group.TaskNumber != expecting)
                    {
                        // if we are not ready for this yet add it to the backlog
                        backlog[group.TaskNumber] = group.ProcessedData;
                        continue;
                    }
                    IEnumerable<Intermediate> toProcess = group.ProcessedData;
                    while (true)
                    {
                        foreach (var element in toProcess)
                        {
                            recombination(element);
                        }
                        expecting++;
                        // now see if we can combine with the backlog
                        if (!backlog.TryGetValue(expecting, out toProcess))
                        {
                            // if we can't find the next task wait for another
                            // task to finish
                            break;
                        }
                        else
                        {
                            backlog.Remove(expecting);
                        }
                    }
                }
            });
        }

        public static void ComputeInParallel(IEnumerable<Data> baseData, Func<Data, int, Intermediate> parallelWork, Action<Intermediate, int> recombination, int numberOfPartitions)
        {
            var intermediateResults = new BlockingCollection<ProcessedPartition>();
            ThreadParallel.Invoke(() =>
            {
                try
                {
                    var partitionSize = (int)Math.Ceiling(baseData.Count() / (float)numberOfPartitions);
                    ThreadParallel.ForEach(baseData.Select((d, i) => new TaggedBaseData(d, i)).GroupBy(d => d.TaskNumber / partitionSize), (IGrouping<int, TaggedBaseData> g) =>
                    {
                        intermediateResults.Add(new ProcessedPartition(g.Select(d => parallelWork(d.Data, g.Key)), g.Key));
                    });
                }
                finally
                {
                    intermediateResults.CompleteAdding();
                }
            }, () =>
            {
                int expecting = 0;
                var backlog = new Dictionary<int, IEnumerable<Intermediate>>();
                foreach (var group in intermediateResults.GetConsumingEnumerable())
                {
                    if (group.TaskNumber != expecting)
                    {
                        // if we are not ready for this yet add it to the backlog
                        backlog[group.TaskNumber] = group.ProcessedData;
                        continue;
                    }
                    IEnumerable<Intermediate> toProcess = group.ProcessedData;
                    while (true)
                    {
                        foreach (var element in toProcess)
                        {
                            recombination(element, expecting);
                        }
                        expecting++;
                        // now see if we can combine with the backlog
                        if (!backlog.TryGetValue(expecting, out toProcess))
                        {
                            // if we can't find the next task wait for another
                            // task to finish
                            break;
                        }
                        else
                        {
                            backlog.Remove(expecting);
                        }
                    }
                }
            });
        }

        public static void ComputeInParallel(IEnumerable<Data> baseData, Func<Data, Intermediate> parallelWork, Action<Intermediate> recombination)
        {
            var intermediateResults = new BlockingCollection<TaggedIntermediate>();
            ThreadParallel.Invoke(() =>
           {
               try
               {
                   ThreadParallel.ForEach(baseData.Select((d, i) => new TaggedBaseData(d, i)), (TaggedBaseData d) =>
                  {
                      intermediateResults.Add(new TaggedIntermediate(parallelWork(d.Data), d.TaskNumber));
                  });
               }
               finally
               {
                   intermediateResults.CompleteAdding();
               }
           }, () =>
           {
               int expecting = 0;
               var backlog = new Dictionary<int, Intermediate>();
               foreach (var newData in intermediateResults.GetConsumingEnumerable())
               {
                   if (newData.TaskNumber != expecting)
                   {
                       // if we are not ready for this yet add it to the backlog
                       backlog[newData.TaskNumber] = newData.ProcessedData;
                       continue;
                   }
                   Intermediate toProcess = newData.ProcessedData;
                   while (true)
                   {
                       recombination(toProcess);
                       expecting++;
                       // now see if we can combine with the backlog
                       if (!backlog.TryGetValue(expecting, out toProcess))
                       {
                           // if we can't find the next task wait for another
                           // task to finish
                           break;
                       }
                       else
                       {
                           backlog.Remove(expecting);
                       }
                   }
               }
           });
        }

        public static void ComputeInParallel(IEnumerable<Data> baseData, Func<Data, int, Intermediate> parallelWork, Action<Intermediate, int> recombination)
        {
            var intermediateResults = new BlockingCollection<TaggedIntermediate>();
            ThreadParallel.Invoke(() =>
            {
                try
                {
                    ThreadParallel.ForEach(baseData.Select((d, i) => new TaggedBaseData(d, i)), (TaggedBaseData d) =>
                    {
                        intermediateResults.Add(new TaggedIntermediate(parallelWork(d.Data, d.TaskNumber), d.TaskNumber));
                    });
                }
                finally
                {
                    intermediateResults.CompleteAdding();
                }
            }, () =>
            {
                int expecting = 0;
                var backlog = new Dictionary<int, Intermediate>();
                foreach (var newData in intermediateResults.GetConsumingEnumerable())
                {
                    if (newData.TaskNumber != expecting)
                    {
                        // if we are not ready for this yet add it to the backlog
                        backlog[newData.TaskNumber] = newData.ProcessedData;
                        continue;
                    }
                    Intermediate toProcess = newData.ProcessedData;
                    while (true)
                    {
                        recombination(toProcess, expecting);
                        expecting++;
                        // now see if we can combine with the backlog
                        if (!backlog.TryGetValue(expecting, out toProcess))
                        {
                            // if we can't find the next task wait for another
                            // task to finish
                            break;
                        }
                        else
                        {
                            backlog.Remove(expecting);
                        }
                    }
                }
            });
        }
    }
}
