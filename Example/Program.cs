using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using JsonExtensions;
using System.Diagnostics;

namespace ConsoleJsonSample
{
    internal class Program
    {
        // Main is mark as async (even if not needed)
        // to be sure we can call the JsonReader.Read() method from an async scope
        static async Task Main(string[] args)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var initialMemory = GC.GetTotalMemory(true);

            using var jsonReader = new JsonReader(GetFileStream()); // test 10 to see buffer increase in debug console

            while (jsonReader.Read())
            {
                // write numbers of space for the current depth
                var indent = new string(' ', jsonReader.Depth * 2);

                if (jsonReader.TokenType == JsonTokenType.StartObject)
                    Console.WriteLine($"{indent}{{");
                else if (jsonReader.TokenType == JsonTokenType.EndObject)
                    Console.WriteLine($"{indent}}}");
                else if (jsonReader.TokenType == JsonTokenType.StartArray)
                    Console.WriteLine($"{indent}[");
                else if (jsonReader.TokenType == JsonTokenType.EndArray)
                    Console.WriteLine($"{indent}]");
                else if (jsonReader.TokenType == JsonTokenType.PropertyName)
                    Console.Write($"{indent}{jsonReader.GetString()}:");
                else if (jsonReader.TokenType == JsonTokenType.String)
                    Console.WriteLine(jsonReader.GetString());
                else if (jsonReader.TokenType == JsonTokenType.Number)
                    Console.WriteLine(jsonReader.GetDouble());
                else if (jsonReader.TokenType == JsonTokenType.True || jsonReader.TokenType == JsonTokenType.False)
                    Console.WriteLine(jsonReader.GetBoolean());
                else
                    Console.WriteLine();

            }

            stopwatch.Stop();

            var finalMemory = GC.GetTotalMemory(true);

            Console.WriteLine($"Elapsed time: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Memory allocated: {finalMemory - initialMemory} bytes");
        }


        static FileStream GetFileStream()
        {
            return new FileStream("Address.json", FileMode.Open);
        }


        static MemoryStream GetSmallMemoryStream()
        {
            var jsonSmallArray = """[12]""";

            byte[] bytes = Encoding.UTF8.GetBytes(jsonSmallArray);
            return new MemoryStream(bytes);
        }
        static MemoryStream GetMemoryStream()
        {
            const string jsonString =
                /*lang=json*/
                """
                [
                    {
                        "Date": "2019-08-01T00:00:00-07:00",
                        "Temperature": 25,
                        "TemperatureRanges": {
                            "Cold": { "High": 20, "Low": -10.5 },
                            "Hot": { "High": 60, "Low": 20 }
                        },
                        "Summary": "Hot"
                    }, 
                    {
                        "Date": "2019-08-01T00:00:00-07:00",
                        "Temperature": 25,
                        "TemperatureRanges": {
                            "Cold": { "High": 20, "Low": -10 },
                            "Hot": { "High": 60, "Low": 20 }
                        },
                        "Summary": "Hot"
                    }]
                """;

            byte[] bytes = Encoding.UTF8.GetBytes(jsonString);
            return new MemoryStream(bytes);
        }

    }

}
