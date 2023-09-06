﻿namespace FizzCode.EtLast;

public enum DelimitedLineHeader { NoHeader, HasHeader, IgnoreHeader }

public sealed class DelimitedLineReader : AbstractRowSource
{
    public required IStreamProvider StreamProvider { get; init; }

    public Dictionary<string, TextReaderColumn> Columns { get; init; }
    public TextReaderDefaultColumn DefaultColumns { get; init; }

    /// <summary>
    /// Default <see cref="DelimitedLineHeader.NoHeader"/>.
    /// </summary>
    public required DelimitedLineHeader Header { get; init; }

    /// <summary>
    /// Default true.
    /// </summary>
    public bool TreatEmptyStringAsNull { get; init; } = true;

    /// <summary>
    /// Default true. If a value starts and ends with double quote (") characters, then both will be removed (this happens before type conversion)
    /// </summary>
    public bool RemoveSurroundingDoubleQuotes { get; init; } = true;

    /// <summary>
    /// Default null. Column names must be set if <see cref="Header"/> is <see cref="DelimitedLineHeader.NoHeader"/> or <see cref="DelimitedLineHeader.IgnoreHeader"/>, otherwise it should be left null.
    /// </summary>
    public string[] ColumnNames { get; init; }

    /// <summary>
    /// Default null.
    /// </summary>
    public string[] IgnoreColumns { get; init; }

    /// <summary>
    /// Default value is ';'.
    /// </summary>
    public required char Delimiter { get; init; } = ';';

    /// <summary>
    /// Default value is 0
    /// </summary>
    public int SkipLinesAtBeginning { get; init; }

    /// <summary>
    /// Default value is \r\n
    /// </summary>
    public string LineEnding { get; init; } = "\r\n";

    /// <summary>
    /// Default value is <see cref="Encoding.UTF8"/>
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>
    /// First stream index is (integer) 0
    /// </summary>
    public string AddStreamIndexToColumn { get; init; }

    public DelimitedLineReader(IEtlContext context)
        : base(context)
    {
    }

    public override string GetTopic()
    {
        return StreamProvider?.GetTopic();
    }

    protected override void ValidateImpl()
    {
        if (StreamProvider == null)
            throw new ProcessParameterNullException(this, nameof(StreamProvider));

        StreamProvider.Validate(this);

        if (Header != DelimitedLineHeader.HasHeader && (ColumnNames == null || ColumnNames.Length == 0))
            throw new ProcessParameterNullException(this, nameof(ColumnNames));

        if (Header == DelimitedLineHeader.HasHeader && ColumnNames?.Length > 0)
            throw new InvalidProcessParameterException(this, nameof(ColumnNames), ColumnNames, nameof(ColumnNames) + " must be null if " + nameof(Header) + " is true.");

        if (Columns == null && DefaultColumns == null)
            throw new InvalidProcessParameterException(this, nameof(Columns), Columns, nameof(DefaultColumns) + " must be specified if " + nameof(Columns) + " is null.");
    }

    protected override IEnumerable<IRow> Produce()
    {
        // key is the SOURCE col name
        var columnMap = Columns?.ToDictionary(
            kvp => kvp.Value.SourceColumn ?? kvp.Key,
            kvp => (rowColumn: kvp.Key, config: kvp.Value),
            StringComparer.InvariantCultureIgnoreCase);

        var resultCount = 0L;

        var initialValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var columnCount = 0;

        var builder = new TextBuilder();

        // capture for performance
        var delimiter = Delimiter;
        var treatEmptyStringAsNull = TreatEmptyStringAsNull;
        var removeSurroundingDoubleQuotes = RemoveSurroundingDoubleQuotes;
        var ignoreColumns = IgnoreColumns?.ToHashSet();
        var skipLines = SkipLinesAtBeginning;

        var streams = StreamProvider.GetStreams(this);
        if (streams == null)
            yield break;

        var streamIndex = 0;
        foreach (var stream in streams)
        {
            if (stream == null)
                yield break;

            if (FlowState.IsTerminating)
                break;

            var firstRow = true;
            var columns = new List<MappedColumn>();
            if (ColumnNames != null)
            {
                foreach (var c in ColumnNames)
                {
                    var colName = c;
                    TextReaderDefaultColumn column = null;
                    if (columnMap != null)
                    {
                        if (columnMap.TryGetValue(c, out var col))
                        {
                            colName = col.rowColumn;
                            column = col.config;
                        }
                        else if (DefaultColumns != null)
                        {
                            column = DefaultColumns;
                        }
                        else
                        {
                            // skipped column
                            columns.Add(new MappedColumn()
                            {
                                NameInRow = c,
                            });

                            continue;
                        }
                    }
                    else
                    {
                        column = DefaultColumns;
                    }

                    columns.Add(new MappedColumn()
                    {
                        NameInRow = colName,
                        Column = column,
                    });
                }
            }

            //Console.WriteLine("new stream :" + stream.Stream.Length);

            StreamReader reader = null;
            try
            {
                const int bufferSize = 8192;
                var buffer = new char[bufferSize];
                var bufferPosition = 0;
                var bufferLength = 0;

                reader = new StreamReader(stream.Stream, Encoding, bufferSize: 1024);

                //var read = reader.ReadBlock(buffer, 0, bufferSize);
                var read = reader.ReadBlock(buffer.AsSpan());
                bufferLength = read;
                bufferPosition = 0;

                var quotes = 0;
                var builderLength = 0;
                var cellStartsWithQuote = false;

                var fileCompleted = false;
                var noMoreData = false;
                var lineCompleted = false;

                var hasColumnNames = ColumnNames != null;

                while (!fileCompleted)
                {
                    var remaining = bufferLength - bufferPosition;
                    if (remaining < 4 && !noMoreData)
                    {
                        if (remaining > 0)
                            Array.Copy(buffer, bufferPosition, buffer, 0, remaining);

                        //read = reader.ReadBlock(buffer, remaining, bufferSize - remaining);
                        read = reader.ReadBlock(buffer.AsSpan(remaining, bufferSize - remaining));
                        if (read == 0)
                            noMoreData = true;

                        bufferLength = remaining + read;
                        bufferPosition = 0;
                    }

                    lineCompleted = false;

                    if (bufferPosition < bufferLength)
                    {
                        var c = buffer[bufferPosition++];

                        var nc = bufferPosition < bufferLength
                            ? buffer[bufferPosition]
                            : '\0';

                        if (c is '\r' or '\n')
                        {
                            if (nc is '\r' or '\n')
                                bufferPosition++;

                            lineCompleted = true;
                        }
                        else
                        {
                            var isQuote = c == '\"';
                            var lastCharInLine = nc is '\r' or '\n';
                            //var nextCharIsQuote = /*!lastCharInLine && */nc == '\"';

                            if (builderLength == 0 && isQuote)
                                quotes++;

                            // quotedCellClosing
                            if (builderLength > 0 && isQuote && quotes > 0 && nc == delimiter)
                                quotes--;

                            // newLineInQuotedCell
                            if (builderLength > 0 && cellStartsWithQuote)
                            {
                                if (!isQuote && lastCharInLine)
                                {
                                    // add char
                                    builder.Append(c);

                                    // add newline
                                    builder.Append(nc);

                                    builderLength += 2;

                                    // skip newline
                                    bufferPosition++;

                                    // peek for a possible \n after \r
                                    if (bufferPosition < bufferLength)
                                    {
                                        var secondNewLineC = buffer[bufferPosition];
                                        if (secondNewLineC is '\r' or '\n')
                                        {
                                            builder.Append(secondNewLineC);
                                            builderLength++;

                                            bufferPosition++;
                                        }
                                    }
                                    //lastCharInLine = false;
                                    continue;
                                }
                                else if (isQuote && nc == '\"'/*nextCharIsQuote*/ && bufferPosition <= bufferLength - 3)
                                {
                                    var nnc = buffer[bufferPosition + 1];
                                    if (nnc is '\r' or '\n')
                                    {
                                        builder.Append(c);

                                        if (quotes > 0 && builderLength > 0)
                                        {
                                            // Skip next quote. RFC 4180, 2/7: If double-quotes are used to enclose fields, then a double-quote appearing inside a field must be escaped by preceding it with another double quote.
                                        }
                                        else
                                        {
                                            builder.Append(nc);
                                            builderLength++;
                                        }

                                        builder.Append(nnc);

                                        builderLength += 2;

                                        // skip quote
                                        bufferPosition++;

                                        // skip newline
                                        bufferPosition++;

                                        // peek for a possible \n after \r
                                        if (bufferPosition < bufferLength)
                                        {
                                            var secondNewLineC = buffer[bufferPosition];
                                            if (secondNewLineC is '\r' or '\n')
                                            {
                                                builder.Append(secondNewLineC);
                                                builderLength++;
                                                bufferPosition++;
                                            }
                                        }

                                        //lastCharInLine = false;
                                        continue;
                                    }
                                }
                            }

                            if (quotes > 0 || c != delimiter)
                            {
                                builder.Append(c);
                                if (builderLength == 0 && isQuote)
                                    cellStartsWithQuote = true;

                                if (quotes > 0 && isQuote && nc == '\"'/*nextCharIsQuote*/ && builderLength > 0)
                                {
                                    bufferPosition++;
                                    // Skip next quote. RFC 4180, 2/7: If double-quotes are used to enclose fields, then a double-quote appearing inside a field must be escaped by preceding it with another double quote.
                                }

                                builderLength++;
                            }

                            if (lastCharInLine || (quotes == 0 && c == delimiter))
                            {
                                if (builderLength > 0)
                                {
                                    // x
                                    NewMethod(columnMap, initialValues, columnCount, builder, removeSurroundingDoubleQuotes, columns, hasColumnNames);
                                    columnCount++;
                                    // x

                                    builder.Clear();
                                    builderLength = 0;
                                    cellStartsWithQuote = false;
                                }
                                else
                                {
                                    if (hasColumnNames)
                                    {
                                        if (columnCount < columns.Count)
                                        {
                                            var column = columns[columnCount];
                                            initialValues[column.NameInRow] = null;
                                        }
                                    }
                                    else
                                    {
                                        columns.Add(new MappedColumn()
                                        {
                                            NameInSource = string.Empty,
                                            NameInRow = string.Empty,
                                            Column = null,
                                        });
                                    }

                                    columnCount++;
                                }
                            }
                        }
                    }
                    else
                    {
                        fileCompleted = true;

                        if (builderLength > 0)
                        {
                            if (hasColumnNames)
                            {
                                NewMethod(columnMap, initialValues, columnCount, builder, removeSurroundingDoubleQuotes, columns, hasColumnNames);
                                columnCount++;
                            }

                            builder.Clear();
                            builderLength = 0;
                            cellStartsWithQuote = false;
                        }
                    }

                    if (lineCompleted || fileCompleted)
                    {
                        if (skipLines > 0)
                        {
                            skipLines--;
                            initialValues.Clear();
                            columnCount = 0;
                            //partList.Clear();
                            builder.Clear();
                            builderLength = 0;
                            quotes = 0;
                            cellStartsWithQuote = false;
                            continue;
                        }

                        if (!hasColumnNames)
                        {
                            hasColumnNames = columns.Count > 0;
                            if (hasColumnNames)
                            {
                                for (var i = 0; i < columns.Count - 1; i++)
                                {
                                    var column = columns[i];
                                    for (var j = i + 1; j < columns.Count; j++)
                                    {
                                        if (string.Equals(column.NameInSource, columns[j].NameInSource, StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            var exception = new DelimitedReadException(this, "delimited input contains more than one columns with the same name", stream);
                                            exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "delimited input contains more than one columns with the same name: {0}, {1}", stream.Name, column.NameInSource));
                                            exception.Data["Column"] = column.NameInSource;

                                            Context.RegisterIoCommandFailed(this, stream.IoCommandKind, stream.IoCommandUid, 0, exception);
                                            throw exception;
                                        }
                                    }
                                }
                            }
                        }

                        if (firstRow)
                        {
                            firstRow = false;

                            if (Header != DelimitedLineHeader.NoHeader)
                            {
                                initialValues.Clear();
                                columnCount = 0;
                                //partList.Clear();
                                builder.Clear();
                                builderLength = 0;
                                quotes = 0;
                                cellStartsWithQuote = false;
                                continue;
                            }
                        }

                        //partList.Clear();
                        builder.Clear();
                        builderLength = 0;
                        quotes = 0;
                        cellStartsWithQuote = false;

                        if (!string.IsNullOrEmpty(AddStreamIndexToColumn))
                            initialValues[AddStreamIndexToColumn] = streamIndex;

                        resultCount++;
                        yield return Context.CreateRow(this, initialValues);
                        initialValues.Clear();
                        columnCount = 0;

                        if (FlowState.IsTerminating)
                            break;
                    }
                }
            }
            finally
            {
                if (stream != null)
                {
                    Context.RegisterIoCommandSuccess(this, stream.IoCommandKind, stream.IoCommandUid, resultCount);
                    stream.Dispose();
                    reader?.Dispose();
                }
            }

            streamIndex++;
        }
    }

    private class MappedColumn
    {
        public string NameInSource { get; set; }
        public string NameInRow { get; set; }
        public TextReaderDefaultColumn Column { get; set; }
    }

    private void NewMethod(Dictionary<string, (string rowColumn, TextReaderColumn config)> columnMap, Dictionary<string, object> initialValues, int columnCount, TextBuilder builder, bool removeSurroundingDoubleQuotes, List<MappedColumn> columns, bool hasColumnNames)
    {
        if (removeSurroundingDoubleQuotes)
            builder.RemoveSurroundingDoubleQuotes();

        if (hasColumnNames)
        {
            if (columnCount >= columns.Count)
                return;

            var col = columns[columnCount];
            if (col.Column == null)
                return;

            try
            {
                initialValues[col.NameInRow] = col.Column.Process(this, builder);
            }
            catch (Exception ex)
            {
                initialValues[col.NameInRow] = new EtlRowError(this, builder.GetContentAsString(), ex);
            }
        }
        else
        {
            var originalName = builder.GetContentAsString();
            var colName = originalName;

            TextReaderDefaultColumn column;
            if (columnMap != null)
            {
                if (columnMap.TryGetValue(colName, out var col))
                {
                    colName = col.rowColumn;
                    column = col.config;
                }
                else if (DefaultColumns != null)
                {
                    column = DefaultColumns;
                }
                else
                {
                    // skipped c
                    columns.Add(new MappedColumn()
                    {
                        NameInSource = colName,
                        NameInRow = colName,
                        Column = null,
                    });

                    return;
                }
            }
            else
            {
                column = DefaultColumns;
            }

            columns.Add(new MappedColumn()
            {
                NameInSource = originalName,
                NameInRow = colName,
                Column = column,
            });
        }
    }
}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class DelimitedFileReaderFluent
{
    public static IFluentSequenceMutatorBuilder ReadDelimitedLines(this IFluentSequenceBuilder builder, DelimitedLineReader reader)
    {
        return builder.ReadFrom(reader);
    }
}
