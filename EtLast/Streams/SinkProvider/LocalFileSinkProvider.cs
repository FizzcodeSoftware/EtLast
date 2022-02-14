﻿namespace FizzCode.EtLast
{
    using System;
    using System.Globalization;
    using System.IO;

    public class LocalFileSinkProvider : ISinkProvider
    {
        public string FileName { get; init; }

        /// <summary>
        /// Default value is true.
        /// </summary>
        public bool ThrowExceptionWhenFileExists { get; init; } = true;

        public string Topic => FileName != null ? PathHelpers.GetFriendlyPathName(FileName) : null;

        public bool AutomaticallyDispose => true;

        public NamedSink GetSink(IProcess caller)
        {
            var iocUid = caller.Context.RegisterIoCommandStart(caller, IoCommandKind.fileWrite, PathHelpers.GetFriendlyPathName(FileName), null, null, null, null,
                "writing to file {FileName}", PathHelpers.GetFriendlyPathName(FileName));

            if (File.Exists(FileName) && ThrowExceptionWhenFileExists)
            {
                var exception = new FileWriteException(caller, "output file already exist", FileName);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "output file already exist: {0}",
                    FileName));

                caller.Context.RegisterIoCommandFailed(caller, IoCommandKind.fileWrite, iocUid, 0, exception);
                throw exception;
            }

            try
            {
                var sinkUid = caller.Context.GetSinkUid(Path.GetDirectoryName(FileName), Path.GetFileName(FileName));

                var stream = new FileStream(FileName, FileMode.Append, FileAccess.Write, FileShare.Read);
                return new NamedSink(FileName, stream, iocUid, IoCommandKind.fileWrite, sinkUid);
            }
            catch (Exception ex)
            {
                caller.Context.RegisterIoCommandFailed(caller, IoCommandKind.fileWrite, iocUid, null, ex);

                var exception = new EtlException(caller, "error while writing file", ex);
                exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "error while writing file: {0}, message: {1}", FileName, ex.Message));
                exception.Data.Add("FileName", FileName);
                throw exception;
            }
        }
    }
}