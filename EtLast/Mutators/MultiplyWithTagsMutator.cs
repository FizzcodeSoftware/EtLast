﻿namespace FizzCode.EtLast
{
    using System.Collections.Generic;
    using System.ComponentModel;

    public sealed class MultiplyWithTagsMutator : AbstractMutator
    {
        /// <summary>
        /// Default true.
        /// </summary>
        public bool RemoveOriginalRow { get; init; } = true;

        public object[] Tags { get; init; }

        public MultiplyWithTagsMutator(IEtlContext context, string topic, string name)
            : base(context, topic, name)
        {
        }

        protected override IEnumerable<IRow> MutateRow(IRow row)
        {
            if (!RemoveOriginalRow)
                yield return row;

            foreach (var tag in Tags)
            {
                var newRow = Context.CreateRow(this, row);
                newRow.Tag = tag;
                yield return newRow;
            }
        }

        protected override void ValidateMutator()
        {
            if (Tags == null || Tags.Length == 0)
                throw new ProcessParameterNullException(this, nameof(Tags));
        }
    }

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public static class MultiplyWithTagsMutatorFluent
    {
        public static IFluentProcessMutatorBuilder MultiplyWithTags(this IFluentProcessMutatorBuilder builder, MultiplyWithTagsMutator mutator)
        {
            return builder.AddMutator(mutator);
        }
    }
}