﻿namespace FizzCode.EtLast
{
    using System;
    using System.Globalization;
    using System.Net;

    public class DownloadFileProcess : AbstractExecutableProcess
    {
        public string Url { get; set; }
        public string FileName { get; set; }

        public DownloadFileProcess(IEtlContext context, string name = null)
            : base(context, name)
        {
        }

        public override void Validate()
        {
            if (string.IsNullOrEmpty(Url))
                throw new ProcessParameterNullException(this, nameof(Url));

            if (string.IsNullOrEmpty(FileName))
                throw new ProcessParameterNullException(this, nameof(FileName));

            Context.Log(LogSeverity.Information, this, null, "downloading file from '{Url}' to '{FileName}'", Url,
                PathHelpers.GetFriendlyPathName(FileName));
        }

        protected override void ExecuteImpl()
        {
            // todo: use HttpClient instead with cancellationTokenSource
            using (var clt = new WebClient())
            {
                try
                {
                    clt.DownloadFile(Url, FileName);
                    Context.Log(LogSeverity.Debug, this, "successfully downloaded from '{Url}' to '{FileName}' in {Elapsed}", Url,
                        PathHelpers.GetFriendlyPathName(FileName), LastInvocation.Elapsed);
                }
                catch (Exception ex)
                {
                    var exception = new ProcessExecutionException(this, "file download failed", ex);
                    exception.AddOpsMessage(string.Format(CultureInfo.InvariantCulture, "file download failed, url: {0}, file name: {1}, message: {2}",
                        Url, FileName, ex.Message));
                    exception.Data.Add("Url", Url);
                    exception.Data.Add("FileName", FileName);
                    throw exception;
                }
            }
        }
    }
}