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

    public class JsonReader : IDisposable
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

            // create a buffer to read the stream into
            this.buffer = new byte[bufferSize];
            this.dataLen = 0;
            this.dataPos = 0;
            this.isFinalBlock = false;
            this.currentState = new JsonReaderState(jsonReaderOptions);
            this.tokensFound = 0;
            this.bytesConsumed = 0;
        }

        // buffer used by Utf8JsonReader to read values
        private byte[]? buffer;

        private int dataLen;
        private int dataPos;

        // number of tokens found in the buffer.
        // if this is 0, it means we need to read more data from the stream
        private int tokensFound;

        // if this is true, it means we have reached the end of the stream
        private bool isFinalBlock;

        // state object used internally by Utf8JsonReader
        private JsonReaderState currentState;

        // bytes consumed by Utf8JsonReader each time it reads a token
        private int bytesConsumed;

        // token found in the current buffer
        private bool foundToken;


        private bool disposedValue;



        /// <summary>
        /// Current value
        /// </summary>
        public JsonReaderValue? Current { get; private set; }

        /// <summary>
        /// Read the next value. Can be any TokenType
        /// </summary>
        /// <returns>true if a token has been read otherwise false</returns>
        /// <exception cref="JsonException"></exception>
        public bool Read()
        {
            if (this.buffer == null)
                throw new ArgumentNullException(nameof(this.buffer));

            bool foundToken = false;

            while (!foundToken)
            {
                // at this point, if there's already any data in the buffer, it has been shifted to start at index 0
                if (this.dataLen < this.buffer.Length && !this.isFinalBlock && !this.foundToken)
                {
                    // there's space left in the buffer, try to fill it with new data
                    int todo = this.buffer.Length - this.dataLen;
                    int done = this.Stream.Read(this.buffer, this.dataLen, todo);
                    this.dataLen += done;
                    this.isFinalBlock = (done < todo);
                    this.bytesConsumed = 0;
                    this.tokensFound = 0;
                }

                this.dataPos += this.bytesConsumed;
                this.dataLen -= this.bytesConsumed;

                // create a new ref struct json reader
                var spanBuffer = new ReadOnlySpan<byte>(this.buffer, this.dataPos, this.dataLen);
                // Trace.WriteLine($"span starting from {dataPos} : {BitConverter.ToString(spanBuffer.ToArray())}");

                var reader = new Utf8JsonReader(spanBuffer, this.isFinalBlock, state: this.currentState);

                // try to read nex token
                foundToken = reader.Read();

                // we have a valid token
                if (foundToken)
                {
                    this.currentState = reader.CurrentState;
                    this.bytesConsumed = (int)reader.BytesConsumed;
                    this.tokensFound++;
                    this.foundToken = true;
                    this.Current = GetValue(ref reader);
                    return true;
                }

                if (!this.isFinalBlock)
                {
                    // regardless if we found tokens or not, there may be data for a partial token remaining at the end.
                    if (this.dataPos > 0)
                    {
                        // Shift partial token data to the start of the buffer
                        Array.Copy(this.buffer, this.dataPos, this.buffer, 0, this.dataLen);
                        this.dataPos = 0;
                    }

                    if (this.tokensFound == 0)
                    {
                        // we didn't find any tokens in the current buffer, so it needs to expand.
                        if (this.buffer.Length > MaxTokenGap)
                            throw new JsonException($"sanity check on input stream failed, json token gap of more than {MaxTokenGap} bytes");

                        Array.Resize(ref this.buffer, this.buffer.Length * 2);
                    }
                }
                else
                {
                    foundToken = false;
                }

            }

            return false;

        }

        /// <summary>
        /// Enumerate over the stream and read the properties
        /// </summary>
        /// <returns></returns>
        public IEnumerable<JsonReaderValue> Values()
        {
            while (this.Read())
            {
                if (this.Current != null)
                    yield return this.Current;
            }
        }

        private static JsonReaderValue GetValue(ref Utf8JsonReader reader)
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray or JsonTokenType.EndObject or JsonTokenType.EndArray)
                return new JsonReaderValue { TokenType = reader.TokenType };

            if (reader.TokenType == JsonTokenType.PropertyName)
                return new JsonReaderValue { TokenType = reader.TokenType, Name = reader.GetString() };

            JsonValue? propertyValue = null;
            if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.None)
                propertyValue = null;
            else if (reader.TokenType == JsonTokenType.String)
                propertyValue = JsonValue.Create(reader.GetString());
            else if (reader.TokenType == JsonTokenType.False || reader.TokenType == JsonTokenType.True)
                propertyValue = JsonValue.Create(reader.GetBoolean());
            else if (reader.TokenType == JsonTokenType.Number)
                propertyValue = JsonValue.Create(reader.GetDouble());

            return new JsonReaderValue { Value = propertyValue, TokenType = reader.TokenType };

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (this.buffer != null)
                    {
                        Array.Clear(this.buffer);
                        this.buffer = null;
                    }
                    this.Current = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class JsonReaderValue
    {
        public string? Name { get; set; }
        public JsonValue? Value { get; set; }
        public JsonTokenType TokenType { get; set; }
    }
}
