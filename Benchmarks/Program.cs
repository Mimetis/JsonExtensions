namespace JsonExtensions.Benchmarks;

using BenchmarkDotNet.Running;

public class Program
{
    static void Main(string[] args)
    {
        _ = BenchmarkRunner.Run<JsonReaderBenchmarks>();
    }
}