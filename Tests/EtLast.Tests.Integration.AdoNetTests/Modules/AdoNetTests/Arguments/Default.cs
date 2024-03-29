﻿namespace FizzCode.EtLast.Tests.Integration.Modules.AdoNetTests;

internal class Default : IDefaultArgumentProvider
{
    public Dictionary<string, object> Arguments => new()
    {
        ["DatabaseName"] = "EtLastIntegrationTest",
        ["ConnectionString"] = () => new NamedConnectionString("test", "Microsoft.Data.SqlClient", "Data Source=(local);Initial Catalog=\"EtLastIntegrationTest\";Integrated Security=SSPI;Connection Timeout=5;Encrypt=False", "2016"),
        ["ConnectionStringMaster"] = () => new NamedConnectionString("test", "Microsoft.Data.SqlClient", "Data Source=(local);Initial Catalog=\"master\";Integrated Security=SSPI;Connection Timeout=5;Encrypt=False", "2016"),
    };
}