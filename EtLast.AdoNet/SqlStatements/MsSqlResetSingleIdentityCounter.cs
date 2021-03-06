﻿namespace FizzCode.EtLast.AdoNet
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using FizzCode.LightWeight.AdoNet;

    public class MsSqlResetSingleIdentityCounter : AbstractSqlStatement
    {
        public string TableName { get; init; }
        public string IdentityColumnName { get; init; }

        public MsSqlResetSingleIdentityCounter(ITopic topic, string name)
            : base(topic, name)
        {
        }

        protected override void ValidateImpl()
        {
            base.ValidateImpl();

            if (TableName == null)
                throw new ProcessParameterNullException(this, nameof(TableName));

            if (IdentityColumnName == null)
                throw new ProcessParameterNullException(this, nameof(IdentityColumnName));

            if (ConnectionString.SqlEngine != SqlEngine.MsSql)
                throw new InvalidProcessParameterException(this, nameof(ConnectionString), ConnectionString.ProviderName, "provider name must be System.Data.SqlClient");
        }

        protected override string CreateSqlStatement(Dictionary<string, object> parameters)
        {
            return "declare @max int select @max=ISNULL(max(" + IdentityColumnName + "),0) from " + TableName + "; DBCC CHECKIDENT ('" + TableName + "', RESEED, @max);";
        }

        protected override void RunCommand(IDbCommand command, string transactionId, Dictionary<string, object> parameters)
        {
            var iocUid = Context.RegisterIoCommandStart(this, IoCommandKind.dbIdentityReset, ConnectionString.Name, TableName, command.CommandTimeout, command.CommandText, transactionId, () => parameters,
                "resetting identity counter on {ConnectionStringName}/{TableName}.{IdentityColumnName}",
                ConnectionString.Name, TableName, IdentityColumnName);

            try
            {
                command.ExecuteNonQuery();
                Context.RegisterIoCommandSuccess(this, IoCommandKind.dbIdentityReset, iocUid, null);
            }
            catch (Exception ex)
            {
                Context.RegisterIoCommandFailed(this, IoCommandKind.dbIdentityReset, iocUid, null, ex);

                var exception = new ProcessExecutionException(this, "identity counter reset failed", ex);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "identity counter reset failed, connection string key: {0}, message: {1}, command: {2}, timeout: {3}",
                    ConnectionString.Name, ex.Message, command.CommandText, command.CommandTimeout));

                exception.Data.Add("ConnectionStringName", ConnectionString.Name);
                exception.Data.Add("Table", TableName);
                exception.Data.Add("IdentityColumn", IdentityColumnName);
                exception.Data.Add("Statement", command.CommandText);
                exception.Data.Add("Timeout", command.CommandTimeout);
                exception.Data.Add("Elapsed", InvocationInfo.LastInvocationStarted.Elapsed);
                throw exception;
            }
        }
    }
}