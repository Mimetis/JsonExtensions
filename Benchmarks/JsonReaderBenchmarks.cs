namespace JsonExtensions.Benchmarks;

using BenchmarkDotNet.Attributes;
using System.Text.Json;

[MemoryDiagnoser]
[Config(typeof(AntiVirusFriendlyConfig))]
public class JsonReaderBenchmarks
{
    private JsonReader? _jsonReader;
    private FileStream? _fileStream;

    private const int IterationsNum = 100;

    [GlobalSetup]
    public void Setup()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dummy-data.json");
        _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        _jsonReader = new JsonReader(_fileStream);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _fileStream?.Dispose();
        _jsonReader?.Dispose();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _fileStream?.Seek(0, SeekOrigin.Begin);
        _jsonReader?.Dispose();
        _jsonReader = new JsonReader(_fileStream);
    }

    [Benchmark]
    [IterationCount(IterationsNum)]
    public void Read()
    {
        ArgumentNullException.ThrowIfNull(_jsonReader);

        int count = 0;

        while (_jsonReader.Read())
        {
            var tokenString = _jsonReader.GetString();

            if (_jsonReader.TokenType == JsonTokenType.PropertyName)
            {
                // TODO: Perform some operation
            }

            count++;
        }

        Console.WriteLine("Count: " + count);
    }

    
    [Benchmark]
    [IterationCount(IterationsNum)]
    public void Values()
    {
        ArgumentNullException.ThrowIfNull(_jsonReader);

        int count = 0;

        foreach (var value in _jsonReader.Values())
        {
            var tokenString = value.ToString();

            if (value.TokenType == JsonTokenType.PropertyName)
            {
                // TODO: Perform some operation
            }

            count++;
        }

        Console.WriteLine("Count: " + count);
    }
}