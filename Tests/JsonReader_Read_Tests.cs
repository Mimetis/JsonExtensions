using JsonExtensions;
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
        public void LargeTokenGap_ShouldThrow()
        {
            var largeGapJson = $"{{ \"a\": {new String(' ', 2 * 1024 * 1024)}10 }}";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(largeGapJson));
            var jsonReader = new JsonReader(stream, 10);

            jsonReader.Read();
            jsonReader.Read();

            Assert.ThrowsAny<JsonException>(() => jsonReader.Read());

        }

        [Fact]
        public void InvalidJson_ShouldThrow()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonInvalid));
            var jsonReader = new JsonReader(stream, 10);

            jsonReader.Read();
            jsonReader.Read();

            Assert.ThrowsAny<JsonException>(() => jsonReader.Read());


        }

        [Fact]
        public void UnbalancedObject_ShouldThrow()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonUnbalancedObject));
            var jsonReader = new JsonReader(stream, 10);

            jsonReader.Read();

            jsonReader.Read();
            jsonReader.Read();

            jsonReader.Read();
            jsonReader.Read();

            jsonReader.Read();
            Assert.ThrowsAny<JsonException>(() => jsonReader.Read());
        }

        [Fact]
        public void UnbalancedArray_ShouldThrow()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonUnbalancedArray));
            var jsonReader = new JsonReader(stream, 10);

            jsonReader.Read();

            jsonReader.Read();
            jsonReader.Read();

            Assert.ThrowsAny<JsonException>(() => jsonReader.Read());

        }

        [Fact]
        public void SmallObject_ShouldContainsAllTokens()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonSmallObject));
            var jsonReader = new JsonReader(stream, 10);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.StartObject, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.PropertyName, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.EndObject, jsonReader.Current.TokenType);

        }

        [Fact]
        public void SmallArray_ShouldContainsAllTokens()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonSmallArray));
            var jsonReader = new JsonReader(stream, 10);

            jsonReader.Read();
            Assert.Equal(JsonTokenType.StartArray, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.Number, jsonReader.Current.TokenType);
            jsonReader.Read();
            Assert.Equal(JsonTokenType.EndArray, jsonReader.Current.TokenType);
        }


        [Fact]
        public void JsonArray_ShouldContainsValidStringTypes()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonArray));
            var jsonReader = new JsonReader(stream, 10);

            do { jsonReader.Read(); } while (jsonReader.Current.Name != "Date");
            jsonReader.Skip();
            Assert.Equal("2019-08-01T00:00:00-07:00", jsonReader.Current.Value?.GetValue<string>());

            do { jsonReader.Read(); } while (jsonReader.Current.Name != "Summary");
            jsonReader.Skip();
            Assert.Equal("Hot", jsonReader.Current.Value?.GetValue<string>());
        }



        [Fact]
        public void JsonArray_ShouldContainsValidBooleans()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonArray));
            var jsonReader = new JsonReader(stream, 10);

            // select a token
            jsonReader.Values().First(jr => jr.Name == "IsHot");

            // skip to value
            jsonReader.Skip();

            // asert Current
            Assert.Equal(true, jsonReader.Current.Value?.GetValue<Boolean>());
        }

    }
}