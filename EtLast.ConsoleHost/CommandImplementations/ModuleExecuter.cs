﻿namespace FizzCode.EtLast.ConsoleHost;

internal static class ModuleExecuter
{
    public static IExecutionResult Execute(Host host, CompiledModule module, string[] taskNames)
    {
        var executionResult = new ExecutionResult
        {
            TaskResults = new List<TaskExectionResult>(),
        };

        var instance = Environment.MachineName;
        var arguments = new ArgumentCollection(module.DefaultArgumentProviders, module.InstanceArgumentProviders, instance);

        var environmentSettings = new EnvironmentSettings();
        Dictionary<string, Func<IArgumentCollection, IEtlTask>> customTasks;
        if (module.Startup != null)
        {
            module.Startup.Configure(environmentSettings);
            customTasks = new Dictionary<string, Func<IArgumentCollection, IEtlTask>>(module.Startup.CustomTasks, StringComparer.InvariantCultureIgnoreCase);
        }
        else
        {
            customTasks = new Dictionary<string, Func<IArgumentCollection, IEtlTask>>();
        }

        var context = new EtlContext(arguments)
        {
            TransactionScopeTimeout = environmentSettings.TransactionScopeTimeout
        };

        try
        {
            if (host.EtlContextListeners?.Count > 0)
            {
                foreach (var listenerCreator in host.EtlContextListeners)
                {
                    context.Listeners.Add(listenerCreator.Invoke(context));
                }
            }
        }
        catch (Exception ex)
        {
            var formattedMessage = ex.FormatExceptionWithDetails();
            context.Log(LogSeverity.Fatal, null, "{ErrorMessage}", formattedMessage);
            context.LogOps(LogSeverity.Fatal, null, "{ErrorMessage}", formattedMessage);
        }

        if (host.SerilogForModulesEnabled)
        {
            if (environmentSettings.FileLogSettings.Enabled || environmentSettings.ConsoleLogSettings.Enabled || !string.IsNullOrEmpty(environmentSettings.SeqSettings.Url))
            {
                var serilogAdapter = new EtlSessionSerilogAdapter(environmentSettings, host.DevLogFolder, host.OpsLogFolder);
                context.Listeners.Add(serilogAdapter);
            }
        }

        if (module.Startup == null)
        {
            context.Log(LogSeverity.Warning, null, "Can't find a startup class implementing " + nameof(IStartup) + ".");
        }

        context.Log(LogSeverity.Information, null, "context {ContextUId} started", context.Uid);

        if (!string.IsNullOrEmpty(environmentSettings.SeqSettings.Url))
        {
            context.Log(LogSeverity.Debug, null, "all context logs will be sent to SEQ listening on {SeqUrl}", environmentSettings.SeqSettings.Url);
        }

        var sessionStartedOn = Stopwatch.StartNew();

        var taskResults = new List<TaskExectionResult>();

        var pipe = new Pipe(context);

        try
        {
            foreach (var taskName in taskNames)
            {
                if (pipe.IsTerminating)
                    break;

                IEtlTask task = null;
                if (customTasks.TryGetValue(taskName, out var taskCreator))
                {
                    task = taskCreator.Invoke(arguments);
                }
                else
                {
                    var taskType = module.TaskTypes.Find(x => string.Equals(x.Name, taskName, StringComparison.InvariantCultureIgnoreCase));
                    if (taskType != null)
                        task = (IEtlTask)Activator.CreateInstance(taskType);
                }

                if (task == null)
                {
                    context.Log(LogSeverity.Error, null, "unknown task/flow type: " + taskName);
                    break;
                }

                try
                {
                    task.SetContext(context, true);
                    task.Execute(null, pipe);

                    taskResults.Add(new TaskExectionResult(task));
                    executionResult.TaskResults.Add(new TaskExectionResult(task));

                    if (pipe.Failed)
                    {
                        context.Log(LogSeverity.Error, task, "failed, terminating execution");
                        executionResult.Status = ExecutionStatusCode.ExecutionFailed;
                        context.Close();
                        break; // stop processing tasks
                    }
                }
                catch (Exception ex)
                {
                    context.Log(LogSeverity.Error, task, "failed, terminating execution, reason: {0}", ex.Message);
                    executionResult.Status = ExecutionStatusCode.ExecutionFailed;
                    context.Close();
                    break; // stop processing tasks
                }

                LogTaskCounters(context, task);
            }

            context.StopServices();

            if (taskResults.Count > 0)
            {
                context.Log(LogSeverity.Information, null, "-------");
                context.Log(LogSeverity.Information, null, "SUMMARY");
                context.Log(LogSeverity.Information, null, "-------");

                var longestTaskName = taskResults.Max(x => x.TaskName.Length);
                foreach (var taskResult in taskResults)
                {
                    LogTaskSummary(context, taskResult, longestTaskName);
                }
            }

            context.Close();
        }
        catch (TransactionAbortedException)
        {
        }

        return executionResult;
    }

    private static void LogTaskCounters(IEtlContext context, IEtlTask task)
    {
        if (task.IoCommandCounters.Count == 0)
            return;

        const string kind = "kind";
        const string invocation = "invoc.";
        const string affected = "affected";

        var maxKeyLength = Math.Max(kind.Length, task.IoCommandCounters.Max(x => x.Key.ToString().Length));
        var maxInvocationLength = Math.Max(invocation.Length, task.IoCommandCounters.Max(x => x.Value.InvocationCount.ToString(SerilogSink.ValueFormatter.DefaultIntegerFormat, CultureInfo.InvariantCulture).Length));

        context.Log(LogSeverity.Debug, task, "{Kind}{spacing1} {InvocationCount}{spacing2}   {AffectedDataCount}", kind,
            "".PadRight(maxKeyLength - kind.Length, ' '),
            invocation,
            "".PadRight(maxInvocationLength - invocation.Length, ' '),
            affected);

        foreach (var kvp in task.IoCommandCounters.OrderBy(kvp => kvp.Key.ToString()))
        {
            if (kvp.Value.AffectedDataCount != null)
            {
                context.Log(LogSeverity.Debug, task, "{Kind}{spacing1} {InvocationCount}{spacing2}   {AffectedDataCount}", kvp.Key.ToString(),
                    "".PadRight(maxKeyLength - kvp.Key.ToString().Length, ' '),
                    kvp.Value.InvocationCount,
                    "".PadRight(maxInvocationLength - kvp.Value.InvocationCount.ToString(SerilogSink.ValueFormatter.DefaultIntegerFormat, CultureInfo.InvariantCulture).Length, ' '),
                    kvp.Value.AffectedDataCount);
            }
            else
            {
                context.Log(LogSeverity.Debug, task, "{Kind}{spacing1} {InvocationCount}", kvp.Key.ToString(),
                    "".PadRight(maxKeyLength - kvp.Key.ToString().Length, ' '), kvp.Value.InvocationCount);
            }
        }
    }

    private static void LogTaskSummary(IEtlContext context, TaskExectionResult result, int longestTaskName)
    {
        var spacing1 = "".PadRight(longestTaskName - result.TaskName.Length);
        var spacing1WithoutName = "".PadRight(longestTaskName);

        if (result.Exceptions.Count == 0)
        {
            context.Log(LogSeverity.Information, null, "{Task}{spacing1} run-time is {Elapsed}, result is {Result}, CPU time: {CpuTime}, total allocations: {AllocatedMemory}, allocation difference: {MemoryDifference}",
                result.TaskName, spacing1, result.Statistics.RunTime, "success", result.Statistics.CpuTime, result.Statistics.TotalAllocations, result.Statistics.AllocationDifference);
        }
        else
        {
            context.Log(LogSeverity.Information, null, "{Task}{spacing1} run-time is {Elapsed}, result is {Result}, CPU time: {CpuTime}, total allocations: {AllocatedMemory}, allocation difference: {MemoryDifference}",
                result.TaskName, spacing1, result.Statistics.RunTime, "failed", result.Statistics.CpuTime, result.Statistics.TotalAllocations, result.Statistics.AllocationDifference);
        }

        if (result.IoCommandCounters.Count > 0)
        {
            var maxKeyLength = result.IoCommandCounters.Max(x => x.Key.ToString().Length);
            var maxInvocationLength = result.IoCommandCounters.Max(x => x.Value.InvocationCount.ToString(SerilogSink.ValueFormatter.DefaultIntegerFormat, CultureInfo.InvariantCulture).Length);

            foreach (var kvp in result.IoCommandCounters.OrderBy(kvp => kvp.Key.ToString()))
            {
                if (kvp.Value.AffectedDataCount != null)
                {
                    context.Log(LogSeverity.Information, null, "{spacing1} {Kind}{spacing2} {InvocationCount}{spacing3}   {AffectedDataCount}", spacing1WithoutName,
                        kvp.Key.ToString(),
                        "".PadRight(maxKeyLength - kvp.Key.ToString().Length, ' '),
                        kvp.Value.InvocationCount,
                        "".PadRight(maxInvocationLength - kvp.Value.InvocationCount.ToString(SerilogSink.ValueFormatter.DefaultIntegerFormat, CultureInfo.InvariantCulture).Length, ' '),
                        kvp.Value.AffectedDataCount);
                }
                else
                {
                    context.Log(LogSeverity.Information, null, "{spacing1} {Kind}{spacing2} {InvocationCount}", spacing1WithoutName,
                        kvp.Key.ToString(),
                        "".PadRight(maxKeyLength - kvp.Key.ToString().Length, ' '),
                        kvp.Value.InvocationCount);
                }
            }
        }
    }
}
