﻿using System;
using FizzCode.EtLast.ConsoleHost;

return (int)HostBuilder.New("EtLast Integration Tests")
    .HandleCommandLineArgs(args)
    .UseCommandLineListener(hostArgs =>
    {
        Console.WriteLine("list of automatically compiled host argument values:");
        foreach (var key in hostArgs.AllKeys)
        {
            var v = hostArgs.GetAs<string>(key);
            if (v != null)
                Console.WriteLine("[" + key + "] = [" + v + "]");
        }

        return new ConsoleCommandLineListener();
    })
    .SetAlias("dwh", "run DwhBuilderTests Main")
    .SetAlias("doex1", "run DwhBuilderTests CustomExceptionTest")
    .SetAlias("doex2", "run DwhBuilderTests ExceptionTest")
    .SetAlias("createdb", "run DwhBuilderTests CreateDatabase")
    .SetAlias("test", "test-modules DwhBuilderTests")
    .SetAlias("ado", "run AdoNetTests Main")
    .SetAlias("flow", "run FlowTests Main")
    //.DisableSerilogForModules()
    //.DisableSerilogForCommands()
    .RegisterEtlContextListener(context => new FizzCode.EtLast.Diagnostics.HttpSender(context)
    {
        MaxCommunicationErrorCount = 2,
        Url = "http://localhost:8642",
    })
    .Build()
    .Run();
