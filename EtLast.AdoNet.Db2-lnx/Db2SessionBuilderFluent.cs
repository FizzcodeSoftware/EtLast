﻿namespace FizzCode.EtLast;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class Db2SessionBuilderFluent
{
    public static ISessionBuilder EnableMicrosoftSqlClient(this ISessionBuilder session)
    {
        DbProviderFactories.RegisterFactory("IBM.Data.Db2", DB2Factory.Instance);
        return session;
    }
}
