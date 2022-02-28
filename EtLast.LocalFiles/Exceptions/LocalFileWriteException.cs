﻿namespace FizzCode.EtLast
{
    using System;
    using System.Runtime.InteropServices;

    [ComVisible(true)]
    [Serializable]
    public class LocalFileWriteException : EtlException
    {
        internal LocalFileWriteException(IProcess process, string message, string fileName)
            : base(process, message)
        {
            Data.Add("FileName", fileName);
        }

        internal LocalFileWriteException(IProcess process, string message, string fileName, Exception innerException)
            : base(process, message, innerException)
        {
            Data.Add("FileName", fileName);
        }
    }
}