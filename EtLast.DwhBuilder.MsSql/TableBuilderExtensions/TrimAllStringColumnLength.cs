﻿namespace FizzCode.EtLast.DwhBuilder.MsSql
{
    using System.Collections.Generic;
    using System.Linq;
    using FizzCode.EtLast;

    public static partial class TableBuilderExtensions
    {
        public static DwhTableBuilder[] TrimAllStringColumnLength(this DwhTableBuilder[] builders)
        {
            foreach (var builder in builders)
            {
                builder.AddMutatorCreator(CreateTrimAllStringColumnLength);
            }

            return builders;
        }

        private static IEnumerable<IMutator> CreateTrimAllStringColumnLength(DwhTableBuilder builder)
        {
            var limitedLengthStringColumns = builder.Table.Columns
                .Where(x => x.GetLimitedStringLength() != null)
                .Select(x => new { column = x, length = x.GetLimitedStringLength().Value, })
                .ToList();

            if (limitedLengthStringColumns.Count == 0)
                yield break;

            yield return new CustomMutator(builder.ResilientTable.Topic, nameof(TrimAllStringColumnLength))
            {
                Then = (proc, row) =>
                {
                    foreach (var col in limitedLengthStringColumns)
                    {
                        var v = row[col.column.Name];
                        if (v == null)
                            continue;

                        if (!(v is string strv))
                            continue;

                        if (strv.Length > col.length)
                        {
                            var trimv = strv.Substring(0, col.length);
                            row.SetStagedValue(col.column.Name, trimv);

                            proc.Context.Log(LogSeverity.Warning, proc, "too long string trimmed on {ConnectionStringName}/{TableName}, column: {Column}, max length: {MaxLength}, original value: {Value}, trimmed value: {TrimValue}",
                                builder.DwhBuilder.ConnectionString.Name, builder.ResilientTable.TableName, col.column.Name, col.length, strv, trimv);
                        }
                    }

                    row.ApplyStaging();

                    return true;
                }
            };
        }
    }
}