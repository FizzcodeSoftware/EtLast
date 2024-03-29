﻿namespace FizzCode.EtLast;

public sealed class ContinuousGroupByOperation : AbstractContinuousAggregationOperation
{
    public delegate void ContinuousGroupByAggregatorDelegate(ContinuousAggregate aggregate, IReadOnlySlimRow row);
    public int AggregatorCount => _aggregators.Count;
    private readonly List<ContinuousGroupByAggregatorDelegate> _aggregators = [];

    public ContinuousGroupByOperation AddAggregator(ContinuousGroupByAggregatorDelegate aggregator)
    {
        _aggregators.Add(aggregator);
        return this;
    }

    public override void TransformAggregate(IReadOnlySlimRow row, ContinuousAggregate aggregate)
    {
        foreach (var aggregator in _aggregators)
        {
            aggregator.Invoke(aggregate, row);
        }
    }
}

public static class ContinuousGroupByOperationExtensions
{
    /// <summary>
    /// StartWith value will be integer.
    /// </summary>
    public static ContinuousGroupByOperation AddIntNumberOfDistinctKeys(this ContinuousGroupByOperation op, string column, RowKeyGenerator keyGenerator)
    {
        var id = op.AggregatorCount.ToString("D", CultureInfo.InvariantCulture) + ":" + nameof(AddIntNumberOfDistinctKeys);
        return op.AddAggregator((aggregate, row) =>
        {
            var key = keyGenerator.Invoke(row);
            if (key != null)
            {
                var hashset = aggregate.GetStateValue<HashSet<string>>(id, null);
                if (hashset == null)
                {
                    hashset = [];
                    aggregate.SetStateValue(id, hashset);
                }

                if (hashset.Add(key))
                {
                    var newValue = hashset.Count;
                    aggregate.ResultRow[column] = newValue;
                }
            }
        });
    }

    /// <summary>
    /// StartWith value will be integer.
    /// </summary>
    public static ContinuousGroupByOperation AddIntCount(this ContinuousGroupByOperation op, string targetColumn)
    {
        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.GetAs(targetColumn, 0) + 1;
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be integer.
    /// </summary>
    public static ContinuousGroupByOperation AddIntCountWhenNotNull(this ContinuousGroupByOperation op, string targetColumn, string columnToCheckForNull)
    {
        return op.AddAggregator((aggregate, row) =>
        {
            if (row.HasValue(columnToCheckForNull))
            {
                var newValue = aggregate.ResultRow.GetAs(targetColumn, 0) + 1;
                aggregate.ResultRow[targetColumn] = newValue;
            }
        });
    }

    /// <summary>
    /// StartWith value will be integer.
    /// </summary>
    public static ContinuousGroupByOperation AddIntCountWhenNull(this ContinuousGroupByOperation op, string targetColumn, string columnToCheckForNull)
    {
        return op.AddAggregator((aggregate, row) =>
        {
            if (!row.HasValue(columnToCheckForNull))
            {
                var newValue = aggregate.ResultRow.GetAs(targetColumn, 0) + 1;
                aggregate.ResultRow[targetColumn] = newValue;
            }
        });
    }

    /// <summary>
    /// StartWith value will be double.
    /// </summary>
    public static ContinuousGroupByOperation AddIntAverage(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        var id = op.AggregatorCount.ToString("D", CultureInfo.InvariantCulture) + ":" + nameof(AddIntAverage);

        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newSum = aggregate.GetStateValue(id, 0) + row.GetAs(sourceColumn, 0);
            aggregate.SetStateValue(id, newSum);

            var newValue = newSum / (double)(aggregate.RowsInGroup + 1);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be double.
    /// </summary>
    public static ContinuousGroupByOperation AddLongAverage(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        var id = op.AggregatorCount.ToString("D", CultureInfo.InvariantCulture) + ":" + nameof(AddLongAverage);

        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newSum = aggregate.GetStateValue(id, 0L) + row.GetAs(sourceColumn, 0L);
            aggregate.SetStateValue(id, newSum);

            var newValue = newSum / (double)(aggregate.RowsInGroup + 1);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be double.
    /// </summary>
    public static ContinuousGroupByOperation AddDoubleAverage(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        var id = op.AggregatorCount.ToString("D", CultureInfo.InvariantCulture) + ":" + nameof(AddDoubleAverage);

        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newSum = aggregate.GetStateValue(id, 0.0d) + row.GetAs(sourceColumn, 0.0d);
            aggregate.SetStateValue(id, newSum);

            var newValue = newSum / (aggregate.RowsInGroup + 1);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be double. Null values are ignored.
    /// </summary>
    public static ContinuousGroupByOperation AddDoubleAverageIgnoreNull(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        var idSum = op.AggregatorCount.ToString("D", CultureInfo.InvariantCulture) + ":" + nameof(AddDoubleAverageIgnoreNull) + ":sum";
        var idCnt = op.AggregatorCount.ToString("D", CultureInfo.InvariantCulture) + ":" + nameof(AddDoubleAverageIgnoreNull) + ":cnt";

        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            if (!row.HasValue(sourceColumn))
                return;

            var newSum = aggregate.GetStateValue(idSum, 0.0d) + row.GetAs(sourceColumn, 0.0);
            aggregate.SetStateValue(idSum, newSum);

            var newCnt = aggregate.GetStateValue(idCnt, 0) + 1;
            aggregate.SetStateValue(idCnt, newCnt);

            var newValue = newSum / newCnt;
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be decimal.
    /// </summary>
    public static ContinuousGroupByOperation AddDecimalAverage(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        var id = op.AggregatorCount.ToString("D", CultureInfo.InvariantCulture) + ":" + nameof(AddDecimalAverage);

        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newSum = aggregate.GetStateValue(id, 0m) + row.GetAs(sourceColumn, 0m);
            aggregate.SetStateValue(id, newSum);

            var newValue = newSum / (aggregate.RowsInGroup + 1);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be int.
    /// </summary>
    public static ContinuousGroupByOperation AddIntSum(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.GetAs(targetColumn, 0) + row.GetAs(sourceColumn, 0);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be long.
    /// </summary>
    public static ContinuousGroupByOperation AddLongSum(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.GetAs(targetColumn, 0L) + row.GetAs(sourceColumn, 0L);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be double.
    /// </summary>
    public static ContinuousGroupByOperation AddDoubleSum(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.GetAs(targetColumn, 0.0d) + row.GetAs(sourceColumn, 0.0d);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be decimal.
    /// </summary>
    public static ContinuousGroupByOperation AddDecimalSum(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.GetAs(targetColumn, 0m) + row.GetAs(sourceColumn, 0m);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be int.
    /// </summary>
    public static ContinuousGroupByOperation AddIntMax(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.HasValue(targetColumn)
                ? Math.Max(aggregate.ResultRow.GetAs(targetColumn, 0), row.GetAs(sourceColumn, 0))
                : row.GetAs(sourceColumn, 0);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be long.
    /// </summary>
    public static ContinuousGroupByOperation AddLongMax(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.HasValue(targetColumn)
                ? Math.Max(aggregate.ResultRow.GetAs(targetColumn, 0L), row.GetAs(sourceColumn, 0L))
                : row.GetAs(sourceColumn, 0L);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be double.
    /// </summary>
    public static ContinuousGroupByOperation AddDoubleMax(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.HasValue(targetColumn)
                ? Math.Max(aggregate.ResultRow.GetAs(targetColumn, 0.0d), row.GetAs(sourceColumn, 0.0d))
                : row.GetAs(sourceColumn, 0.0d);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be decimal.
    /// </summary>
    public static ContinuousGroupByOperation AddDecimalMax(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.HasValue(targetColumn)
                ? Math.Max(aggregate.ResultRow.GetAs(targetColumn, 0m), row.GetAs(sourceColumn, 0m))
                : row.GetAs(sourceColumn, 0m);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be datetime.
    /// </summary>
    public static ContinuousGroupByOperation AddDateTimeMax(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            if (aggregate.ResultRow.HasValue(targetColumn))
            {
                var source = row.GetAs(sourceColumn, DateTime.MinValue);
                var target = aggregate.ResultRow.GetAs<DateTime>(targetColumn);
                if (source > target)
                    aggregate.ResultRow[targetColumn] = source;
            }
            else
            {
                aggregate.ResultRow[targetColumn] = row.GetAs(sourceColumn, DateTime.MinValue);
            }
        });
    }

    /// <summary>
    /// StartWith value will be int.
    /// </summary>
    public static ContinuousGroupByOperation AddIntMin(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.HasValue(targetColumn)
                ? Math.Min(aggregate.ResultRow.GetAs(targetColumn, 0), row.GetAs(sourceColumn, 0))
                : row.GetAs(sourceColumn, 0);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be long.
    /// </summary>
    public static ContinuousGroupByOperation AddLongMin(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.HasValue(targetColumn)
                ? Math.Min(aggregate.ResultRow.GetAs(targetColumn, 0L), row.GetAs(sourceColumn, 0L))
                : row.GetAs(sourceColumn, 0L);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be double.
    /// </summary>
    public static ContinuousGroupByOperation AddDoubleMin(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.HasValue(targetColumn)
                ? Math.Min(aggregate.ResultRow.GetAs(targetColumn, 0.0d), row.GetAs(sourceColumn, 0.0d))
                : row.GetAs(sourceColumn, 0.0d);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be decimal.
    /// </summary>
    public static ContinuousGroupByOperation AddDecimalMin(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            var newValue = aggregate.ResultRow.HasValue(targetColumn)
                ? Math.Min(aggregate.ResultRow.GetAs(targetColumn, 0m), row.GetAs(sourceColumn, 0m))
                : row.GetAs(sourceColumn, 0m);
            aggregate.ResultRow[targetColumn] = newValue;
        });
    }

    /// <summary>
    /// StartWith value will be datetime.
    /// </summary>
    public static ContinuousGroupByOperation AddDateTimeMin(this ContinuousGroupByOperation op, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        return op.AddAggregator((aggregate, row) =>
        {
            if (aggregate.ResultRow.HasValue(targetColumn))
            {
                var source = row.GetAs(sourceColumn, DateTime.MaxValue);
                var target = aggregate.ResultRow.GetAs<DateTime>(targetColumn);
                if (source < target)
                    aggregate.ResultRow[targetColumn] = source;
            }
            else
            {
                aggregate.ResultRow[targetColumn] = row.GetAs(sourceColumn, DateTime.MaxValue);
            }
        });
    }

    /// <summary>
    /// Calculates the standard deviation for an aggregate.
    /// https://math.stackexchange.com/questions/198336/how-to-calculate-standard-deviation-with-streaming-inputs
    /// </summary>
    /// <param name="op">The operation</param>
    /// <param name="useEntirePopulation">If true, equivalent to STDEV.P, if false, STDEV.S</param>
    /// <param name="sourceColumn">The source column.</param>
    /// <param name="targetColumn">The targe column.</param>
    public static ContinuousGroupByOperation AddDoubleStandardDeviation(this ContinuousGroupByOperation op, bool useEntirePopulation, string sourceColumn, string targetColumn = null)
    {
        targetColumn ??= sourceColumn;

        var idM2 = op.AggregatorCount.ToString("D", CultureInfo.InvariantCulture) + ":" + nameof(AddDoubleAverageIgnoreNull) + ":m2";
        var idCnt = op.AggregatorCount.ToString("D", CultureInfo.InvariantCulture) + ":" + nameof(AddDoubleAverageIgnoreNull) + ":cnt";
        var idMean = op.AggregatorCount.ToString("D", CultureInfo.InvariantCulture) + ":" + nameof(AddDoubleAverageIgnoreNull) + ":mean";

        return op.AddAggregator((aggregate, row) =>
        {
            if (!row.HasValue(sourceColumn))
                return;

            var m2 = aggregate.GetStateValue(idM2, 0.0);
            var newCount = aggregate.GetStateValue(idCnt, 0) + 1;
            var mean = aggregate.GetStateValue(idMean, 0.0);

            var value = row.GetAs(sourceColumn, 0.0);

            var delta = value - mean;
            mean += delta / newCount;
            m2 += delta * (value - mean);

            if (!useEntirePopulation && newCount < 2)
            {
                aggregate.ResultRow[targetColumn] = null;
            }
            else
            {
                var divider = useEntirePopulation
                    ? newCount
                    : newCount - 1;

                aggregate.ResultRow[targetColumn] = Math.Sqrt(m2 / divider);
            }

            aggregate.SetStateValue(idM2, m2);
            aggregate.SetStateValue(idCnt, newCount);
            aggregate.SetStateValue(idMean, mean);
        });
    }
}
