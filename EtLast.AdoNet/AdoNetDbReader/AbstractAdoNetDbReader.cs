﻿namespace FizzCode.EtLast.AdoNet
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Transactions;
    using FizzCode.LightWeight.AdoNet;

    public delegate void ConnectionCreatorDelegate(AbstractAdoNetDbReader process, out DatabaseConnection connection, out IDbTransaction transaction);

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class AbstractAdoNetDbReader : AbstractProducer, IRowReader
    {
        public NamedConnectionString ConnectionString { get; init; }

        public List<ReaderColumnConfiguration> ColumnConfiguration { get; init; }
        public ReaderDefaultColumnConfiguration DefaultColumnConfiguration { get; init; }

        /// <summary>
        /// If true, this process will execute out of ambient transaction scope. Default value is false.
        /// See <see cref="TransactionScopeOption.Suppress"/>.
        /// </summary>
        public bool SuppressExistingTransactionScope { get; init; }

        public ConnectionCreatorDelegate CustomConnectionCreator { get; init; }

        /// <summary>
        /// Default value is 3600.
        /// </summary>
        public int CommandTimeout { get; init; } = 3600;

        public DateTime LastDataRead { get; private set; }
        public List<ISqlValueProcessor> SqlValueProcessors { get; } = new List<ISqlValueProcessor>();

        public Dictionary<string, object> Parameters { get; init; }

        /// <summary>
        /// Some SQL connector implementations does not support passing arrays due to parameters (like MySQL).
        /// If set to true, then all int[], long[], List&lt;int&gt; and List&lt;long&gt; parameters will be converted to a comma separated list and inlined into the SQL statement right before execution.
        /// Default value is true.
        /// </summary>
        public bool InlineArrayParameters { get; init; } = true;

        protected AbstractAdoNetDbReader(ITopic topic, string name)
            : base(topic, name)
        {
            SqlValueProcessors.Add(new MySqlValueProcessor());
        }

        protected override void ValidateImpl()
        {
            if (ConnectionString == null)
                throw new ProcessParameterNullException(this, nameof(ConnectionString));
        }

        protected override IEnumerable<IRow> Produce()
        {
            var usedSqlValueProcessors = SqlValueProcessors.Where(x => x.Init(ConnectionString)).ToList();
            if (usedSqlValueProcessors.Count == 0)
                usedSqlValueProcessors = null;

            var sqlStatement = CreateSqlStatement();

            DatabaseConnection connection = null;
            IDbTransaction transaction = null;
            IDataReader reader = null;
            IDbCommand cmd = null;
            Stopwatch swQuery;

            var sqlStatementProcessed = InlineArrayParametersIfNecessary(sqlStatement);
            int iocUid;

            using (var scope = Context.BeginScope(this, SuppressExistingTransactionScope ? TransactionScopeKind.Suppress : TransactionScopeKind.None, LogSeverity.Debug))
            {
                if (CustomConnectionCreator != null)
                {
                    CustomConnectionCreator.Invoke(this, out connection, out transaction);
                }
                else
                {
                    connection = EtlConnectionManager.GetConnection(ConnectionString, this);
                }

                cmd = connection.Connection.CreateCommand();
                cmd.CommandTimeout = CommandTimeout;
                cmd.CommandText = sqlStatementProcessed;
                cmd.Transaction = transaction;
                cmd.FillCommandParameters(Parameters);

                var transactionId = (CustomConnectionCreator != null && cmd.Transaction != null)
                    ? "custom (" + cmd.Transaction.IsolationLevel.ToString() + ")"
                    : Transaction.Current.ToIdentifierString();

                iocUid = RegisterIoCommandStart(transactionId, cmd.CommandTimeout, sqlStatement);

                swQuery = Stopwatch.StartNew();
                try
                {
                    reader = cmd.ExecuteReader();
                }
                catch (Exception ex)
                {
                    Context.RegisterIoCommandFailed(this, IoCommandKind.dbRead, iocUid, null, ex);

                    var exception = new ProcessExecutionException(this, "error while executing query", ex);
                    exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "error while executing query, message: {0}, connection string key: {1}, SQL statement: {2}",
                        ex.Message, ConnectionString.Name, sqlStatement));
                    exception.Data.Add("ConnectionStringName", ConnectionString.Name);
                    exception.Data.Add("Statement", cmd.CommandText);
                    throw exception;
                }
            }

            LastDataRead = DateTime.Now;

            var resultCount = 0;
            if (reader != null && !Context.CancellationTokenSource.IsCancellationRequested)
            {
                var initialValues = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                var columnMap = ColumnConfiguration?.ToDictionary(x => x.SourceColumn);
                while (!Context.CancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {
                        if (!reader.Read())
                            break;
                    }
                    catch (Exception ex)
                    {
                        Context.RegisterIoCommandFailed(this, IoCommandKind.dbRead, iocUid, resultCount, ex);
                        var exception = new ProcessExecutionException(this, "error while executing query", ex);
                        exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "error while executing query after successfully reading {0} rows, message: {1}, connection string key: {2}, SQL statement: {3}",
                            resultCount, ex.Message, ConnectionString.Name, sqlStatement));
                        exception.Data.Add("ConnectionStringName", ConnectionString.Name);
                        exception.Data.Add("Statement", cmd.CommandText);
                        exception.Data.Add("RowIndex", resultCount);
                        exception.Data.Add("SecondsSinceLastRead", LastDataRead.Subtract(DateTime.Now).TotalSeconds.ToString(CultureInfo.InvariantCulture));
                        throw exception;
                    }

                    LastDataRead = DateTime.Now;

                    initialValues.Clear();
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var dbColumn = string.Intern(reader.GetName(i));
                        var rowColumn = dbColumn;

                        ReaderColumnConfiguration columnConfig = null;
                        if (columnMap != null && columnMap.TryGetValue(dbColumn, out columnConfig))
                        {
                            rowColumn = columnConfig.RowColumn ?? columnConfig.SourceColumn;
                        }

                        var config = columnConfig ?? DefaultColumnConfiguration;

                        var value = reader.GetValue(i);
                        if (value is DBNull)
                            value = null;

                        if (usedSqlValueProcessors != null)
                        {
                            foreach (var processor in usedSqlValueProcessors)
                            {
                                value = processor.ProcessValue(value, dbColumn);
                            }
                        }

                        if (config != null)
                        {
                            value = HandleConverter(value, config);
                        }

                        initialValues[rowColumn] = value;
                    }

                    resultCount++;
                    yield return Context.CreateRow(this, initialValues);
                }
            }

            Context.RegisterIoCommandSuccess(this, IoCommandKind.dbRead, iocUid, resultCount);

            if (reader != null)
            {
                try
                {
                    reader.Close();
                    reader.Dispose();
                }
                catch (Exception)
                {
                }

                reader = null;
            }

            if (cmd != null)
            {
                try
                {
                    cmd.Dispose();
                }
                catch (Exception)
                {
                }

                cmd = null;
            }

            if (CustomConnectionCreator == null)
            {
                EtlConnectionManager.ReleaseConnection(this, ref connection);
            }
        }

        private string InlineArrayParametersIfNecessary(string sqlStatement)
        {
            if (InlineArrayParameters && Parameters != null)
            {
                var parameters = Parameters.ToList();
                foreach (var kvp in parameters)
                {
                    var paramReference = "@" + kvp.Key;

                    var startIndex = 0;
                    while (startIndex < sqlStatement.Length - paramReference.Length) // handle multiple occurrences
                    {
                        var idx = sqlStatement.IndexOf(paramReference, startIndex, StringComparison.InvariantCultureIgnoreCase);
                        if (idx == -1)
                            break;

                        string newParamText = null;

                        if (kvp.Value is int[] intArray)
                        {
                            newParamText = string.Join(",", intArray.Select(x => x.ToString("D", CultureInfo.InvariantCulture)));
                        }
                        else if (kvp.Value is long[] longArray)
                        {
                            newParamText = string.Join(",", longArray.Select(x => x.ToString("D", CultureInfo.InvariantCulture)));
                        }
                        else if (kvp.Value is string[] stringArray)
                        {
                            var sb = new StringBuilder();
                            foreach (var s in stringArray)
                            {
                                if (sb.Length > 0)
                                    sb.Append(',');

                                sb.Append('\'');
                                sb.Append(s);
                                sb.Append('\'');
                            }

                            newParamText = sb.ToString();
                        }
                        else if (kvp.Value is List<int> intList)
                        {
                            newParamText = string.Join(",", intList.Select(x => x.ToString("D", CultureInfo.InvariantCulture)));
                        }
                        else if (kvp.Value is List<long> longList)
                        {
                            newParamText = string.Join(",", longList.Select(x => x.ToString("D", CultureInfo.InvariantCulture)));
                        }
                        else if (kvp.Value is List<string> stringList)
                        {
                            var sb = new StringBuilder();
                            foreach (var s in stringList)
                            {
                                if (sb.Length > 0)
                                    sb.Append(',');

                                sb.Append('\'');
                                sb.Append(s);
                                sb.Append('\'');
                            }

                            newParamText = sb.ToString();
                        }

                        if (newParamText != null)
                        {
                            sqlStatement = sqlStatement.Substring(0, idx) + newParamText + sqlStatement.Substring(idx + paramReference.Length);
                            startIndex = idx + newParamText.Length;

                            Parameters.Remove(kvp.Key);
                        }
                        else
                        {
                            startIndex += paramReference.Length;
                        }
                    }
                }
            }

            return sqlStatement;
        }

        protected abstract int RegisterIoCommandStart(string transactionId, int timeout, string statement);
        protected abstract string CreateSqlStatement();
    }
}