﻿namespace FizzCode.EtLast.AdoNet
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Transactions;

    public class MsSqlEnableConstraintCheckJob : AbstractSqlStatementsJob
    {
        public string[] TableNames { get; set; }

        protected override void Validate(IProcess process)
        {
            if (TableNames == null || TableNames.Length == 0)
                throw new JobParameterNullException(process, this, nameof(TableNames));
        }

        protected override List<string> CreateSqlStatements(IProcess process, ConnectionStringSettings settings)
        {
            return TableNames.Select(tableName => "ALTER TABLE " + tableName + " WITH CHECK CHECK CONSTRAINT ALL;").ToList();
        }

        protected override void RunCommand(IProcess process, IDbCommand command, int statementIndex, Stopwatch startedOn)
        {
            var tableName = TableNames[statementIndex];

            process.Context.Log(LogSeverity.Debug, process, "enable constraint check on {ConnectionStringKey}/{TableName} with SQL statement {SqlStatement}, timeout: {Timeout} sec, transaction: {Transaction}",
                ConnectionStringSettings.Name, Helpers.UnEscapeTableName(tableName), command.CommandText, command.CommandTimeout, Transaction.Current?.TransactionInformation.CreationTime.ToString("yyyy.MM.dd HH:mm:ss.ffff", CultureInfo.InvariantCulture) ?? "NULL");

            try
            {
                command.ExecuteNonQuery();
                process.Context.Log(LogSeverity.Debug, process, "constraint check on {ConnectionStringKey}/{TableName} is enabled in {Elapsed}",
                    ConnectionStringSettings.Name, Helpers.UnEscapeTableName(tableName), startedOn.Elapsed);
            }
            catch (Exception ex)
            {
                var exception = new JobExecutionException(process, this, "failed to enable constraint check", ex);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "failed to enable constraint check, connection string key: {0}, table: {1}, message: {2}, command: {3}, timeout: {4}",
                    ConnectionStringSettings.Name, tableName, ex.Message, command.CommandText, CommandTimeout));

                exception.Data.Add("ConnectionStringKey", ConnectionStringSettings.Name);
                exception.Data.Add("TableName", Helpers.UnEscapeTableName(tableName));
                exception.Data.Add("Statement", command.CommandText);
                exception.Data.Add("Timeout", CommandTimeout);
                exception.Data.Add("Elapsed", startedOn.Elapsed);
                throw exception;
            }
        }

        protected override void LogSucceeded(IProcess process, int lastSucceededIndex, Stopwatch startedOn)
        {
            if (lastSucceededIndex == -1)
                return;

            process.Context.Log(LogSeverity.Information, process, "constraint check successfully enabled on {ConnectionStringKey}/{TableNames}",
                ConnectionStringSettings.Name, startedOn.Elapsed,
                TableNames
                    .Take(lastSucceededIndex + 1)
                    .Select(Helpers.UnEscapeTableName)
                    .ToArray());
        }
    }
}