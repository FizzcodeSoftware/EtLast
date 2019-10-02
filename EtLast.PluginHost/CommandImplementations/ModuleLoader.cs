﻿namespace FizzCode.EtLast.PluginHost
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Loader;
    using System.Threading;
    using FizzCode.EtLast;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Text;
    using Serilog.Events;

    internal static class ModuleLoader
    {
        private static long _moduleAutoincrementId = 0;

        public static Module LoadModule(CommandContext commandContext, string moduleName, string[] moduleSettingOverrides, string[] pluginListOverride)
        {
            var moduleConfiguration = ModuleConfigurationLoader.LoadModuleConfiguration(commandContext, moduleName, moduleSettingOverrides, pluginListOverride);
            if (moduleConfiguration == null)
                return null;

            var sharedFolder = Path.Combine(commandContext.HostConfiguration.ModulesFolder, "Shared");
            var sharedConfigFileName = Path.Combine(sharedFolder, "shared-configuration.json");

            var startedOn = Stopwatch.StartNew();

            if (!commandContext.HostConfiguration.ForceDynamicCompilation
                && (!commandContext.HostConfiguration.EnableDynamicCompilation || Debugger.IsAttached))
            {
                commandContext.Logger.Write(LogEventLevel.Information, "loading plugins directly from AppDomain if namespace ends with {ModuleName}", moduleName);
                var appDomainPlugins = LoadPluginsFromAppDomain(moduleName);
                commandContext.Logger.Write(LogEventLevel.Debug, "finished in {Elapsed}", startedOn.Elapsed);
                var module = new Module()
                {
                    ModuleConfiguration = moduleConfiguration,
                    Plugins = appDomainPlugins,
                    EnabledPlugins = FilterExecutablePlugins(moduleConfiguration, appDomainPlugins),
                };

                commandContext.Logger.Write(LogEventLevel.Debug, "{PluginCount} plugin(s) found: {PluginNames}",
                    module.EnabledPlugins.Count, module.EnabledPlugins.Select(plugin => TypeHelpers.GetFriendlyTypeName(plugin.GetType())).ToArray());

                return module;
            }

            commandContext.Logger.Write(LogEventLevel.Information, "compiling plugins from {ModuleFolder} using shared files from {SharedFolder}", PathHelpers.GetFriendlyPathName(moduleConfiguration.ModuleFolder), PathHelpers.GetFriendlyPathName(sharedFolder));
            var selfFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var referenceAssemblyFolder = @"c:\Program Files\dotnet\shared\Microsoft.NETCore.App\3.0.0";
            var referenceAssemblyPattern = "System*.dll";
            commandContext.Logger.Write(LogEventLevel.Information, "using reference assemblies from {ReferenceAssemblyFolder} using pattern: {ReferenceAssemblyPattern}", referenceAssemblyFolder, referenceAssemblyPattern);
            var referenceDllFileNames = Directory.GetFiles(referenceAssemblyFolder, referenceAssemblyPattern, SearchOption.TopDirectoryOnly);

            var referenceFileNames = new List<string>();
            referenceFileNames.AddRange(referenceDllFileNames);

            var localDllFileNames = Directory.GetFiles(selfFolder, "*.dll", SearchOption.TopDirectoryOnly);
            referenceFileNames.AddRange(localDllFileNames);

            var metadataReferences = referenceFileNames.Distinct().Select(fn => MetadataReference.CreateFromFile(fn)).ToArray();

            var csFileNames = Directory.GetFiles(moduleConfiguration.ModuleFolder, "*.cs", SearchOption.AllDirectories);

            if (Directory.Exists(sharedFolder))
            {
                csFileNames = csFileNames
                    .Concat(Directory.GetFiles(sharedFolder, "*.cs", SearchOption.AllDirectories))
                    .ToArray();
            }

            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp8);

            var syntaxTrees = csFileNames
                .Select(fn => SyntaxFactory.ParseSyntaxTree(SourceText.From(File.ReadAllText(fn)), options, fn))
                .ToArray();

            using (var assemblyStream = new MemoryStream())
            {
                var id = Interlocked.Increment(ref _moduleAutoincrementId);
                var compilation = CSharpCompilation.Create("compiled_" + id.ToString("D", CultureInfo.InvariantCulture) + ".dll", syntaxTrees, metadataReferences, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));

                var result = compilation.Emit(assemblyStream);
                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);
                    foreach (var error in failures)
                    {
                        // DiagnosticFormatter can be used for custom formatting
                        commandContext.Logger.Write(LogEventLevel.Error, "syntax error in plugin: {Message}", error.ToString());
                        commandContext.OpsLogger.Write(LogEventLevel.Error, "syntax error in plugin: {Message}", error.GetMessage());
                    }

                    return null;
                }

                assemblyStream.Seek(0, SeekOrigin.Begin);

                var assemblyLoadContext = new AssemblyLoadContext("loader", false);
                var assembly = assemblyLoadContext.LoadFromStream(assemblyStream);

                var compiledPlugins = LoadPluginsFromAssembly(assembly);
                commandContext.Logger.Write(LogEventLevel.Debug, "finished in {Elapsed}", startedOn.Elapsed);
                var module = new Module()
                {
                    ModuleConfiguration = moduleConfiguration,
                    Plugins = compiledPlugins,
                    EnabledPlugins = FilterExecutablePlugins(moduleConfiguration, compiledPlugins),
                };

                commandContext.Logger.Write(LogEventLevel.Debug, "{PluginCount} plugin(s) found: {PluginNames}",
                    module.EnabledPlugins.Count, module.EnabledPlugins.Select(plugin => TypeHelpers.GetFriendlyTypeName(plugin.GetType())).ToArray());

                return module;
            }
        }

        public static void UnloadModule(CommandContext commandContext, Module module)
        {
            commandContext.Logger.Write(LogEventLevel.Debug, "unloading module {ModuleName}", module.ModuleConfiguration.ModuleName);

            module.ModuleConfiguration = null;
            module.Plugins = null;
            module.EnabledPlugins = null;
        }

        private static List<IEtlPlugin> FilterExecutablePlugins(ModuleConfiguration moduleConfiguration, List<IEtlPlugin> plugins)
        {
            if (plugins == null || plugins.Count == 0)
                return new List<IEtlPlugin>();

            return plugins
                .Where(plugin => moduleConfiguration.EnabledPluginList.Contains(plugin.GetType().Name))
                .ToList();
        }

        private static List<IEtlPlugin> LoadPluginsFromAssembly(Assembly assembly)
        {
            var result = new List<IEtlPlugin>();
            var pluginInterfaceType = typeof(IEtlPlugin);
            foreach (var foundType in assembly.GetTypes().Where(x => pluginInterfaceType.IsAssignableFrom(x) && x.IsClass && !x.IsAbstract))
            {
                if (pluginInterfaceType.IsAssignableFrom(foundType) && foundType.IsClass && !foundType.IsAbstract)
                {
                    var plugin = (IEtlPlugin)Activator.CreateInstance(foundType, Array.Empty<object>());
                    if (plugin != null)
                    {
                        result.Add(plugin);
                    }
                }
            }

            return result;
        }

        private static List<IEtlPlugin> LoadPluginsFromAppDomain(string moduleName)
        {
            var plugins = new List<IEtlPlugin>();
            var pluginInterfaceType = typeof(IEtlPlugin);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var matchingTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && pluginInterfaceType.IsAssignableFrom(t) && t.Namespace.EndsWith(moduleName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var foundType in matchingTypes)
                {
                    var plugin = (IEtlPlugin)Activator.CreateInstance(foundType, Array.Empty<object>());
                    if (plugin != null)
                    {
                        plugins.Add(plugin);
                    }
                }
            }

            return plugins;
        }
    }
}