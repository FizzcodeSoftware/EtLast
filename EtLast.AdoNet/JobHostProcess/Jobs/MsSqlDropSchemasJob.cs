﻿namespace FizzCode.EtLast.AdoNet
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Transactions;
    using FizzCode.DbTools.Configuration;

    public class MsSqlDropSchemasJob : AbstractSqlStatementsJob
    {
        public string[] SchemaNames { get; set; }

        protected override void Validate()
        {
            if (SchemaNames == null || SchemaNames.Length == 0)
                throw new JobParameterNullException(Process, this, nameof(SchemaNames));

            var knownProvider = Process.Context.GetConnectionString(ConnectionStringKey)?.KnownProvider;
            if (knownProvider != KnownProvider.SqlServer)
                throw new InvalidJobParameterException(Process, this, nameof(ConnectionString), nameof(ConnectionString.ProviderName), "provider name must be System.Data.SqlClient");
        }

        protected override List<string> CreateSqlStatements(ConnectionStringWithProvider connectionString, IDbConnection connection)
        {
            return SchemaNames
                .Select(schemaName => "DROP SCHEMA IF EXISTS " + schemaName + ";")
                .ToList();
        }

        protected override void RunCommand(IDbCommand command, int statementIndex, Stopwatch startedOn)
        {
            var schemaName = SchemaNames[statementIndex];

            Process.Context.Log(LogSeverity.Debug, Process, this, null, "drop schema {ConnectionStringKey}/{SchemaName} with SQL statement {SqlStatement}, timeout: {Timeout} sec, transaction: {Transaction}",
                ConnectionString.Name, ConnectionString.Unescape(schemaName), command.CommandText, command.CommandTimeout, Transaction.Current.ToIdentifierString());

            try
            {
                command.ExecuteNonQuery();

                Process.Context.Log(LogSeverity.Debug, Process, this, null, "schema {ConnectionStringKey}/{SchemaName} is dropped in {Elapsed}, transaction: {Transaction}",
                    ConnectionString.Name, ConnectionString.Unescape(schemaName), startedOn.Elapsed, Transaction.Current.ToIdentifierString());

                Process.Context.Stat.IncrementCounter("database schemas dropped / " + ConnectionString.Name, 1);
                Process.Context.Stat.IncrementCounter("database schemas dropped time / " + ConnectionString.Name, startedOn.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                var exception = new JobExecutionException(Process, this, "failed to drop schema", ex);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "failed to drop schema, connection string key: {0}, schema: {1}, message: {2}, command: {3}, timeout: {4}",
                    ConnectionString.Name, ConnectionString.Unescape(schemaName), ex.Message, command.CommandText, command.CommandTimeout));

                exception.Data.Add("ConnectionStringKey", ConnectionString.Name);
                exception.Data.Add("SchemaName", ConnectionString.Unescape(schemaName));
                exception.Data.Add("Statement", command.CommandText);
                exception.Data.Add("Timeout", command.CommandTimeout);
                exception.Data.Add("Elapsed", startedOn.Elapsed);
                throw exception;
            }
        }

        protected override void LogSucceeded(int lastSucceededIndex, Stopwatch startedOn)
        {
            if (lastSucceededIndex == -1)
                return;

            Process.Context.Log(LogSeverity.Information, Process, this, null, "{SchemaCount} schema(s) successfully dropped on {ConnectionStringKey} in {Elapsed}, transaction: {Transaction}",
                 lastSucceededIndex + 1, ConnectionString.Name, startedOn.Elapsed, Transaction.Current.ToIdentifierString());
        }
    }
}