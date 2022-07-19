﻿namespace FizzCode.EtLast;

public sealed class AddKeyHashMutator : AbstractMutator
{
    public string[] KeyColumns { get; init; }
    public string TargetColumn { get; init; }

    /// <summary>
    /// Creates the hash algorithm used by this mutator. Default is <see cref="SHA256.Create()"/>.
    /// </summary>
    public Func<HashAlgorithm> HashAlgorithmCreator { get; init; } = () => SHA256.Create();

    /// <summary>
    /// Default value is false.
    /// </summary>
    public bool IgnoreKeyCase { get; set; }

    /// <summary>
    /// Default value is false.
    /// </summary>
    public bool UpperCaseHash { get; set; }

    private HashAlgorithm _hashAlgorithm;
    private StringBuilder _hashStringBuilder;

    public AddKeyHashMutator(IEtlContext context)
        : base(context)
    {
    }

    protected override void CloseMutator()
    {
        if (_hashAlgorithm != null)
        {
            _hashAlgorithm.Dispose();
            _hashAlgorithm = null;
        }
    }

    protected override IEnumerable<IRow> MutateRow(IRow row)
    {
        var columns = KeyColumns
            ?? row.Values.Select(x => x.Key).ToArray();

        var key = IgnoreKeyCase
            ? row.GenerateKeyUpper(columns)
            : row.GenerateKey(columns);

        if (key != null)
        {
            if (_hashAlgorithm == null)
                _hashAlgorithm = HashAlgorithmCreator.Invoke();

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var hash = _hashAlgorithm.ComputeHash(keyBytes);

            if (_hashStringBuilder == null)
                _hashStringBuilder = new StringBuilder();

            for (var i = 0; i < hash.Length; i++)
            {
                _hashStringBuilder.Append(hash[i].ToString(UpperCaseHash ? "X2" : "x2"));
            }

            row[TargetColumn] = _hashStringBuilder.ToString();
            _hashStringBuilder.Clear();
        }

        yield return row;
    }

    protected override void ValidateMutator()
    {
        if (KeyColumns?.Length == 0)
            throw new ProcessParameterNullException(this, nameof(KeyColumns));

        if (HashAlgorithmCreator == null)
            throw new ProcessParameterNullException(this, nameof(HashAlgorithmCreator));

        if (TargetColumn == null)
            throw new ProcessParameterNullException(this, nameof(TargetColumn));
    }
}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class AddHashMutatorFluent
{
    public static IFluentSequenceMutatorBuilder AddKeyHash(this IFluentSequenceMutatorBuilder builder, AddKeyHashMutator mutator)
    {
        return builder.AddMutator(mutator);
    }

    public static IFluentSequenceMutatorBuilder AddKeyHash(this IFluentSequenceMutatorBuilder builder, string targetColumn, params string[] keyColumns)
    {
        return builder.AddMutator(new AddKeyHashMutator(builder.ProcessBuilder.Result.Context)
        {
            TargetColumn = targetColumn,
            KeyColumns = keyColumns,
        });
    }

    public static IFluentSequenceMutatorBuilder AddKeyHash(this IFluentSequenceMutatorBuilder builder, string targetColumn, Func<HashAlgorithm> hashAlgorithmCreator)
    {
        return builder.AddMutator(new AddKeyHashMutator(builder.ProcessBuilder.Result.Context)
        {
            TargetColumn = targetColumn,
            HashAlgorithmCreator = hashAlgorithmCreator,
        });
    }

    public static IFluentSequenceMutatorBuilder AddKeyHash(this IFluentSequenceMutatorBuilder builder, string targetColumn, Func<HashAlgorithm> hashAlgorithmCreator, params string[] keyColumns)
    {
        return builder.AddMutator(new AddKeyHashMutator(builder.ProcessBuilder.Result.Context)
        {
            TargetColumn = targetColumn,
            HashAlgorithmCreator = hashAlgorithmCreator,
            KeyColumns = keyColumns,
        });
    }
}