﻿using System.Globalization;
using System.Text;

namespace FizzCode.EtLast.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(BenchmarkDotNet.Jobs.RuntimeMoniker.Net70, 1, 1, 1, 3)]
public class ReadFromDelimitedTests
{
    [Params(1000000)]
    public int RowCount;

    private MemoryStream _stream;
    private string _file;

    [GlobalSetup]
    public void SetupDelimited()
    {
        _stream = new MemoryStream();
        _file = Path.GetTempFileName() + ".csv";

        var context = new EtlContext(null);
        SequenceBuilder.Fluent
            .ImportEnumerable(new EnumerableImporter()
            {
                InputGenerator = proc => GenerateRows(13, RowCount, proc),
            })
            .WriteToDelimitedFile(new WriteToDelimitedMutator()
            {
                Delimiter = ';',
                WriteHeader = true,
                Encoding = Encoding.UTF8,
                FormatProvider = CultureInfo.InvariantCulture,
                Quote = '"',
                SinkProvider = new MemorySinkProvider()
                {
                    Stream = _stream,
                    AutomaticallyDispose = true,
                },
                Columns = new()
                {
                    ["1"] = null,
                    ["2"] = null,
                },
            })
            .WriteToDelimitedFile(new WriteToDelimitedMutator()
            {
                Delimiter = ';',
                WriteHeader = true,
                Encoding = Encoding.UTF8,
                FormatProvider = CultureInfo.InvariantCulture,
                Quote = '"',
                SinkProvider = new LocalFileSinkProvider()
                {
                    Path = _file,
                    ActionWhenFileExists = LocalSinkFileExistsAction.Overwrite,
                    FileMode = FileMode.CreateNew,
                },
                Columns = new()
                {
                    ["1"] = null,
                    ["2"] = null,
                },
            })
            .Build()
            .Execute(context);

        Console.WriteLine(_file);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _stream?.Dispose();
        if (File.Exists(_file))
            File.Delete(_file);
    }

    [Benchmark]
    public void ReadFromDelimitedStream()
    {
        var stream = new MemoryStream(_stream.Capacity);
        _stream.Seek(0, SeekOrigin.Begin);
        _stream.CopyTo(stream);
        stream.Seek(0, SeekOrigin.Begin);

        var context = new EtlContext(null);
        var process = new DelimitedLineReader()
        {
            StreamProvider = new OneMemoryStreamProvider()
            {
                Stream = stream,
            },
            Columns = new()
            {
                ["1"] = new TextReaderColumn(),
                ["2"] = new TextReaderColumn().AsInt(),
            },
            Header = DelimitedLineHeader.HasHeader,
            Delimiter = ';',
        };

        var result = process.TakeRowsAndReleaseOwnership(null, null).ToList();
        if (result.Count != RowCount)
            throw new Exception();
    }

    [Benchmark]
    public void ReadFromDelimitedFile()
    {
        var context = new EtlContext(null);
        var process = new DelimitedLineReader()
        {
            StreamProvider = new LocalFileStreamProvider()
            {
                Path = _file,
            },
            Columns = new()
            {
                ["1"] = new TextReaderColumn(),
                ["2"] = new TextReaderColumn().AsInt(),
            },
            Header = DelimitedLineHeader.HasHeader,
            Delimiter = ';',
        };

        var result = process.TakeRowsAndReleaseOwnership(null, null).ToList();
        if (result.Count != RowCount)
            throw new Exception();
    }

    private IEnumerable<IRow> GenerateRows(int seed, int count, EnumerableImporter process)
    {
        var random = new Random(seed);
        var values = new Dictionary<string, object>();

        for (var i = 0; i < count; i++)
        {
            values["1"] = System.Text.Encoding.Unicode.GetString(
                    Enumerable.Range(0, 2 + (random.Next(1000) * 2))
                    .Select(i => i % 2 == 1
                        ? (byte)0
                        : (byte)random.Next(48, 91)).ToArray());
            values["2"] = random.Next(int.MinValue, int.MaxValue);

            yield return process.Context.CreateRow(process, values);
        }
    }
}