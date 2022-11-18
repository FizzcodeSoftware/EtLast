﻿namespace FizzCode.EtLast;

public sealed class AdoNetDbReaderOld : AbstractAdoNetDbReaderOld
{
    public string TableName { get; init; }
    public string CustomWhereClause { get; init; }
    public string CustomOrderByClause { get; init; }
    public int RecordCountLimit { get; init; }

    public AdoNetDbReaderOld(IEtlContext context)
        : base(context)
    {
    }

    protected override CommandType GetCommandType()
    {
        return CommandType.Text;
    }

    public override string GetTopic()
    {
        return TableName != null
            ? ConnectionString?.Unescape(TableName)
            : null;
    }

    protected override void ValidateImpl()
    {
        base.ValidateImpl();

        if (TableName == null)
            throw new ProcessParameterNullException(this, nameof(TableName));
    }

    protected override string CreateSqlStatement()
    {
        var columnList = "*";
        if (Columns?.Count > 0)
        {
            columnList = string.Join(", ", Columns.Select(x => ConnectionString.Escape(x.Value?.SourceColumn ?? x.Key)));
        }

        var prefix = "";
        var postfix = "";

        if (!string.IsNullOrEmpty(CustomWhereClause))
        {
            postfix += (string.IsNullOrEmpty(postfix) ? "" : " ") + "WHERE " + CustomWhereClause;
        }

        if (!string.IsNullOrEmpty(CustomOrderByClause))
        {
            postfix += (string.IsNullOrEmpty(postfix) ? "" : " ") + "ORDER BY " + CustomOrderByClause;
        }

        if (RecordCountLimit > 0)
        {
            if (ConnectionString.SqlEngine == SqlEngine.MySql)
            {
                postfix += (string.IsNullOrEmpty(postfix) ? "" : " ") + "LIMIT " + RecordCountLimit.ToString("D", CultureInfo.InvariantCulture);
            }
            else
            {
                prefix = "TOP " + RecordCountLimit.ToString("D", CultureInfo.InvariantCulture);
            }
            // todo: support Oracle Syntax: https://www.w3schools.com/sql/sql_top.asp
        }

        return "SELECT "
            + (!string.IsNullOrEmpty(prefix) ? prefix + " " : "")
            + columnList
            + " FROM "
            + TableName
            + (!string.IsNullOrEmpty(postfix) ? " " + postfix : "");
    }

    protected override int RegisterIoCommandStart(string transactionId, int timeout, string statement)
    {
        return Context.RegisterIoCommandStart(this, IoCommandKind.dbRead, ConnectionString.Name, ConnectionString.Unescape(TableName), timeout, statement, transactionId, () => Parameters,
            "querying from {ConnectionStringName}/{TableName}",
            ConnectionString.Name, ConnectionString.Unescape(TableName));
    }
}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class AdoNetDbReaderOldFluent
{
    public static IFluentSequenceMutatorBuilder ReadFromSqlOld(this IFluentSequenceBuilder builder, AdoNetDbReaderOld reader)
    {
        return builder.ReadFrom(reader);
    }
}