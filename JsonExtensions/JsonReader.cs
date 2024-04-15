using System;
using System.Buffers;
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

        // byte buffer
        private byte[]? buffer = null;

        // buffer size
        private readonly int bufferSize;

        private JsonReaderState currentState;

        // these two variables are used to keep track of the overall bytes consumed until the buffer is reset
        private int bytesConsumedInBuffer = 0;
        private int tokensCountInBuffer = 0;
        private int streamReadLength = 0;
        private int virtualBufferLength = 0;

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
            this.currentState = new JsonReaderState(jsonReaderOptions);

            if (!this.Stream.CanRead)
                throw new Exception("Stream is not readable");
        }


        /// <summary>
        /// Enumerate over the stream and read the properties
        /// </summary>
        /// <returns></returns>
        public bool Read()
        {
            // create a buffer to read the stream into
            if (buffer == null)
            {
                buffer = new byte[bufferSize];

                // Read the stream
                streamReadLength = Stream.Read(buffer);

                // on first line buffer length is the same as stream read length
                virtualBufferLength = streamReadLength;

                if (streamReadLength == 0)
                    return false;
            }

            // move the buffer to the next position according to the overall (in progress) bytes consumed
            var spanBuffer = buffer.AsSpan(bytesConsumedInBuffer, virtualBufferLength);

            // create a new ref struct json reader
            var reader = new Utf8JsonReader(spanBuffer, isFinalBlock: false, state: currentState);
            try
            {
                // if we have at leat one token in the buffer, we can try to read it
                while (!reader.Read() && streamReadLength > 0)
                {
                    // if we have not read at least one token in the buffer, we need to read more bytes from the stream and increase the buffer size
                    // else we are at the end of the current buffer and we can move the buffer back to the initial position (and fill with bytes from the stream)
                    if (this.tokensCountInBuffer == 0)
                    {
                        (streamReadLength, virtualBufferLength) = GetMoreBytesFromStream(Stream, ref buffer, ref reader);

                        Console.WriteLine("Stream read length: " + streamReadLength + ". Virtual buffer length: " + virtualBufferLength + ". Buffer length: " + buffer.Length + "bytes consumed in buffer: " + this.bytesConsumedInBuffer);
                    }
                    else
                    {
                        // check if we are still have something to read from the stream
                        (streamReadLength, virtualBufferLength) = MoveBufferBackToInitialPosition(Stream, ref buffer, bytesConsumedInBuffer);

                        Console.WriteLine("Stream read length: " + streamReadLength + ". Virtual buffer length: " + virtualBufferLength + ". Buffer length: " + buffer.Length + "bytes consumed in buffer: " + this.bytesConsumedInBuffer);

                        this.tokensCountInBuffer = 0;
                        this.bytesConsumedInBuffer = 0;

                        // we are at the end and we don't have anything else to read
                        if (streamReadLength == 0)
                            return false;
                    }

                }

                // we have read at least one token
                tokensCountInBuffer++;
            }
            catch (JsonException)
            {
                if (streamReadLength == 0)
                {
                    Array.Clear(buffer);
                    buffer = null;
                    return false;
                }

                throw;
            }

            // temp save
            this.bytesConsumedInBuffer += (int)reader.BytesConsumed;
            this.currentState = reader.CurrentState;
            return true;
        }




        public JsonReaderValue GetValue()
        {

            // move the buffer to the next position according to the overall (in progress) bytes consumed
            var spanBuffer = buffer.AsSpan(bytesConsumedInBuffer);

            // create a new ref struct json reader
            var reader = new Utf8JsonReader(spanBuffer, isFinalBlock: false, state: currentState);


            if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray || reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
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


        ///// <summary>
        ///// Try to read the next token from the buffer
        ///// </summary>
        //private bool InnerTryRead(ref Utf8JsonReader reader)
        //{
        //    try
        //    {
        //        if (!reader.Read())
        //        {
        //            this.currentValue = null;
        //            return false;
        //        }

        //        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray || reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
        //        {
        //            this.currentValue = new JsonReaderValue { TokenType = reader.TokenType };
        //            return true;
        //        }

        //        if (reader.TokenType == JsonTokenType.PropertyName)
        //        {
        //            this.currentValue = new JsonReaderValue { TokenType = reader.TokenType, Name = reader.GetString() };
        //            return true;
        //        }

        //        JsonValue? propertyValue = null;
        //        if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.None)
        //            propertyValue = null;
        //        else if (reader.TokenType == JsonTokenType.String)
        //            propertyValue = JsonValue.Create(reader.GetString());
        //        else if (reader.TokenType == JsonTokenType.False || reader.TokenType == JsonTokenType.True)
        //            propertyValue = JsonValue.Create(reader.GetBoolean());
        //        else if (reader.TokenType == JsonTokenType.Number)
        //            propertyValue = JsonValue.Create(reader.GetDouble());

        //        this.currentValue = new JsonReaderValue { Value = propertyValue, TokenType = reader.TokenType };
        //        return true;
        //    }
        //    catch (JsonException)
        //    {
        //        this.currentValue = null;
        //        return false;
        //    }
        //}


        /// <summary>
        /// Move back the buffer to the initial position and fill empty bytes with stream bytes
        /// </summary>
        private static (int byteReads, int virtualBufferLength) MoveBufferBackToInitialPosition(Stream stream, ref byte[] buffer, int overAllBytesConsumedUntilResetBuffer)
        {
#if DEBUG
            Debug.WriteLine("Buffer reused.");
#endif
            // prepare the buffer
            ReadOnlySpan<byte> leftover = buffer.AsSpan((int)overAllBytesConsumedUntilResetBuffer);
            // copy the leftover bytes to the beginning of the buffer
            leftover.CopyTo(buffer);

            int virtalBufferLength = leftover.Length;

            // if needed, read more bytes from the stream
            var bytesRead = stream.Read(buffer.AsSpan(leftover.Length));

            virtalBufferLength += bytesRead;

            return (bytesRead, virtalBufferLength);

        }

        /// <summary>
        /// Get more bytes from the stream because the reader has consumed all the bytes in the buffer and this buffer is not long enough to hold the next token
        /// </summary>
        private static (int byteReads, int virtualBufferLength) GetMoreBytesFromStream(Stream stream, ref byte[] buffer, ref Utf8JsonReader reader)
        {
            int bytesRead;
            int virtualBufferLength;
            if (reader.BytesConsumed < buffer.Length)
            {
                ReadOnlySpan<byte> leftover = buffer.AsSpan((int)reader.BytesConsumed);

                virtualBufferLength = leftover.Length;

                if (virtualBufferLength == buffer.Length)
                    Array.Resize(ref buffer, buffer.Length * 2);
#if DEBUG
                Debug.WriteLine("Buffer increased to: " + buffer.Length);
#endif

                leftover.CopyTo(buffer);
                bytesRead = stream.Read(buffer.AsSpan(leftover.Length));
                virtualBufferLength += bytesRead;
            }
            else
            {
                bytesRead = stream.Read(buffer);
                virtualBufferLength = bytesRead;
            }

            reader = new Utf8JsonReader(buffer, isFinalBlock: bytesRead == 0, reader.CurrentState);

            return (bytesRead, virtualBufferLength);
        }


    }
}
