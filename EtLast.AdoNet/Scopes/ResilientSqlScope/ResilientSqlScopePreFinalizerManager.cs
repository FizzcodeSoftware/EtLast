﻿namespace FizzCode.EtLast.AdoNet
{
    using System;
    using System.Diagnostics;
    using System.Linq;

    internal class ResilientSqlScopePreFinalizerManager : IProcess
    {
        public int InvocationUID { get; set; }
        public int InstanceUID { get; set; }
        public int InvocationCounter { get; set; }
        public IProcess Caller { get; set; }
        public Stopwatch LastInvocationStarted { get; set; }
        public DateTimeOffset? LastInvocationFinished { get; set; }

        private readonly ResilientSqlScope _scope;
        public IEtlContext Context => _scope.Context;
        public string Name { get; } = "PreFinalizerManager";
        public ITopic Topic => _scope.Topic;
        public ProcessKind Kind => ProcessKind.unknown;
        public StatCounterCollection CounterCollection { get; }

        public ResilientSqlScopePreFinalizerManager(ResilientSqlScope scope)
        {
            _scope = scope;
            CounterCollection = new StatCounterCollection(scope.Context.CounterCollection);
        }

        public void Execute()
        {
            Context.RegisterProcessInvocationStart(this, _scope);

            IExecutable[] finalizers;

            using (var creatorScope = Context.BeginScope(this, TransactionScopeKind.Suppress, LogSeverity.Information))
            {
                finalizers = _scope.Configuration.PreFinalizerCreator.Invoke(_scope, this)
                    ?.Where(x => x != null)
                    .ToArray();

                Context.Log(LogSeverity.Information, this, "created {PreFinalizerCount} pre-finalizers", finalizers?.Length ?? 0);
            }

            if (finalizers?.Length > 0)
            {
                Context.Log(LogSeverity.Information, this, "starting pre-finalizers");

                foreach (var finalizer in finalizers)
                {
                    var preExceptionCount = Context.ExceptionCount;
                    finalizer.Execute(this);
                    if (Context.ExceptionCount > preExceptionCount)
                    {
                        break;
                    }
                }
            }

            Context.RegisterProcessInvocationEnd(this);
        }
    }
}