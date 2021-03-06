﻿namespace FizzCode.EtLast.PluginHost
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CommandDotNet;
    using FizzCode.EtLast.AdoNet;
    using FizzCode.LightWeight.AdoNet;
    using Serilog.Events;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
    [Command(Name = "test", Description = "Test connection strings, modules, etc.")]
    [SubCommand]
    public class TestCommand
    {
        [Command(Name = "modules", Description = "Tests one or more modules.")]
        public int ValidateModule(
        [Operand(Name = "names", Description = "The space-separated list of module names.")] List<string> moduleNames,
        [Option(LongName = "all", ShortName = "a")] bool all)
        {
            if (moduleNames == null || moduleNames.Count == 0)
            {
                if (!all)
                {
                    CommandLineHandler.DisplayHelp("test modules");
                    return (int)ExecutionResult.HostArgumentError;
                }
            }
            else if (all)
            {
                CommandLineHandler.DisplayHelp("test modules");
                return (int)ExecutionResult.HostArgumentError;
            }

            var commandContext = CommandLineHandler.Context;

            if (all)
            {
                moduleNames = ModuleLister.GetAllModules(commandContext);
            }

            var result = ExecutionResult.Success;

            foreach (var moduleName in moduleNames)
            {
                commandContext.Logger.Information("loading module {Module}", moduleName);

                ModuleLoader.LoadModule(commandContext, moduleName, null, null, true, out var module);
                if (module != null)
                {
                    ModuleLoader.UnloadModule(commandContext, module);
                    commandContext.Logger.Information("validation {ValidationResult} for {Module}", "PASSED", moduleName);
                }
                else
                {
                    commandContext.Logger.Information("validation {ValidationResult} for {Module}", "FAILED", moduleName);
                    result = ExecutionResult.ModuleLoadError;
                }
            }

            return (int)result;
        }

        [Command(Name = "connection-strings", Description = "Tests connection strings.")]
        public void ValidateConnectionStrings(
        [Operand(Name = "names", Description = "The space-separated list of module names.")] List<string> moduleNames,
        [Option(LongName = "all", ShortName = "a")] bool all)
        {
            if (moduleNames == null || moduleNames.Count == 0)
            {
                if (!all)
                {
                    CommandLineHandler.DisplayHelp("test connection-strings");
                    return;
                }
            }
            else if (all)
            {
                CommandLineHandler.DisplayHelp("test connection-strings");
                return;
            }

            var commandContext = CommandLineHandler.Context;

            if (all)
            {
                moduleNames = ModuleLister.GetAllModules(commandContext);
            }

            var allConnectionStrings = new List<NamedConnectionString>();
            var index = 0;
            foreach (var moduleName in moduleNames)
            {
                var moduleConfiguration = ModuleConfigurationLoader.LoadModuleConfiguration(commandContext, moduleName, null, null);
                if (moduleConfiguration == null)
                    continue;

                if (index == 0)
                {
                    var sharedCs = new ConnectionStringCollection();
                    sharedCs.LoadFromConfiguration(moduleConfiguration.Configuration, "ConnectionStrings:Shared", commandContext.HostConfiguration.SecretProtector);
                    if (sharedCs.All.Any())
                    {
                        commandContext.Logger.Information("connection strings for: {Module}", "Shared");
                        foreach (var cs in sharedCs.All)
                        {
                            commandContext.Logger.Information("\t{ConnectionStringName} ({Provider})", cs.Name, cs.GetFriendlyProviderName());
                            allConnectionStrings.Add(cs);
                        }
                    }
                }

                commandContext.Logger.Information("connection strings for: {Module}", moduleName);

                var connectionStrings = new ConnectionStringCollection();
                connectionStrings.LoadFromConfiguration(moduleConfiguration.Configuration, "ConnectionStrings:Module", commandContext.HostConfiguration.SecretProtector);
                foreach (var cs in connectionStrings.All)
                {
                    commandContext.Logger.Information("\t{ConnectionStringName} ({Provider})", cs.Name, cs.GetFriendlyProviderName());
                    allConnectionStrings.RemoveAll(x => x.Name == cs.Name);
                    allConnectionStrings.Add(cs);
                }

                index++;
            }

            commandContext.Logger.Information("relevant connection strings");
            var originalNames = allConnectionStrings
                .Select(x => x.Name.Split('-')[0])
                .Distinct()
                .ToList();

            foreach (var originalName in originalNames)
            {
                var connectionString = allConnectionStrings.Find(x => string.Equals(x.Name, originalName + "-" + Environment.MachineName, StringComparison.InvariantCultureIgnoreCase))
                    ?? allConnectionStrings.Find(x => string.Equals(x.Name, originalName, StringComparison.InvariantCultureIgnoreCase));

                var knownFields = connectionString.GetKnownConnectionStringFields();
                if (knownFields == null)
                {
                    commandContext.Logger.Information("\ttesting: {ConnectionStringName} ({Provider})",
                        connectionString.Name, connectionString.GetFriendlyProviderName());
                }
                else
                {
                    var message = "\ttesting: {ConnectionStringName} ({Provider})";
                    var args = new List<object>()
                    {
                        connectionString.Name,
                        connectionString.GetFriendlyProviderName(),
                    };

                    if (knownFields.Server != null)
                    {
                        message += ", server: {Server}";
                        args.Add(knownFields.Server);
                    }

                    if (knownFields.Port != null)
                    {
                        message += ", port: {Port}";
                        args.Add(knownFields.Port);
                    }

                    if (knownFields.Database != null)
                    {
                        message += ", database: {Database}";
                        args.Add(knownFields.Database);
                    }

                    if (knownFields.IntegratedSecurity != null)
                    {
                        message += ", integrated security: {IntegratedSecurity}";
                        args.Add(knownFields.IntegratedSecurity);
                    }

                    if (knownFields.UserId != null)
                    {
                        message += ", user: {UserId}";
                        args.Add(knownFields.UserId);
                    }

                    commandContext.Logger.Information(message, args.ToArray());
                }

                try
                {
                    EtlConnectionManager.TestConnection(connectionString);
                    commandContext.Logger.Information("\t\tPASSED");
                }
                catch (Exception ex)
                {
                    commandContext.Logger.Write(LogEventLevel.Fatal, "\t\t{ErrorMessage}", ex.FormatExceptionWithDetails(false));
                }
            }

            commandContext.Logger.Information("connection string test(s) finished");
        }
    }
#pragma warning restore CA1812 // Avoid uninstantiated internal classes
}