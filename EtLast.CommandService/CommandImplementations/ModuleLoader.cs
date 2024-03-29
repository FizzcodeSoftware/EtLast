﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace FizzCode.EtLast;

internal static class ModuleLoader
{
    private static long _moduleAutoincrementId;

    public static ExecutionStatusCode LoadModule(CommandService host, string moduleName, ModuleCompilationMode compilationMode, out CompiledModule module)
    {
        module = null;

        var moduleDirectory = Path.Combine(host.ModulesDirectory, moduleName);
        if (!Directory.Exists(moduleDirectory))
        {
            host.Logger.Write(LogEventLevel.Fatal, "can't find the module directory: {Directory}", moduleDirectory);
            return ExecutionStatusCode.ModuleLoadError;
        }

        // read back actual directory name casing
        moduleDirectory = Directory
            .GetDirectories(host.ModulesDirectory, moduleName, SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        moduleName = Path.GetFileName(moduleDirectory);

        var startedOn = Stopwatch.StartNew();

        var useAppDomain = (compilationMode == ModuleCompilationMode.ForceAppDomain) || (compilationMode == ModuleCompilationMode.Dynamic && Debugger.IsAttached);

        if (useAppDomain)
        {
            host.Logger.Information("loading module directly from AppDomain where namespace ends with '{Module}'", moduleName);

            ForceLoadLocalDllsToAppDomain();

            var appDomainTasks = FindTypesFromAppDomain<IEtlTask>(moduleName);
            var startup = LoadInstancesFromAppDomain<IStartup>(moduleName).FirstOrDefault();
            var instanceConfigurationProviders = LoadInstancesFromAppDomain<IInstanceArgumentProvider>(moduleName);
            var defaultConfigurationProviders = LoadInstancesFromAppDomain<IDefaultArgumentProvider>(moduleName);
            host.Logger.Debug("finished in {Elapsed}", startedOn.Elapsed);

            module = new CompiledModule()
            {
                Name = moduleName,
                Directory = moduleDirectory,
                Startup = startup,
                InstanceArgumentProviders = instanceConfigurationProviders,
                DefaultArgumentProviders = defaultConfigurationProviders,
                TaskTypes = appDomainTasks.Where(x => x.Name != null).ToList(),
                LoadContext = null,
            };

            var tasks = module.TaskTypes.Where(x => x.IsAssignableTo(typeof(AbstractEtlTask)));

            host.Logger.Debug("{TaskCount} task(s) found: {Task}",
                tasks.Count(), tasks.Select(task => task.Name).ToArray());

            return ExecutionStatusCode.Success;
        }

        host.Logger.Information("compiling module from {Directory}", PathHelpers.GetFriendlyPathName(moduleDirectory));

        var metadataReferences = host.GetReferenceAssemblyFilePaths()
            .Select(fn => MetadataReference.CreateFromFile(fn))
            .ToArray();

        var csFileNames = Directory.GetFiles(moduleDirectory, "*.cs", SearchOption.AllDirectories).ToList();
        var globalCsFileName = Path.Combine(host.ModulesDirectory, "Global.cs");
        if (File.Exists(globalCsFileName))
            csFileNames.Add(globalCsFileName);

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
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
                    host.Logger.Write(LogEventLevel.Fatal, "syntax error in module: {ErrorMessage}", error.ToString());
                }

                return ExecutionStatusCode.ModuleLoadError;
            }

            assemblyStream.Seek(0, SeekOrigin.Begin);

            var assemblyLoadContext = new AssemblyLoadContext(null, isCollectible: true);
            var assembly = assemblyLoadContext.LoadFromStream(assemblyStream);

            var compiledTasks = FindTypesFromAssembly<IEtlTask>(assembly);
            var compiledStartup = LoadInstancesFromAssembly<IStartup>(assembly).FirstOrDefault();
            var instanceConfigurationProviders = LoadInstancesFromAssembly<IInstanceArgumentProvider>(assembly);
            var defaultConfigurationProviders = LoadInstancesFromAssembly<IDefaultArgumentProvider>(assembly);
            host.Logger.Debug("compilation finished in {Elapsed}", startedOn.Elapsed);

            module = new CompiledModule()
            {
                Name = moduleName,
                Directory = moduleDirectory,
                Startup = compiledStartup,
                InstanceArgumentProviders = instanceConfigurationProviders,
                DefaultArgumentProviders = defaultConfigurationProviders,
                TaskTypes = compiledTasks.Where(x => x.Name != null).ToList(),
                LoadContext = assemblyLoadContext,
            };

            var tasks = module.TaskTypes.Where(x => x.IsAssignableTo(typeof(AbstractEtlTask)));

            host.Logger.Debug("{TaskCount} task(s) found: {Task}",
                tasks.Count(), tasks.Select(task => task.Name).ToArray());

            return ExecutionStatusCode.Success;
        }
    }

    private static void ForceLoadLocalDllsToAppDomain()
    {
        var selfDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var localDllFileNames = Directory.GetFiles(selfDirectory, "*.dll", SearchOption.TopDirectoryOnly)
            .Where(x =>
            {
                var fn = Path.GetFileName(x);
                if (fn.Equals("testhost.dll", StringComparison.InvariantCultureIgnoreCase))
                    return false;

                if (fn.StartsWith("Microsoft", StringComparison.InvariantCultureIgnoreCase))
                    return false;

                if (fn.StartsWith("System", StringComparison.InvariantCultureIgnoreCase))
                    return false;

                return true;
            });

        foreach (var fn in localDllFileNames)
        {
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic);
            var match = false;
            foreach (var loadedAssembly in loadedAssemblies)
            {
                try
                {
                    if (string.Equals(fn, loadedAssembly.Location, StringComparison.InvariantCultureIgnoreCase))
                    {
                        match = true;
                        break;
                    }
                }
                catch (Exception)
                {
                }
            }

            if (!match)
            {
                Debug.WriteLine("loading " + fn);
                try
                {
                    Assembly.LoadFile(fn);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    public static void UnloadModule(CommandService host, CompiledModule module)
    {
        host.Logger.Debug("unloading module {Module}", module.Name);

        module.TaskTypes.Clear();

        module.LoadContext?.Unload();
    }

    private static List<T> LoadInstancesFromAssembly<T>(Assembly assembly)
    {
        var result = new List<T>();
        var interfaceType = typeof(T);
        foreach (var foundType in assembly.GetTypes().Where(x => interfaceType.IsAssignableFrom(x) && x.IsClass && !x.IsAbstract))
        {
            var instance = (T)Activator.CreateInstance(foundType, []);
            if (instance != null)
                result.Add(instance);
        }

        return result;
    }

    private static List<T> LoadInstancesFromAppDomain<T>(string moduleName)
    {
        var result = new List<T>();
        var interfaceType = typeof(T);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var matchingTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t) && t.Namespace.EndsWith(moduleName, StringComparison.OrdinalIgnoreCase));

                foreach (var foundType in matchingTypes)
                {
                    var instance = (T)Activator.CreateInstance(foundType, []);
                    if (instance != null)
                        result.Add(instance);
                }
            }
            catch (Exception) { }
        }

        return result;
    }

    private static List<Type> FindTypesFromAssembly<T>(Assembly assembly)
    {
        var interfaceType = typeof(T);
        return assembly.GetTypes()
            .Where(x => interfaceType.IsAssignableFrom(x) && x.IsClass && !x.IsAbstract)
            .ToList();
    }

    private static List<Type> FindTypesFromAppDomain<T>(string moduleName)
    {
        var result = new List<Type>();
        var interfaceType = typeof(T);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var matchingTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && interfaceType.IsAssignableFrom(t) && t.Namespace.EndsWith(moduleName, StringComparison.OrdinalIgnoreCase));

                result.AddRange(matchingTypes);
            }
            catch (Exception) { }
        }

        return result;
    }
}