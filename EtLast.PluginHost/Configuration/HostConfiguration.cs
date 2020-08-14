﻿namespace FizzCode.EtLast.PluginHost
{
    using System;
    using System.Collections.Generic;
    using FizzCode.LightWeight.Configuration;
    using Microsoft.Extensions.Configuration;
    using Serilog.Events;

    public enum DynamicCompilationMode { Never, Always, Default }

    public class HostConfiguration
    {
        public TimeSpan TransactionScopeTimeout { get; set; } = TimeSpan.FromMinutes(120);

        public string SeqUrl { get; set; }
        public string SeqApiKey { get; set; }
        public int RetainedLogFileCountLimitImportant { get; set; } = 30;
        public int RetainedLogFileCountLimitInfo { get; set; } = 14;
        public int RetainedLogFileCountLimitLow { get; set; } = 4;
        public string ModulesFolder { get; set; } = @".\modules";
        public LogEventLevel MinimumLogLevelOnConsole { get; set; }
        public LogEventLevel MinimumLogLevelInFile { get; set; }
        public LogEventLevel MinimumLogLevelIo { get; set; }
        public DynamicCompilationMode DynamicCompilationMode { get; set; }
        public Dictionary<string, string> CommandAliases { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IConfigurationSecretProtector SecretProtector { get; set; }

        private IConfigurationRoot _configuration;
        private string _section;

        public void LoadFromConfiguration(IConfigurationRoot configuration, string section)
        {
            _configuration = configuration;
            _section = section;

            SeqUrl = ConfigurationReader.GetCurrentValue(configuration, section, "Seq:Url", null);
            SeqApiKey = ConfigurationReader.GetCurrentValue(configuration, section, "Seq:ApiKey", null);
            RetainedLogFileCountLimitImportant = ConfigurationReader.GetCurrentValue(configuration, section, "RetainedLogFileCountLimit:Important", 30);
            RetainedLogFileCountLimitInfo = ConfigurationReader.GetCurrentValue(configuration, section, "RetainedLogFileCountLimit:Info", 14);
            RetainedLogFileCountLimitLow = ConfigurationReader.GetCurrentValue(configuration, section, "RetainedLogFileCountLimit:Low", 4);
            TransactionScopeTimeout = TimeSpan.FromMinutes(ConfigurationReader.GetCurrentValue(configuration, section, "TransactionScopeTimeoutMinutes", 120));
            ModulesFolder = ConfigurationReader.GetCurrentValue(configuration, section, "ModulesFolder", @".\modules", SecretProtector);

            var v = ConfigurationReader.GetCurrentValue(configuration, section, "DynamicCompilation:Mode", "Default", SecretProtector);
            if (!string.IsNullOrEmpty(v) && Enum.TryParse(v, out DynamicCompilationMode mode))
            {
                DynamicCompilationMode = mode;
            }

            v = ConfigurationReader.GetCurrentValue(configuration, section, "MinimumLogLevel:Console", "Information", SecretProtector);
            if (!string.IsNullOrEmpty(v) && Enum.TryParse(v, out LogEventLevel level))
            {
                MinimumLogLevelOnConsole = level;
            }

            v = ConfigurationReader.GetCurrentValue(configuration, section, "MinimumLogLevel:File", "Debug", SecretProtector);
            if (!string.IsNullOrEmpty(v) && Enum.TryParse(v, out level))
            {
                MinimumLogLevelInFile = level;
            }

            v = ConfigurationReader.GetCurrentValue(configuration, section, "MinimumLogLevel:IoFile", "Verbose", SecretProtector);
            if (!string.IsNullOrEmpty(v) && Enum.TryParse(v, out level))
            {
                MinimumLogLevelIo = level;
            }

            v = ConfigurationReader.GetCurrentValue(configuration, section, "SecretProtector:Type", null);
            if (!string.IsNullOrEmpty(v))
            {
                var type = Type.GetType(v);
                if (type != null && typeof(IConfigurationSecretProtector).IsAssignableFrom(type))
                {
                    var secretProtectorSection = configuration.GetSection(section + ":SecretProtector");
                    try
                    {
                        SecretProtector = (IConfigurationSecretProtector)Activator.CreateInstance(type);
                        SecretProtector.Init(secretProtectorSection);
                    }
                    catch (Exception ex)
                    {
                        var exception = new Exception("Can't initialize secret protector.", ex);
                        exception.Data.Add("FullyQualifiedTypeName", v);
                        throw exception;
                    }
                }
                else
                {
                    var exception = new Exception("Secret protector type not found.");
                    exception.Data.Add("FullyQualifiedTypeName", v);
                    throw exception;
                }
            }

            GetCommandAliases(configuration, section);
        }

        public List<IEtlContextListener> GetEtlContextListeners(IExecutionContext executionContext)
        {
            var result = new List<IEtlContextListener>();

            var listenersSection = _configuration.GetSection(_section + ":EtlContextListeners");
            if (listenersSection == null)
                return result;

            var children = listenersSection.GetChildren();
            foreach (var childSection in children)
            {
                var type = Type.GetType(childSection.Key);
                if (type != null && typeof(IEtlContextListener).IsAssignableFrom(type))
                {
                    var ctors = type.GetConstructors();
                    try
                    {
                        var instance = (IEtlContextListener)Activator.CreateInstance(type);
                        var ok = instance.Init(executionContext, childSection);
                        if (ok)
                        {
                            result.Add(instance);
                        }
                    }
                    catch (Exception ex)
                    {
                        var exception = new Exception("Can't initialize secret protector.", ex);
                        exception.Data.Add("FullyQualifiedTypeName", childSection.Key);
                        throw exception;
                    }
                }
                else
                {
                    var exception = new Exception("EtlContextListener type not found.");
                    exception.Data.Add("FullyQualifiedTypeName", childSection.Key);
                    throw exception;
                }
            }

            return result;
        }

        private void GetCommandAliases(IConfigurationRoot configuration, string section)
        {
            var aliasSection = configuration.GetSection(section + ":Aliases");
            if (aliasSection == null)
                return;

            foreach (var child in aliasSection.GetChildren())
            {
                CommandAliases.Add(child.Key, child.Value);
            }
        }
    }
}