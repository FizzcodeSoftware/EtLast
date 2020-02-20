﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class GroupByOperation : AbstractAggregationOperation
    {
        public Dictionary<string, Func<List<IRow>, string, object>> ColumnAggregators { get; set; } = new Dictionary<string, Func<List<IRow>, string, object>>();

        public GroupByOperation AddColumnAggregator(string column, Func<List<IRow>, string, object> aggregator)
        {
            ColumnAggregators.Add(column, aggregator);
            return this;
        }

        public override IRow TransformGroup(string[] groupingColumns, List<IRow> rows)
        {
            var initialValues = groupingColumns.Select(x => new KeyValuePair<string, object>(x, rows[0][x]))
                .Concat(ColumnAggregators.Select(agg => new KeyValuePair<string, object>(agg.Key, agg.Value.Invoke(rows, agg.Key))));

            return Process.Context.CreateRow(Process, initialValues);
        }
    }

    public static class GroupByOperationExtensions
    {
        /// <summary>
        /// New value will be double.
        /// </summary>
        public static GroupByOperation AddIntAverage(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Average(x => x.GetAs<int>(col)));
        }

        /// <summary>
        /// New value will be double.
        /// </summary>
        public static GroupByOperation AddLongAverage(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Average(x => x.GetAs<long>(col)));
        }

        /// <summary>
        /// New value will be double.
        /// </summary>
        public static GroupByOperation AddDoubleAverage(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Average(x => x.GetAs<double>(col)));
        }

        /// <summary>
        /// New value will be decimal.
        /// </summary>
        public static GroupByOperation AddDecimalAverage(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Average(x => x.GetAs<decimal>(col)));
        }

        /// <summary>
        /// New value will be int.
        /// </summary>
        public static GroupByOperation AddIntSum(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Sum(x => x.GetAs<int>(col)));
        }

        /// <summary>
        /// New value will be long.
        /// </summary>
        public static GroupByOperation AddLongSum(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Sum(x => x.GetAs<long>(col)));
        }

        /// <summary>
        /// New value will be double.
        /// </summary>
        public static GroupByOperation AddDoubleSum(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Sum(x => x.GetAs<double>(col)));
        }

        /// <summary>
        /// New value will be decimal.
        /// </summary>
        public static GroupByOperation AddDecimalSum(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Sum(x => x.GetAs<decimal>(col)));
        }

        /// <summary>
        /// New value will be int.
        /// </summary>
        public static GroupByOperation AddIntMax(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Max(x => x.GetAs<int>(col)));
        }

        /// <summary>
        /// New value will be long.
        /// </summary>
        public static GroupByOperation AddLongMax(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Max(x => x.GetAs<long>(col)));
        }

        /// <summary>
        /// New value will be double.
        /// </summary>
        public static GroupByOperation AddDoubleMax(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Max(x => x.GetAs<double>(col)));
        }

        /// <summary>
        /// New value will be decimal.
        /// </summary>
        public static GroupByOperation AddDecimalMax(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Max(x => x.GetAs<decimal>(col)));
        }

        /// <summary>
        /// New value will be int.
        /// </summary>
        public static GroupByOperation AddIntMin(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Min(x => x.GetAs<int>(col)));
        }

        /// <summary>
        /// New value will be long.
        /// </summary>
        public static GroupByOperation AddLongMin(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Min(x => x.GetAs<long>(col)));
        }

        /// <summary>
        /// New value will be double.
        /// </summary>
        public static GroupByOperation AddDoubleMin(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Min(x => x.GetAs<double>(col)));
        }

        /// <summary>
        /// New value will be decimal.
        /// </summary>
        public static GroupByOperation AddDecimalMin(this GroupByOperation op, string column)
        {
            return op.AddColumnAggregator(column, (groupRows, col) => groupRows.Min(x => x.GetAs<decimal>(col)));
        }
    }
}