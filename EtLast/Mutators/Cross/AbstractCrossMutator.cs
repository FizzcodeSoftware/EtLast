﻿namespace FizzCode.EtLast;

[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class AbstractCrossMutator : AbstractMutator
{
    [ProcessParameterMustHaveValue]
    public required RowLookupBuilder LookupBuilder { get; init; }

    protected AbstractCrossMutator()
    {
    }
}