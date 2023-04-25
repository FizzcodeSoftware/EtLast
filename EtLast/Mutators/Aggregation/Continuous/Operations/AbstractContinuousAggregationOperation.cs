﻿namespace FizzCode.EtLast;

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public abstract class AbstractContinuousAggregationOperation : IContinuousAggregationOperation
{
    public abstract void TransformAggregate(IReadOnlySlimRow row, ContinuousAggregate aggregate);
}
