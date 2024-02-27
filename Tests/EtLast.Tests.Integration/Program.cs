﻿using System;
using FizzCode.EtLast;

if (args?.Length == 1 && args[0].Equals("stop"))
{
    AbstractHost.StopGracefully();
    return 0;
}

return (int)new ConsoleHost("EtLast Integration Tests")
    .UseCommandListener(hostArgs =>
    {
        Console.WriteLine("list of automatically compiled host argument values:");
        foreach (var key in hostArgs.AllKeys)
        {
            var v = hostArgs.GetAs<string>(key);
            if (v != null)
                Console.WriteLine("[" + key + "] = [" + v + "]");
        }

        return new ConsoleCommandListener();
    })
    .SetAlias("test", "test-modules AdoNetTests FlowTests")
    .SetAlias("ado", "run AdoNetTests Main")
    .SetAlias("flow", "run FlowTests Main")
    //.DisableSerilogForModules()
    //.DisableSerilogForCommands()

    .ConfigureSession((builder, sessionArguments) => builder.UseRollingDevLogManifestFiles(null))

    .IfInstanceIs("WSDEVONE", host => host
        .IfDebuggerAttached(host => host
            .RegisterEtlContextListener(context => new DiagnosticsHttpSender(context)
            {
                MaxCommunicationErrorCount = 2,
                Url = "http://localhost:8642",
            }))
        )
    .Run();
