﻿namespace FizzCode.EtLast.DwhBuilder.Alpha
{
    using System;

    public static partial class TableBuilderExtensions
    {
        public static DwhTableBuilder[] SetValidFromToRecordTimestampIfAvailable(this DwhTableBuilder[] builders)
        {
            foreach (var builder in builders)
            {
                if (string.IsNullOrEmpty(builder.ValidFromColumnName))
                    continue;

                if (builder.RecordTimestampIndicatorColumn == null)
                    continue;

                builder.AddMutatorCreator(builder => new[]
                {
                    new CustomMutator(builder.DwhBuilder.Context, nameof(SetValidFromToRecordTimestampIfAvailable), builder.Topic)
                    {
                        Then = (proc, row) =>
                        {
                            var value = row[builder.RecordTimestampIndicatorColumn.Name];
                            if (value != null)
                            {
                                if (value is DateTime dt)
                                {
                                    value = (DateTimeOffset)dt;
                                }

                                if (value is DateTimeOffset)
                                {
                                    row.SetValue(builder.ValidFromColumnName, value, proc);
                                }
                                else
                                {
                                    proc.Context.Log(LogSeverity.Warning, proc, "record timestamp is not DateTimeOffset in: {Row}", row.ToDebugString());
                                }
                            }

                            return true;
                        },
                    },
                });
            }

            return builders;
        }
    }
}