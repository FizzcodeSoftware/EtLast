﻿using System.Net.Http;

namespace FizzCode.EtLast;

public sealed class HttpDownloadToLocalFile : AbstractJob
{
    /// <summary>
    /// According to MSDN, it is recommended to reuse HttpClient instances if possible.
    /// https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines
    /// </summary>
    [ProcessParameterMustHaveValue]
    public required HttpClient Client { get; init; }

    [ProcessParameterMustHaveValue]
    public required string Url { get; init; }

    [ProcessParameterMustHaveValue]
    public required string OutputFileName { get; init; }

    /// <summary>
    /// Default false.
    /// </summary>
    public bool SkipIfTargetFileExistsButLarger { get; init; }

    public Dictionary<string, string> Headers { get; set; }

    protected override void ExecuteImpl(Stopwatch netTimeStopwatch)
    {
        var directory = Path.GetDirectoryName(OutputFileName);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                var exception = new HttpDownloadToLocalFileException(this, "http download to local file failed / directory creation failed", ex);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "http download to local file failed: {0}, directory creation failed, message: {1}",
                    OutputFileName, ex.Message));
                exception.Data["FileName"] = OutputFileName;
                exception.Data["Directory"] = directory;
                throw exception;
            }
        }

        IoCommand ioCommandHttpGet = null;
        IoCommand ioCommandFileWrite = null;
        try
        {
            using (Context.CancellationToken.Register(Client.CancelPendingRequests))
            {
                ioCommandHttpGet = Context.RegisterIoCommand(new IoCommand()
                {
                    Process = this,
                    Kind = IoCommandKind.httpGet,
                    Location = Url,
                    Command = "GET",
                    Message = "downloading file",
                });

                ioCommandFileWrite = Context.RegisterIoCommand(new IoCommand()
                {
                    Process = this,
                    Kind = IoCommandKind.fileWrite,
                    Location = OutputFileName,
                    Message = "writing downloaded content to file"
                });

                var message = new HttpRequestMessage(HttpMethod.Get, Url);
                if (Headers != null)
                {
                    foreach (var kvp in Headers)
                    {
                        message.Headers.Add(kvp.Key, kvp.Value);
                    }
                }

                using (var response = Client.SendAsync(message).Result)
                {
                    response.EnsureSuccessStatusCode();
                    using (var responseStream = response.Content.ReadAsStream())
                    {
                        if (!SkipIfTargetFileExistsButLarger || !File.Exists(OutputFileName))
                        {
                            using (var fileStream = new FileStream(OutputFileName, FileMode.Create))
                            {
                                responseStream.CopyTo(fileStream);
                            }
                        }
                        else
                        {
                            var existingLength = new FileInfo(OutputFileName).Length;
                            using (var ms = new MemoryStream())
                            {
                                responseStream.CopyTo(ms);
                                if (existingLength <= ms.Length)
                                {
                                    using (var fileStream = new FileStream(OutputFileName, FileMode.Create))
                                    {
                                        ms.Position = 0;
                                        ms.CopyTo(fileStream);
                                    }
                                }
                            }
                        }
                    }
                }

                var size = new FileInfo(OutputFileName).Length;
                ioCommandHttpGet.AffectedDataCount += size;
                ioCommandFileWrite.AffectedDataCount += size;

                ioCommandHttpGet.End();
                ioCommandFileWrite.End();
            }
        }
        catch (Exception ex)
        {
            var exception = new HttpDownloadToLocalFileException(this, "http download to local file failed", ex);
            exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "http download to local file failed, url: {0}, file name: {1}, message: {2}",
                Url, OutputFileName, ex.Message));
            exception.Data["Url"] = Url;
            exception.Data["FileName"] = OutputFileName;

            ioCommandHttpGet.Failed(exception);
            ioCommandFileWrite.Failed(exception);
            throw exception;
        }
    }
}

[EditorBrowsable(EditorBrowsableState.Never)]
public static class HttpDownloadToLocalFileFluent
{
    public static IFlow HttpDownloadToLocalFile(this IFlow builder, Func<HttpDownloadToLocalFile> processCreator)
    {
        return builder.ExecuteProcess(processCreator);
    }
}