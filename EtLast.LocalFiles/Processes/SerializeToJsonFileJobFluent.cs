﻿namespace FizzCode.EtLast;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class SerializeToLocalJsonFileFluent
{
    public static IFlow SerializeToLocalJsonFile<T>(this IFlow builder, string name, string targetFileName, T data, Encoding customEncoding = null)
    {
        return builder.ExecuteProcess(() => new SerializeToJsonFile<T>()
        {
            Name = name,
            Data = data,
            Encoding = customEncoding ?? Encoding.UTF8,
            SinkProvider = new LocalFileSinkProvider()
            {
                Path = targetFileName,
                ActionWhenFileExists = LocalSinkFileExistsAction.Overwrite,
                FileMode = FileMode.CreateNew,
            },
        });
    }
}