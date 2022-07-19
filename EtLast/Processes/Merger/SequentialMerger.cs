﻿namespace FizzCode.EtLast;

public sealed class SequentialMerger : AbstractMerger
{
    public SequentialMerger(IEtlContext context)
        : base(context)
    {
    }

    protected override void ValidateImpl()
    {
    }

    protected override IEnumerable<IRow> EvaluateImpl(Stopwatch netTimeStopwatch)
    {
        foreach (var sequence in SequenceList)
        {
            if (Context.CancellationToken.IsCancellationRequested)
                yield break;

            var rows = sequence.Evaluate(this).TakeRowsAndTransferOwnership();
            foreach (var row in rows)
            {
                yield return row;
            }
        }
    }
}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class SequentialMergerFluent
{
    public static IFluentSequenceMutatorBuilder SequentialMerge(this IFluentSequenceBuilder builder, IEtlContext context, string name, Action<SequentialMergerBuilder> merger)
    {
        var subBuilder = new SequentialMergerBuilder(context, name);
        merger.Invoke(subBuilder);
        return builder.ReadFrom(subBuilder.Merger);
    }
}

public class SequentialMergerBuilder
{
    public SequentialMerger Merger { get; }

    internal SequentialMergerBuilder(IEtlContext context, string name)
    {
        Merger = new SequentialMerger(context)
        {
            Name = name,
            SequenceList = new List<ISequence>(),
        };
    }

    public SequentialMergerBuilder Add(ISequence sequence)
    {
        Merger.SequenceList.Add(sequence);
        return this;
    }
}