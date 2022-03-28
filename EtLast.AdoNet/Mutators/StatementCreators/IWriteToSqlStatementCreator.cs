﻿namespace FizzCode.EtLast;

public interface IWriteToSqlStatementCreator
{
    void Prepare(WriteToSqlMutator process, DetailedDbTableDefinition tableDefinition);
    string CreateRowStatement(NamedConnectionString connectionString, IReadOnlySlimRow row, WriteToSqlMutator operation);
    string CreateStatement(NamedConnectionString connectionString, List<string> rowStatements);
}
