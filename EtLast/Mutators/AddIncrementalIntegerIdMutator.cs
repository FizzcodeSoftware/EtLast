﻿namespace FizzCode.EtLast;

public sealed class AddIncrementalIntegerIdMutator : AbstractMutator
{
    [ProcessParameterNullException]
    public required string Column { get; init; }

    public required int FirstId { get; init; }

    private int _nextId;

    public AddIncrementalIntegerIdMutator(IEtlContext context)
        : base(context)
    {
    }

    protected override void StartMutator()
    {
        _nextId = FirstId;
    }

    protected override IEnumerable<IRow> MutateRow(IRow row)
    {
        row[Column] = _nextId;
        _nextId++;
        yield return row;
    }
}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class AddIncrementalIdMutatorFluent
{
    public static IFluentSequenceMutatorBuilder AddIncrementalIntegerId(this IFluentSequenceMutatorBuilder builder, AddIncrementalIntegerIdMutator mutator)
    {
        return builder.AddMutator(mutator);
    }
}
