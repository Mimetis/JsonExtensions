namespace JsonExtensions.Benchmarks;

using BenchmarkDotNet.Attributes;
using System.Text.Json;
using System.Text.Json.Stream;
using Wololo.Text.Json;

[MemoryDiagnoser]
[Config(typeof(AntiVirusFriendlyConfig))]
public class JsonReaderBenchmarks
{
    private JsonReader? _jsonReader;
    private Utf8JsonStreamReader? _jsonStreamReader;
    private Utf8JsonAsyncStreamReader? _jsonStreamAsyncReader;

    private FileStream _fileStream = null!;

    private const int IterationsNum = 100;

    [GlobalSetup]
    public void Setup()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dummy-data.json");
        _fileStream = new(path, FileMode.Open, FileAccess.Read);
        _jsonReader = new(_fileStream);
        _jsonStreamReader = new();
        _jsonStreamAsyncReader = new(_fileStream);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _fileStream.Dispose();
        _jsonReader?.Dispose();
        _jsonStreamAsyncReader?.Dispose(true);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _fileStream.Seek(0, SeekOrigin.Begin);
        _jsonReader?.Dispose();
        _jsonReader = new JsonReader(_fileStream);
    }

    [Benchmark]
    [IterationCount(IterationsNum)]
    public void ReadMimetis()
    {
        ArgumentNullException.ThrowIfNull(_jsonReader);

        int count = 0;

        while (_jsonReader.Read())
        {
            switch (_jsonReader.TokenType)
            {
                case JsonTokenType.PropertyName:
                case JsonTokenType.String:
                    var tokenString = _jsonReader.GetString();
                    break;
            }

            count++;
        }

        Console.WriteLine("Count: " + count);
    }

    [Benchmark]
    [IterationCount(IterationsNum)]
    public void ValuesMimetis()
    {
        ArgumentNullException.ThrowIfNull(_jsonReader);

        int count = 0;

        foreach (var value in _jsonReader.Values())
        {
            var tokenString = value.ToString();

            if (value.TokenType == JsonTokenType.PropertyName)
            {
                // Console.WriteLine("PropertyName Found");
            }

            count++;
        }

        Console.WriteLine("Count: " + count);
    }

    [Benchmark]
    [IterationCount(IterationsNum)]
    public async Task ReadHarrtell()
    {
        ArgumentNullException.ThrowIfNull(_jsonStreamReader);

        int count = 0;

        void CountTokens(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                case JsonTokenType.String:
                    var tokenString = reader.GetString();
                    break;
            }

            count++;
        }

        await _jsonStreamReader.ReadAsync(_fileStream, CountTokens);

        Console.WriteLine("Count: " + count);
    }

    [Benchmark]
    [IterationCount(IterationsNum)]
    public async Task ReadGraGra()
    {
        ArgumentNullException.ThrowIfNull(_jsonStreamAsyncReader);

        int count = 0;

        while (await _jsonStreamAsyncReader.ReadAsync())
        {
            switch (_jsonStreamAsyncReader.TokenType)
            {
                case JsonTokenType.PropertyName:
                case JsonTokenType.String:
                    var tokenString = _jsonStreamAsyncReader.GetString();
                    break;
            }

            count++;
        }

        Console.WriteLine("Count: " + count);
    }
}