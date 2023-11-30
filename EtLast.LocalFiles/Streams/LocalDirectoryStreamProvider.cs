﻿namespace FizzCode.EtLast;

[ContainsProcessParameterValidation]
public class LocalDirectoryStreamProvider : IStreamProvider
{
    [ProcessParameterMustHaveValue]
    public required string Directory { get; init; }

    /// <summary>
    /// Default value is "*.*"
    /// </summary>
    [ProcessParameterMustHaveValue]
    public required string SearchPattern { get; init; } = "*.*";

    /// <summary>
    /// Default value is true.
    /// </summary>
    public bool ThrowExceptionWhenFileNotFound { get; init; } = true;

    public string GetTopic()
    {
        return Directory != null
            ? PathHelpers.GetFriendlyPathName(Directory)
                + (SearchPattern != null ? @"\" + SearchPattern : "")
            : null;
    }

    public IEnumerable<NamedStream> GetStreams(IProcess caller)
    {
        var fileNames = new List<string>();

        if (System.IO.Directory.Exists(Directory))
        {
            fileNames.AddRange(System.IO.Directory.EnumerateFiles(Directory, SearchPattern));
        }

        if (fileNames.Count == 0)
        {
            if (ThrowExceptionWhenFileNotFound)
            {
                var exception = new LocalFileReadException(caller, "local directory doesn't contain any matching files", Directory);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "local directory doesn't contain any matching files: {0}",
                    Directory));

                throw exception;
            }

            yield break;
        }

        foreach (var fileName in fileNames)
        {
            yield return GetFileStream(caller, fileName);
        }
    }

    private NamedStream GetFileStream(IProcess caller, string fileName)
    {
        var ioCommand = caller.Context.RegisterIoCommandStart(new IoCommand()
        {
            Process = caller,
            Kind = IoCommandKind.fileRead,
            Location = Directory,
            Path = fileName.Replace(Directory, "", StringComparison.InvariantCultureIgnoreCase),
            Message = "reading from local file",
        });

        if (!File.Exists(fileName))
        {
            if (ThrowExceptionWhenFileNotFound)
            {
                var exception = new LocalFileReadException(caller, "local file doesn't exist", fileName);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "local file doesn't exist: {0}",
                    fileName));

                ioCommand.AffectedDataCount = 0;
                ioCommand.Failed(exception);
                throw exception;
            }

            ioCommand.AffectedDataCount = 0;
            ioCommand.End();
            return null;
        }

        try
        {
            var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new NamedStream(fileName, stream, ioCommand);
        }
        catch (Exception ex)
        {
            var exception = new LocalFileReadException(caller, "error while opening local file", fileName, ex);
            exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "error while opening local file: {0}, message: {1}", fileName, ex.Message));
            exception.Data["FileName"] = fileName;

            ioCommand.Failed(exception);
            throw exception;
        }
    }
}
