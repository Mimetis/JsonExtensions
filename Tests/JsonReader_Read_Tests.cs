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
            var jsonReader = new JsonReader(stream, bufferSize:10);

            jsonReader.Read();
            jsonReader.Read();

            Assert.ThrowsAny<JsonException>(() => jsonReader.Read());

        }

        [Fact]
        public async Task InvalidJson_ShouldThrow()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonInvalid));
            var jsonReader = new JsonReader(stream, bufferSize: 10);

            jsonReader.Read();
            jsonReader.Read();

            Assert.ThrowsAny<JsonException>(() => jsonReader.Read());
        }

        [Fact]
        public async Task UnbalancedObject_ShouldThrow()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonUnbalancedObject));
            var jsonReader = new JsonReader(stream, bufferSize: 10);

            jsonReader.Read();

            jsonReader.Read();
            jsonReader.Read();

            jsonReader.Read();
            jsonReader.Read();

            jsonReader.Read();
            Assert.ThrowsAny<JsonException>(() => jsonReader.Read());
        }

        [Fact]
        public async Task UnbalancedArray_ShouldThrow()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonUnbalancedArray));
            var jsonReader = new JsonReader(stream, bufferSize: 10);

            jsonReader.Read();

            jsonReader.Read();
            jsonReader.Read();

            Assert.ThrowsAny<JsonException>(() => jsonReader.Read());
        }

        [Fact]
        public async Task SmallObject_ShouldContainsAllTokens()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonSmallObject));
            var jsonReader = new JsonReader(stream, bufferSize: 10);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
        }

        [Fact]
        public async Task SmallArray_ShouldContainsAllTokens()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonSmallArray));
            var jsonReader = new JsonReader(stream, bufferSize: 10);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.StartArray, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.EndArray, jsonReader.TokenType);
        }


        [Fact]
        public async Task JsonArray_ShouldContainsValidStringTypes()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonArray));
            var jsonReader = new JsonReader(stream, bufferSize: 10);

            // first method: Using Read() the GetString()
            do { jsonReader.Read(); } while (jsonReader.TokenType != JsonTokenType.PropertyName && jsonReader.GetString() != "Date");
            var tmp = jsonReader.ReadAsString();
            Assert.Equal("2019-08-01T00:00:00-07:00", tmp);

            // second method: Using ReadAsString() method
            while (jsonReader.Read() && (jsonReader.TokenType != JsonTokenType.PropertyName || jsonReader.GetString() != "Summary")) { };

            // skip to value and use GetAsString()
            jsonReader.Skip();
            Assert.Equal("Hot", jsonReader.GetString());
        }

        [Fact]
        public async Task ReadAsEscapedString_ShouldReturnEscapedString()
        {
            const string jsonEscapedString = 
                """
                "Hello\nWorld"
                """;

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonEscapedString));
            var jsonReader = new JsonReader(stream, bufferSize: 10);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
            var result = jsonReader.GetEscapedString();

            const string escapedString = """Hello\nWorld""";

            Assert.Equal(escapedString, result);
        }

        [Fact]
        public async Task ReadAsString_ShouldReturnUnescapedString()
        {
            const string jsonEscapedString = 
                """
                "Hello\nWorld"
                """;

            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonEscapedString));
            var jsonReader = new JsonReader(stream, bufferSize: 10);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
            var result = jsonReader.GetString();

            const string unescapedString = "Hello\nWorld";

            Assert.Equal(unescapedString, result);
        }

        [Fact]
        public async Task JsonArray_ShouldContainsValidBooleans()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonArray));
            var jsonReader = new JsonReader(stream, bufferSize: 10);

            // select first token
            jsonReader.Values().First(jr => jr.Value?.ToString() == "IsHot");

            // skip to value
            jsonReader.Skip();

            // asert Current
            Assert.True(jsonReader.GetBoolean());

            // select next token
            jsonReader.Values().First(jr => jr.Value?.ToString() == "IsHot");

            // asert Current
            Assert.False(jsonReader.ReadAsBoolean());
        }

        [Fact]
        public async Task PropertyNameToken_ShouldHaveCorrectValue()
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonSmallObject));
            var jsonReader = new JsonReader(stream, bufferSize: 10);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
            Assert.Equal("a", jsonReader.GetString());

            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
            Assert.Equal("b", jsonReader.GetString());

            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
            Assert.Equal("c", jsonReader.GetString());

            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.TokenType);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
        }
    }
}