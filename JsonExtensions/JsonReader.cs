using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace JsonExtensions
{
    public class JsonReader : IDisposable
    {
        // encoding used to convert bytes to string
        private static readonly UTF8Encoding utf8Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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
        public JsonReader(Stream stream, JsonReaderOptions jsonReaderOptions = default, int bufferSize = 1024)
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
        private JsonReaderState prevState;

        // bytes consumed by Utf8JsonReader each time it reads a token
        private int bytesConsumed;

        // more tokens to be found
        private bool hasMore;

        private bool disposedValue;


        ///// <summary>
        ///// Current value
        ///// </summary>
        //public JsonReaderValue Current { get; private set; }

        /// <summary>
        /// Gets the token value. Can be a value or a property name
        /// </summary>
        public ReadOnlyMemory<byte> Value { get; private set; }

        /// <summary>
        /// Gets the token type
        /// </summary>
        public JsonTokenType TokenType { get; private set; } = JsonTokenType.None;

        /// <summary>
        /// Gets the current depth
        /// </summary>
        public int Depth { get; private set; } = 0;

        /// <summary>
        /// Read the next value. Can be any TokenType
        /// </summary>
        /// <returns>true if a token has been read otherwise false</returns>
        /// <exception cref="JsonException"></exception>
        public async ValueTask<bool> ReadAsync()
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
                    int done = await this.Stream.ReadAsync(this.buffer.AsMemory(this.dataLen, todo));
                    this.dataLen += done;
                    this.isFinalBlock = done < todo;
                    this.bytesConsumed = 0;
                    this.tokensFound = 0;
                }

                this.dataPos += this.bytesConsumed;
                this.dataLen -= this.bytesConsumed;

                foundToken = this.TryReadNextToken();

                // if we don't have any more bytes and in final block, we can exit
                if (foundToken || this.dataLen <= 0 && this.isFinalBlock)
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

            return foundToken;
        }

        private bool TryReadNextToken()
        {
            // create a new ref struct json reader
            var spanBuffer = new ReadOnlySpan<byte>(this.buffer, this.dataPos, this.dataLen);

            var reader = new Utf8JsonReader(spanBuffer, this.isFinalBlock, state: this.currentState);

            // try to read next token
            if (reader.Read())
            {
                this.currentState = reader.CurrentState;
                this.bytesConsumed = (int)reader.BytesConsumed;
                this.tokensFound++;
                this.hasMore = true;
                this.TokenType = reader.TokenType;
                this.Depth = reader.CurrentDepth;
                this.Value = new ReadOnlyMemory<byte>(reader.ValueSpan.ToArray());
                return true;
            }

            return false;
        }

        /// <summary>
        /// Enumerate over the stream and read the properties
        /// </summary>
        /// <returns></returns>
        public async IAsyncEnumerable<JsonReaderValue> Values()
        {
            while (await this.ReadAsync())
            {
                JsonReaderValue jsonReaderValue = new() { TokenType = this.TokenType, Depth = this.Depth };
                if (this.TokenType == JsonTokenType.PropertyName)
                    jsonReaderValue.Value = JsonValue.Create(this.GetString());
                else if (this.TokenType == JsonTokenType.Null || this.TokenType == JsonTokenType.None)
                    jsonReaderValue.Value = null;
                else if (this.TokenType == JsonTokenType.String)
                    jsonReaderValue.Value = JsonValue.Create(this.GetString());
                else if (this.TokenType == JsonTokenType.False || this.TokenType == JsonTokenType.True)
                    jsonReaderValue.Value = JsonValue.Create(this.GetBoolean());
                else if (this.TokenType == JsonTokenType.Number)
                    jsonReaderValue.Value = JsonValue.Create(this.GetDouble());

                yield return jsonReaderValue;
            }
        }

        /// <summary>
        /// Skips the children of the current token.
        /// </summary>
        public async ValueTask<bool> SkipAsync()
        {
            if (this.TokenType == JsonTokenType.PropertyName)
                return await this.ReadAsync();

            if (this.TokenType == JsonTokenType.StartObject || this.TokenType == JsonTokenType.StartArray)
            {
                int depth = this.Depth;
                do
                {
                    bool hasRead = await this.ReadAsync();

                    if (!hasRead)
                        return false;
                }
                while (depth < this.Depth);

                return true;
            }
            return false;
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

        public async ValueTask<string?> ReadAsString()
        {
            await this.ReadAsync();
            return this.GetString();
        }

        public string? GetString()
        {
            if (this.TokenType != JsonTokenType.PropertyName && this.TokenType != JsonTokenType.String)
                return null;

#if NET6_0_OR_GREATER
            var str = utf8Encoding.GetString(this.Value.Span);
#else   
            var str = utf8Encoding.GetString(this.Value.ToArray());
#endif

            return Regex.Unescape(str);
        }

        public async ValueTask<string?> ReadAsEscapedString()
        {
            await this.ReadAsync();
            return this.GetEscapedString();
        }

        public string? GetEscapedString()
        {
            if (this.TokenType != JsonTokenType.PropertyName && this.TokenType != JsonTokenType.String)
                return null;

#if NET6_0_OR_GREATER
            var str = utf8Encoding.GetString(this.Value.Span);
#else   
            var str = utf8Encoding.GetString(this.Value.ToArray());
#endif

            return str;
        }

        public async ValueTask<Guid?> ReadAsGuid()
        {
            await this.ReadAsync();
            return this.GetGuid();
        }

        public Guid? GetGuid()
        {
            if (this.TokenType != JsonTokenType.String)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out Guid tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse double");
        }

        public async ValueTask<TimeSpan?> ReadAsTimeSpan()
        {
            await this.ReadAsync();
            return this.GetTimeSpan();
        }

        public TimeSpan? GetTimeSpan()
        {
            if (this.TokenType != JsonTokenType.String)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out TimeSpan tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse TimeSpan");
        }

        public async ValueTask<DateTimeOffset?> ReadAsDateTimeOffset()
        {
            await this.ReadAsync();
            return this.GetDateTimeOffset();
        }

        public DateTimeOffset? GetDateTimeOffset()
        {
            if (this.TokenType != JsonTokenType.String)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out DateTimeOffset tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse DateTimeOffset");
        }

        public async ValueTask<DateTime?> ReadAsDateTime()
        {
            await this.ReadAsync();
            return this.GetDateTime();
        }

        public DateTime? GetDateTime()
        {
            if (this.TokenType != JsonTokenType.String)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out DateTime tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse GetDateTime");
        }

        public async ValueTask<double?> ReadAsDouble()
        {
            await this.ReadAsync();
            return this.GetDouble();
        }

        public double? GetDouble()
        {
            if (this.TokenType != JsonTokenType.Number)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out double tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse double");
        }

        public async ValueTask<decimal?> ReadAsDecimal()
        {
            await this.ReadAsync();
            return this.GetDecimal();
        }

        public decimal? GetDecimal()
        {
            if (this.TokenType != JsonTokenType.Number)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out decimal tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse decimal");
        }

        public async ValueTask<float?> ReadAsSingle()
        {
            await this.ReadAsync();
            return this.GetSingle();
        }

        public float? GetSingle()
        {
            if (this.TokenType != JsonTokenType.Number)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out float tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse float");
        }

        public async ValueTask<long?> ReadAsInt64()
        {
            await this.ReadAsync();
            return this.GetInt64();
        }

        public long? GetInt64()
        {
            if (this.TokenType != JsonTokenType.Number)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out long tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse long");
        }

        public async ValueTask<int?> ReadAsInt32()
        {
            await this.ReadAsync();
            return this.GetInt32();
        }

        public int? GetInt32()
        {
            if (this.TokenType != JsonTokenType.Number)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out int tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse int");
        }

        public async ValueTask<short?> ReadAsInt16()
        {
            await this.ReadAsync();
            return this.GetInt16();
        }

        public short? GetInt16()
        {
            if (this.TokenType != JsonTokenType.Number)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out short tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse short");
        }

        public async ValueTask<byte?> ReadAsByte()
        {
            await this.ReadAsync();
            return this.GetByte();
        }

        public byte? GetByte()
        {
            if (this.TokenType != JsonTokenType.Number)
                return null;

            if (Utf8Parser.TryParse(this.Value.Span, out byte tmp, out int bytesConsumed) && this.Value.Span.Length == bytesConsumed)
                return tmp;

            throw new FormatException("Can't parse byte");
        }

        public async ValueTask<bool?> ReadAsBoolean()
        {
            await this.ReadAsync();
            return this.GetBoolean();
        }
    
        public bool? GetBoolean()
        {
            if (this.TokenType == JsonTokenType.True)
                return true;
            else if (this.TokenType == JsonTokenType.False)
                return false;
            else
                return null;
        }

        //public byte[] GetBytesFromBase64()
        //{
        //    return null;
        //}

    }

    public struct JsonReaderValue
    {
        public JsonValue? Value { get; set; }
        public JsonTokenType TokenType { get; set; } = JsonTokenType.None;
        public int Depth { get; set; } = 0;

        public JsonReaderValue() { }

        public override string ToString()
        {
            var sb = new StringBuilder($"Type: {this.TokenType} - Depth: {this.Depth}");

            if (this.TokenType == JsonTokenType.PropertyName)
                sb.Append(" - Property: ").Append(this.Value);

            if (this.Value != null)
                sb.Append(" - Value: ").Append(this.Value);

            return sb.ToString();
        }
    }
}
