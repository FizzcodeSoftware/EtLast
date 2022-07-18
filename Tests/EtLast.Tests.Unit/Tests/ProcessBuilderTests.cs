﻿namespace FizzCode.EtLast.Tests.Unit;

[TestClass]
public class ProcessBuilderTests
{
    [TestMethod]
    public void InputAndOneMutator()
    {
        var context = TestExecuter.GetContext();
        var builder = new ProcessBuilder()
        {
            InputJob = TestData.Person(context),
            Mutators = new MutatorList()
            {
                new CustomMutator(context)
                {
                    Action = row => true,
                },
            },
        };

        var process = builder.Build();
        Assert.IsNotNull(process);
        Assert.IsTrue(process is CustomMutator);
        Assert.IsNotNull((process as CustomMutator).Input);
    }

    [TestMethod]
    public void InputAndTwoMutators()
    {
        var context = TestExecuter.GetContext();
        var builder = new ProcessBuilder()
        {
            InputJob = TestData.Person(context),
            Mutators = new MutatorList()
            {
                new CustomMutator(context)
                {
                    Action = row => true,
                },
                new CustomMutator(context)
                {
                    Action = row => true,
                },
            },
        };

        var process = builder.Build();
        Assert.IsNotNull(process);
        Assert.IsTrue(process is CustomMutator);
        Assert.IsTrue((process as CustomMutator).Input is CustomMutator);
        Assert.IsNotNull(((process as CustomMutator).Input as CustomMutator).Input);
    }

    [TestMethod]
    public void OneMutator()
    {
        var context = TestExecuter.GetContext();
        var builder = new ProcessBuilder()
        {
            Mutators = new MutatorList()
            {
                new CustomMutator(context)
                {
                    Action = row => true,
                },
            },
        };

        var process = builder.Build();
        Assert.IsNotNull(process);
        Assert.IsTrue(process is CustomMutator);
        Assert.IsNull((process as CustomMutator).Input);
    }

    [TestMethod]
    public void InputOnly()
    {
        var context = TestExecuter.GetContext();
        var builder = new ProcessBuilder()
        {
            InputJob = TestData.Person(context),
            Mutators = new MutatorList(),
        };

        var process = builder.Build();
        Assert.IsNotNull(process);
        Assert.IsTrue(process is AbstractRowSource);
    }
}
