﻿namespace FizzCode.EtLast;

public sealed class AddIncrementalIntegerIdMutator : AbstractMutator
{
    public string Column { get; init; }

    /// <summary>
    /// Default value is 0.
    /// </summary>
    public int FirstId { get; init; }

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

    protected override void ValidateMutator()
    {
        if (string.IsNullOrEmpty(Column))
            throw new ProcessParameterNullException(this, nameof(Column));
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
