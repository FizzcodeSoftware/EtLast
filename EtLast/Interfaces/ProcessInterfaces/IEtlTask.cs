﻿namespace FizzCode.EtLast;

public interface IEtlTask : IProcess
{
    public IExecutionStatistics Statistics { get; }
    public Dictionary<IoCommandKind, IoCommandCounter> IoCommandCounters { get; }

    public void ValidateParameters();
}