﻿namespace FizzCode.EtLast;

public sealed class ThrowExceptionOnDuplicateKeyMutator : AbstractMutator
{
    public Func<IReadOnlyRow, string> RowKeyGenerator { get; init; }

    private readonly HashSet<string> _keys = new();

    public ThrowExceptionOnDuplicateKeyMutator(IEtlContext context)
        : base(context)
    {
    }

    public override void ValidateParameters()
    {
        base.ValidateParameters();

        if (RowKeyGenerator == null)
            throw new ProcessParameterNullException(this, nameof(RowKeyGenerator));
    }

    protected override void CloseMutator()
    {
        base.CloseMutator();

        _keys.Clear();
    }

    protected override IEnumerable<IRow> MutateRow(IRow row)
    {
        var key = RowKeyGenerator.Invoke(row);
        if (_keys.Contains(key))
        {
            var exception = new DuplicateKeyException(this, row, key);
            throw exception;
        }

        _keys.Add(key);

        yield return row;
    }
}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class ThrowExceptionOnDuplicateKeyMutatorFluent
{
    /// <summary>
    /// Throw an exception if a subsequent occurence of a row key is found.
    /// <para>- input can be unordered</para>
    /// <para>- all keys are stored in memory</para>
    /// </summary>
    public static IFluentSequenceMutatorBuilder ThrowExceptionOnDuplicateKey(this IFluentSequenceMutatorBuilder builder, ThrowExceptionOnDuplicateKeyMutator mutator)
    {
        return builder.AddMutator(mutator);
    }
}
