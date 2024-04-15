using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace JsonExtensions
{

    public class JsonReader
    {
        /// <summary>
        /// Stream to read
        /// </summary>
        public Stream Stream { get; }

        // buffer size
        private readonly int bufferSize;
        private readonly JsonReaderOptions jsonReaderOptions;
        private const int MaxTokenGap = 1024 * 1024;

        /// <summary>
        /// Create a fast forward json reader
        /// </summary>
        /// <param name="stream">Stream to read</param>
        /// <param name="bufferSize">buffer size. will adapt if needed</param>
        /// <exception cref="Exception">If stream is not readable</exception>
        public JsonReader(Stream stream, int bufferSize = 1024, JsonReaderOptions jsonReaderOptions = default)
        {
            this.Stream = stream;
            this.bufferSize = bufferSize;
            this.jsonReaderOptions = jsonReaderOptions;

            if (!this.Stream.CanRead)
                throw new Exception("Stream is not readable");
        }


        /// <summary>
        /// Enumerate over the stream and read the properties
        /// </summary>
        /// <returns></returns>
        public IEnumerable<JsonReaderValue> Read()
        {
            // state shared accross all instances of Utf8JsonReader
            var currentState = new JsonReaderState(jsonReaderOptions);

            // create a buffer to read the stream into
            var buffer = new byte[bufferSize];
            int dataLen = 0;
            bool isFinalBlock = false;

            while(!isFinalBlock)
            {
                // at this point, if there's already any data in the buffer, it has been shifted to start at index 0

                if(dataLen < buffer.Length)
                {
                    // there's space left in the buffer, try to fill it with new data
                    int todo = buffer.Length - dataLen;
                    int done = Stream.Read(buffer, dataLen, todo);
                    dataLen += done;
                    isFinalBlock = (done < todo);
                }

                bool foundToken;
                int tokensFound = 0;
                int dataPos = 0;

                do
                {
                    // create a new ref struct json reader
                    var spanBuffer = new ReadOnlySpan<byte>(buffer,dataPos,dataLen);
                    // Trace.WriteLine($"span starting from {dataPos} : {BitConverter.ToString(spanBuffer.ToArray())}");

                    var reader = new Utf8JsonReader(spanBuffer, isFinalBlock, state: currentState);

                    if(InnerTryRead(ref reader, out var jsonProperty))
                    {
                        foundToken = true;
                        currentState = reader.CurrentState;
                        dataPos += (int)reader.BytesConsumed;
                        dataLen -= (int)reader.BytesConsumed;
                        tokensFound++;
                        yield return jsonProperty!;
                    }
                    else
                    {
                        foundToken = false;
                    }
                } while(foundToken);

                if(!isFinalBlock)
                {
                    // regardless if we found tokens or not, there may be data for a partial token remaining at the end.
                    if(dataPos > 0)
                    {
                        // Shift partial token data to the start of the buffer
                        Array.Copy(buffer, dataPos, buffer, 0, dataLen);
                    }

                    if(tokensFound == 0)
                    {
                        // we didn't find any tokens in the current buffer, so it needs to expand.
                        if(buffer.Length > MaxTokenGap)
                        {
                            throw new JsonException($"sanity check on input stream failed, json token gap of more than {MaxTokenGap} bytes");
                        }
                        Array.Resize(ref buffer, buffer.Length * 2);
                    }
                }
            }
        }

        /// <summary>
        /// Try to read the next token from the buffer
        /// </summary>
        private static bool InnerTryRead(ref Utf8JsonReader reader, out JsonReaderValue? value)
        {
            if (!reader.Read())
            {
                value = null;
                return false;
            }

            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray or JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                value = new JsonReaderValue { TokenType = reader.TokenType };
                return true;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                value = new JsonReaderValue { TokenType = reader.TokenType, Name = reader.GetString() };
                return true;
            }

            JsonValue? propertyValue = null;
            if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.None)
                propertyValue = null;
            else if (reader.TokenType == JsonTokenType.String)
                propertyValue = JsonValue.Create(reader.GetString());
            else if (reader.TokenType == JsonTokenType.False || reader.TokenType == JsonTokenType.True)
                propertyValue = JsonValue.Create(reader.GetBoolean());
            else if (reader.TokenType == JsonTokenType.Number)
                propertyValue = JsonValue.Create(reader.GetDouble());

            value = new JsonReaderValue { Value = propertyValue, TokenType = reader.TokenType };
            return true;
        }
    }

    public class JsonReaderValue
    {
        public string? Name { get; set; }
        public JsonValue? Value { get; set; }
        public JsonTokenType TokenType { get; set; }
    }
}
