﻿namespace FizzCode.EtLast;

public sealed class MergeToSqlMutator(IEtlContext context)
    : AbstractMutator(context), IRowSink
{
    [ProcessParameterMustHaveValue]
    public NamedConnectionString ConnectionString { get; init; }

    /// <summary>
    /// Default value is 3600.
    /// </summary>
    public int CommandTimeout { get; init; } = 60 * 60;

    /// <summary>
    /// Default value is 30.
    /// </summary>
    public int MaximumParameterCount { get; init; } = 30;

    [ProcessParameterMustHaveValue]
    public required string TableName { get; init; }

    [ProcessParameterMustHaveValue]
    public required DbColumn[] KeyColumns { get; set; }

    [ProcessParameterMustHaveValue]
    public required DbColumn[] ValueColumns { get; set; }

    [ProcessParameterMustHaveValue]
    public IMergeToSqlStatementCreator SqlStatementCreator { get; init; }

    /// <summary>
    /// Default value is 5000
    /// </summary>
    public int ForceWriteAfterNoDataMilliseconds { get; init; } = 5000;

    private DatabaseConnection _connection;
    private List<string> _statements;

    private long _rowsWritten;

    private IDbCommand _command;
    private long? _sinkUid;
    private Stopwatch _lastWrite;
    private bool _prepared = false;

    protected override void StartMutator()
    {
        _prepared = false;
        _rowsWritten = 0;
        _statements = [];
    }

    protected override void CloseMutator()
    {
        if (_command != null)
        {
            ExecuteStatements();
        }

        _statements = null;

        EtlConnectionManager.ReleaseConnection(this, ref _connection);
    }

    protected override void ProcessHeartBeatTag(HeartBeatTag tag)
    {
        if (_rowsWritten > 0 && _lastWrite != null && _command != null && _statements.Count > 0 && _lastWrite.ElapsedMilliseconds >= ForceWriteAfterNoDataMilliseconds)
        {
            lock (_connection.Lock)
            {
                ExecuteStatements();
            }
        }
    }

    protected override IEnumerable<IRow> MutateRow(IRow row, long rowInputIndex)
    {
        _sinkUid ??= Context.GetSinkUid(ConnectionString.Name, ConnectionString.Unescape(TableName));

        Context.RegisterWriteToSink(row, _sinkUid.Value);

        InitConnection();

        KeyColumns ??= row.Values.Select(x => new DbColumn(x.Key)).ToArray();

        if (!_prepared)
        {
            SqlStatementCreator.Prepare(this, TableName, KeyColumns, ValueColumns);
            _prepared = true;
        }

        lock (_connection.Lock)
        {
            if (_command == null)
            {
                _command = _connection.Connection.CreateCommand();
                _command.CommandTimeout = CommandTimeout;
            }

            var statement = SqlStatementCreator.CreateRowStatement(ConnectionString, row, this);
            _statements.Add(statement);

            if (_command.Parameters.Count >= MaximumParameterCount - 1)
            {
                ExecuteStatements();
            }
        }

        yield return row;
    }

    private void InitConnection()
    {
        if (_connection != null)
            return;

        try
        {
            _connection = EtlConnectionManager.GetConnection(ConnectionString, this);
        }
        catch (Exception ex)
        {
            var exception = new SqlConnectionException(this, ex);
            exception.Data["ConnectionStringName"] = ConnectionString.Name;
            throw exception;
        }
    }

    public int ParameterCount => _command?.Parameters.Count ?? 0;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public void CreateParameter(DbType? dbType, object value)
    {
        var parameter = _command.CreateParameter();
        parameter.ParameterName = "@" + _command.Parameters.Count.ToString("D", CultureInfo.InvariantCulture);

        parameter.SetValue(value, dbType);

        _command.Parameters.Add(parameter);
    }

    private void ExecuteStatements()
    {
        if (_lastWrite == null)
        {
            _lastWrite = new Stopwatch();
        }
        else
        {
            _lastWrite.Restart();
        }

        if (Transaction.Current == null)
            Context.Log(LogSeverity.Warning, this, "there is no active transaction!");

        var sqlStatement = SqlStatementCreator.CreateStatement(ConnectionString, _statements);
        var recordCount = _statements.Count;

        _command.CommandText = sqlStatement;

        var iocUid = Context.RegisterIoCommandStartWithPath(this, IoCommandKind.dbWriteBatch, ConnectionString.Name, ConnectionString.Unescape(TableName), _command.CommandTimeout, sqlStatement, Transaction.Current.ToIdentifierString(), null,
            "write to table", null);

        try
        {
            _command.ExecuteNonQuery();

            _rowsWritten += recordCount;

            Context.RegisterIoCommandSuccess(this, IoCommandKind.dbWriteBatch, iocUid, recordCount);
        }
        catch (Exception ex)
        {
            var exception = new SqlWriteException(this, ex);
            exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "db write failed, connection string key: {0}, table: {1}, message: {2}, statement: {3}",
                ConnectionString.Name, ConnectionString.Unescape(TableName), ex.Message, sqlStatement));
            exception.Data["ConnectionStringName"] = ConnectionString.Name;
            exception.Data["TableName"] = ConnectionString.Unescape(TableName);
            exception.Data["KeyColumns"] = string.Join(", ", KeyColumns.Select(x => x.RowColumn + " => " + ConnectionString.Unescape(x.NameInDatabase)));
            exception.Data["ValueColumns"] = string.Join(", ", ValueColumns.Select(x => x.RowColumn + " => " + ConnectionString.Unescape(x.NameInDatabase)));
            exception.Data["SqlStatement"] = sqlStatement;
            exception.Data["SqlStatementCompiled"] = _command.CompileSql();
            exception.Data["Timeout"] = CommandTimeout;
            exception.Data["SqlStatementCreator"] = SqlStatementCreator.GetType().GetFriendlyTypeName();
            exception.Data["TotalRowsWritten"] = _rowsWritten;

            Context.RegisterIoCommandFailed(this, IoCommandKind.dbWriteBatch, iocUid, recordCount, exception);
            throw exception;
        }

        _command = null;
        _statements.Clear();
    }

}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class MergeToSqlMutatorFluent
{
    /// <summary>
    /// Merge rows into a database table in batches, using statements generated by any implementation of <see cref="IMergeToSqlStatementCreator"/>.
    /// <para>Doesn't create or suppress any transaction scope.</para>
    /// <para>Doesn't support retrying the SQL operation and any failure will put the flow into a failed state.</para>
    /// <para>It is not recommended to use this mutator to access a remote SQL database.</para>
    /// </summary>
    public static IFluentSequenceMutatorBuilder MergeToSql(this IFluentSequenceMutatorBuilder builder, MergeToSqlMutator mutator)
    {
        return builder.AddMutator(mutator);
    }
}