namespace JsonExtensions.Benchmarks;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

public class Program
{
#if DEBUG
    static void Main(string[] args) => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());
#else
    static void Main(string[] args)
    {
        _ = BenchmarkRunner.Run<JsonReaderBenchmarks>();
    }
#endif
}