﻿namespace FizzCode.EtLast.Diagnostics.Interface
{
    using System.Collections.Generic;

    public class EventParser
    {
        private readonly Dictionary<int, string> _textDictionary;

        public EventParser()
        {
            _textDictionary = new Dictionary<int, string>()
            {
                [0] = null,
            };
        }

        public void AddText(int id, string text)
        {
            if (text != null)
                text = string.Intern(text);

            _textDictionary[id] = text;
        }

        public string GetTextById(int id)
        {
            _textDictionary.TryGetValue(id, out var value);
            return value;
        }

        public ProcessInvocationStartEvent ReadProcessInvocationStartEvent(ExtendedBinaryReader reader)
        {
            return new ProcessInvocationStartEvent
            {
                InvocationUID = reader.Read7BitEncodedInt(),
                InstanceUID = reader.Read7BitEncodedInt(),
                InvocationCounter = reader.Read7BitEncodedInt(),
                Type = reader.ReadString(),
                Kind = (ProcessKind)reader.ReadByte(),
                Name = reader.ReadString(),
                Topic = reader.ReadNullableString(),
                CallerInvocationUID = reader.ReadNullableInt32()
            };
        }

        public ProcessInvocationEndEvent ReadProcessInvocationEndEvent(ExtendedBinaryReader reader)
        {
            return new ProcessInvocationEndEvent
            {
                InvocationUID = reader.Read7BitEncodedInt(),
                ElapsedMilliseconds = reader.ReadInt64(),
                NetTimeMilliseconds = reader.ReadNullableInt64(),
            };
        }

        public IoCommandStartEvent ReadIoCommandStartEvent(ExtendedBinaryReader reader)
        {
            var evt = new IoCommandStartEvent
            {
                Uid = reader.Read7BitEncodedInt(),
                ProcessInvocationUid = reader.Read7BitEncodedInt(),
                Kind = (IoCommandKind)reader.ReadByte(),
                Location = GetTextById(reader.Read7BitEncodedInt()),
                Path = GetTextById(reader.Read7BitEncodedInt()),
                TimeoutSeconds = reader.ReadNullableInt32(),
                Command = reader.ReadNullableString(),
                TransactionId = GetTextById(reader.Read7BitEncodedInt()),
            };

            var argCount = reader.Read7BitEncodedInt();
            if (argCount > 0)
            {
                evt.Arguments = new KeyValuePair<string, object>[argCount];
                for (var i = 0; i < argCount; i++)
                {
                    var name = GetTextById(reader.Read7BitEncodedInt());
                    var value = reader.ReadObject();
                    evt.Arguments[i] = new KeyValuePair<string, object>(name, value);
                }
            }

            return evt;
        }

        public IoCommandEndEvent ReadIoCommandEndEvent(ExtendedBinaryReader reader)
        {
            var evt = new IoCommandEndEvent
            {
                Uid = reader.Read7BitEncodedInt(),
                AffectedDataCount = reader.ReadNullableInt32(),
                ErrorMessage = reader.ReadNullableString(),
            };

            return evt;
        }

        public RowCreatedEvent ReadRowCreatedEvent(ExtendedBinaryReader reader)
        {
            var evt = new RowCreatedEvent
            {
                ProcessInvocationUid = reader.Read7BitEncodedInt(),
                RowUid = reader.Read7BitEncodedInt()
            };

            var columnCount = reader.Read7BitEncodedInt();
            if (columnCount > 0)
            {
                evt.Values = new KeyValuePair<string, object>[columnCount];
                for (var i = 0; i < columnCount; i++)
                {
                    var column = GetTextById(reader.Read7BitEncodedInt());
                    var value = reader.ReadObject();
                    evt.Values[i] = new KeyValuePair<string, object>(column, value);
                }
            }

            return evt;
        }

        public RowOwnerChangedEvent ReadRowOwnerChangedEvent(ExtendedBinaryReader reader)
        {
            return new RowOwnerChangedEvent
            {
                RowUid = reader.Read7BitEncodedInt(),
                PreviousProcessInvocationUid = reader.Read7BitEncodedInt(),
                NewProcessInvocationUid = reader.ReadNullableInt32()
            };
        }

        public RowValueChangedEvent ReadRowValueChangedEvent(ExtendedBinaryReader reader)
        {
            var evt = new RowValueChangedEvent
            {
                RowUid = reader.Read7BitEncodedInt(),
                ProcessInvocationUID = reader.ReadNullableInt32()
            };

            var columnCount = reader.Read7BitEncodedInt();
            if (columnCount > 0)
            {
                evt.Values = new KeyValuePair<string, object>[columnCount];
                for (var i = 0; i < columnCount; i++)
                {
                    var column = GetTextById(reader.Read7BitEncodedInt());
                    var value = reader.ReadObject();
                    evt.Values[i] = new KeyValuePair<string, object>(column, value);
                }
            }

            return evt;
        }

        public RowStoreStartedEvent ReadRowStoreStartedEvent(ExtendedBinaryReader reader)
        {
            var evt = new RowStoreStartedEvent
            {
                UID = reader.Read7BitEncodedInt(),
                Location = GetTextById(reader.Read7BitEncodedInt()),
                Path = GetTextById(reader.Read7BitEncodedInt()),
            };

            return evt;
        }

        public RowStoredEvent ReadRowStoredEvent(ExtendedBinaryReader reader)
        {
            var evt = new RowStoredEvent
            {
                RowUid = reader.Read7BitEncodedInt(),
                ProcessInvocationUID = reader.Read7BitEncodedInt(),
                StoreUid = reader.Read7BitEncodedInt()
            };

            var columnCount = reader.Read7BitEncodedInt();
            if (columnCount > 0)
            {
                evt.Values = new KeyValuePair<string, object>[columnCount];
                for (var i = 0; i < columnCount; i++)
                {
                    var column = GetTextById(reader.Read7BitEncodedInt());
                    var value = reader.ReadObject();
                    evt.Values[i] = new KeyValuePair<string, object>(column, value);
                }
            }

            return evt;
        }

        public LogEvent ReadLogEvent(ExtendedBinaryReader reader)
        {
            var evt = new LogEvent
            {
                TransactionId = GetTextById(reader.Read7BitEncodedInt()),
                Text = reader.ReadString(),
                Severity = (LogSeverity)reader.ReadByte(),
                ProcessInvocationUID = reader.ReadNullableInt32()
            };

            var argCount = reader.Read7BitEncodedInt();
            if (argCount > 0)
            {
                evt.Arguments = new KeyValuePair<string, object>[argCount];
                for (var i = 0; i < argCount; i++)
                {
                    var key = GetTextById(reader.Read7BitEncodedInt());
                    var value = reader.ReadObject();
                    evt.Arguments[i] = new KeyValuePair<string, object>(key, value);
                }
            }

            return evt;
        }
    }
}