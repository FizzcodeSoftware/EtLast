﻿namespace FizzCode.EtLast.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using FizzCode.EtLast;
    using FizzCode.EtLast.Diagnostics.Interface;
    using FizzCode.EtLast.PluginHost;
    using FizzCode.LightWeight.Configuration;
    using Microsoft.Extensions.Configuration;
    using Serilog.Events;
    using Serilog.Parsing;

    public class HttpSender : IDisposable, IExecutionContextListener
    {
        public int MaxCommunicationErrorCount { get; } = 2;

        private Uri _uri;
        private HttpClient _client;
        private Thread _workerThread;
        private string _sessionId;
        private string _contextName;
        private ExtendedBinaryWriter _currentWriter;
        private ExtendedBinaryWriter _currentDictionaryWriter;
        private readonly object _currentWriterLock = new object();
        private readonly Dictionary<string, int> _textDictionary = new Dictionary<string, int>();
        private bool _finished;
        private int _communicationErrorCount;

        private readonly Dictionary<string, MessageTemplate> _messageTemplateCache = new Dictionary<string, MessageTemplate>();
        private readonly object _messageTemplateCacheLock = new object();
        private readonly MessageTemplateParser _messageTemplateParser = new MessageTemplateParser();

        public bool Init(IExecutionContext executionContext, IConfigurationSection configurationSection, IConfigurationSecretProtector configurationSecretProtector)
        {
            _sessionId = executionContext.SessionId;
            _contextName = executionContext.Name;
            var url = ConfigurationReader.GetCurrentValue(configurationSection, "Url", null, configurationSecretProtector);
            if (url == null)
                return false;

            _uri = new Uri(url);

            _client = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(10000),
            };

            SendWriter(null);

            _workerThread = new Thread(WorkerMethod);
            _workerThread.Start();
            return true;
        }

        private void WorkerMethod()
        {
            var swLastSent = Stopwatch.StartNew();

            while (!_finished && (_communicationErrorCount <= MaxCommunicationErrorCount))
            {
                ExtendedBinaryWriter dictionaryWriterToSend = null;
                ExtendedBinaryWriter writerToSend = null;
                lock (_currentWriterLock)
                {
                    if (_currentWriter != null
                        && (_currentWriter.BaseStream.Length >= 1024 * 1024
                            || (_currentWriter.BaseStream.Length > 0 && swLastSent.ElapsedMilliseconds > 500)))
                    {
                        dictionaryWriterToSend = _currentDictionaryWriter;
                        writerToSend = _currentWriter;
                        _currentDictionaryWriter = null;
                        _currentWriter = null;
                    }
                }

                if (dictionaryWriterToSend != null)
                    SendWriter(dictionaryWriterToSend);

                if (writerToSend != null)
                    SendWriter(writerToSend);

                Thread.Sleep(10);
            }

            if (_currentDictionaryWriter != null)
            {
                if (_currentDictionaryWriter.BaseStream.Length > 0 && _communicationErrorCount <= MaxCommunicationErrorCount)
                {
                    SendWriter(_currentDictionaryWriter);
                }

                _currentDictionaryWriter = null;
            }

            if (_currentWriter != null)
            {
                if (_currentWriter.BaseStream.Length > 0 && _communicationErrorCount <= MaxCommunicationErrorCount)
                    SendWriter(_currentWriter);

                _currentWriter = null;
            }
        }

        private void SendWriter(ExtendedBinaryWriter writer)
        {
            writer?.Flush();

            var fullUri = new Uri(_uri, "diag?sid=" + _sessionId + (_contextName != null ? "&ctx=" + _contextName : ""));

            var binaryContent = writer != null
                ? (writer.BaseStream as MemoryStream).ToArray()
                : Array.Empty<byte>();

            using (var content = new ByteArrayContent(binaryContent))
            {
                try
                {
                    var response = _client.PostAsync(fullUri, content).Result;
                    var responseBody = response.Content.ReadAsStringAsync().Result;
                    if (responseBody != "ACK")
                    {
                        _communicationErrorCount++;
                    }
                }
                catch (Exception)
                {
                    _communicationErrorCount++;
                }
            }

            writer?.BaseStream.Dispose();
            writer?.Dispose();
        }

        public int GetTextDictionaryKey(string text)
        {
            if (text == null)
                return 0;

            lock (_textDictionary)
            {
                if (!_textDictionary.TryGetValue(text, out var key))
                {
                    key = _textDictionary.Count + 1;
                    _textDictionary.Add(text, key);

                    if (_currentDictionaryWriter == null)
                        _currentDictionaryWriter = new ExtendedBinaryWriter(new MemoryStream(), Encoding.UTF8);

                    _currentDictionaryWriter.Write((byte)DiagnosticsEventKind.TextDictionaryKeyAdded);
                    var eventDataLengthPos = (int)_currentDictionaryWriter.BaseStream.Position;
                    _currentDictionaryWriter.Write(0);
                    var startPos = (int)_currentDictionaryWriter.BaseStream.Position;

                    _currentDictionaryWriter.Write7BitEncodedInt(key);
                    _currentDictionaryWriter.WriteNullable(text);

                    var endPos = (int)_currentDictionaryWriter.BaseStream.Position;
                    _currentDictionaryWriter.Seek(eventDataLengthPos, SeekOrigin.Begin);
                    _currentDictionaryWriter.Write(endPos - startPos);
                    _currentDictionaryWriter.Seek(endPos, SeekOrigin.Begin);
                }

                return key;
            }
        }

        public void SendDiagnostics(DiagnosticsEventKind kind, Action<ExtendedBinaryWriter> writerDelegate)
        {
            if (_communicationErrorCount > MaxCommunicationErrorCount)
                return;

            lock (_currentWriterLock)
            {
                if (_finished)
                    throw new Exception("unexpected call of " + nameof(SendDiagnostics));

                if (_currentWriter == null)
                    _currentWriter = new ExtendedBinaryWriter(new MemoryStream(), Encoding.UTF8);

                _currentWriter.Write((byte)kind);
                var eventDataLengthPos = (int)_currentWriter.BaseStream.Position;
                _currentWriter.Write(0);

                var startPos = (int)_currentWriter.BaseStream.Position;
                _currentWriter.Write(DateTime.Now.Ticks);

                writerDelegate?.Invoke(_currentWriter);
                var endPos = (int)_currentWriter.BaseStream.Position;
                _currentWriter.Seek(eventDataLengthPos, SeekOrigin.Begin);
                _currentWriter.Write(endPos - startPos);
                _currentWriter.Seek(endPos, SeekOrigin.Begin);
            }
        }

        private bool _isDisposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _client?.Dispose();
                    _client = null;

                    _currentWriter?.Dispose();
                    _currentWriter = null;

                    _currentDictionaryWriter?.Dispose();
                    _currentDictionaryWriter = null;
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Flush()
        {
            _finished = true;
            _workerThread.Join();
        }

        public void OnLog(LogSeverity severity, bool forOps, string transactionId, IProcess process, string text, params object[] args)
        {
            if ((severity >= LogSeverity.Debug) && !string.IsNullOrEmpty(text) && !forOps)
            {
                if (args.Length == 0)
                {
                    SendDiagnostics(DiagnosticsEventKind.Log, writer =>
                    {
                        writer.Write7BitEncodedInt(GetTextDictionaryKey(transactionId));
                        writer.Write(text);
                        writer.Write((byte)severity);
                        writer.WriteNullable(process?.InvocationInfo?.InvocationUid);
                        writer.Write7BitEncodedInt(0);
                    });

                    return;
                }

                var template = GetMessageTemplate(text);
                var tokens = template.Tokens.ToList();

                SendDiagnostics(DiagnosticsEventKind.Log, writer =>
                {
                    writer.Write7BitEncodedInt(GetTextDictionaryKey(transactionId));
                    writer.Write(text);
                    writer.Write((byte)severity);
                    writer.WriteNullable(process?.InvocationInfo?.InvocationUid);

                    var argCount = 0;
                    for (var i = 0; i < tokens.Count && argCount < args.Length; i++)
                    {
                        if (tokens[i] is PropertyToken pt)
                            argCount++;
                    }

                    writer.Write7BitEncodedInt(argCount);
                    for (int i = 0, idx = 0; i < tokens.Count && idx < args.Length; i++)
                    {
                        if (tokens[i] is PropertyToken pt)
                        {
                            var rawText = text.Substring(pt.StartIndex, pt.Length);
                            writer.Write7BitEncodedInt(GetTextDictionaryKey(rawText));
                            writer.WriteObject(args[idx]);
                            idx++;
                        }
                    }
                });
            }
        }

        public MessageTemplate GetMessageTemplate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            lock (_messageTemplateCacheLock)
            {
                if (_messageTemplateCache.TryGetValue(text, out var existingTemplate))
                    return existingTemplate;
            }

            var template = _messageTemplateParser.Parse(text);
            lock (_messageTemplateCacheLock)
            {
                if (!_messageTemplateCache.ContainsKey(text))
                {
                    if (_messageTemplateCache.Count == 1000)
                        _messageTemplateCache.Clear();

                    _messageTemplateCache[text] = template;
                }
            }

            return template;
        }

        public void OnCustomLog(bool forOps, string fileName, IProcess process, string text, params object[] args)
        {
        }

        public void OnException(IProcess process, Exception exception)
        {
        }

        public void OnRowCreated(IReadOnlyRow row, IProcess process)
        {
            SendDiagnostics(DiagnosticsEventKind.RowCreated, writer =>
            {
                writer.Write7BitEncodedInt(process.InvocationInfo.InvocationUid);
                writer.Write7BitEncodedInt(row.Uid);
                writer.Write7BitEncodedInt(row.ColumnCount);
                foreach (var kvp in row.Values)
                {
                    writer.Write7BitEncodedInt(GetTextDictionaryKey(kvp.Key));
                    writer.WriteObject(kvp.Value);
                }
            });
        }

        public void OnRowOwnerChanged(IReadOnlyRow row, IProcess previousProcess, IProcess currentProcess)
        {
            SendDiagnostics(DiagnosticsEventKind.RowOwnerChanged, writer =>
            {
                writer.Write7BitEncodedInt(row.Uid);
                writer.Write7BitEncodedInt(previousProcess.InvocationInfo.InvocationUid);
                writer.WriteNullable(currentProcess?.InvocationInfo?.InvocationUid);
            });
        }

        public void OnRowValueChanged(IProcess process, IReadOnlyRow row, params KeyValuePair<string, object>[] values)
        {
            SendDiagnostics(DiagnosticsEventKind.RowValueChanged, writer =>
            {
                writer.Write7BitEncodedInt(row.Uid);
                writer.WriteNullable(process?.InvocationInfo?.InvocationUid);

                writer.Write7BitEncodedInt(values.Length);
                foreach (var kvp in values)
                {
                    writer.Write7BitEncodedInt(GetTextDictionaryKey(kvp.Key));
                    writer.WriteObject(kvp.Value);
                }
            });
        }

        public void OnRowStoreStarted(int storeUid, string location, string path)
        {
            SendDiagnostics(DiagnosticsEventKind.RowStoreStarted, writer =>
            {
                writer.Write7BitEncodedInt(storeUid);
                writer.Write7BitEncodedInt(GetTextDictionaryKey(location));
                writer.Write7BitEncodedInt(GetTextDictionaryKey(path));
            });
        }

        public void OnRowStored(IProcess process, IReadOnlyRow row, int storeUid)
        {
            SendDiagnostics(DiagnosticsEventKind.RowStored, writer =>
            {
                writer.Write7BitEncodedInt(row.Uid);
                writer.Write7BitEncodedInt(process.InvocationInfo.InvocationUid);
                writer.Write7BitEncodedInt(storeUid);
                writer.Write7BitEncodedInt(row.ColumnCount);
                foreach (var kvp in row.Values)
                {
                    writer.Write7BitEncodedInt(GetTextDictionaryKey(kvp.Key));
                    writer.WriteObject(kvp.Value);
                }
            });
        }

        public void OnProcessInvocationStart(IProcess process)
        {
            SendDiagnostics(DiagnosticsEventKind.ProcessInvocationStart, writer =>
            {
                writer.Write7BitEncodedInt(process.InvocationInfo.InvocationUid);
                writer.Write7BitEncodedInt(process.InvocationInfo.InstanceUid);
                writer.Write7BitEncodedInt(process.InvocationInfo.Number);
                writer.Write(process.GetType().GetFriendlyTypeName());
                writer.Write((byte)process.Kind);
                writer.Write(process.Name);
                writer.WriteNullable(process.Topic.Name);
                writer.WriteNullable(process.InvocationInfo.Caller?.InvocationInfo?.InvocationUid);
            });
        }

        public void OnProcessInvocationEnd(IProcess process)
        {
            SendDiagnostics(DiagnosticsEventKind.ProcessInvocationEnd, writer =>
            {
                writer.Write7BitEncodedInt(process.InvocationInfo.InvocationUid);
                writer.Write(process.InvocationInfo.LastInvocationStarted.ElapsedMilliseconds);
                writer.WriteNullable(process.InvocationInfo.LastInvocationNetTimeMilliseconds);
            });
        }

        public void OnContextIoCommandStart(int uid, IoCommandKind kind, string location, string path, IProcess process, int? timeoutSeconds, string command, string transactionId, Func<IEnumerable<KeyValuePair<string, object>>> argumentListGetter, string message, params object[] messageArgs)
        {
            SendDiagnostics(DiagnosticsEventKind.IoCommandStart, writer =>
            {
                writer.Write7BitEncodedInt(uid);
                writer.Write7BitEncodedInt(process.InvocationInfo.InvocationUid);
                writer.Write((byte)kind);
                writer.Write7BitEncodedInt(GetTextDictionaryKey(location));
                writer.Write7BitEncodedInt(GetTextDictionaryKey(path));
                writer.WriteNullable(timeoutSeconds);
                writer.WriteNullable(command);
                writer.Write7BitEncodedInt(GetTextDictionaryKey(transactionId));
                var arguments = argumentListGetter?.Invoke()?.ToArray();
                if (arguments?.Length > 0)
                {
                    writer.Write7BitEncodedInt(arguments.Length);
                    foreach (var kvp in arguments)
                    {
                        writer.Write7BitEncodedInt(GetTextDictionaryKey(kvp.Key));
                        writer.WriteObject(kvp.Value);
                    }
                }
                else
                {
                    writer.Write7BitEncodedInt(0);
                }
            });
        }

        public void OnContextIoCommandEnd(IProcess process, int uid, IoCommandKind kind, int? affectedDataCount, Exception ex)
        {
            SendDiagnostics(DiagnosticsEventKind.IoCommandEnd, writer =>
            {
                writer.Write7BitEncodedInt(uid);
                writer.WriteNullable(affectedDataCount);
                writer.WriteNullable(ex?.FormatExceptionWithDetails());
            });
        }

        public void OnContextClosed()
        {
            SendDiagnostics(DiagnosticsEventKind.ContextEnded, null);

            Flush();
        }
    }
}