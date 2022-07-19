﻿namespace FizzCode.EtLast;

public delegate IEnumerable<ISlimRow> ExplodeDelegate(IReadOnlyRow row);

public sealed class ExplodeMutator : AbstractMutator
{
    /// <summary>
    /// Default true.
    /// </summary>
    public bool RemoveOriginalRow { get; init; } = true;

    public ExplodeDelegate RowCreator { get; init; }

    public ExplodeMutator(IEtlContext context)
        : base(context)
    {
    }

    protected override IEnumerable<IRow> MutateRow(IRow row)
    {
        if (!RemoveOriginalRow)
            yield return row;

        var newRows = RowCreator.Invoke(row);
        if (newRows != null)
        {
            foreach (var newRow in newRows)
            {
                yield return Context.CreateRow(this, newRow);
            }
        }
    }

    protected override void ValidateMutator()
    {
        if (RowCreator == null)
            throw new ProcessParameterNullException(this, nameof(RowCreator));
    }
}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class ExplodeMutatorFluent
{
    public static IFluentSequenceMutatorBuilder Explode(this IFluentSequenceMutatorBuilder builder, ExplodeMutator mutator)
    {
        return builder.AddMutator(mutator);
    }

    public static IFluentSequenceMutatorBuilder Explode(this IFluentSequenceMutatorBuilder builder, string name, ExplodeDelegate rowCreator)
    {
        return builder.AddMutator(new ExplodeMutator(builder.ProcessBuilder.Result.Context)
        {
            Name = name,
            RowCreator = rowCreator,
        });
    }
}
