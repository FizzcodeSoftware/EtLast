﻿namespace FizzCode.EtLast.Tests.Unit.RowSources
{
    using System;
    using System.Collections.Generic;
    using FizzCode.LightWeight.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DelimitedFileReaderTests
    {
        private static IProducer GetReader(IEtlContext context, string fileName, bool removeSurroundingDoubleQuotes = true)
        {
            return new DelimitedFileReader(context, null, null)
            {
                FileName = fileName,
                ColumnConfiguration = new List<ReaderColumnConfiguration>()
                {
                    new ReaderColumnConfiguration("Id", new IntConverter()),
                    new ReaderColumnConfiguration("Name", new StringConverter()),
                    new ReaderColumnConfiguration("Value1", "ValueString", new StringConverter()),
                    new ReaderColumnConfiguration("Value2", "ValueInt", new IntConverter()),
                    new ReaderColumnConfiguration("Value3", "ValueDate", new DateConverter()),
                    new ReaderColumnConfiguration("Value4", "ValueDouble", new DoubleConverter())
                },
                HasHeaderRow = true,
                RemoveSurroundingDoubleQuotes = removeSurroundingDoubleQuotes
            };
        }

        private static IProducer GetSimpleReader(IEtlContext context, string fileName, bool treatEmptyStringsAsNull = true)
        {
            return new DelimitedFileReader(context, null, null)
            {
                FileName = fileName,
                ColumnConfiguration = new List<ReaderColumnConfiguration>()
                {
                    new ReaderColumnConfiguration("Id", new IntConverter()),
                    new ReaderColumnConfiguration("Name", new StringConverter()),
                    new ReaderColumnConfiguration("Value", new StringConverter())
                },
                HasHeaderRow = true,
                TreatEmptyStringAsNull = treatEmptyStringsAsNull,
            };
        }

        [TestMethod]
        public void BasicTest()
        {
            var context = TestExecuter.GetContext();
            var builder = ProcessBuilder.Fluent
                .ReadFrom(GetReader(context, @"TestData\Sample.csv"))
                .ReplaceErrorWithValue(new ReplaceErrorWithValueMutator(context, null, null)
                {
                    Columns = new[] { "ValueDate" },
                    Value = null,
                });

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(2, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 0, ["Name"] = "A", ["ValueString"] = "AAA", ["ValueInt"] = -1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "B", ["ValueInt"] = 3, ["ValueDate"] = new DateTime(2019, 4, 25, 0, 0, 0, 0), ["ValueDouble"] = 1.234d } });
            var exceptions = context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void QuotedTest1()
        {
            var context = TestExecuter.GetContext();
            var builder = ProcessBuilder.Fluent
                .ReadFrom(GetReader(context, @"TestData\QuotedSample1.csv"));

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(2, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "A", ["ValueString"] = "te\"s\"t;test", ["ValueInt"] = -1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["Name"] = "tes\"t;t\"est", ["ValueInt"] = -1 } });
            var exceptions = context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void QuotedTest1KeepSurroundingDoubleQuotes()
        {
            var context = TestExecuter.GetContext();
            var builder = ProcessBuilder.Fluent
                .ReadFrom(GetReader(context, @"TestData\QuotedSample1.csv", removeSurroundingDoubleQuotes: false))
                .ThrowExceptionOnRowError(new ThrowExceptionOnRowErrorMutator(context));

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(2, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "A", ["ValueString"] = "\"te\"s\"t;test\"", ["ValueInt"] = -1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["Name"] = "\"tes\"t;t\"est\"", ["ValueInt"] = -1 } });
            var exceptions = context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void QuotedTest2()
        {
            var context = TestExecuter.GetContext();
            var builder = ProcessBuilder.Fluent
                .ReadFrom(GetSimpleReader(context, @"TestData\QuotedSample2.csv"));

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(3, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "A", ["Value"] = "test" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["Name"] = "B", ["Value"] = "test\"" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 3, ["Name"] = "C", ["Value"] = "test\"\"" } });
            var exceptions = context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void QuotedTest3EmptyStringsUntouched()
        {
            var context = TestExecuter.GetContext();
            var builder = ProcessBuilder.Fluent
                .ReadFrom(GetSimpleReader(context, @"TestData\QuotedSample3.csv", treatEmptyStringsAsNull: false));

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(8, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "", ["Value"] = "A" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["Name"] = "B", ["Value"] = "" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 3, ["Name"] = "C", ["Value"] = "\"" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 4, ["Name"] = "\"", ["Value"] = "D" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 5, ["Name"] = "E", ["Value"] = "\"\"" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 6, ["Name"] = "\"\"", ["Value"] = "F" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 7, ["Name"] = "G", ["Value"] = "\"a\"" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 8, ["Name"] = "\"b\"", ["Value"] = "H" } });
            var exceptions = context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void QuotedTest3EmptyStringsRemoved()
        {
            var context = TestExecuter.GetContext();
            var builder = ProcessBuilder.Fluent
                .ReadFrom(GetSimpleReader(context, @"TestData\QuotedSample3.csv", treatEmptyStringsAsNull: true));

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(8, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Value"] = "A" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["Name"] = "B" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 3, ["Name"] = "C", ["Value"] = "\"" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 4, ["Name"] = "\"", ["Value"] = "D" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 5, ["Name"] = "E", ["Value"] = "\"\"" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 6, ["Name"] = "\"\"", ["Value"] = "F" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 7, ["Name"] = "G", ["Value"] = "\"a\"" },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 8, ["Name"] = "\"b\"", ["Value"] = "H" } });
            var exceptions = context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void NewLineTest1()
        {
            var context = TestExecuter.GetContext();
            var builder = ProcessBuilder.Fluent
                .ReadFrom(GetReader(context, @"TestData\NewLineSample1.csv"))
                .ReplaceErrorWithValue(new ReplaceErrorWithValueMutator(context, null, null)
                {
                    Columns = new[] { "ValueDate" },
                    Value = null,
                });

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(1, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "A", ["ValueString"] = "test\n continues", ["ValueInt"] = -1 } });
            var exceptions = context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void NewLineTest2()
        {
            var context = TestExecuter.GetContext();
            var builder = ProcessBuilder.Fluent
                .ReadFrom(GetReader(context, @"TestData\NewLineSample2.csv"))
                .ReplaceErrorWithValue(new ReplaceErrorWithValueMutator(context, null, null)
                {
                    Columns = new[] { "ValueDate" },
                    Value = null,
                });

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(1, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "A", ["ValueString"] = "test\"\ncontinues", ["ValueInt"] = -1 } });
            var exceptions = context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }

        [TestMethod]
        public void InvalidConversion()
        {
            var context = TestExecuter.GetContext();
            var builder = ProcessBuilder.Fluent
                .ReadFrom(GetReader(context, @"TestData\SampleInvalidConversion.csv"))
                .ReplaceErrorWithValue(new ReplaceErrorWithValueMutator(context, null, null)
                {
                    Columns = new[] { "ValueDate" },
                    Value = null,
                });

            var result = TestExecuter.Execute(builder);
            Assert.AreEqual(2, result.MutatedRows.Count);
            Assert.That.ExactMatch(result.MutatedRows, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = new EtlRowError("X"), ["Name"] = "A", ["ValueString"] = "AAA", ["ValueInt"] = -1 },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "B", ["ValueInt"] = 3, ["ValueDate"] = new DateTime(2019, 4, 25, 0, 0, 0, 0), ["ValueDouble"] = 1.234d } });
            var exceptions = context.GetExceptions();
            Assert.AreEqual(0, exceptions.Count);
        }
    }
}