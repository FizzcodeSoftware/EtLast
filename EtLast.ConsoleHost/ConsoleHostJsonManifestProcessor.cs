﻿namespace FizzCode.EtLast.ConsoleHost;

internal class ConsoleHostJsonManifestProcessor : IManifestProcessor
{
    public required string Folder { get; init; }
    public required Func<ContextManifest, string> FileNameFunc { get; init; }
    public int BufferTimeoutMilliseconds { get; init; } = 2000;
    public JsonSerializerOptions JsonSerializerOptions { get; init; } = new()
    {
        WriteIndented = true,
    };

    private Stopwatch _lastSave = null;

    public void RegisterToManifestEvents(ContextManifest manifest)
    {
        manifest.ManifestChanged += ManifestChanged;
        manifest.ManifestClosed += ManifestClosed;
    }

    private void ManifestClosed(ContextManifest manifest)
    {
        SaveManifest(manifest);
    }

    private void ManifestChanged(ContextManifest manifest)
    {
        if (_lastSave == null || _lastSave.ElapsedMilliseconds > BufferTimeoutMilliseconds)
            SaveManifest(manifest);
    }

    private void SaveManifest(ContextManifest manifest)
    {
        if (_lastSave == null)
            _lastSave = Stopwatch.StartNew();
        else
            _lastSave.Restart();

        if (!Directory.Exists(Folder))
            Directory.CreateDirectory(Folder);

        var fileName = Path.Combine(Folder, FileNameFunc.Invoke(manifest));
        var content = JsonSerializer.Serialize(manifest, JsonSerializerOptions);
        try
        {
            File.WriteAllText(fileName, content, Encoding.UTF8);
        }
        catch (Exception)
        {
        }
    }
}