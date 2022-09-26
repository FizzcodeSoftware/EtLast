﻿namespace FizzCode.EtLast;

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public abstract class AbstractBatchedMutator : AbstractSequence, IMutator
{
    public ISequence Input { get; set; }
    public RowTestDelegate RowFilter { get; set; }
    public RowTagTestDelegate RowTagFilter { get; set; }

    public abstract int BatchSize { get; init; }

    /// <summary>
    /// Default false.
    /// </summary>
    protected bool UseBatchKeys { get; init; }

    protected AbstractBatchedMutator(IEtlContext context)
        : base(context)
    {
    }

    protected sealed override IEnumerable<IRow> EvaluateImpl(Stopwatch netTimeStopwatch)
    {
        try
        {
            StartMutator();
        }
        catch (Exception ex)
        {
            Pipe.AddException(this, ex);
            yield break;
        }

        var mutatedRows = new List<IRow>();
        var removedRows = new List<IRow>();

        netTimeStopwatch.Stop();
        var enumerator = Input.TakeRowsAndTransferOwnership(this).GetEnumerator();
        netTimeStopwatch.Start();

        var batch = new List<IRow>();
        var batchKeys = new HashSet<string>();

        var failed = false;
        var mutatedRowCount = 0;
        var ignoredRowCount = 0;
        var batchCount = 0;

        while (!Pipe.IsTerminating)
        {
            netTimeStopwatch.Stop();
            var finished = !enumerator.MoveNext();
            if (finished)
                break;

            var row = enumerator.Current;
            netTimeStopwatch.Start();

            if (row.Tag is HeartBeatTag)
            {
                netTimeStopwatch.Stop();
                yield return row;
                netTimeStopwatch.Start();
                continue;
            }

            var apply = false;
            if (RowFilter != null)
            {
                try
                {
                    apply = RowFilter.Invoke(row);
                }
                catch (Exception ex)
                {
                    Pipe.AddException(this, ex, row);
                    break;
                }

                if (!apply)
                {
                    ignoredRowCount++;
                    netTimeStopwatch.Stop();
                    yield return row;
                    netTimeStopwatch.Start();
                    continue;
                }
            }

            if (RowTagFilter != null)
            {
                try
                {
                    apply = RowTagFilter.Invoke(row.Tag);
                }
                catch (Exception ex)
                {
                    Pipe.AddException(this, ex, row);
                    break;
                }

                if (!apply)
                {
                    ignoredRowCount++;
                    netTimeStopwatch.Stop();
                    yield return row;
                    netTimeStopwatch.Start();
                    continue;
                }
            }

            mutatedRowCount++;

            bool mutationHappened, removeOriginal;
            try
            {
                MutateSingleRow(row, mutatedRows, out removeOriginal, out mutationHappened);
            }
            catch (Exception ex)
            {
                Pipe.AddException(this, ex, row);
                failed = true;
                break;
            }

            if (mutationHappened)
            {
                if (removeOriginal)
                {
                    Context.SetRowOwner(row, null);
                }

                netTimeStopwatch.Stop();

                foreach (var mutatedRow in mutatedRows)
                {
                    if (mutatedRow.CurrentProcess != this)
                    {
                        Pipe.AddException(this, new ProcessExecutionException(this, mutatedRow, "mutator returned a row without proper ownership"));
                        failed = true;
                        break;
                    }

                    netTimeStopwatch.Stop();
                    yield return mutatedRow;
                    netTimeStopwatch.Start();
                }

                mutatedRows.Clear();
            }
            else
            {
                batch.Add(row);

                if (UseBatchKeys)
                {
                    try
                    {
                        var key = GetBatchKey(row);
                        batchKeys.Add(key);
                    }
                    catch (Exception ex)
                    {
                        Pipe.AddException(this, ex, row);
                        failed = true;
                        break;
                    }
                }

                if ((UseBatchKeys && batchKeys.Count >= BatchSize) || (!UseBatchKeys && batch.Count >= BatchSize))
                {
                    batchCount++;
                    try
                    {
                        MutateBatch(batch, mutatedRows, removedRows);
                    }
                    catch (Exception ex)
                    {
                        Pipe.AddException(this, ex, row);
                        failed = true;
                        break;
                    }

                    foreach (var removedRow in removedRows)
                    {
                        Context.SetRowOwner(removedRow, null);
                    }

                    netTimeStopwatch.Stop();
                    foreach (var mutatedRow in mutatedRows)
                    {
                        if (mutatedRow.CurrentProcess != this)
                        {
                            Pipe.AddException(this, new ProcessExecutionException(this, mutatedRow, "mutator returned a row without proper ownership"));
                            failed = true;
                            break;
                        }

                        yield return mutatedRow;
                    }

                    netTimeStopwatch.Start();

                    mutatedRows.Clear();
                    removedRows.Clear();
                    batch.Clear();
                    batchKeys.Clear();
                }
            }
        }

        netTimeStopwatch.Start();

        // process remaining rows
        if (batch.Count > 0 && !failed)
        {
            batchCount++;
            try
            {
                MutateBatch(batch, mutatedRows, removedRows);
            }
            catch (Exception ex)
            {
                Pipe.AddException(this, ex);
                failed = true;
            }

            if (!failed)
            {
                foreach (var removedRow in removedRows)
                {
                    Context.SetRowOwner(removedRow, null);
                }

                netTimeStopwatch.Stop();
                foreach (var mutatedRow in mutatedRows)
                {
                    if (mutatedRow.CurrentProcess != this)
                    {
                        Pipe.AddException(this, new ProcessExecutionException(this, mutatedRow, "mutator returned a row without proper ownership"));
                        failed = true;
                        break;
                    }

                    yield return mutatedRow;
                }

                netTimeStopwatch.Start();
            }

            mutatedRows.Clear();
            removedRows.Clear();
            batch.Clear();
            batchKeys.Clear();
        }

        try
        {
            CloseMutator();
        }
        catch (Exception ex)
        {
            Pipe.AddException(this, ex);
        }

        netTimeStopwatch.Stop();

        if (mutatedRowCount + ignoredRowCount > 0)
        {
            Context.Log(LogSeverity.Debug, this, "mutated {MutatedRowCount}/{TotalRowCount} rows in {Elapsed}/{ElapsedWallClock} in {BatchCount} batches",
                mutatedRowCount, mutatedRowCount + ignoredRowCount, InvocationInfo.LastInvocationStarted.Elapsed, netTimeStopwatch.Elapsed, batchCount);
        }
    }

    protected sealed override void ValidateImpl()
    {
        if (Input == null)
            throw new ProcessParameterNullException(this, nameof(Input));

        ValidateMutator();
    }

    protected virtual void ValidateMutator()
    {
    }

    protected virtual void StartMutator()
    {
    }

    protected virtual void CloseMutator()
    {
    }

    protected abstract void MutateSingleRow(IRow row, List<IRow> mutatedRows, out bool removeOriginal, out bool processed);

    protected abstract void MutateBatch(List<IRow> rows, List<IRow> mutatedRows, List<IRow> removedRows);

    protected virtual string GetBatchKey(IRow row)
    {
        return null;
    }

    public IEnumerable<IMutator> GetMutators()
    {
        yield return this;
    }

    public IEnumerator<IMutator> GetEnumerator()
    {
        yield return this;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        yield return this;
    }
}
