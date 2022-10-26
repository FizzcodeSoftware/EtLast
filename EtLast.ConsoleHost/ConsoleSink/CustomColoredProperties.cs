﻿namespace FizzCode.EtLast.ConsoleHost.SerilogSink;

internal static class CustomColoredProperties
{
    internal static Dictionary<string, ColorCode> Map { get; } = new Dictionary<string, ColorCode>()
    {
        //["Module"] = ColorCode.Module,
        //["Plugin"] = ColorCode.Plugin,
        ["ActiveTopic"] = ColorCode.Topic,
        ["Caller"] = ColorCode.Process,
        ["Process"] = ColorCode.Process,
        ["ActiveProcess"] = ColorCode.Process,
        ["ProcessInvocationUid"] = ColorCode.Process,
        ["ActiveProcessInvocationUid"] = ColorCode.Process,
        ["Task"] = ColorCode.Task,
        ["ActiveTask"] = ColorCode.Task,
        ["TaskInvocationUid"] = ColorCode.Task,
        ["ActiveTaskInvocationUid"] = ColorCode.Task,
        ["Input"] = ColorCode.Process,
        ["Operation"] = ColorCode.Operation,
        ["Job"] = ColorCode.Job,
        ["Transaction"] = ColorCode.Transaction,
        ["ConnectionStringName"] = ColorCode.Location,
        ["TableName"] = ColorCode.Location,
        ["TableNames"] = ColorCode.Location,
        ["SchemaName"] = ColorCode.Location,
        ["SchemaNames"] = ColorCode.Location,
        ["SourceTableName"] = ColorCode.Location,
        ["TargetTableName"] = ColorCode.Location,
        ["FileName"] = ColorCode.Location,
        ["SourceFileName"] = ColorCode.Location,
        ["TargetFileName"] = ColorCode.Location,
        ["Folder"] = ColorCode.Location,
        ["Path"] = ColorCode.Location,
        ["SourcePath"] = ColorCode.Location,
        ["TargetPath"] = ColorCode.Location,
        ["Url"] = ColorCode.Location,
        ["SourceUrl"] = ColorCode.Location,
        ["TargetUrl"] = ColorCode.Location,
        ["Container"] = ColorCode.Location,
        ["SourceContainer"] = ColorCode.Location,
        ["TargetContainer"] = ColorCode.Location,
        ["Pattern"] = ColorCode.Location,
        ["SourcePattern"] = ColorCode.Location,
        ["TargetPattern"] = ColorCode.Location,
    };
}
