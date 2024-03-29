﻿namespace FizzCode.EtLast;

public sealed class AddIncrementalLongIdMutator : AbstractMutator
{
    [ProcessParameterMustHaveValue]
    public required string Column { get; init; }

    public required long FirstId { get; init; }

    private long _nextId;

    protected override void StartMutator()
    {
        _nextId = FirstId;
    }

    protected override IEnumerable<IRow> MutateRow(IRow row, long rowInputIndex)
    {
        row[Column] = _nextId;
        _nextId++;
        yield return row;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public static class AddIncrementalLongIdMutatorFluent
{
    public static IFluentSequenceMutatorBuilder AddIncrementalLongId(this IFluentSequenceMutatorBuilder builder, AddIncrementalLongIdMutator mutator)
    {
        return builder.AddMutator(mutator);
    }
}
