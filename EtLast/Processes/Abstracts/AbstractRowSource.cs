﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;

    /// <summary>
    /// Row sources create rows - they may create or generate, read from different sources, copy from existing rows.
    /// </summary>
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class AbstractRowSource : AbstractEvaluable, IRowSource
    {
        /// <summary>
        /// Default false.
        /// </summary>
        public bool IgnoreRowsWithError { get; init; }

        /// <summary>
        /// Default true.
        /// </summary>
        public bool IgnoreNullOrEmptyRows { get; init; } = true;

        /// <summary>
        /// First row index is (integer) 1
        /// </summary>
        public string AddRowIndexToColumn { get; init; }

        private int _currentRowIndex;
        protected bool AutomaticallyEvaluateAndYieldInputProcessRows { get; init; } = true;

        protected AbstractRowSource(IEtlContext context)
            : base(context)
        {
        }

        protected sealed override IEnumerable<IRow> EvaluateImpl(Stopwatch netTimeStopwatch)
        {
            var resultCount = 0;

            netTimeStopwatch.Stop();
            var enumerator = Produce().GetEnumerator();
            netTimeStopwatch.Start();

            while (!Context.CancellationTokenSource.IsCancellationRequested)
            {
                IRow row;
                try
                {
                    if (!enumerator.MoveNext())
                        break;

                    row = enumerator.Current;
                    if (!ProcessRowBeforeYield(row))
                        continue;
                }
                catch (Exception ex)
                {
                    Context.AddException(this, ProcessExecutionException.Wrap(this, ex));
                    break;
                }

                resultCount++;
                netTimeStopwatch.Stop();
                yield return row;
                netTimeStopwatch.Start();
            }

            netTimeStopwatch.Stop();
            Context.Log(LogSeverity.Debug, this, "produced {RowCount} rows in {Elapsed}/{ElapsedWallClock}",
                resultCount, InvocationInfo.LastInvocationStarted.Elapsed, netTimeStopwatch.Elapsed);

            Context.RegisterProcessInvocationEnd(this, netTimeStopwatch.ElapsedMilliseconds);
        }

        protected abstract IEnumerable<IRow> Produce();

        private bool ProcessRowBeforeYield(IRow row)
        {
            if (IgnoreRowsWithError && row.HasError())
                return false;

            if (IgnoreNullOrEmptyRows && row.IsNullOrEmpty())
                return false;

            if (AddRowIndexToColumn != null && !row.HasValue(AddRowIndexToColumn))
                row[AddRowIndexToColumn] = _currentRowIndex;

            _currentRowIndex++;

            return true;
        }
    }
}