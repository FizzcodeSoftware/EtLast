﻿namespace FizzCode.EtLast.Tests.Integration.Modules.AdoNetTests;

public class Exception : AbstractEtlTask
{
    public NamedConnectionString ConnectionString { get; init; }
    public override void ValidateParameters()
    {
        if (ConnectionString == null)
            throw new ProcessParameterNullException(this, nameof(ConnectionString));
    }

    public override void Execute(IFlow flow)
    {
        flow
            .CustomJob(nameof(Exception), job =>
            {
                throw new System.Exception("Test Exception.");
            });
    }
}
