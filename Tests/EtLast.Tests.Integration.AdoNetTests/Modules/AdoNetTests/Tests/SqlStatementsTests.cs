﻿namespace FizzCode.EtLast.Tests.Integration.Modules.AdoNetTests;

[TestClass]
public class SqlStatementsTests
{
    [ClassInitialize]
    public static void Initialize(TestContext context)
    {
#if INTEGRATION
        TestAdapter.Run($"run AdoNetTests {nameof(CreateDatabase)}");
#endif
    }

    [ClassCleanup]
    public static void Cleanup()
    {
#if INTEGRATION
        TestAdapter.Run($"run AdoNetTests {nameof(DropDatabase)}");
#endif
    }

    [TestMethodIntegration]
    public void GetTableMaxValueTest()
    {
        TestAdapter.Run($"run AdoNetTests {nameof(GetTableMaxValue)}");
    }

    [TestMethodIntegration]
    public void GetTableRecordCountTest()
    {
        TestAdapter.Run($"run AdoNetTests {nameof(GetTableRecordCount)}");
    }
}