﻿namespace FizzCode.EtLast
{
    using System;

    public class ContextCustomLogEventArgs : EventArgs
    {
        public string FileName { get; set; }
        public IProcess Process { get; set; }
        public string Text { get; set; }
        public object[] Arguments { get; set; }
        public bool ForOps { get; set; }
    }
}