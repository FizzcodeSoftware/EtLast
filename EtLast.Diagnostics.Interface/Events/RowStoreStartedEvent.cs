﻿namespace FizzCode.EtLast.Diagnostics.Interface
{
    public class RowStoreStartedEvent : AbstractRowEvent
    {
        public int UID { get; set; }
        public string Location { get; set; }
        public string Path { get; set; }
    }
}