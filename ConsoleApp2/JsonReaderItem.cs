using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConsoleApp2
{
    public class JsonReaderItem
    {

        public static JsonProperty GetNextJsonProperty(Stream stream)
        {
            if (stream.Position > 0)
                stream.Seek(0, SeekOrigin.Begin);

            var buffer = new byte[1024];
            stream.Read(buffer);


            var jsonReaderstate = new JsonReaderState(new JsonReaderOptions { AllowTrailingCommas = true });
            var reader = new Utf8JsonReader(buffer, isFinalBlock: false, state: jsonReaderstate);

            var jsonProperty = InnerRead(ref reader);

            return jsonProperty;
        }

        private static JsonProperty InnerRead(ref Utf8JsonReader reader, Stream stream)
        {
            try
            {
                while (!reader.Read())
                {
                    if (stream.Position >= stream.Length)
                        return null;

                    GetMoreBytesFromStream(stream, ref buffer, ref reader);
                }

                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray || reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
                    return new JsonProperty { TokenType = reader.TokenType };

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    object propertyValue = null;

                    while (!reader.Read())
                        GetMoreBytesFromStream(Stream, ref buffer, ref reader);

                    if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.None)
                        propertyValue = null;
                    else if (reader.TokenType == JsonTokenType.String)
                        propertyValue = reader.GetString();
                    else if (reader.TokenType == JsonTokenType.False || reader.TokenType == JsonTokenType.True)
                        propertyValue = reader.GetBoolean();
                    else if (reader.TokenType == JsonTokenType.Number)
                        propertyValue = reader.GetDouble();

                    return new JsonProperty { Name = propertyName, Value = propertyValue, TokenType = reader.TokenType };
                }

                return null;
            }
            catch (JsonException)
            {
                if (Stream.Position >= Stream.Length)
                    return null;

                throw;
            }
        }

        private static void GetMoreBytesFromStream(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
        {
            int bytesRead;
            if (reader.BytesConsumed < buffer.Length)
            {
                ReadOnlySpan<byte> leftover = buffer.AsSpan((int)reader.BytesConsumed);

                if (leftover.Length == buffer.Length)
                {
                    Array.Resize(ref buffer, buffer.Length * 2);
                    Console.WriteLine($"Increased buffer size to {buffer.Length}");
                }

                leftover.CopyTo(buffer);
                bytesRead = stream.Read(buffer.AsSpan(leftover.Length));
            }
            else
            {
                bytesRead = stream.Read(buffer);
            }
            //Console.WriteLine($"String in buffer is: {Encoding.UTF8.GetString(buffer)}");
            reader = new Utf8JsonReader(buffer, isFinalBlock: bytesRead == 0, reader.CurrentState);
        }


    }
}
