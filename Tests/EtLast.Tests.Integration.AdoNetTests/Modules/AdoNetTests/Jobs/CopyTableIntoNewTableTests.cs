﻿namespace FizzCode.EtLast.Tests.Integration.Modules.AdoNetTests;

public class CopyTableIntoNewTableTests : AbstractEtlTask
{
    public NamedConnectionString ConnectionString { get; init; }

    public override void ValidateParameters()
    {
        if (ConnectionString == null)
            throw new ProcessParameterNullException(this, nameof(ConnectionString));
    }

    public override IEnumerable<IJob> CreateJobs()
    {
        yield return new CustomSqlStatement(Context)
        {
            Name = "Create source table",
            ConnectionString = ConnectionString,
            SqlStatement = $"CREATE TABLE {nameof(CopyTableIntoNewTableTests)} (Id INT NOT NULL, Value NVARCHAR(255));" +
                    $"INSERT INTO {nameof(CopyTableIntoNewTableTests)} (Id, Value) VALUES (1, 'etlast');" +
                    $"INSERT INTO {nameof(CopyTableIntoNewTableTests)} (Id, Value) VALUES (2, 'CopyTableIntoExistingTableTest');",
        };

        yield return new CopyTableIntoNewTable(Context)
        {
            ConnectionString = ConnectionString,
            Configuration = new TableCopyConfiguration()
            {
                SourceTableName = $"{nameof(CopyTableIntoNewTableTests)}",
                TargetTableName = $"{nameof(CopyTableIntoNewTableTests)}Target"
            }
        };

        yield return new CustomJob(Context)
        {
            Name = "Check target table contents",
            Action = job =>
            {
                var result = SequenceBuilder.Fluent
                .ReadFrom(new AdoNetDbReader(Context)
                {
                    Name = "Read target table contents",
                    ConnectionString = ConnectionString,
                    TableName = $"{nameof(CopyTableIntoNewTableTests)}Target"
                }).Build().TakeRowsAndReleaseOwnership(this).ToList();

                Assert.AreEqual(2, result.Count);
                Assert.That.ExactMatch(result, new List<CaseInsensitiveStringKeyDictionary<object>>()
                {
                    new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Value"] = "etlast" },
                    new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["Value"] = "CopyTableIntoExistingTableTest" }
                });
            }
        };
    }
}