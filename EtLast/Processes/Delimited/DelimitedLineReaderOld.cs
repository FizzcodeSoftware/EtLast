﻿namespace FizzCode.EtLast;

public sealed class DelimitedLineReaderOld : AbstractRowSource
{
    public IStreamProvider StreamProvider { get; init; }

    public Dictionary<string, ReaderColumn> Columns { get; init; }
    public ReaderDefaultColumn DefaultColumns { get; init; }

    /// <summary>
    /// Default true.
    /// </summary>
    public bool TreatEmptyStringAsNull { get; init; } = true;

    /// <summary>
    /// Default true. If a value starts and ends with double quote (") characters, then both will be removed (this happens before type conversion)
    /// </summary>
    public bool RemoveSurroundingDoubleQuotes { get; init; } = true;

    /// <summary>
    /// Default <see cref="DelimitedLineHeader.NoHeader"/>.
    /// </summary>
    public DelimitedLineHeader Header { get; init; }

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
    public char Delimiter { get; init; } = ';';

    /// <summary>
    /// Default value is 0
    /// </summary>
    public int SkipLinesAtBeginning { get; init; }

    /// <summary>
    /// Default value is \r\n
    /// </summary>
    public string LineEnding { get; init; } = "\r\n";

    public DelimitedLineReaderOld(IEtlContext context)
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
        var columnMap = Columns?.ToDictionary(kvp => kvp.Value.SourceColumn ?? kvp.Key, kvp => (rowColumn: kvp.Key, config: kvp.Value), StringComparer.InvariantCultureIgnoreCase);

        var resultCount = 0;

        var initialValues = new Dictionary<string, object>();

        var partList = new List<string>(100);
        var builder = new StringBuilder(2000);

        // capture for performance
        var delimiter = Delimiter;
        var treatEmptyStringAsNull = TreatEmptyStringAsNull;
        var removeSurroundingDoubleQuotes = RemoveSurroundingDoubleQuotes;
        var ignoreColumns = IgnoreColumns?.ToHashSet();

        var skipLines = SkipLinesAtBeginning;

        var streams = StreamProvider.GetStreams(this);
        if (streams == null)
            yield break;

        foreach (var stream in streams)
        {
            if (stream == null)
                yield break;

            if (Pipe.IsTerminating)
                break;

            var firstRow = true;
            var columnNames = ColumnNames;

            StreamReader reader = null;
            try
            {
                reader = new StreamReader(stream.Stream);

                while (!Pipe.IsTerminating)
                {
                    var line = GetLine(stream, reader, resultCount);
                    if (line == null)
                        break;

                    if (line.EndsWith(delimiter))
                        line = line[0..^1];

                    if (string.IsNullOrEmpty(line))
                        continue;

                    partList.Clear();
                    builder.Clear();

                    var quotes = 0;
                    var builderLength = 0;
                    var cellStartsWithQuote = false;
                    var lineLength = line.Length;

                    for (var linePos = 0; linePos < lineLength; linePos++)
                    {
                        var c = line[linePos];
                        var isQuote = c == '\"';
                        var lastCharInLine = linePos == lineLength - 1;

                        var nextCharIsDelimiter = false;
                        var nextCharIsQuote = false;
                        if (!lastCharInLine)
                        {
                            var nc = line[linePos + 1];
                            if (nc == delimiter)
                                nextCharIsDelimiter = true;
                            else if (nc == '\"')
                                nextCharIsQuote = true;
                        }

                        if (builderLength == 0 && isQuote)
                            quotes++;

                        var quotedCellClosing = builderLength > 0
                                && isQuote
                                && quotes > 0
                                && nextCharIsDelimiter;

                        if (quotedCellClosing)
                            quotes--;

                        var endOfCell = lastCharInLine || (nextCharIsDelimiter && quotes == 0);

                        var newLineInQuotedCell = builderLength > 0
                            && cellStartsWithQuote
                            && (
                                (!isQuote && lastCharInLine)
                                || (isQuote && nextCharIsQuote && linePos == lineLength - 2)
                                );

                        if (newLineInQuotedCell)
                        {
                            var nextLine = GetLine(stream, reader, resultCount);
                            if (nextLine == null)
                                break;

                            if (nextLine.EndsWith(delimiter))
                                nextLine = nextLine[0..^1];

                            if (string.IsNullOrEmpty(nextLine))
                                continue;

                            line += LineEnding + nextLine;
                            lineLength = line.Length;
                            linePos--;
                            continue;
                        }

                        if (quotes > 0 || c != delimiter)
                        {
                            builder.Append(c);
                            if (builderLength == 0 && isQuote)
                                cellStartsWithQuote = true;

                            if (quotes > 0 && isQuote && nextCharIsQuote && builderLength > 0)
                            {
                                linePos++; // Skip next quote. RFC 4180, 2/7: If double-quotes are used to enclose fields, then a double-quote appearing inside a field must be escaped by preceding it with another double quote.
                            }

                            builderLength++;
                        }

                        if (lastCharInLine || (quotes == 0 && c == delimiter))
                        {
                            if (builderLength == 0)
                            {
                                partList.Add(string.Empty);
                            }
                            else
                            {
                                partList.Add(builder.ToString());

                                builder.Clear();
                                builderLength = 0;
                                cellStartsWithQuote = false;
                            }
                        }
                    }

                    if (skipLines > 0)
                    {
                        skipLines--;
                        continue;
                    }

                    if (firstRow)
                    {
                        firstRow = false;

                        if (Header != DelimitedLineHeader.NoHeader)
                        {
                            if (Header == DelimitedLineHeader.HasHeader)
                            {
                                columnNames = partList.ToArray();

                                if (removeSurroundingDoubleQuotes)
                                {
                                    for (var i = 0; i < columnNames.Length; i++)
                                    {
                                        var columnName = columnNames[i];
                                        if (columnName.Length > 1
                                            && columnName.StartsWith("\"", StringComparison.Ordinal)
                                            && columnName.EndsWith("\"", StringComparison.Ordinal))
                                        {
                                            columnNames[i] = columnName[1..^1];
                                        }
                                    }
                                }

                                for (var i = 0; i < columnNames.Length - 1; i++)
                                {
                                    var columnName = columnNames[i];
                                    for (var j = i + 1; j < columnNames.Length; j++)
                                    {
                                        if (string.Equals(columnName, columnNames[j], StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            var exception = new DelimitedReadException(this, "delimited input contains more than one columns with the same name", stream);
                                            exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "delimited input contains more than one columns with the same name: {0}, {1}", stream.Name, columnName));
                                            exception.Data["Column"] = columnName;

                                            Context.RegisterIoCommandFailed(this, stream.IoCommandKind, stream.IoCommandUid, 0, exception);
                                            throw exception;
                                        }
                                    }
                                }
                            }

                            continue;
                        }
                    }

                    initialValues.Clear();
                    var colCnt = Math.Min(columnNames.Length, partList.Count);
                    for (var i = 0; i < colCnt; i++)
                    {
                        var csvColumn = columnNames[i];
                        if (ignoreColumns?.Contains(csvColumn) == true)
                            continue;

                        var valueString = partList[i];

                        object value = valueString;

                        if (removeSurroundingDoubleQuotes
                           && valueString.Length > 1
                           && valueString.StartsWith("\"", StringComparison.Ordinal)
                           && valueString.EndsWith("\"", StringComparison.Ordinal))
                        {
                            value = valueString[1..^1];
                        }

                        if (value != null && treatEmptyStringAsNull && (value is string str) && string.IsNullOrEmpty(str))
                        {
                            value = null;
                        }

                        if (columnMap != null && columnMap.TryGetValue(csvColumn, out var col))
                        {
                            try
                            {
                                initialValues[col.rowColumn] = col.config.Process(this, value);
                            }
                            catch (Exception ex)
                            {
                                initialValues[col.rowColumn] = new EtlRowError(this, value, ex);
                            }
                        }
                        else if (DefaultColumns != null)
                        {
                            try
                            {
                                initialValues[csvColumn] = DefaultColumns.Process(this, value);
                            }
                            catch (Exception ex)
                            {
                                initialValues[csvColumn] = new EtlRowError(this, value, ex);
                            }
                        }
                    }

                    resultCount++;
                    yield return Context.CreateRow(this, initialValues);
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
        }
    }

    private string GetLine(NamedStream stream, StreamReader reader, int resultCount)
    {
        try
        {
            var line = reader.ReadLine();
            return line;
        }
        catch (Exception ex)
        {
            var exception = new DelimitedReadException(this, "error while reading delimited data from stream", stream, ex);
            exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "error while reading delimited data from stream: {0}, message: {1}", stream.Name, ex.Message));

            Context.RegisterIoCommandFailed(this, stream.IoCommandKind, stream.IoCommandUid, resultCount, exception);
            throw exception;
        }
    }
}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class DelimitedFileReaderOldFluent
{
    public static IFluentSequenceMutatorBuilder ReadDelimitedLinesOld(this IFluentSequenceBuilder builder, DelimitedLineReaderOld reader)
    {
        return builder.ReadFrom(reader);
    }
}