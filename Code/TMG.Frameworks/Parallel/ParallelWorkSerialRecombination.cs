/*
    Copyright 2016-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using ThreadParallel = System.Threading.Tasks.Parallel;
namespace TMG.Frameworks.Parallel;

/// <summary>
/// This is used for helping work through different parallel programming problems
/// where there is a section of code that can be run in parallel but the results
/// need to be aggregated in order to ensure reproducibility.
/// </summary>
/// <typeparam name="TData"></typeparam>
/// <typeparam name="TIntermediate"></typeparam>
public static class ParallelWorkSerialRecombination<TData, TIntermediate>
{
    private struct TaggedBaseData
    {
        internal readonly TData Data;
        internal readonly int TaskNumber;

        public TaggedBaseData(TData d, int taskNumber)
        {
            Data = d;
            TaskNumber = taskNumber;
        }
    }

    private struct TaggedIntermediate
    {
        internal readonly TIntermediate ProcessedData;
        internal readonly int TaskNumber;

        public TaggedIntermediate(TIntermediate d, int taskNumber)
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
        internal readonly IEnumerable<TIntermediate> ProcessedData;
        internal readonly int TaskNumber;

        public ProcessedPartition(IEnumerable<TIntermediate> d, int taskNumber)
        {
            ProcessedData = d;
            TaskNumber = taskNumber;
        }
    }

    public static void ComputeInParallel(IList<TData> baseData, Func<TData, TIntermediate> parallelWork, Action<TIntermediate> recombination, int numberOfPartitions)
    {
        var intermediateResults = new BlockingCollection<ProcessedPartition>();
        ThreadParallel.Invoke(() =>
        {
            try
            {
                var partitionSize = (int)Math.Ceiling(baseData.Count / (float)numberOfPartitions);
                ThreadParallel.ForEach(baseData.Select((d, i) => new TaggedBaseData(d, i)).GroupBy(d => d.TaskNumber / partitionSize), g =>
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
            var backlog = new Dictionary<int, IEnumerable<TIntermediate>>();
            foreach (var group in intermediateResults.GetConsumingEnumerable())
            {
                if (group.TaskNumber != expecting)
                {
                    // if we are not ready for this yet add it to the backlog
                    backlog[group.TaskNumber] = group.ProcessedData;
                    continue;
                }
                IEnumerable<TIntermediate> toProcess = group.ProcessedData;
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

    public static void ComputeInParallel(IList<TData> baseData, Func<TData, int, TIntermediate> parallelWork, Action<TIntermediate, int> recombination, int numberOfPartitions)
    {
        var intermediateResults = new BlockingCollection<ProcessedPartition>();
        ThreadParallel.Invoke(() =>
        {
            try
            {
                var partitionSize = (int)Math.Ceiling(baseData.Count / (float)numberOfPartitions);
                ThreadParallel.ForEach(baseData.Select((d, i) => new TaggedBaseData(d, i)).GroupBy(d => d.TaskNumber / partitionSize), g =>
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
            var backlog = new Dictionary<int, IEnumerable<TIntermediate>>();
            foreach (var group in intermediateResults.GetConsumingEnumerable())
            {
                if (group.TaskNumber != expecting)
                {
                    // if we are not ready for this yet add it to the backlog
                    backlog[group.TaskNumber] = group.ProcessedData;
                    continue;
                }
                IEnumerable<TIntermediate> toProcess = group.ProcessedData;
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

    public static void ComputeInParallel(IList<TData> baseData, Func<TData, TIntermediate> parallelWork, Action<TIntermediate> recombination)
    {
        var intermediateResults = new BlockingCollection<TaggedIntermediate>();
        ThreadParallel.Invoke(() =>
       {
           try
           {
               ThreadParallel.ForEach(baseData.Select((d, i) => new TaggedBaseData(d, i)), d =>
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
           var backlog = new Dictionary<int, TIntermediate>();
           foreach (var newData in intermediateResults.GetConsumingEnumerable())
           {
               if (newData.TaskNumber != expecting)
               {
                   // if we are not ready for this yet add it to the backlog
                   backlog[newData.TaskNumber] = newData.ProcessedData;
                   continue;
               }
               TIntermediate toProcess = newData.ProcessedData;
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

    public static void ComputeInParallel(IList<TData> baseData, Func<TData, int, TIntermediate> parallelWork, Action<TIntermediate, int> recombination)
    {
        var intermediateResults = new BlockingCollection<TaggedIntermediate>();
        ThreadParallel.Invoke(() =>
        {
            try
            {
                ThreadParallel.ForEach(baseData.Select((d, i) => new TaggedBaseData(d, i)), d =>
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
            var backlog = new Dictionary<int, TIntermediate>();
            foreach (var newData in intermediateResults.GetConsumingEnumerable())
            {
                if (newData.TaskNumber != expecting)
                {
                    // if we are not ready for this yet add it to the backlog
                    backlog[newData.TaskNumber] = newData.ProcessedData;
                    continue;
                }
                TIntermediate toProcess = newData.ProcessedData;
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
