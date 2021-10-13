﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;

    public class MergeDateWithTimeMutator : AbstractMutator
    {
        public string TargetColumn { get; init; }
        public string SourceDateColumn { get; init; }
        public string SourceTimeColumn { get; init; }

        /// <summary>
        /// Default value is <see cref="InvalidValueAction.WrapError"/>
        /// </summary>
        public InvalidValueAction ActionIfInvalid { get; init; } = InvalidValueAction.WrapError;

        public object SpecialValueIfInvalid { get; init; }

        public MergeDateWithTimeMutator(ITopic topic, string name)
            : base(topic, name)
        {
        }

        protected override IEnumerable<IRow> MutateRow(IRow row)
        {
            var sourceDate = row[SourceDateColumn];
            var sourceTime = row[SourceTimeColumn];
            if (sourceDate is DateTime date && sourceTime != null)
            {
                if (sourceTime is DateTime dt)
                {
                    row.SetValue(TargetColumn, new DateTime(date.Year, date.Month, date.Day, dt.Hour, dt.Minute, dt.Second));
                    yield return row;
                    yield break;
                }
                else if (sourceTime is TimeSpan ts)
                {
                    row.SetValue(TargetColumn, new DateTime(date.Year, date.Month, date.Day, ts.Hours, ts.Minutes, ts.Seconds));
                    yield return row;
                    yield break;
                }
            }

            var removeRow = false;
            switch (ActionIfInvalid)
            {
                case InvalidValueAction.SetSpecialValue:
                    row.SetValue(TargetColumn, SpecialValueIfInvalid);
                    break;
                case InvalidValueAction.RemoveRow:
                    removeRow = true;
                    break;
                default:
                    var exception = new ProcessExecutionException(this, row, "invalid value found");
                    exception.Data.Add("SourceDate", sourceDate != null ? sourceDate.ToString() + " (" + sourceDate.GetType().GetFriendlyTypeName() + ")" : "NULL");
                    exception.Data.Add("SourceTime", sourceTime != null ? sourceTime.ToString() + " (" + sourceTime.GetType().GetFriendlyTypeName() + ")" : "NULL");
                    throw exception;
            }

            if (!removeRow)
                yield return row;
        }

        protected override void ValidateMutator()
        {
            if (string.IsNullOrEmpty(TargetColumn))
                throw new ProcessParameterNullException(this, nameof(TargetColumn));

            if (string.IsNullOrEmpty(SourceDateColumn))
                throw new ProcessParameterNullException(this, nameof(SourceDateColumn));

            if (string.IsNullOrEmpty(SourceTimeColumn))
                throw new ProcessParameterNullException(this, nameof(SourceTimeColumn));

            if (ActionIfInvalid != InvalidValueAction.SetSpecialValue && SpecialValueIfInvalid != null)
                throw new InvalidProcessParameterException(this, nameof(SpecialValueIfInvalid), SpecialValueIfInvalid, "value must be null if " + nameof(ActionIfInvalid) + " is not " + nameof(InvalidValueAction.SetSpecialValue));
        }
    }

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static class MergeDateWithTimeMutatorFluent
    {
        public static IFluentProcessMutatorBuilder MergeDateWithTime(this IFluentProcessMutatorBuilder builder, MergeDateWithTimeMutator mutator)
        {
            return builder.AddMutator(mutator);
        }
    }
}