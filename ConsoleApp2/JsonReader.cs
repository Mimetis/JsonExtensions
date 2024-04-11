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
        private readonly JsonReaderOptions jsonReaderOptions;


        /// <summary>
        /// Create a fast forward json reader
        /// </summary>
        /// <param name="stream">Stream to read</param>
        /// <param name="bufferSize">buffer size. will adapat if needed</param>
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
            var currentState = new JsonReaderState(jsonReaderOptions);

            // create a buffer to read the stream into
            var buffer = new byte[bufferSize];

            if (this.Stream.Position > 0)
                this.Stream.Seek(0, SeekOrigin.Begin);

            // start reading the stream
            var streamReadLength = Stream.Read(buffer);

            if (streamReadLength == 0)
                yield break;

            // these two variables are used to keep track of the overall bytes consumed until the buffer is reset
            var overAllBytesConsumedUntilResetBuffer = 0;
            var tokenCountReadUntilResetBuffer = 0;

            while (true)
            {
                // move the buffer to the next position according to the overall (in progress) bytes consumed
                var spanBuffer = buffer.AsSpan(overAllBytesConsumedUntilResetBuffer);

                // create a new ref struct json reader
                var reader = new Utf8JsonReader(spanBuffer, isFinalBlock: false, state: currentState);
                JsonReaderValue jsonProperty;
                try
                {
                    // if we have at leat one token in the buffer, we can try to read it
                    if (!InnerTryRead(ref reader, out jsonProperty))
                    {
                        // if we have not read at least one token in the buffer, we need to read more bytes from the stream and increase the buffer size
                        // else we are at the end of the current buffer and we can move the buffer back to the initial position (and fill with bytes from the stream)
                        if (tokenCountReadUntilResetBuffer == 0)
                        {
                            GetMoreBytesFromStream(Stream, ref buffer, ref reader);
                        }
                        else
                        {
                            streamReadLength = MoveBufferBackToInitialPosition(ref buffer, overAllBytesConsumedUntilResetBuffer);

                            tokenCountReadUntilResetBuffer = 0;
                            overAllBytesConsumedUntilResetBuffer = 0;

                            // if we have read 0 bytes, we are at the end of the stream
                            if (streamReadLength == 0)
                                yield break;
                        }

                        // loop again to try to read the next token from the buffer
                        continue;
                    }

                    // we have read at least one token
                    tokenCountReadUntilResetBuffer++;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine(ex.Message);
                    if (Stream.Position >= Stream.Length)
                        yield break;

                    throw;
                }

                // temp save
                int bytesConsumed = (int)reader.BytesConsumed;
                overAllBytesConsumedUntilResetBuffer += bytesConsumed;
                currentState = reader.CurrentState;

                // end of stream
                if (jsonProperty == null && Stream.Position >= Stream.Length)
                    yield break;

                yield return jsonProperty;
            }
        }



        /// <summary>
        /// Try to read the next token from the buffer
        /// </summary>
        private bool InnerTryRead(ref Utf8JsonReader reader, out JsonReaderValue value)
        {
            try
            {
                if (!reader.Read())
                {
                    value = null;
                    return false;
                }

                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray || reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
                {
                    value = new JsonReaderValue { TokenType = reader.TokenType };
                    return true;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    value = new JsonReaderValue { TokenType = reader.TokenType, Name = reader.GetString() };
                    return true;
                }

                JsonValue propertyValue = null;
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
            catch (JsonException ex)
            {
                Console.WriteLine(ex.Message);

                if (Stream.Position >= Stream.Length)
                {
                    value = null;
                    return false;
                }

                throw;
            }
        }


        /// <summary>
        /// Move back the buffer to the initial position and fill empty bytes with stream bytes
        /// </summary>
        private int MoveBufferBackToInitialPosition(ref byte[] buffer, int overAllBytesConsumedUntilResetBuffer)
        {
            // prepare the buffer
            ReadOnlySpan<byte> leftover = buffer.AsSpan((int)overAllBytesConsumedUntilResetBuffer);
            // copy the leftover bytes to the beginning of the buffer
            leftover.CopyTo(buffer);

            // if needed, read more bytes from the stream
            return Stream.Read(buffer.AsSpan(leftover.Length));

        }

        /// <summary>
        /// Get more bytes from the stream because the reader has consumed all the bytes in the buffer and this buffer is not long enough to hold the next token
        /// </summary>
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
