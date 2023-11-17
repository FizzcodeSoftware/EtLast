﻿namespace FizzCode.EtLast;

public delegate IEnumerable<ISlimRow> ExplodeDelegate(IReadOnlyRow row);

public sealed class ExplodeMutator(IEtlContext context) : AbstractMutator(context)
{
    /// <summary>
    /// Default true.
    /// </summary>
    public required bool RemoveOriginalRow { get; init; } = true;

    [ProcessParameterMustHaveValue]
    public required ExplodeDelegate RowCreator { get; init; }

    protected override IEnumerable<IRow> MutateRow(IRow row, long rowInputIndex)
    {
        if (!RemoveOriginalRow)
            yield return row;

        var newRows = RowCreator.Invoke(row);
        if (newRows != null)
        {
            foreach (var newRow in newRows)
                yield return Context.CreateRow(this, newRow);
        }
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
            RemoveOriginalRow = true,
            RowCreator = rowCreator,
        });
    }
}
