﻿namespace FizzCode.EtLast
{
    using System.Collections.Generic;

    public class RemoveColumnsMutator : AbstractEvaluableProcess, IMutator
    {
        public IEvaluable InputProcess { get; set; }
        public RowTestDelegate If { get; set; }
        public string[] Columns { get; set; }

        public RemoveColumnsMutator(IEtlContext context, string name, string topic)
            : base(context, name, topic)
        {
        }

        protected override IEnumerable<IRow> EvaluateImpl()
        {
            var rows = InputProcess.Evaluate().TakeRowsAndTransferOwnership(this);
            foreach (var row in rows)
            {
                if (If?.Invoke(row) == false)
                {
                    yield return row;
                    continue;
                }

                foreach (var column in Columns)
                {
                    row.SetValue(column, null, this);
                }

                yield return row;
            }
        }

        protected override void ValidateImpl()
        {
            if (InputProcess == null)
                throw new ProcessParameterNullException(this, nameof(InputProcess));

            if (Columns == null || Columns.Length == 0)
                throw new ProcessParameterNullException(this, nameof(Columns));
        }
    }
}