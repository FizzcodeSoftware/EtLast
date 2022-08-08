﻿namespace FizzCode.EtLast;

public sealed class TableCopyConfiguration
{
    public string SourceTableName { get; init; }
    public string TargetTableName { get; init; }

    /// <summary>
    /// Optional. In case of NULL all columns will be copied to the target table.
    /// </summary>
    public Dictionary<string, string> Columns { get; init; }

    public override string ToString()
    {
        return SourceTableName + "->" + TargetTableName + (Columns != null
            ? ": " + string.Join(',', Columns.Select(x => x.Key + (x.Value != null && x.Key != x.Value ? "->" + x.Value : "")))
            : "");
    }
}