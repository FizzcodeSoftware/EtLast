﻿namespace FizzCode.EtLast
{
    using System.Collections.Generic;

    internal class FluentProcessMutatorBuilder : IFluentProcessMutatorBuilder
    {
        public IFluentProcessBuilder ProcessBuilder { get; }

        internal FluentProcessMutatorBuilder(IFluentProcessBuilder parent)
        {
            ProcessBuilder = parent;
        }

        public IFluentProcessMutatorBuilder AddMutator(IMutator mutator)
        {
            mutator.InputProcess = ProcessBuilder.Result;
            ProcessBuilder.Result = mutator;
            return this;
        }

        public IFluentProcessMutatorBuilder AddMutators(IEnumerable<IMutator> mutators)
        {
            foreach (var mutator in mutators)
            {
                mutator.InputProcess = ProcessBuilder.Result;
                ProcessBuilder.Result = mutator;
            }

            return this;
        }

        public IEvaluable Build()
        {
            return ProcessBuilder.Build();
        }
    }
}