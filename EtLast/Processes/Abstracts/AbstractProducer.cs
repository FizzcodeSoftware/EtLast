﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;

    /// <summary>
    /// Producer processes create rows. They may create or generate, read from different sources, copy from existing rows.
    /// </summary>
    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class AbstractProducer : AbstractEvaluable
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

        protected AbstractProducer(ITopic topic, string name)
            : base(topic, name)
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

            _currentRowIndex++;

            if (AddRowIndexToColumn != null && !row.HasValue(AddRowIndexToColumn))
                row.SetValue(AddRowIndexToColumn, _currentRowIndex);

            return true;
        }

        protected object HandleConverter(object value, ReaderDefaultColumnConfiguration configuration)
        {
            if (value == null)
            {
                switch (configuration.NullSourceHandler)
                {
                    case NullSourceHandler.WrapError:
                        return new EtlRowError()
                        {
                            Process = this,
                            OriginalValue = null,
                            Message = string.Format(CultureInfo.InvariantCulture, "failed to convert by {0}", configuration.Converter.GetType().GetFriendlyTypeName()),
                        };
                    case NullSourceHandler.SetSpecialValue:
                        return configuration.SpecialValueIfSourceIsNull;
                    default:
                        throw new NotImplementedException(configuration.NullSourceHandler.ToString() + " is not supported yet");
                }
            }

            if (value != null && configuration.Converter != null)
            {
                var newValue = configuration.Converter.Convert(value);
                if (newValue != null)
                    return newValue;

                switch (configuration.InvalidSourceHandler)
                {
                    case InvalidSourceHandler.WrapError:
                        return new EtlRowError()
                        {
                            Process = this,
                            OriginalValue = value,
                            Message = string.Format(CultureInfo.InvariantCulture, "failed to convert by {0}", configuration.Converter.GetType().GetFriendlyTypeName()),
                        };
                    case InvalidSourceHandler.SetSpecialValue:
                        return configuration.SpecialValueIfSourceIsInvalid;
                    default:
                        throw new NotImplementedException(configuration.NullSourceHandler.ToString() + " is not supported yet");
                }
            }

            return value;
        }
    }
}