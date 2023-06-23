﻿namespace FizzCode.EtLast.Tests.Integration.Modules.FlowTests;

public class GetFiles : AbstractEtlTask
{
    public List<string> FileNames { get; private set; }

    public override void ValidateParameters()
    {
    }

    public override void Execute(IFlow flow)
    {
        flow
            .ContinueWith(() => new CustomJob(Context)
            {
                Action = job => FileNames = new() { "a.txt", "b.txt", "c.txt" },
            });
    }
}