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
            var jsonReader = new JsonReader(GetMemoryStream(), 1024); // test 10 to see buffer increase in debug console

            //foreach (var prop in jsonReader.Values())
            //{
            //    if (prop.TokenType == JsonTokenType.StartObject || prop.TokenType == JsonTokenType.StartArray || prop.TokenType == JsonTokenType.EndObject || prop.TokenType == JsonTokenType.EndArray)
            //        Console.WriteLine($"- ({prop.TokenType})");
            //    else if (prop.TokenType == JsonTokenType.PropertyName)
            //        Console.WriteLine($"Property: {prop.Name}");
            //    else
            //        Console.WriteLine($"Value: {prop.Value}");
            //}

            while (jsonReader.Read())
            {

                if (jsonReader.Current.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = jsonReader.Current.Name;
                    jsonReader.Skip();
                    var value = jsonReader.Current.Value;
                    Console.WriteLine($"{prop}: {value}");
                }
            }

            while (jsonReader.Read())
            {
                Console.WriteLine(jsonReader);
            }
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

    }

}
