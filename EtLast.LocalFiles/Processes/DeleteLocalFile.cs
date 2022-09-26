﻿namespace FizzCode.EtLast;

public sealed class DeleteLocalFile : AbstractJob
{
    public string FileName { get; init; }

    public DeleteLocalFile(IEtlContext context)
        : base(context)
    {
    }

    public override void ValidateParameters()
    {
        if (string.IsNullOrEmpty(FileName))
            throw new ProcessParameterNullException(this, nameof(FileName));
    }

    protected override void ExecuteImpl(Stopwatch netTimeStopwatch)
    {
        if (!File.Exists(FileName))
        {
            Context.Log(LogSeverity.Debug, this, "can't delete local file because it doesn't exist '{FileName}'", FileName);
            return;
        }

        Context.Log(LogSeverity.Information, this, "deleting local file '{FileName}'", FileName);

        try
        {
            File.Delete(FileName);
            Context.Log(LogSeverity.Debug, this, "successfully deleted local file '{FileName}' in {Elapsed}", FileName,
                InvocationInfo.LastInvocationStarted.Elapsed);
        }
        catch (Exception ex)
        {
            var exception = new LocalFileDeleteException(this, "local file deletion failed", FileName, ex);
            exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "local file deletion failed, file name: {0}, message: {1}",
                FileName, ex.Message));
            throw exception;
        }
    }
}
