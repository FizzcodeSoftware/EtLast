﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.IO;
    using OfficeOpenXml;

    public sealed class EpPlusSimpleRowWriterMutator : AbstractMutator, IRowSink
    {
        public string FileName { get; init; }
        public ExcelPackage ExistingPackage { get; init; }
        public string SheetName { get; init; }
        public Dictionary<string, ExcelColumnConfiguration> Columns { get; init; }
        public Action<ExcelPackage, SimpleExcelWriterState> Finalize { get; init; }

        private SimpleExcelWriterState _state;
        private ExcelPackage _package;
        private int? _sinkUid;
        private int _rowCount;

        public EpPlusSimpleRowWriterMutator(IEtlContext context)
            : base(context)
        {
        }

        protected override void StartMutator()
        {
            _state = new SimpleExcelWriterState();
            _rowCount = 0;
        }

        protected override void CloseMutator()
        {
            if (_state.LastWorksheet != null)
            {
                Finalize?.Invoke(_package, _state);
            }

            if (ExistingPackage == null && _package != null)
            {
                var iocUid = Context.RegisterIoCommandStart(this, IoCommandKind.fileWrite, PathHelpers.GetFriendlyPathName(FileName), null, null, null, null,
                    "saving file to {FileName}",
                    PathHelpers.GetFriendlyPathName(FileName));

                try
                {
                    _package.Save();
                    Context.RegisterIoCommandSuccess(this, IoCommandKind.fileWrite, iocUid, _rowCount);
                }
                catch (Exception ex)
                {
                    Context.RegisterIoCommandFailed(this, IoCommandKind.fileWrite, iocUid, null, ex);
                    throw;
                }

                _package.Dispose();
                _package = null;
            }

            _state = null;
        }

        protected override IEnumerable<IRow> MutateRow(IRow row)
        {
            if (_sinkUid == null)
            {
                _sinkUid = Context.GetSinkUid(PathHelpers.GetFriendlyPathName(FileName), SheetName);
            }

            Context.RegisterWriteToSink(row, _sinkUid.Value);
            _rowCount++;

            if (_package == null) // lazy load here instead of prepare
            {
                _package = ExistingPackage ?? new ExcelPackage(new FileInfo(FileName));

                _state.LastWorksheet = _package.Workbook.Worksheets.Add(SheetName);
                _state.LastRow = 1;
                _state.LastCol = 1;
                foreach (var col in Columns)
                {
                    _state.LastWorksheet.Cells[_state.LastRow, _state.LastCol].Value = col.Key;
                    _state.LastCol++;
                }

                _state.LastRow++;
            }

            try
            {
                _state.LastCol = 1;
                foreach (var col in Columns)
                {
                    var range = _state.LastWorksheet.Cells[_state.LastRow, _state.LastCol];
                    range.Value = row[col.Value?.SourceColumn ?? col.Key];
                    if (col.Value?.NumberFormat != null)
                        range.Style.Numberformat.Format = col.Value.NumberFormat;

                    _state.LastCol++;
                }

                _state.LastRow++;
            }
            catch (Exception ex)
            {
                var exception = new ProcessExecutionException(this, row, "error raised during writing an excel file", ex);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "error raised during writing an excel file, file name: {0}, message: {1}, row: {2}", FileName, ex.Message, row.ToDebugString()));
                exception.Data.Add("FileName", FileName);
                exception.Data.Add("SheetName", SheetName);
                throw exception;
            }

            yield return row;
        }

        protected override void ValidateMutator()
        {
            base.ValidateMutator();

            if (string.IsNullOrEmpty(FileName))
                throw new ProcessParameterNullException(this, nameof(FileName));

            if (string.IsNullOrEmpty(SheetName))
                throw new ProcessParameterNullException(this, nameof(SheetName));

            if (Columns == null)
                throw new ProcessParameterNullException(this, nameof(Columns));
        }
    }

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static class EpPlusSimpleRowWriterMutatorFluent
    {
        public static IFluentProcessMutatorBuilder WriteRowToExcelSimple(this IFluentProcessMutatorBuilder builder, EpPlusSimpleRowWriterMutator mutator)
        {
            return builder.AddMutator(mutator);
        }
    }
}