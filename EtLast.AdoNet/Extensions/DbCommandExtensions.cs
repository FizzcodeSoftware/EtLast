﻿namespace FizzCode.EtLast.AdoNet
{
    using System.Collections.Generic;
    using System.Data;

    public static class DbCommandExtensions
    {
        public static void FillCommandParameters(this IDbCommand command, Dictionary<string, object> source)
        {
            if (source == null || source.Count == 0)
                return;

            var isSqlServer = command is System.Data.SqlClient.SqlCommand;

            foreach (var kvp in source)
            {
                var parameter = command.CreateParameter();

                if (isSqlServer)
                {
                    if (kvp.Value is System.DateTime)
                    {
                        parameter.DbType = DbType.DateTime2;
                    }
                }

                parameter.ParameterName = kvp.Key;
                parameter.Value = kvp.Value;
                command.Parameters.Add(parameter);
            }
        }
    }
}