namespace JsonExtensions.Benchmarks;

using BenchmarkDotNet.Attributes;
using System.Text.Json;

[MemoryDiagnoser]
[Config(typeof(AntiVirusFriendlyConfig))]
public class JsonReaderBenchmarks
{
    private JsonReader jsonReader = null!;
    private FileStream fileStream = null!;

    private const int IterationsNum = 100;

    [GlobalSetup]
    public void Setup()
    {
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dummy-data.json");
        this.fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
        this.jsonReader = new JsonReader(this.fileStream);
    }

    [GlobalCleanup]
    public async ValueTask Cleanup()
    {
        await this.fileStream.DisposeAsync();
        this.jsonReader.Dispose();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        this.fileStream.Seek(0, SeekOrigin.Begin);
        this.jsonReader.Dispose();
        this.jsonReader = new JsonReader(this.fileStream);
    }

    [Benchmark]
    [IterationCount(IterationsNum)]
    public async Task Read()
    {
        ArgumentNullException.ThrowIfNull(this.jsonReader);

        int count = 0;

        while (await this.jsonReader.ReadAsync())
        {
            var tokenString = this.jsonReader.GetString();

            if (this.jsonReader.TokenType == JsonTokenType.PropertyName)
            {
                // TODO: Perform some operation
            }

            count++;
        }

        Console.WriteLine("Count: " + count);
    }

    
    [Benchmark]
    [IterationCount(IterationsNum)]
    public async Task Values()
    {
        ArgumentNullException.ThrowIfNull(this.jsonReader);

        int count = 0;

        await foreach (var value in this.jsonReader.Values())
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