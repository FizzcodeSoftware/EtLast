﻿namespace FizzCode.EtLast
{
    public class DebugLogRowsOperation : AbstractRowOperation
    {
        public RowTestDelegate If { get; set; }
        public LogSeverity Severity { get; set; }

        public override void Apply(IRow row)
        {
            if (If?.Invoke(row) == false)
                return;

            if (PrevOperation != null)
            {
                Process.Context.LogRow(Process, row, "by {PreviousOperation} ", PrevOperation.Name);
            }
            else
            {
                Process.Context.LogRow(Process, row, null);
            }
        }

        public override void Prepare()
        {
        }
    }
}