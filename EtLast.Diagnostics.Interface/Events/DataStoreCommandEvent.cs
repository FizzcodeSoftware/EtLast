﻿namespace FizzCode.EtLast.Diagnostics.Interface
{
    using System.Text.Json.Serialization;

    public class DataStoreCommandEvent : AbstractEvent
    {
        [JsonPropertyName("p")]
        public int? ProcessUid { get; set; }

        [JsonPropertyName("o")]
        public OperationInfo Operation { get; set; }

        [JsonPropertyName("c")]
        public string Command { get; set; }

        [JsonPropertyName("l")]
        public string Location { get; set; }

        [JsonPropertyName("a")]
        public NamedArgument[] Arguments { get; set; }
    }
}