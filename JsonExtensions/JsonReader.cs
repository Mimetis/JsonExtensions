using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

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

        // more tokens to be found
        private bool hasMore;

        private bool disposedValue;


        /// <summary>
        /// Current value
        /// </summary>
        public JsonReaderValue Current { get; private set; }

        /// <summary>
        /// Read the next value. Can be any TokenType
        /// </summary>
        /// <returns>true if a token has been read otherwise false</returns>
        /// <exception cref="JsonException"></exception>
        public bool Read()
        {
            if (this.buffer == null)
                throw new ArgumentNullException(nameof(this.buffer));

            // if we don't have any more bytes and in final block, we can exit
            if (this.dataLen <= 0 && this.isFinalBlock)
                return false;

            bool foundToken = false;

            while (!foundToken)
            {
                // at this point, if there's already any data in the buffer, it has been shifted to start at index 0
                if (this.dataLen < this.buffer.Length && !this.isFinalBlock && !this.hasMore)
                {
                    // there's space left in the buffer, try to fill it with new data
                    int todo = this.buffer.Length - this.dataLen;
                    int done = this.Stream.Read(this.buffer, this.dataLen, todo);
                    this.dataLen += done;
                    this.isFinalBlock = done < todo;
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
                    this.hasMore = true;
                    this.Current = GetValue(ref reader);
                    return true;
                }


                // if we don't have any more bytes and in final block, we can exit
                if (this.dataLen <= 0 && this.isFinalBlock)
                    break;

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

                    this.hasMore = false;
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
                yield return this.Current;
            }
        }


        public bool Skip()
        {
            if (this.Current.TokenType == JsonTokenType.PropertyName)
                return this.Read();

            if (this.Current.TokenType == JsonTokenType.StartObject || this.Current.TokenType == JsonTokenType.StartArray)
            {
                int depth = this.Current.Depth;
                do
                {
                    bool hasRead = this.Read();

                    if (!hasRead)
                        return false;
                }
                while (depth < this.Current.Depth);

                return true;
            }
            return false;
        }

        private static JsonReaderValue GetValue(ref Utf8JsonReader reader)
        {
            if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray or JsonTokenType.EndObject or JsonTokenType.EndArray)
                return new JsonReaderValue { TokenType = reader.TokenType, Depth = reader.CurrentDepth };

            if (reader.TokenType == JsonTokenType.PropertyName)
                return new JsonReaderValue { TokenType = reader.TokenType, Name = reader.GetString(), Depth = reader.CurrentDepth };

            JsonValue? propertyValue = null;
            if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.None)
                propertyValue = null;
            else if (reader.TokenType == JsonTokenType.String)
                propertyValue = JsonValue.Create(reader.GetString());
            else if (reader.TokenType == JsonTokenType.False || reader.TokenType == JsonTokenType.True)
                propertyValue = JsonValue.Create(reader.GetBoolean());
            else if (reader.TokenType == JsonTokenType.Number)
                propertyValue = JsonValue.Create(reader.GetDouble());

            return new JsonReaderValue { Value = propertyValue, TokenType = reader.TokenType, Depth = reader.CurrentDepth };

        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    if (this.buffer != null)
                    {
#if NET6_0_OR_GREATER
                        Array.Clear(this.buffer);
#else
                        Array.Clear(this.buffer, 0, this.buffer.Length);
#endif
                        this.buffer = null;
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return this.Current.ToString();
        }

    }

    public struct JsonReaderValue
    {

        public string? Name { get; set; } = string.Empty;
        public JsonValue? Value { get; set; }
        public JsonTokenType TokenType { get; set; } = JsonTokenType.None;
        public int Depth { get; set; } = 0;

        public JsonReaderValue() { }

        public override string ToString()
        {
            var sb = new StringBuilder($"Type: {this.TokenType} - Depth: {this.Depth}");

            if (this.TokenType == JsonTokenType.PropertyName)
                sb.Append($" - Property: {this.Name}");

            if (this.Value != null)
                sb.Append($" - Value: {this.Value}");

            return sb.ToString();
        }
    }
}
