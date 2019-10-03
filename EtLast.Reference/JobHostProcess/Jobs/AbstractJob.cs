﻿namespace FizzCode.EtLast
{
    using System.Threading;

    public abstract class AbstractJob : IJob
    {
        public string Name { get; set; }
        public IfJobDelegate If { get; set; }

        protected AbstractJob()
        {
            Name = TypeHelpers.GetFriendlyTypeName(GetType());
        }

        public abstract void Execute(IProcess process, CancellationTokenSource cancellationTokenSource);
    }
}