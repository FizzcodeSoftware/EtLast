﻿using System.Net.Http;

namespace FizzCode.EtLast;
public sealed class HttpDownload : AbstractJob
{
    /// <summary>
    /// According to MSDN, it is recommended to reuse HttpClient instances if possible.
    /// https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
    /// </summary>
    [ProcessParameterNullException]
    public required HttpClient Client { get; init; }

    [ProcessParameterNullException]
    public required string Url { get; init; }

    [ProcessParameterNullException]
    public required MemoryStream OutputStream { get; init; }

    public HttpDownload(IEtlContext context)
        : base(context)
    {
    }

    protected override void ExecuteImpl(Stopwatch netTimeStopwatch)
    {
        var iocUid = 0L;
        try
        {
            using (Context.CancellationToken.Register(Client.CancelPendingRequests))
            {
                iocUid = Context.RegisterIoCommandStartWithLocation(this, IoCommandKind.httpGet, Url, null, "GET", null, null,
                    "downloading file", null);

                var initialSize = OutputStream.Length;
                using (var response = Client.GetStreamAsync(Url).Result)
                    response.CopyTo(OutputStream);
                var downloadedBytes = OutputStream.Length - initialSize;

                Context.RegisterIoCommandSuccess(this, IoCommandKind.httpGet, iocUid, downloadedBytes);
            }
        }
        catch (Exception ex)
        {
            var exception = new HttpDownloadException(this, "http download failed", ex);
            exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "http download failed, url: {0}, message: {1}",
                Url, ex.Message));
            exception.Data["Url"] = Url;

            Context.RegisterIoCommandFailed(this, IoCommandKind.httpGet, iocUid, null, exception);
            throw exception;
        }
    }
}

[Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
public static class HttpDownloadFluent
{
    public static IFlow HttpDownload(this IFlow builder, Func<HttpDownload> processCreator)
    {
        return builder.ExecuteProcess(processCreator);
    }
}