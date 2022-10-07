﻿namespace FizzCode.EtLast;

[ComVisible(true)]
[Serializable]
public class ConfigurationFileException : EtlException
{
    public ConfigurationFileException(string path, string message)
        : base(message)
    {
        Data["Path"] = path;
    }

    public ConfigurationFileException(string path, string message, Exception innerException)
        : base(message, innerException)
    {
        Data["Path"] = path;
    }
}
