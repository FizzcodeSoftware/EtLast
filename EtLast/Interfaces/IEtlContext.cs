﻿namespace FizzCode.EtLast;

public interface IEtlContext : ICaller
{
    public void SetRowType<T>() where T : IRow;

    public ArgumentCollection Arguments { get; }
    public T Service<T>() where T : IEtlService, new();
    public AdditionalData AdditionalData { get; }

    public void RegisterScopeAction(ScopeAction action);
    public ScopeAction[] GetScopeActions();

    public long Id { get; }
    public string Name { get; }
    public DateTimeOffset CreatedOnUtc { get; }
    public DateTimeOffset CreatedOnLocal { get; }

    public int ElapsedMillisecondsLimitToLog { get; set; }

    public TimeSpan TransactionScopeTimeout { get; set; }
    public EtlTransactionScope BeginTransactionScope(IProcess process, TransactionScopeKind kind, LogSeverity logSeverity, TimeSpan? timeoutOverride = null);

    public void Terminate();
    public bool IsTerminating { get; }
    public CancellationToken CancellationToken { get; }

    public ContextManifest Manifest { get; }
    public List<IEtlContextListener> Listeners { get; }

    public IRow CreateRow(IProcess process);
    public IRow CreateRow(IProcess process, IEnumerable<KeyValuePair<string, object>> initialValues);
    public IRow CreateRow(IProcess process, IReadOnlySlimRow source);

    public void Log(string transactionId, LogSeverity severity, IProcess process, string text, params object[] args);
    public void Log(LogSeverity severity, IProcess process, string text, params object[] args);
    public void LogOps(LogSeverity severity, IProcess process, string text, params object[] args);

    public void LogCustom(string fileName, IProcess process, string text, params object[] args);
    public void LogCustomOps(string fileName, IProcess process, string text, params object[] args);

    public IoCommand RegisterIoCommandStart(IProcess process, IoCommand ioCommand);
    public void RegisterIoCommandEnd(IProcess process, IoCommand ioCommand);

    public void SetRowOwner(IRow row, IProcess currentProcess);

    public void RegisterProcessInvocationStart(IProcess process, ICaller caller);
    public void RegisterProcessInvocationEnd(IProcess process);
    public void RegisterProcessInvocationEnd(IProcess process, long netElapsedMilliseconds);

    public Sink GetSink(string location, string path, string sinkFormat, Type sinkWriter);
    public void RegisterWriteToSink(IReadOnlyRow row, Sink sink);

    public void Close();
    public void StopServices();
}