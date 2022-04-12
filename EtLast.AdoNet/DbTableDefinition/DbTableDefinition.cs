﻿namespace FizzCode.EtLast;

public sealed class DbTableDefinition
{
    public string TableName { get; init; }

    /// <summary>
    /// Key is column in the row, value is column in the database table (can be null).
    /// </summary>
    public Dictionary<string, string> Columns { get; init; }

    public override string ToString()
    {
        return TableName + ": " + string.Join(',', Columns.Select(x => x.Key + "->" + x.Value));
    }
}