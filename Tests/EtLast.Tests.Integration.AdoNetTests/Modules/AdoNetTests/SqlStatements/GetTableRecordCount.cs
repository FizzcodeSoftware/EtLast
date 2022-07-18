﻿namespace FizzCode.EtLast.Tests.Integration.Modules.AdoNetTests;

public class GetTableRecordCount : AbstractEtlTask
{
    public NamedConnectionString ConnectionString { get; init; }
    public string DatabaseName { get; init; }

    public override void ValidateParameters()
    {
        if (ConnectionString == null)
            throw new ProcessParameterNullException(this, nameof(ConnectionString));

        if (DatabaseName == null)
            throw new ProcessParameterNullException(this, nameof(DatabaseName));
    }

    public override IEnumerable<IExecutable> CreateProcesses()
    {
        yield return new CustomSqlStatement(Context)
        {
            ConnectionString = ConnectionString,
            SqlStatement = $"CREATE TABLE {nameof(GetTableRecordCount)} (Id INT NOT NULL, DateTimeValue DATETIME2);" +
                    $"INSERT INTO {nameof(GetTableRecordCount)} (Id, DateTimeValue) VALUES (1, '2022.07.08');" +
                    $"INSERT INTO {nameof(GetTableRecordCount)} (Id, DateTimeValue) VALUES (1, '2022.07.09');",
        };
        
        yield return new CustomAction(Context)
        {
            Name = nameof(GetTableRecordCount),
            Action = proc =>
            {
                var result = new EtLast.GetTableRecordCount(Context)
                {
                    ConnectionString = ConnectionString,
                    TableName = ConnectionString.Escape(nameof(GetTableRecordCount)),
                }.Execute();

                Assert.AreEqual(2, result);
            }
        };
    }
}