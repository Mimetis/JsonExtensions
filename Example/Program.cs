using System.Text;
using System.Text.Json;
using JsonExtensions;

namespace ConsoleJsonSample
{
    internal class Program
    {
        // Main is mark as async (even if not needed)
        // to be sure we can call the JsonReader.Read() method from an async scope
        static async Task Main(string[] args)
        {
            //var jsonEnumerator = new JsonEnumerator(GetLittleMemoryStream(), 1024); // test 10 to see buffer increase in debug console

            //foreach (var prop in jsonEnumerator.Values())
            //{
            //    if (prop.TokenType == JsonTokenType.StartObject || prop.TokenType == JsonTokenType.StartArray || prop.TokenType == JsonTokenType.EndObject || prop.TokenType == JsonTokenType.EndArray)
            //        Console.WriteLine($"- ({prop.TokenType})");
            //    else if (prop.TokenType == JsonTokenType.PropertyName)
            //        Console.WriteLine($"Property: {prop.Name}");
            //    else
            //        Console.WriteLine($"Value: {prop.Value}");
            //}

            var jsonReader = new JsonReader(GetLittleMemoryStream(), 1024);
            while (jsonReader.Read())
            {
                //var prop = jsonReader.GetValue();

                //if (prop.TokenType == JsonTokenType.StartObject || prop.TokenType == JsonTokenType.StartArray || prop.TokenType == JsonTokenType.EndObject || prop.TokenType == JsonTokenType.EndArray)
                //    Console.WriteLine($"- ({prop.TokenType})");
                //else if (prop.TokenType == JsonTokenType.PropertyName)
                //    Console.WriteLine($"Property: {prop.Name}");
                //else
                //    Console.WriteLine($"Value: {prop.Value}");
            }

        }


        static FileStream GetFileStream()
        {
            return new FileStream("Address.json", FileMode.Open);
        }

        static MemoryStream GetMemoryStream()
        {
            var jsonString = @"[{
                ""Date"": ""2019-08-01T00:00:00-07:00"",
                ""Temperature"": 25,
                ""TemperatureRanges"": {
                    ""Cold"": { ""High"": 20, ""Low"": -10.5 },
                    ""Hot"": { ""High"": 60, ""Low"": 20 }
                },
                ""Summary"": ""Hot""
            }, 
            {
                ""Date"": ""2019-08-01T00:00:00-07:00"",
                ""Temperature"": 25,
                ""TemperatureRanges"": {
                    ""Cold"": { ""High"": 20, ""Low"": -10 },
                    ""Hot"": { ""High"": 60, ""Low"": 20 }
                },
                ""Summary"": ""Hot""
            }]";

            byte[] bytes = Encoding.UTF8.GetBytes(jsonString);
            return new MemoryStream(bytes);
        }

        static MemoryStream GetLittleMemoryStream()
        {
            var jsonString = @"{""Date"": ""2019-08-01T00:00:00-07:00""}";

            byte[] bytes = Encoding.UTF8.GetBytes(jsonString);
            return new MemoryStream(bytes);
        }

    }

}
