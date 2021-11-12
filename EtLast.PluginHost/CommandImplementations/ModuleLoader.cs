﻿namespace FizzCode.EtLast.PluginHost
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading;
    using FizzCode.EtLast;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Text;
    using Serilog.Events;

    internal static class ModuleLoader
    {
        private static long _moduleAutoincrementId;

        public static ExecutionResult LoadModule(CommandContext commandContext, string moduleName, string[] moduleSettingOverrides, string[] pluginListOverride, bool forceCompilation, out CompiledModule module)
        {
            module = null;

            EtlModuleConfiguration moduleConfiguration;
            try
            {
                moduleConfiguration = ModuleConfigurationLoader.LoadModuleConfiguration(commandContext, moduleName, moduleSettingOverrides, pluginListOverride);
                if (moduleConfiguration == null)
                {
                    return ExecutionResult.ModuleConfigurationError;
                }
            }
            catch (Exception ex)
            {
                var msg = ex.FormatExceptionWithDetails(false);
                Console.WriteLine("error during initialization:");
                Console.WriteLine(msg);
                if (Debugger.IsAttached)
                {
                    Console.WriteLine("press any key to exit...");
                    Console.ReadKey();
                }
                else
                {
                    Thread.Sleep(3000);
                }

                return ExecutionResult.ModuleConfigurationError;
            }

            if (moduleConfiguration.ConnectionStrings.All.Any())
            {
                commandContext.Logger.Information("relevant connection strings");

                foreach (var connectionString in moduleConfiguration.ConnectionStrings.All)
                {
                    var knownFields = connectionString.GetKnownConnectionStringFields();
                    if (knownFields == null)
                    {
                        commandContext.Logger.Information("\t{ConnectionStringName} ({Provider})",
                            connectionString.Name, connectionString.GetFriendlyProviderName());
                    }
                    else
                    {
                        var message = "\t{ConnectionStringName} ({Provider})";
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
                }
            }

            var sharedFolder = Path.Combine(commandContext.HostConfiguration.ModulesFolder, "Shared");
            var sharedConfigFileName = Path.Combine(sharedFolder, "shared-configuration.json");

            var startedOn = Stopwatch.StartNew();

            var useAppDomain = !forceCompilation
                && (commandContext.HostConfiguration.DynamicCompilationMode == DynamicCompilationMode.Never
                    || (commandContext.HostConfiguration.DynamicCompilationMode == DynamicCompilationMode.Default && Debugger.IsAttached));

            if (useAppDomain)
            {
                commandContext.Logger.Information("loading plugins directly from AppDomain where namespace ends with '{Module}'", moduleName);
                var appDomainPlugins = LoadInstancesFromAppDomain<IEtlPlugin>(moduleName);
                commandContext.Logger.Debug("finished in {Elapsed}", startedOn.Elapsed);
                module = new CompiledModule()
                {
                    Configuration = moduleConfiguration,
                    Plugins = appDomainPlugins,
                    EnabledPlugins = GetEnabledPlugins(moduleConfiguration, appDomainPlugins),
                    LoadContext = null,
                };

                foreach (var unknownPluginName in GetUnknownPlugins(moduleConfiguration, appDomainPlugins))
                {
                    commandContext.Logger.Warning("unknown plugin '{PluginName}'", unknownPluginName);
                }

                commandContext.Logger.Debug("{PluginCount} plugin(s) found: {PluginNames}",
                    module.EnabledPlugins.Count, module.EnabledPlugins.Select(plugin => plugin.GetType().GetFriendlyTypeName()).ToArray());

                return ExecutionResult.Success;
            }

            commandContext.Logger.Information("compiling plugins from {Folder} using shared files from {SharedFolder}", PathHelpers.GetFriendlyPathName(moduleConfiguration.ModuleFolder), PathHelpers.GetFriendlyPathName(sharedFolder));
            var selfFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            var referenceAssemblyFolder = commandContext.HostConfiguration.CustomReferenceAssemblyFolder;
            if (string.IsNullOrEmpty(referenceAssemblyFolder))
            {
                referenceAssemblyFolder = Directory.GetDirectories(@"c:\Program Files\dotnet\shared\Microsoft.NETCore.App\", "5.*")
                    .OrderByDescending(x => new DirectoryInfo(x).CreationTime)
                    .FirstOrDefault();
            }

            commandContext.Logger.Information("using assemblies from {ReferenceAssemblyFolder}", referenceAssemblyFolder);

            var referenceDllFileNames = new List<string>();
            referenceDllFileNames.AddRange(Directory.GetFiles(referenceAssemblyFolder, "System*.dll", SearchOption.TopDirectoryOnly));
            referenceDllFileNames.AddRange(Directory.GetFiles(referenceAssemblyFolder, "netstandard.dll", SearchOption.TopDirectoryOnly));

            var referenceFileNames = new List<string>();
            referenceFileNames.AddRange(referenceDllFileNames);

            var localDllFileNames = Directory.GetFiles(selfFolder, "*.dll", SearchOption.TopDirectoryOnly);
            referenceFileNames.AddRange(localDllFileNames);

            var metadataReferences = referenceFileNames
                .Distinct()
                .Select(fn => MetadataReference.CreateFromFile(fn))
                .ToArray();

            var csFileNames = Directory.GetFiles(moduleConfiguration.ModuleFolder, "*.cs", SearchOption.AllDirectories);

            if (Directory.Exists(sharedFolder))
            {
                csFileNames = csFileNames
                    .Concat(Directory.GetFiles(sharedFolder, "*.cs", SearchOption.AllDirectories))
                    .ToArray();
            }

            var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp9);
            var syntaxTrees = csFileNames
                .Select(fn => SyntaxFactory.ParseSyntaxTree(SourceText.From(File.ReadAllText(fn)), parseOptions, fn))
                .ToArray();

            using (var assemblyStream = new MemoryStream())
            {
                var id = Interlocked.Increment(ref _moduleAutoincrementId);
                var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);

                var compilation = CSharpCompilation.Create("compiled_" + id.ToString("D", CultureInfo.InvariantCulture) + ".dll", syntaxTrees, metadataReferences, compilationOptions);

                var result = compilation.Emit(assemblyStream);
                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                    foreach (var error in failures)
                    {
                        // DiagnosticFormatter can be used for custom formatting
                        commandContext.Logger.Write(LogEventLevel.Fatal, "syntax error in plugin: {ErrorMessage}", error.ToString());
                        commandContext.OpsLogger.Write(LogEventLevel.Fatal, "syntax error in plugin: {ErrorMessage}", error.GetMessage());
                    }

                    return ExecutionResult.ModuleLoadError;
                }

                assemblyStream.Seek(0, SeekOrigin.Begin);

                var assemblyLoadContext = new AssemblyLoadContext(null, isCollectible: true);
                var assembly = assemblyLoadContext.LoadFromStream(assemblyStream);

                var compiledPlugins = LoadInstancesFromAssembly<IEtlPlugin>(assembly);
                commandContext.Logger.Debug("compilation finished in {Elapsed}", startedOn.Elapsed);
                module = new CompiledModule()
                {
                    Configuration = moduleConfiguration,
                    Plugins = compiledPlugins,
                    EnabledPlugins = GetEnabledPlugins(moduleConfiguration, compiledPlugins),
                    LoadContext = assemblyLoadContext,
                };

                foreach (var unknownPluginName in GetUnknownPlugins(moduleConfiguration, compiledPlugins))
                {
                    commandContext.Logger.Warning("unknown plugin '{PluginName}'", unknownPluginName);
                }

                commandContext.Logger.Debug("{PluginCount} plugin(s) found: {PluginNames}",
                    module.EnabledPlugins.Count, module.EnabledPlugins.Select(plugin => plugin.GetType().GetFriendlyTypeName()).ToArray());

                return ExecutionResult.Success;
            }
        }

        public static void UnloadModule(CommandContext commandContext, CompiledModule module)
        {
            commandContext.Logger.Debug("unloading module {Module}", module.Configuration.ModuleName);

            module.Plugins.Clear();
            module.EnabledPlugins.Clear();

            module.LoadContext?.Unload();
        }

        private static List<IEtlPlugin> GetEnabledPlugins(EtlModuleConfiguration moduleConfiguration, List<IEtlPlugin> plugins)
        {
            if (plugins == null || plugins.Count == 0)
                return new List<IEtlPlugin>();

            return moduleConfiguration.EnabledPluginList
                .Select(enabledName => plugins.Find(plugin => string.Equals(enabledName, plugin.GetType().Name, StringComparison.InvariantCultureIgnoreCase)))
                .Where(plugin => plugin != null)
                .ToList();
        }

        private static List<string> GetUnknownPlugins(EtlModuleConfiguration moduleConfiguration, List<IEtlPlugin> plugins)
        {
            if (plugins == null || plugins.Count == 0)
                return new List<string>();

            return moduleConfiguration.EnabledPluginList
                .Where(enabledName => plugins.Find(plugin => string.Equals(enabledName, plugin.GetType().Name, StringComparison.InvariantCultureIgnoreCase)) == null)
                .ToList();
        }

        private static List<T> LoadInstancesFromAssembly<T>(System.Reflection.Assembly assembly)
        {
            var result = new List<T>();
            var interfaceType = typeof(T);
            foreach (var foundType in assembly.GetTypes().Where(x => interfaceType.IsAssignableFrom(x) && x.IsClass && !x.IsAbstract))
            {
                if (interfaceType.IsAssignableFrom(foundType) && foundType.IsClass && !foundType.IsAbstract)
                {
                    var instance = (T)Activator.CreateInstance(foundType, Array.Empty<object>());
                    if (instance != null)
                        result.Add(instance);
                }
            }

            return result;
        }

        private static List<T> LoadInstancesFromAppDomain<T>(string moduleName)
        {
            var result = new List<T>();
            var interfaceType = typeof(T);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var matchingTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t) && t.Namespace.EndsWith(moduleName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var foundType in matchingTypes)
                {
                    var instance = (T)Activator.CreateInstance(foundType, Array.Empty<object>());
                    if (instance != null)
                        result.Add(instance);
                }
            }

            return result;
        }
    }
}