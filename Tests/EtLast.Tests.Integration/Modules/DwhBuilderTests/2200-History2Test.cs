﻿namespace FizzCode.EtLast.Tests.Integration.Modules.DwhBuilderTests
{
    using System;
    using System.Collections.Generic;
    using FizzCode.EtLast;
    using FizzCode.EtLast.DwhBuilder;
    using FizzCode.EtLast.DwhBuilder.Extenders.DataDefinition;
    using FizzCode.EtLast.DwhBuilder.Extenders.DataDefinition.MsSql;
    using FizzCode.EtLast.DwhBuilder.MsSql;
    using FizzCode.LightWeight.Collections;
    using FizzCode.LightWeight.RelationalModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // EtlRunInfo ON
    public class History2Test : AbstractDwhBuilderTestPlugin
    {
        public override void Execute()
        {
            DatabaseDeclaration.GetTable("dbo", "People").HasHistoryTable();
            DatabaseDeclaration.GetTable("sec", "Pet").EtlRunInfoDisabled();

            var configuration = new DwhBuilderConfiguration();
            var model = DwhDataDefinitionToRelationalModelConverter.Convert(DatabaseDeclaration, "dbo");

            DataDefinitionExtenderMsSql2016.Extend(DatabaseDeclaration, configuration);
            RelationalModelExtender.Extend(model, configuration);

            CreateDatabase(DatabaseDeclaration);

            Init(configuration, model);
            Update(configuration, model);
        }

        private void Init(DwhBuilderConfiguration configuration, RelationalModel model)
        {
            var builder = new MsSqlDwhBuilder(PluginTopic, "run#1", EtlRunId1)
            {
                Configuration = configuration,
                ConnectionString = TestConnectionString,
                Model = model,
            };

            // BaseIsCurrentFinalizer + HasHistoryTable enabled
            builder.AddTables(model["dbo"]["People"])
                .InputIsCustomProcess(CreatePeople1)
                .SetValidFromToDefault()
                .SetValidFromToRecordTimestampIfAvailable()
                .AddMutators(PeopleMutators)
                .RemoveExistingRows(b => b
                    .MatchByPrimaryKey()
                    .CompareAllColumnsButValidity())
                .DisableConstraintCheck()
                .BaseIsCurrentFinalizer(b => b
                    .MatchByPrimaryKey());

            // BaseIsHistoryFinalizer + AutoValidityRange
            builder.AddTables(model["dbo"]["PeopleRating"])
                .InputIsCustomProcess(CreatePeopleRating1)
                .AutoValidityRange(b => b
                    .MatchBySpecificColumns("PeopleId")
                    .CompareAllColumnsButValidity()
                    .UsePreviousValue("Rating", "PreviousRating"))
                .DisableConstraintCheck()
                .BaseIsHistoryFinalizer(b => b
                    .MatchBySpecificColumns("PeopleId"));

            // BaseIsCurrentFinalizer + HasHistoryTable disabled
            builder.AddTables(model["sec"]["Pet"])
                .InputIsCustomProcess(CreatePet1)
                .SetValidFromToDefault()
                .SetValidFromToRecordTimestampIfAvailable()
                .AddMutators(PetMutators)
                .RemoveExistingRows(b => b
                    .MatchByPrimaryKey()
                    .CompareAllColumnsButValidity())
                .DisableConstraintCheck()
                .BaseIsCurrentFinalizer(b => b
                    .MatchByPrimaryKey());

            var process = builder.Build();
            Context.ExecuteOne(true, process);

            var result = ReadRows("dbo", "People");
            Assert.AreEqual(5, result.Count);
            Assert.That.ExactMatch(result, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 0, ["Name"] = "A", ["FavoritePetId"] = 2, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "B", ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["Name"] = "C", ["FavoritePetId"] = 3, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 3, ["Name"] = "D", ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 4, ["Name"] = "E", ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) } });

            result = ReadRows("dbo", "People_hist");
            Assert.AreEqual(5, result.Count);
            Assert.That.ExactMatch(result, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 1, ["Id"] = 0, ["Name"] = "A", ["FavoritePetId"] = 2, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 2, ["Id"] = 1, ["Name"] = "B", ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 3, ["Id"] = 2, ["Name"] = "C", ["FavoritePetId"] = 3, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 4, ["Id"] = 3, ["Name"] = "D", ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 5, ["Id"] = 4, ["Name"] = "E", ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)) } });

            result = ReadRows("dbo", "PeopleRating");
            Assert.AreEqual(4, result.Count);
            Assert.That.ExactMatch(result, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["PeopleId"] = 0, ["Rating"] = 7, ["_ValidFrom"] = new DateTimeOffset(new DateTime(1900, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["PeopleId"] = 1, ["Rating"] = 4, ["_ValidFrom"] = new DateTimeOffset(new DateTime(1900, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 3, ["PeopleId"] = 2, ["Rating"] = 3, ["_ValidFrom"] = new DateTimeOffset(new DateTime(1900, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 4, ["PeopleId"] = 4, ["Rating"] = 9, ["_ValidFrom"] = new DateTimeOffset(new DateTime(1900, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0) } });

            result = ReadRows("sec", "Pet");
            Assert.AreEqual(3, result.Count);
            Assert.That.ExactMatch(result, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "pet#1", ["OwnerPeopleId"] = 0, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["Name"] = "pet#2", ["OwnerPeopleId"] = 0, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 3, ["Name"] = "pet#3", ["OwnerPeopleId"] = 2, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0) } });

            result = ReadRows("dbo", "_temp_People");
            Assert.AreEqual(5, result.Count);

            result = ReadRows("dbo", "_temp_PeopleRating");
            Assert.AreEqual(4, result.Count);

            result = ReadRows("sec", "_temp_Pet");
            Assert.AreEqual(3, result.Count);
        }

        private void Update(DwhBuilderConfiguration configuration, RelationalModel model)
        {
            var builder = new MsSqlDwhBuilder(PluginTopic, "run#2", EtlRunId2)
            {
                Configuration = configuration,
                ConnectionString = TestConnectionString,
                Model = model,
            };

            builder.AddTables(model["dbo"]["People"])
                .InputIsCustomProcess(CreatePeople2)
                .SetValidFromToDefault()
                .SetValidFromToRecordTimestampIfAvailable()
                .AddMutators(PeopleMutators)
                .RemoveExistingRows(b => b
                    .MatchByPrimaryKey()
                    .CompareAllColumnsButValidity())
                .DisableConstraintCheck()
                .BaseIsCurrentFinalizer(b => b
                    .MatchByPrimaryKey());

            builder.AddTables(model["dbo"]["PeopleRating"])
                .InputIsCustomProcess(CreatePeopleRating2)
                .AutoValidityRange(b => b
                    .MatchBySpecificColumns("PeopleId")
                    .CompareAllColumnsButValidity()
                    .UsePreviousValue("Rating", "PreviousRating"))
                .DisableConstraintCheck()
                .BaseIsHistoryFinalizer(b => b
                    .MatchBySpecificColumns("PeopleId"));

            builder.AddTables(model["sec"]["Pet"])
                .InputIsCustomProcess(CreatePet2)
                .SetValidFromToDefault()
                .SetValidFromToRecordTimestampIfAvailable()
                .AddMutators(PetMutators)
                .RemoveExistingRows(b => b
                    .MatchByPrimaryKey()
                    .CompareAllColumnsButValidity())
                .DisableConstraintCheck()
                .BaseIsCurrentFinalizer(b => b
                    .MatchByPrimaryKey());

            var process = builder.Build();
            Context.ExecuteOne(true, process);

            var result = ReadRows("dbo", "People");
            Assert.AreEqual(5, result.Count);
            Assert.That.ExactMatch(result, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 0, ["Name"] = "A", ["FavoritePetId"] = 2, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "Bx", ["LastChangedOn"] = new DateTime(2010, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2010, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["Name"] = "C", ["FavoritePetId"] = 3, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 3, ["Name"] = "Dx", ["LastChangedOn"] = new DateTime(2010, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2010, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 4, ["Name"] = "E", ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) } });

            result = ReadRows("dbo", "People_hist");
            Assert.AreEqual(7, result.Count);
            Assert.That.ExactMatch(result, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 1, ["Id"] = 0, ["Name"] = "A", ["FavoritePetId"] = 2, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 2, ["Id"] = 1, ["Name"] = "B", ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunTo"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2010, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 3, ["Id"] = 2, ["Name"] = "C", ["FavoritePetId"] = 3, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 4, ["Id"] = 3, ["Name"] = "D", ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunTo"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2010, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 5, ["Id"] = 4, ["Name"] = "E", ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2000, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 6, ["Id"] = 1, ["Name"] = "Bx", ["LastChangedOn"] = new DateTime(2010, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunUpdate"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunFrom"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2010, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["People_histID"] = 7, ["Id"] = 3, ["Name"] = "Dx", ["LastChangedOn"] = new DateTime(2010, 1, 1, 1, 1, 1, 0), ["EtlRunInsert"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunUpdate"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunFrom"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["_ValidFrom"] = new DateTimeOffset(new DateTime(2010, 1, 1, 1, 1, 1, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)) } });

            result = ReadRows("dbo", "PeopleRating");
            Assert.AreEqual(6, result.Count);
            Assert.That.ExactMatch(result, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["PeopleId"] = 0, ["Rating"] = 7, ["_ValidFrom"] = new DateTimeOffset(new DateTime(1900, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2022, 2, 2, 2, 2, 2, 0), new TimeSpan(0, 0, 0, 0, 0)), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunTo"] = new DateTime(2022, 2, 2, 2, 2, 2, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["PeopleId"] = 1, ["Rating"] = 4, ["_ValidFrom"] = new DateTimeOffset(new DateTime(1900, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 3, ["PeopleId"] = 2, ["Rating"] = 3, ["_ValidFrom"] = new DateTimeOffset(new DateTime(1900, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 4, ["PeopleId"] = 4, ["Rating"] = 9, ["_ValidFrom"] = new DateTimeOffset(new DateTime(1900, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["EtlRunInsert"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunUpdate"] = new DateTime(2001, 1, 1, 1, 1, 1, 0), ["EtlRunFrom"] = new DateTime(2001, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 5, ["PeopleId"] = 0, ["Rating"] = 29, ["PreviousRating"] = 7, ["_ValidFrom"] = new DateTimeOffset(new DateTime(2022, 2, 2, 2, 2, 2, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["EtlRunInsert"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunUpdate"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunFrom"] = new DateTime(2022, 2, 2, 2, 2, 2, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 6, ["PeopleId"] = 3, ["Rating"] = 23, ["_ValidFrom"] = new DateTimeOffset(new DateTime(1900, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["_ValidTo"] = new DateTimeOffset(new DateTime(2500, 1, 1, 0, 0, 0, 0), new TimeSpan(0, 0, 0, 0, 0)), ["EtlRunInsert"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunUpdate"] = new DateTime(2022, 2, 2, 2, 2, 2, 0), ["EtlRunFrom"] = new DateTime(2022, 2, 2, 2, 2, 2, 0) } });

            result = ReadRows("sec", "Pet");
            Assert.AreEqual(4, result.Count);
            Assert.That.ExactMatch(result, new List<CaseInsensitiveStringKeyDictionary<object>>() {
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 1, ["Name"] = "pet#1", ["OwnerPeopleId"] = 0, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 2, ["Name"] = "pet#2x", ["OwnerPeopleId"] = 0, ["LastChangedOn"] = new DateTime(2010, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 3, ["Name"] = "pet#3", ["OwnerPeopleId"] = 2, ["LastChangedOn"] = new DateTime(2000, 1, 1, 1, 1, 1, 0) },
                new CaseInsensitiveStringKeyDictionary<object>() { ["Id"] = 4, ["Name"] = "pet#4x", ["OwnerPeopleId"] = 0, ["LastChangedOn"] = new DateTime(2010, 1, 1, 1, 1, 1, 0) } });

            result = ReadRows("dbo", "_temp_People");
            Assert.AreEqual(2, result.Count);

            result = ReadRows("dbo", "_temp_PeopleRating");
            Assert.AreEqual(2, result.Count);

            result = ReadRows("sec", "_temp_Pet");
            Assert.AreEqual(2, result.Count);
        }

        public static IEvaluable CreatePeople1(DwhTableBuilder tableBuilder, DateTimeOffset? maxRecordTimestamp)
        {
            return new RowCreator(tableBuilder.ResilientTable.Topic, null)
            {
                Columns = new[] { "Id", "Name", "FavoritePetId", "LastChangedOn" },
                InputRows = new List<object[]>()
                {
                    new object[] { 0, "A", 2, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 1, "B", null, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 2, "C", 3, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 3, "D", null, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 4, "E", null, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 5, "F", -1, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                },
            };
        }

        public static IEvaluable CreatePeople2(DwhTableBuilder tableBuilder, DateTimeOffset? maxRecordTimestamp)
        {
            return new RowCreator(tableBuilder.ResilientTable.Topic, null)
            {
                Columns = new[] { "Id", "Name", "FavoritePetId", "LastChangedOn" },
                InputRows = new List<object[]>()
                {
                    new object[] { 0, "A", 2, new DateTime(2010, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 1, "Bx", null, new DateTime(2010, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 2, "C", 3, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 3, "Dx", null, new DateTime(2010, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 4, "E", null, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 5, "Fx", -1, new DateTime(2010, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                },
            };
        }

        public static IEvaluable CreatePeopleRating1(DwhTableBuilder tableBuilder, DateTimeOffset? maxRecordTimestamp)
        {
            return new RowCreator(tableBuilder.ResilientTable.Topic, null)
            {
                Columns = new[] { "PeopleId", "Rating" },
                InputRows = new List<object[]>()
                {
                    new object[] { 0, 7 },
                    new object[] { 1, 4 },
                    new object[] { 2, 3 },
                    new object[] { 4, 9 },
                },
            };
        }

        public static IEvaluable CreatePeopleRating2(DwhTableBuilder tableBuilder, DateTimeOffset? maxRecordTimestamp)
        {
            return new RowCreator(tableBuilder.ResilientTable.Topic, null)
            {
                Columns = new[] { "PeopleId", "Rating" },
                InputRows = new List<object[]>()
                {
                    new object[] { 0, 29 },
                    new object[] { 3, 23 },
                },
            };
        }

        public static IEvaluable CreatePet1(DwhTableBuilder tableBuilder, DateTimeOffset? maxRecordTimestamp)
        {
            return new RowCreator(tableBuilder.ResilientTable.Topic, null)
            {
                Columns = new[] { "Id", "Name", "OwnerPeopleId", "LastChangedOn" },
                InputRows = new List<object[]>()
                {
                    new object[] { 1, "pet#1", 0, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 2, "pet#2", 0, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 3, "pet#3", 2, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 4, "pet#4", null, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 5, "pet#5", -1, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                },
            };
        }

        public static IEvaluable CreatePet2(DwhTableBuilder tableBuilder, DateTimeOffset? maxRecordTimestamp)
        {
            return new RowCreator(tableBuilder.ResilientTable.Topic, null)
            {
                Columns = new[] { "Id", "Name", "OwnerPeopleId", "LastChangedOn" },
                InputRows = new List<object[]>()
                {
                    new object[] { 1, "pet#1", 0, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 2, "pet#2x", 0, new DateTime(2010, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 3, "pet#3", 2, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 4, "pet#4x", 0, new DateTime(2010, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                    new object[] { 5, "pet#5", -1, new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc) },
                },
            };
        }

        private static IEnumerable<IMutator> PeopleMutators(DwhTableBuilder tableBuilder)
        {
            yield return new CustomMutator(tableBuilder.ResilientTable.Topic, "FkFix")
            {
                Then = (proc, row) =>
                {
                    var fk = row.GetAs<int?>("FavoritePetId");
                    return fk == null || fk.Value >= 0;
                },
            };
        }

        private static IEnumerable<IMutator> PetMutators(DwhTableBuilder tableBuilder)
        {
            yield return new CustomMutator(tableBuilder.ResilientTable.Topic, "FkFix")
            {
                Then = (proc, row) =>
                {
                    var fk = row.GetAs<int?>("OwnerPeopleId");
                    return fk != null && fk.Value >= 0;
                },
            };
        }
    }
}