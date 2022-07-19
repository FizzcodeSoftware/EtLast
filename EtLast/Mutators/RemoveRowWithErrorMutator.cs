﻿namespace FizzCode.EtLast;

public sealed class RemoveRowWithErrorMutator : AbstractMutator
{
    public RemoveRowWithErrorMutator(IEtlContext context)
        : base(context)
    {
    }

    protected override IEnumerable<IRow> MutateRow(IRow row)
    {
        if (!row.HasError())
            yield return row;
    }
}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class RemoveRowWithErrorMutatorFluent
{
    public static IFluentSequenceMutatorBuilder RemoveRow(this IFluentSequenceMutatorBuilder builder, RemoveRowWithErrorMutator mutator)
    {
        return builder.AddMutator(mutator);
    }
}
