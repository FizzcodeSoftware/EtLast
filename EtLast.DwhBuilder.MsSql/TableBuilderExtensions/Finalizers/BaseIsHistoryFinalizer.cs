﻿namespace FizzCode.EtLast.DwhBuilder.MsSql;

public static partial class TableBuilderExtensions
{
    public static DwhTableBuilder[] BaseIsHistoryFinalizer(this DwhTableBuilder[] builders, Action<KeyBasedFinalizerBuilder> customizer)
    {
        foreach (var tableBuilder in builders)
        {
            var tempBuilder = new KeyBasedFinalizerBuilder(tableBuilder);
            customizer.Invoke(tempBuilder);

            if (tempBuilder.MatchColumns == null)
                throw new NotSupportedException("you must specify the key columns of " + nameof(BaseIsHistoryFinalizer) + " for table " + tableBuilder.ResilientTable.TableName);

            tableBuilder.AddFinalizerCreator(_ => CreateBaseIsHistoryFinalizer(tempBuilder));
        }

        return builders;
    }

    private static IEnumerable<IProcess> CreateBaseIsHistoryFinalizer(KeyBasedFinalizerBuilder builder)
    {
        var mergeIntoBaseColumns = builder.TableBuilder.Table.Columns
            .Where(x => !x.GetUsedByEtlRunInfo());

        if (builder.TableBuilder.Table.AnyPrimaryKeyColumnIsIdentity)
        {
            mergeIntoBaseColumns = mergeIntoBaseColumns
                .Where(x => !x.IsPrimaryKey);
        }

        var mergeIntoBaseColumnNames = mergeIntoBaseColumns
            .Select(c => c.NameEscaped(builder.TableBuilder.DwhBuilder.ConnectionString))
            .ToArray();

        var columnNamesToMatch = builder.MatchColumns
            .Select(c => c.NameEscaped(builder.TableBuilder.DwhBuilder.ConnectionString))
            .ToArray();

        var parameters = new Dictionary<string, object>();
        if (builder.TableBuilder.HasEtlRunInfo)
            parameters.Add("EtlRunId", builder.TableBuilder.DwhBuilder.EtlRunId.Value);

        if (builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime != null)
            parameters.Add("InfiniteFuture", builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime);

        // todo: support NoHistoryColumnProperty

        yield return new CustomMsSqlMergeStatement(builder.TableBuilder.ResilientTable.Scope.Context)
        {
            Name = "CloseOpenEndedBaseRecords",
            ConnectionString = builder.TableBuilder.ResilientTable.Scope.ConnectionString,
            CommandTimeout = 60 * 60,
            SourceTableName = builder.TableBuilder.ResilientTable.TempTableName,
            TargetTableName = builder.TableBuilder.ResilientTable.TableName,
            SourceTableAlias = "s",
            TargetTableAlias = "t",
            OnCondition = string.Join(" and ", columnNamesToMatch.Select(x => "((s." + x + "=t." + x + ") or (s." + x + " is null and t." + x + " is null))"))
                + " and t." + builder.TableBuilder.ValidToColumnNameEscaped + (builder.TableBuilder.DwhBuilder.Configuration.InfiniteFutureDateTime == null ? " IS NULL" : "=@InfiniteFuture"),
            WhenMatchedAction = "UPDATE SET t."
                + builder.TableBuilder.ValidToColumnNameEscaped + "=s." + builder.TableBuilder.ValidFromColumnNameEscaped
                + (builder.TableBuilder.HasEtlRunInfo
                    ? ", " + builder.TableBuilder.EtlRunUpdateColumnNameEscaped + "=@EtlRunId, " + builder.TableBuilder.EtlRunToColumnNameEscaped + "=@EtlRunId"
                    : ""),
            Parameters = parameters,
        };

        var columnDefaults = new Dictionary<string, object>();
        if (builder.TableBuilder.HasEtlRunInfo)
        {
            columnDefaults[builder.TableBuilder.EtlRunInsertColumnNameEscaped] = builder.TableBuilder.DwhBuilder.EtlRunId.Value;
            columnDefaults[builder.TableBuilder.EtlRunUpdateColumnNameEscaped] = builder.TableBuilder.DwhBuilder.EtlRunId.Value;
            columnDefaults[builder.TableBuilder.EtlRunFromColumnNameEscaped] = builder.TableBuilder.DwhBuilder.EtlRunId.Value;
        }

        yield return new CopyTableIntoExistingTable(builder.TableBuilder.ResilientTable.Scope.Context)
        {
            Name = "CopyToBase",
            ConnectionString = builder.TableBuilder.ResilientTable.Scope.ConnectionString,
            Configuration = new TableCopyConfiguration()
            {
                SourceTableName = builder.TableBuilder.ResilientTable.TempTableName,
                TargetTableName = builder.TableBuilder.ResilientTable.TableName,
                Columns = mergeIntoBaseColumnNames.ToDictionary(x => x),
            },
            ColumnDefaults = columnDefaults,
            CommandTimeout = 60 * 60,
        };
    }
}
