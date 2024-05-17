using JsonExtensions;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Tests
{
    public class JsonReader_Values_Tests
    {
        private const string jsonInvalid =
            """
            {
                "a":::::::,,,,,,
            }
            """;

        private const string jsonUnbalancedObject =
            """
            {
                "a": 12,
                "b": 12,
                "c": 12
            """;

        private const string jsonUnbalancedArray =
            """
            [10,20,30
            """;

        private const string jsonSmallObject = 
            """
                                                                                                {
                "a": 12,
                "b": 12,
                "c": 12
            }
            """;

        private const string jsonSmallArray = """[12,12,12]""";

        private const string jsonArray =
            """
            [{
                "Date": "2019-08-01T00:00:00-07:00",
                "Temperature": 25,
                "TemperatureRanges": {
                    "Cold": { "High": 20, "Low": -10.5 },
                    "Hot": { "High": 60, "Low": 20 }
                },
                "Summary": "Hot",
                "IsHot": true
            }, 
            {
                "Date": "2019-08-01T00:00:00-07:00",
                "Temperature": 25,
                "TemperatureRanges": {
                    "Cold": { "High": 20, "Low": -10 },
                    "Hot": { "High": 60, "Low": 20 }
                },
                "Summary": "Hot",
                "IsHot": false
            }]
            """;

        private readonly ITestOutputHelper output;

        public JsonReader_Values_Tests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task LargeTokenGap_ShouldThrow()
        {
            var largeGapJson = $"{{ \"a\": {new String(' ', 2 * 1024 * 1024)}10 }}";
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(largeGapJson));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            await Assert.ThrowsAnyAsync<JsonException>(async () =>
            {
                await foreach(var v in jsonReader.Values())
                {
                    output.WriteLine($"{v.TokenType}");
                }
            });
        }

        [Fact]
        public async Task InvalidJson_ShouldThrow()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonInvalid));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            await Assert.ThrowsAnyAsync<JsonException>(async () =>
            {
                await foreach(var v in jsonReader.Values())
                {
                    output.WriteLine($"{v.TokenType}");
                }
            });
        }

        [Fact]
        public async Task UnbalancedObject_ShouldThrow()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonUnbalancedObject));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            var x = await Assert.ThrowsAnyAsync<JsonException>(async () =>
            {
                await foreach(var v in jsonReader.Values())
                {
                    output.WriteLine($"{v.TokenType}");
                }
            });
        }

        [Fact]
        public async Task UnbalancedArray_ShouldThrow()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonUnbalancedArray));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            await Assert.ThrowsAnyAsync<JsonException>(async () =>
            {
                await foreach(var v in jsonReader.Values())
                {
                    output.WriteLine($"{v.TokenType}");
                }
            });
        }

        [Fact]
        public async Task SmallObject_ShouldContainsAllTokens()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonSmallObject));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);
            var tokens = await jsonReader.Values().Select(x => x.TokenType).ToListAsync();

            Assert.Equal([
                JsonTokenType.StartObject,
                JsonTokenType.PropertyName,
                JsonTokenType.Number,
                JsonTokenType.PropertyName,
                JsonTokenType.Number,
                JsonTokenType.PropertyName,
                JsonTokenType.Number,
                JsonTokenType.EndObject],
             tokens);
        }

        [Fact]
        public async Task SmallArray_ShouldContainsAllTokens()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonSmallArray));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);
            var tokens = await jsonReader.Values().Select(x => x.TokenType).ToListAsync();

            Assert.Equal([
                JsonTokenType.StartArray,
                JsonTokenType.Number,
                JsonTokenType.Number,
                JsonTokenType.Number,
                JsonTokenType.EndArray],
             tokens);
        }


        [Fact]
        public async Task JsonArray_ShouldContainsValidStringTypes()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonArray));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);
            var tokens = await jsonReader.Values().Where(x => x.TokenType == JsonTokenType.String).Select(v => v.Value?.ToString()).ToListAsync();

            Assert.Equal(["2019-08-01T00:00:00-07:00", "Hot", "2019-08-01T00:00:00-07:00", "Hot"], tokens);
        }

        [Fact]
        public async Task JsonArray_ShouldContainsValidNumberTypes()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonArray));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);
            var tokens = await jsonReader.Values().Where(x => x.TokenType == JsonTokenType.Number).Select(v => v.Value.Deserialize<double>()).ToListAsync();

            Assert.Equal([25, 20, -10.5, 60, 20, 25, 20, -10, 60, 20], tokens);
        }

        [Fact]
        public async Task JsonArray_ShouldContainsValidBooleanTypes()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonArray));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);
            var tokens = await jsonReader.Values().Where(x => x.TokenType == JsonTokenType.False || x.TokenType == JsonTokenType.True).Select(v => v.Value.Deserialize<bool>()).ToListAsync();

            Assert.Equal([true, false], tokens);
        }
    }
}