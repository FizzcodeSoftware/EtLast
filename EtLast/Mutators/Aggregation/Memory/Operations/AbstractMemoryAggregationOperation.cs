﻿namespace FizzCode.EtLast;

[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class AbstractMemoryAggregationOperation : IMemoryAggregationOperation
{
    public abstract void TransformGroup(List<IReadOnlySlimRow> groupRows, Func<ISlimRow> aggregateCreator);
}