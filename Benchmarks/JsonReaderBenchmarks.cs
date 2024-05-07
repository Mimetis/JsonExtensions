namespace JsonExtensions.Benchmarks;

using BenchmarkDotNet.Attributes;
using System.Text.Json;

[MemoryDiagnoser]
[Config(typeof(AntiVirusFriendlyConfig))]
public class JsonReaderBenchmarks
{
    private JsonReader? _jsonReader;
    private FileStream? _fileStream;

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
    }

    [Benchmark]
    public void Read()
    {
        ArgumentNullException.ThrowIfNull(_jsonReader);

        while (_jsonReader.Read())
        {
            if (_jsonReader.TokenType == JsonTokenType.PropertyName)
            {
                // Perform some operation
                var propertyName = _jsonReader.GetString();
            }
        }
    }

    
    [Benchmark]
    public void Values()
    {
        ArgumentNullException.ThrowIfNull(_jsonReader);

        foreach (var value in _jsonReader.Values())
        {
            if (value.TokenType == JsonTokenType.PropertyName)
            {
                var tokenString = value.ToString();
                Console.WriteLine(tokenString);
            }
        }
    }
}