﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;

    [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
    public abstract class AbstractAggregationMutator : AbstractEvaluable, IMutator
    {
        public IEvaluable InputProcess { get; set; }
        public RowTestDelegate If { get; init; }

        public List<ColumnCopyConfiguration> FixColumns { get; init; }
        public Func<IRow, string> KeyGenerator { get; init; }

        protected AbstractAggregationMutator(ITopic topic, string name)
            : base(topic, name)
        {
        }

        public IEnumerator<IMutator> GetEnumerator()
        {
            yield return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            yield return this;
        }
    }
}