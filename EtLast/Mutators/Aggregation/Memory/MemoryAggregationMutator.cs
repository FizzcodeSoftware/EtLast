﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;

    /// <summary>
    /// Input can be unordered. Group key generation is applied on the input rows on-the-fly, but group processing is started only after all groups are created.
    /// - keeps all input rows in memory (!)
    /// - uses very flexible <see cref="IMemoryAggregationOperation"/> which takes all rows in a group and generates the aggregate.
    /// </summary>
    public class MemoryAggregationMutator : AbstractMemoryAggregationMutator
    {
        public MemoryAggregationMutator(ITopic topic, string name)
            : base(topic, name)
        {
        }

        protected override IEnumerable<IRow> EvaluateImpl(Stopwatch netTimeStopwatch)
        {
            var groups = new Dictionary<string, List<IReadOnlySlimRow>>();

            netTimeStopwatch.Stop();
            var enumerator = InputProcess.Evaluate(this).TakeRowsAndTransferOwnership().GetEnumerator();
            netTimeStopwatch.Start();

            var rowCount = 0;
            var ignoredRowCount = 0;
            while (!Context.CancellationTokenSource.IsCancellationRequested)
            {
                netTimeStopwatch.Stop();
                var finished = !enumerator.MoveNext();
                netTimeStopwatch.Start();
                if (finished)
                    break;

                var row = enumerator.Current;

                var apply = false;
                try
                {
                    apply = If?.Invoke(row) != false;
                }
                catch (Exception ex)
                {
                    Context.AddException(this, ProcessExecutionException.Wrap(this, row, ex));
                    break;
                }

                if (!apply)
                {
                    ignoredRowCount++;
                    netTimeStopwatch.Stop();
                    yield return row;
                    netTimeStopwatch.Start();
                    continue;
                }

                rowCount++;
                var key = KeyGenerator.Invoke(row);
                if (!groups.TryGetValue(key, out var list))
                {
                    list = new List<IReadOnlySlimRow>();
                    groups.Add(key, list);
                }

                list.Add(row);
            }

            Context.Log(LogSeverity.Debug, this, "evaluated {RowCount} input rows and created {GroupCount} groups in {Elapsed}",
                rowCount, groups.Count, InvocationInfo.LastInvocationStarted.Elapsed);

            var aggregateCount = 0;
            var aggregates = new List<SlimRow>();
            foreach (var group in groups.Values)
            {
                if (Context.CancellationTokenSource.IsCancellationRequested)
                    break;

                try
                {
                    Operation.TransformGroup(group, () =>
                    {
                        var aggregate = new SlimRow();

                        if (FixColumns != null)
                        {
                            foreach (var column in FixColumns)
                            {
                                aggregate.SetValue(column.ToColumn, group[0][column.FromColumn]);
                            }
                        }

                        aggregates.Add(aggregate);
                        return aggregate;
                    });
                }
                catch (Exception ex)
                {
                    var exception = new MemoryAggregationException(this, Operation, group, ex);
                    Context.AddException(this, exception);
                    break;
                }

                foreach (var row in group)
                {
                    Context.SetRowOwner(row as IRow, null);
                }

                foreach (var aggregate in aggregates)
                {
                    aggregateCount++;
                    var aggregateRow = Context.CreateRow(this, aggregate);

                    netTimeStopwatch.Stop();
                    yield return aggregateRow;
                    netTimeStopwatch.Start();
                }

                group.Clear();
                aggregates.Clear();
            }

            groups.Clear();

            netTimeStopwatch.Stop();
            Context.Log(LogSeverity.Debug, this, "created {AggregateCount} aggregates in {Elapsed}/{ElapsedWallClock}, ignored: {IgnoredRowCount}",
                aggregateCount, InvocationInfo.LastInvocationStarted.Elapsed, netTimeStopwatch.Elapsed, ignoredRowCount);

            Context.RegisterProcessInvocationEnd(this, netTimeStopwatch.ElapsedMilliseconds);
        }
    }

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static class MemoryAggregationMutatorFluent
    {
        /// <summary>
        /// <para>- input can be unordered</para>
        /// <para>- returns all aggregates at once when everything is processed (blocks execution)</para>
        /// <para>- memory footprint is high because all rows in all groups are collected before aggregation</para>
        /// <para>- if the input is ordered then <see cref="SortedMemoryAggregationMutatorFluent.AggregateOrdered(IFluentProcessMutatorBuilder, SortedMemoryAggregationMutator)"/> should be used for much lower memory footprint and stream-like behavior</para>
        /// <para>- if the input is unordered but only basic operations are used then <see cref="ContinuousAggregationMutatorFluent.AggregateContinuously(IFluentProcessMutatorBuilder, ContinuousAggregationMutator)"/> should be used</para>
        /// </summary>
        public static IFluentProcessMutatorBuilder Aggregate(this IFluentProcessMutatorBuilder builder, MemoryAggregationMutator mutator)
        {
            return builder.AddMutators(mutator);
        }
    }
}