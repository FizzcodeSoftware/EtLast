﻿namespace FizzCode.EtLast;

public sealed class ReplaceStringMutator : AbstractMutator
{
    [ProcessParameterMustHaveValue]
    public required string ColumnName { get; init; }

    [ProcessParameterMustHaveValue]
    public required string OldString { get; init; }

    public required string NewString { get; init; }

    /// <summary>
    /// Default value is <see cref="StringComparison.InvariantCulture"/>.
    /// </summary>
    public required StringComparison StringComparison { get; init; } = StringComparison.InvariantCulture;

    protected override IEnumerable<IRow> MutateRow(IRow row, long rowInputIndex)
    {
        if (row.HasValue(ColumnName) && row[ColumnName] is string value && value.IndexOf(OldString, StringComparison) > -1)
        {
            value = value.Replace(OldString, NewString, StringComparison);
            row[ColumnName] = value;
        }

        yield return row;
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public static class ReplaceStringMutatorFluent
{
    public static IFluentSequenceMutatorBuilder ReplaceString(this IFluentSequenceMutatorBuilder builder, ReplaceStringMutator mutator)
    {
        return builder.AddMutator(mutator);
    }
}
