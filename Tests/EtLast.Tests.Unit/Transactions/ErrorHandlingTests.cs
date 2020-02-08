﻿namespace FizzCode.EtLast.Tests.Unit
{
    using System;
    using System.Linq;
    using FizzCode.EtLast.Tests.Base;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ErrorHandlingTests : AbstractBaseTestUsingSeed
    {
        [TestMethod]
        public void InvalidCastInOperation()
        {
            var context = new EtlContext();

            var process = CreateMutatorBuilder(1, context);
            process.Mutators.Add(new CustomMutator(context, null, null)
            {
                Then = (proc, row) =>
                {
                    var x = row.GetAs<int>("x");
                    return true;
                }
            });

            RunEtl(process);

            var exceptions = context.GetExceptions();
            Assert.IsTrue(exceptions.Any(ex => ex is ProcessExecutionException));
            Assert.IsTrue(exceptions.All(ex => ex is ProcessExecutionException));
            Assert.IsTrue(exceptions.All(ex => ex.InnerException is InvalidCastException));
        }

        [TestMethod]
        public void InvalidOperationInOperation()
        {
            var context = new EtlContext();

            var process = CreateMutatorBuilder(1, context);
            process.Mutators.Add(new CustomMutator(context, null, null)
            {
                Then = (proc, row) =>
                {
                    int? x = null;
                    var y = x.Value;
                    return true;
                }
            });

            RunEtl(process);

            var exceptions = context.GetExceptions();
            Assert.IsTrue(exceptions.Any(ex => ex is ProcessExecutionException));
            Assert.IsTrue(exceptions.All(ex => ex is ProcessExecutionException));
            Assert.IsTrue(exceptions.All(ex => ex.InnerException is InvalidOperationException));
        }

        [TestMethod]
        public void InvalidOperationParameter()
        {
            var context = new EtlContext();
            var process = CreateMutatorBuilder(1, context);
            process.Mutators.Add(new CustomMutator(context, null, null));

            RunEtl(process);

            var exceptions = context.GetExceptions();
            Assert.IsTrue(exceptions.Any(ex => ex is InvalidProcessParameterException));
            Assert.IsTrue(exceptions.All(ex => ex is InvalidProcessParameterException));
        }
    }
}