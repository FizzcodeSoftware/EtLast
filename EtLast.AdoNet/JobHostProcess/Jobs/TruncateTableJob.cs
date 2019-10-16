﻿namespace FizzCode.EtLast.AdoNet
{
    using System;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.Transactions;
    using FizzCode.DbTools.Configuration;

    public class TruncateTableJob : AbstractSqlStatementJob
    {
        public string TableName { get; set; }

        protected override void Validate(IProcess process)
        {
            if (string.IsNullOrEmpty(TableName))
                throw new JobParameterNullException(process, this, nameof(TableName));
        }

        protected override string CreateSqlStatement(IProcess process, ConnectionStringWithProvider connectionString)
        {
            return "TRUNCATE TABLE " + TableName;
        }

        protected override void RunCommand(IProcess process, IDbCommand command, Stopwatch startedOn)
        {
            process.Context.Log(LogSeverity.Debug, process, "({Job}) truncating {ConnectionStringKey}/{TableName} with SQL statement {SqlStatement}, timeout: {Timeout} sec, transaction: {Transaction}",
                Name, ConnectionString.Name, Helpers.UnEscapeTableName(TableName), command.CommandText, command.CommandTimeout, Transaction.Current.ToIdentifierString());

            var originalStatement = command.CommandText;

            try
            {
                command.CommandText = "SELECT COUNT(*) FROM " + TableName;
                var recordCount = command.ExecuteScalar();

                command.CommandText = originalStatement;
                command.ExecuteNonQuery();
                process.Context.Log(LogSeverity.Information, process, "({Job}) {RecordCount} records deleted in {ConnectionStringKey}/{TableName} in {Elapsed}",
                    Name, recordCount, ConnectionString.Name, TableName, startedOn.Elapsed);
            }
            catch (Exception ex)
            {
                var exception = new JobExecutionException(process, this, "database table truncate failed", ex);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "database table truncate failed, connection string key: {0}, table: {1}, message: {2}, command: {3}, timeout: {4}",
                    ConnectionString.Name, Helpers.UnEscapeTableName(TableName), ex.Message, originalStatement, CommandTimeout));

                exception.Data.Add("ConnectionStringKey", ConnectionString.Name);
                exception.Data.Add("TableName", Helpers.UnEscapeTableName(TableName));
                exception.Data.Add("Statement", originalStatement);
                exception.Data.Add("Timeout", CommandTimeout);
                exception.Data.Add("Elapsed", startedOn.Elapsed);
                throw exception;
            }
        }
    }
}