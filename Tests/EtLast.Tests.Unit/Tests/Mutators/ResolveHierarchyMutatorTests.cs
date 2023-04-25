﻿namespace FizzCode.EtLast.Tests.Unit.Mutators;

[TestClass]
public class ResolveHierarchyMutatorTests
{
    [TestMethod]
    public void KeepOriginalLevelColumns()
    {
        var context = TestExecuter.GetContext();
        var builder = SequenceBuilder.Fluent
        .ReadFrom(TestData.RoleHierarchy(context))
        .ResolveHierarchy(new ResolveHierarchyMutator(context)
        {
            IdentityColumn = "id",
            NewColumnWithParentId = "parentId",
            NewColumnWithLevel = "level",
            LevelColumns = new[] { "level1", "level2", "level3" },
            RemoveLevelColumns = false,
            NewColumnWithName = null,
        });

        var result = TestExecuter.Execute(builder);
        Assert.AreEqual(6, result.MutatedRows.Count);
        Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 0, ["code"] = "A", ["level1"] = "AAA", ["level"] = 0 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 1, ["code"] = "B", ["level2"] = "BBB", ["parentId"] = 0, ["level"] = 1 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 2, ["code"] = "C", ["level3"] = "CCC", ["parentId"] = 1, ["level"] = 2 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 3, ["code"] = "D", ["level3"] = "DDD", ["parentId"] = 1, ["level"] = 2 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 4, ["code"] = "E", ["level2"] = "EEE", ["parentId"] = 0, ["level"] = 1 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 5, ["code"] = "F", ["level2"] = "FFF", ["parentId"] = 0, ["level"] = 1 } });

        Assert.AreEqual(0, result.Process.FlowState.Exceptions.Count);
    }

    [TestMethod]
    public void RemoveLevelColumns()
    {
        var context = TestExecuter.GetContext();
        var builder = SequenceBuilder.Fluent
        .ReadFrom(TestData.RoleHierarchy(context))
        .ResolveHierarchy(new ResolveHierarchyMutator(context)
        {
            IdentityColumn = "id",
            NewColumnWithParentId = "parentId",
            NewColumnWithLevel = "level",
            LevelColumns = new[] { "level1", "level2", "level3" },
            RemoveLevelColumns = true,
            NewColumnWithName = null,
        });

        var result = TestExecuter.Execute(builder);
        Assert.AreEqual(6, result.MutatedRows.Count);
        Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 0, ["code"] = "A", ["level"] = 0 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 1, ["code"] = "B", ["parentId"] = 0, ["level"] = 1 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 2, ["code"] = "C", ["parentId"] = 1, ["level"] = 2 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 3, ["code"] = "D", ["parentId"] = 1, ["level"] = 2 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 4, ["code"] = "E", ["parentId"] = 0, ["level"] = 1 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 5, ["code"] = "F", ["parentId"] = 0, ["level"] = 1 } });

        Assert.AreEqual(0, result.Process.FlowState.Exceptions.Count);
    }

    [TestMethod]
    public void NoNewLevelColumn()
    {
        var context = TestExecuter.GetContext();
        var builder = SequenceBuilder.Fluent
        .ReadFrom(TestData.RoleHierarchy(context))
        .ResolveHierarchy(new ResolveHierarchyMutator(context)
        {
            IdentityColumn = "id",
            NewColumnWithParentId = "parentId",
            LevelColumns = new[] { "level1", "level2", "level3" },
            RemoveLevelColumns = true,
            NewColumnWithLevel = null,
            NewColumnWithName = null,
        });

        var result = TestExecuter.Execute(builder);
        Assert.AreEqual(6, result.MutatedRows.Count);
        Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 0, ["code"] = "A" },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 1, ["code"] = "B", ["parentId"] = 0 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 2, ["code"] = "C", ["parentId"] = 1 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 3, ["code"] = "D", ["parentId"] = 1 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 4, ["code"] = "E", ["parentId"] = 0 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 5, ["code"] = "F", ["parentId"] = 0 } });

        Assert.AreEqual(0, result.Process.FlowState.Exceptions.Count);
    }

    [TestMethod]
    public void NewNameColumn()
    {
        var context = TestExecuter.GetContext();
        var builder = SequenceBuilder.Fluent
        .ReadFrom(TestData.RoleHierarchy(context))
        .ResolveHierarchy(new ResolveHierarchyMutator(context)
        {
            IdentityColumn = "id",
            NewColumnWithParentId = "parentId",
            NewColumnWithLevel = "level",
            NewColumnWithName = "name",
            LevelColumns = new[] { "level1", "level2", "level3" },
            RemoveLevelColumns = true,
        });

        var result = TestExecuter.Execute(builder);
        Assert.AreEqual(6, result.MutatedRows.Count);
        Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 0, ["code"] = "A", ["level"] = 0, ["name"] = "AAA" },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 1, ["code"] = "B", ["parentId"] = 0, ["level"] = 1, ["name"] = "BBB" },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 2, ["code"] = "C", ["parentId"] = 1, ["level"] = 2, ["name"] = "CCC" },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 3, ["code"] = "D", ["parentId"] = 1, ["level"] = 2, ["name"] = "DDD" },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 4, ["code"] = "E", ["parentId"] = 0, ["level"] = 1, ["name"] = "EEE" },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = 5, ["code"] = "F", ["parentId"] = 0, ["level"] = 1, ["name"] = "FFF" } });

        Assert.AreEqual(0, result.Process.FlowState.Exceptions.Count);
    }

    [TestMethod]
    public void IdentityColumnIsString()
    {
        var context = TestExecuter.GetContext();
        var builder = SequenceBuilder.Fluent
        .ReadFrom(TestData.RoleHierarchy(context))
        .ConvertValue(new InPlaceConvertMutator(context)
        {
            Columns = new[] { "id" },
            TypeConverter = new StringConverter(),
        })
        .ResolveHierarchy(new ResolveHierarchyMutator(context)
        {
            IdentityColumn = "id",
            NewColumnWithParentId = "parentId",
            NewColumnWithLevel = "level",
            LevelColumns = new[] { "level1", "level2", "level3" },
            RemoveLevelColumns = false,
            NewColumnWithName = null,
        });

        var result = TestExecuter.Execute(builder);
        Assert.AreEqual(6, result.MutatedRows.Count);
        Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "0", ["code"] = "A", ["level1"] = "AAA", ["level"] = 0 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "1", ["code"] = "B", ["level2"] = "BBB", ["parentId"] = "0", ["level"] = 1 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "2", ["code"] = "C", ["level3"] = "CCC", ["parentId"] = "1", ["level"] = 2 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "3", ["code"] = "D", ["level3"] = "DDD", ["parentId"] = "1", ["level"] = 2 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "4", ["code"] = "E", ["level2"] = "EEE", ["parentId"] = "0", ["level"] = 1 },
            new CaseInsensitiveStringKeyDictionary<object>() { ["id"] = "5", ["code"] = "F", ["level2"] = "FFF", ["parentId"] = "0", ["level"] = 1 } });

        Assert.AreEqual(0, result.Process.FlowState.Exceptions.Count);
    }
}