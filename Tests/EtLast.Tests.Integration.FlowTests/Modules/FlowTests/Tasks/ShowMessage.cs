﻿namespace FizzCode.EtLast.Tests.Integration.Modules.FlowTests;

public class ShowMessage : AbstractEtlTask
{
    [ProcessParameterNullException]
    public string Message { get; set; }

    public override void Execute(IFlow flow)
    {
        flow
            .CustomJob("ShowMessageJob", job =>
            {
                Context.Log(LogSeverity.Warning, job, Message);
            });
    }
}