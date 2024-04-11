using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace ConsoleApp2
{

    public class JsonReader
    {
        /// <summary>
        /// Stream to read
        /// </summary>
        public Stream Stream { get; }

        // buffer size
        private readonly int bufferSize;


        /// <summary>
        /// Create a fast forward json reader
        /// </summary>
        /// <param name="stream">Stream to read</param>
        /// <param name="bufferSize">buffer size. will adapat if needed</param>
        /// <exception cref="Exception">If stream is not readable</exception>
        public JsonReader(Stream stream, int bufferSize = 1024)
        {
            this.Stream = stream;
            this.bufferSize = bufferSize;

            if (!this.Stream.CanRead)
                throw new Exception("Stream is not readable");
        }

        /// <summary>
        /// Enumerate over the stream and read the properties
        /// </summary>
        /// <returns></returns>
        public IEnumerable<JsonReaderValue> Read()
        {
            var currentState = new JsonReaderState(new JsonReaderOptions { AllowTrailingCommas = true });
            int bytesConsumed = 0;

            // create a buffer to read the stream into
            var buffer = new byte[bufferSize];

            if (this.Stream.Position > 0)
                this.Stream.Seek(0, SeekOrigin.Begin);

            // start reading the stream
            Stream.Read(buffer);

            // iterate over the stream
            while (true)
            {
                // don't need on first pass
                if (bytesConsumed > 0)
                {
                    // prepare the buffer
                    ReadOnlySpan<byte> leftover = buffer.AsSpan((int)bytesConsumed);
                    // copy the leftover bytes to the beginning of the buffer
                    leftover.CopyTo(buffer);
                    // if needed, read more bytes from the stream
                    Stream.Read(buffer.AsSpan(leftover.Length));
                }

                // create a new ref struct json reader
                var reader = new Utf8JsonReader(buffer, isFinalBlock: false, state: currentState);
                JsonReaderValue jsonProperty;
                try
                {
                    jsonProperty = InnerRead(ref reader, ref buffer);

                }
                catch (JsonException)
                {
                    if (Stream.Position >= Stream.Length)
                        yield break;

                    throw;
                }

                // temp save
                bytesConsumed = (int)reader.BytesConsumed;
                currentState = reader.CurrentState;

                // end of stream
                if (jsonProperty == null && Stream.Position >= Stream.Length)
                    yield break;

                yield return jsonProperty;
            }
        }


        // Read the next property from the buffer
        private JsonReaderValue InnerRead(ref Utf8JsonReader reader, ref byte[] buffer)
        {
            try
            {
                while (!reader.Read())
                {
                    if (Stream.Position >= Stream.Length)
                        return null;

                    GetMoreBytesFromStream(Stream, ref buffer, ref reader);
                }

                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray || reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
                    return new JsonReaderValue { TokenType = reader.TokenType };

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    JsonValue propertyValue = null;

                    while (!reader.Read())
                        GetMoreBytesFromStream(Stream, ref buffer, ref reader);

                    if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.None)
                        propertyValue = null;
                    else if (reader.TokenType == JsonTokenType.String)
                        propertyValue = JsonValue.Create(reader.GetString());
                    else if (reader.TokenType == JsonTokenType.False || reader.TokenType == JsonTokenType.True)
                        propertyValue = JsonValue.Create(reader.GetBoolean());
                    else if (reader.TokenType == JsonTokenType.Number)
                        propertyValue = JsonValue.Create(reader.GetDouble());

                    return new JsonReaderValue { Name = propertyName, Value = propertyValue, TokenType = reader.TokenType };
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

        // Get more bytes from the stream because the reader has consumed all the bytes in the buffer and this buffer is not long enough to hold the next token
        private static void GetMoreBytesFromStream(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
        {
            int bytesRead;
            if (reader.BytesConsumed < buffer.Length)
            {
                ReadOnlySpan<byte> leftover = buffer.AsSpan((int)reader.BytesConsumed);

                if (leftover.Length == buffer.Length)
                    Array.Resize(ref buffer, buffer.Length * 2);

                leftover.CopyTo(buffer);
                bytesRead = stream.Read(buffer.AsSpan(leftover.Length));
            }
            else
            {
                bytesRead = stream.Read(buffer);
            }
            reader = new Utf8JsonReader(buffer, isFinalBlock: bytesRead == 0, reader.CurrentState);
        }
    }



    public class JsonReaderValue
    {
        public string Name { get; set; }
        public JsonValue Value { get; set; }
        public JsonTokenType TokenType { get; set; }
    }
}
