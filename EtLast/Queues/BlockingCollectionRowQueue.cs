﻿namespace FizzCode.EtLast
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;

    public class BlockingCollectionRowQueue : IRowQueue
    {
        private readonly BlockingCollection<IRow> _collection = new BlockingCollection<IRow>();

        public void AddRow(IRow row)
        {
            _collection.Add(row);
        }

        public void AddRowNoSignal(IRow row)
        {
            _collection.Add(row);
        }

        public void Signal()
        {
        }

        public void SignalNoMoreRows()
        {
            _collection.CompleteAdding();
        }

        public IEnumerable<IRow> GetConsumer(CancellationToken token)
        {
            return _collection.GetConsumingEnumerable(token);
        }

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _collection.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}