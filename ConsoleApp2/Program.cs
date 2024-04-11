﻿using System.Text.Json;
using System.Text;
using System.IO.Pipelines;
using System.Buffers;

namespace ConsoleApp2
{
    internal class Program
    {
        static async Task Main(string[] args)
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

            // Will eventually be replaced with a FileStream
            byte[] bytes = Encoding.UTF8.GetBytes(jsonString);
            var stream = new MemoryStream(bytes);

            var jsonReader = new JsonReader(stream, 30);

            foreach (var prop in jsonReader.Read())
            {
                if (!string.IsNullOrEmpty(prop.Name))
                {
                    Console.WriteLine($"{prop.Name} ({prop.TokenType}): {prop.Value}");
                }
                else
                {
                    Console.WriteLine($"- {prop.TokenType}");
                }

            }

        }
    }

}
