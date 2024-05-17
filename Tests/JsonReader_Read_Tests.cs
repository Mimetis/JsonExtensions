using JsonExtensions;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Tests
{
    public class JsonReader_Read_Tests
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

        public JsonReader_Read_Tests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task LargeTokenGap_ShouldThrow()
        {
            var largeGapJson = $"{{ \"a\": {new String(' ', 2 * 1024 * 1024)}10 }}";
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(largeGapJson));
            using var jsonReader = new JsonReader(stream, bufferSize:10);

            await jsonReader.ReadAsync();
            await jsonReader.ReadAsync();

            await Assert.ThrowsAnyAsync<JsonException>(async () => await jsonReader.ReadAsync());

        }

        [Fact]
        public async Task InvalidJson_ShouldThrow()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonInvalid));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            await jsonReader.ReadAsync();
            await jsonReader.ReadAsync();

            await Assert.ThrowsAnyAsync<JsonException>(async () => await jsonReader.ReadAsync());
        }

        [Fact]
        public async Task UnbalancedObject_ShouldThrow()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonUnbalancedObject));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            await jsonReader.ReadAsync();

            await jsonReader.ReadAsync();
            await jsonReader.ReadAsync();

            await jsonReader.ReadAsync();
            await jsonReader.ReadAsync();

            await jsonReader.ReadAsync();
            await Assert.ThrowsAnyAsync<JsonException>(async () => await jsonReader.ReadAsync());
        }

        [Fact]
        public async Task UnbalancedArray_ShouldThrow()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonUnbalancedArray));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            await jsonReader.ReadAsync();

            await jsonReader.ReadAsync();
            await jsonReader.ReadAsync();

            await Assert.ThrowsAnyAsync<JsonException>(async () => await jsonReader.ReadAsync());

        }

        [Fact]
        public async Task SmallObject_ShouldContainsAllTokens()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonSmallObject));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);

        }

        [Fact]
        public async Task SmallArray_ShouldContainsAllTokens()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonSmallArray));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.StartArray, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.EndArray, jsonReader.TokenType);
        }


        [Fact]
        public async Task JsonArray_ShouldContainsValidStringTypes()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonArray));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            // first method: Using Read() the GetString()
            do { await jsonReader.ReadAsync(); } while (jsonReader.TokenType != JsonTokenType.PropertyName && jsonReader.GetString() != "Date");
            var tmp = await jsonReader.ReadAsString();
            Assert.Equal("2019-08-01T00:00:00-07:00", tmp);

            // second method: Using ReadAsString() method
            while (await jsonReader.ReadAsync() && (jsonReader.TokenType != JsonTokenType.PropertyName || jsonReader.GetString() != "Summary")) { };

            // skip to value and use GetAsString()
            await jsonReader.SkipAsync();
            Assert.Equal("Hot", jsonReader.GetString());
        }



        [Fact]
        public async Task JsonArray_ShouldContainsValidBooleans()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonArray));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            // select first token
            await jsonReader.Values().FirstAsync(jr => jr.Value?.ToString() == "IsHot");

            // skip to value
            await jsonReader.SkipAsync();

            // asert Current
            Assert.True(jsonReader.GetBoolean());

            // select next token
            await jsonReader.Values().FirstAsync(jr => jr.Value?.ToString() == "IsHot");

            var result = await jsonReader.ReadAsBoolean();

            // asert Current
            Assert.False(result);
        }

        [Fact]
        public async Task ReadAsEscapedString_ShouldReturnEscapedString()
        {
            const string jsonEscapedString = "\"Hello\\nWorld\"";

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonEscapedString));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
            var result = jsonReader.GetEscapedString();

            Assert.Equal("Hello\\nWorld", result);
        }

        [Fact]
        public async Task ReadAsString_ShouldReturnUnescapedString()
        {
            const string jsonEscapedString = "\"Hello\\n\\u003EWorld\"";

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonEscapedString));
            using var jsonReader = new JsonReader(stream, bufferSize: 10);

            await jsonReader.ReadAsync();
            Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
            var result = jsonReader.GetString();

            const string jsonUnescapedString = "Hello\n>World";

            Assert.Equal(jsonUnescapedString, result);
        }
    }
}