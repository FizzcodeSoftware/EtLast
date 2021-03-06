﻿namespace FizzCode.EtLast.AdoNet
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using FizzCode.LightWeight.AdoNet;

    public class GenericInsertSqlStatementCreator : IAdoNetWriteToTableSqlStatementCreator
    {
        private string _dbColumns;
        private DetailedDbTableDefinition _tableDefinition;
        private DetailedDbColumnDefinition[] _columns;

        public void Prepare(WriteToTableMutator process, DetailedDbTableDefinition tableDefinition)
        {
            _tableDefinition = tableDefinition;
            _columns = _tableDefinition.Columns.Where(x => x.Insert).ToArray();
            _dbColumns = string.Join(", ", _columns.Select(x => x.DbColumn));
        }

        public string CreateRowStatement(NamedConnectionString connectionString, IReadOnlySlimRow row, WriteToTableMutator operation)
        {
            var startIndex = operation.ParameterCount;
            foreach (var column in _columns)
            {
                operation.CreateParameter(column, row[column.RowColumn]);
            }

            var statement = "(" + string.Join(", ", _columns.Select(_ => "@" + startIndex++.ToString("D", CultureInfo.InvariantCulture))) + ")";
            return statement;
        }

        public string CreateStatement(NamedConnectionString connectionString, List<string> rowStatements)
        {
            return "INSERT INTO " + _tableDefinition.TableName + " (" + _dbColumns + ") VALUES \n" + string.Join(",\n", rowStatements) + ";";
        }
    }
}