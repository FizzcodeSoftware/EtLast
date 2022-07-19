﻿namespace FizzCode.EtLast.Tests.Unit.Producers;

[TestClass]
public class EnumerableImporterTests
{
    [TestMethod]
    public void FullCopy()
    {
        var context = TestExecuter.GetContext();
        var builder = SequenceBuilder.Fluent
        .ImportEnumerable(new EnumerableImporter(context)
        {
            InputGenerator = caller => TestData.Person(context)
                .Evaluate(caller)
                .TakeRowsAndReleaseOwnership(),
        });

        var result = TestExecuter.Execute(builder);
        Assert.AreEqual(7, result.MutatedRows.Count);
        Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 0, ["name"] = "A", ["age"] = 17, ["height"] = 160, ["eyeColor"] = "brown", ["countryId"] = 1, ["birthDate"] = new DateTime(2010, 12, 9, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 12, 19, 12, 0, 1, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 1, ["name"] = "B", ["age"] = 8, ["height"] = 190, ["countryId"] = 1, ["birthDate"] = new DateTime(2011, 2, 1, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 12, 19, 13, 2, 0, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 2, ["name"] = "C", ["age"] = 27, ["height"] = 170, ["eyeColor"] = "green", ["countryId"] = 2, ["birthDate"] = new DateTime(2014, 1, 21, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 11, 21, 17, 11, 58, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 3, ["name"] = "D", ["age"] = 39, ["height"] = 160, ["eyeColor"] = "fake", ["birthDate"] = "2018.07.11", ["lastChangedTime"] = new DateTime(2017, 8, 1, 4, 9, 1, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 4, ["name"] = "E", ["age"] = -3, ["height"] = 160, ["countryId"] = 1, ["lastChangedTime"] = new DateTime(2019, 1, 1, 23, 59, 59, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 5, ["name"] = "A", ["age"] = 11, ["height"] = 140, ["birthDate"] = new DateTime(2013, 5, 15, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2018, 1, 1, 0, 0, 0, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 6, ["name"] = "fake", ["height"] = 140, ["countryId"] = 5, ["birthDate"] = new DateTime(2018, 1, 9, 0, 0, 0, 0) } });
        var exceptions = context.GetExceptions();
        Assert.AreEqual(0, exceptions.Count);
    }

    [TestMethod]
    public void CopyOnlySpecifiedColumnsOff()
    {
        var context = TestExecuter.GetContext();
        var builder = SequenceBuilder.Fluent
        .ImportEnumerable(new EnumerableImporter(context)
        {
            InputGenerator = caller => TestData.Person(context)
                .Evaluate(caller)
                .TakeRowsAndReleaseOwnership(),
            Columns = new()
            {
                ["ID"] = new ReaderColumnConfiguration(new StringConverter()),
                ["age"] = new ReaderColumnConfiguration(new LongConverter()).ValueWhenSourceIsNull(-1L),
            },
        });

        var result = TestExecuter.Execute(builder);
        Assert.AreEqual(7, result.MutatedRows.Count);
        Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "0", ["age"] = 17L, ["name"] = "A", ["height"] = 160, ["eyeColor"] = "brown", ["countryId"] = 1, ["birthDate"] = new DateTime(2010, 12, 9, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 12, 19, 12, 0, 1, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "1", ["age"] = 8L, ["name"] = "B", ["height"] = 190, ["countryId"] = 1, ["birthDate"] = new DateTime(2011, 2, 1, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 12, 19, 13, 2, 0, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "2", ["age"] = 27L, ["name"] = "C", ["height"] = 170, ["eyeColor"] = "green", ["countryId"] = 2, ["birthDate"] = new DateTime(2014, 1, 21, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2015, 11, 21, 17, 11, 58, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "3", ["age"] = 39L, ["name"] = "D", ["height"] = 160, ["eyeColor"] = "fake", ["birthDate"] = "2018.07.11", ["lastChangedTime"] = new DateTime(2017, 8, 1, 4, 9, 1, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "4", ["age"] = -3L, ["name"] = "E", ["height"] = 160, ["countryId"] = 1, ["lastChangedTime"] = new DateTime(2019, 1, 1, 23, 59, 59, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "5", ["age"] = 11L, ["name"] = "A", ["height"] = 140, ["birthDate"] = new DateTime(2013, 5, 15, 0, 0, 0, 0), ["lastChangedTime"] = new DateTime(2018, 1, 1, 0, 0, 0, 0) },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "6", ["age"] = -1L, ["name"] = "fake", ["height"] = 140, ["countryId"] = 5, ["birthDate"] = new DateTime(2018, 1, 9, 0, 0, 0, 0) } });
        var exceptions = context.GetExceptions();
        Assert.AreEqual(0, exceptions.Count);
    }

    [TestMethod]
    public void CopyOnlySpecifiedColumnsOn()
    {
        var context = TestExecuter.GetContext();
        var builder = SequenceBuilder.Fluent
        .ImportEnumerable(new EnumerableImporter(context)
        {
            InputGenerator = caller => TestData.Person(context)
                .Evaluate(caller)
                .TakeRowsAndReleaseOwnership(),
            Columns = new()
            {
                ["ID"] = new ReaderColumnConfiguration(new StringConverter()),
                ["age"] = new ReaderColumnConfiguration(new LongConverter()).ValueWhenSourceIsNull(-1L),
            },
            CopyOnlySpecifiedColumns = true,
        });

        var result = TestExecuter.Execute(builder);
        Assert.AreEqual(7, result.MutatedRows.Count);
        Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "0", ["age"] = 17L },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "1", ["age"] = 8L },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "2", ["age"] = 27L },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "3", ["age"] = 39L },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "4", ["age"] = -3L },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "5", ["age"] = 11L },
            new CaseInsensitiveStringKeyDictionary<object>() { ["ID"] = "6", ["age"] = -1L } });
        var exceptions = context.GetExceptions();
        Assert.AreEqual(0, exceptions.Count);
    }
}