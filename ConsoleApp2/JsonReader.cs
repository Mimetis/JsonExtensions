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
        private int bufferSize;


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


        //public IEnumerable<JsonProperty> Read()
        //{


        //    var jsonReaderstate = new JsonReaderState(new JsonReaderOptions { AllowTrailingCommas = true });
        //    int bytesConsumed = 0;
        //    // create a buffer to read the stream into
        //    var buffer = new byte[10];

        //    if (this.Stream.Position > 0)
        //        this.Stream.Seek(0, SeekOrigin.Begin);

        //    Stream.Read(buffer);

        //    for (var index = 1; index < 100; index++)
        //    {
        //        var p = JsonReaderItem.GetNextJsonProperty(ref buffer, bytesConsumed, Stream, ref jsonReaderstate);

        //        jsonReaderstate = p.state;
        //        bytesConsumed = p.bytesConsumed;

      
        //        yield return p.property;
        //    }
        //}

        public IEnumerable<JsonReaderValue> EnumerateProperties()
        {
            var state = new JsonReaderState(new JsonReaderOptions { AllowTrailingCommas = true });
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

                var reader = new Utf8JsonReader(buffer, isFinalBlock: false, state: state);
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
                state = reader.CurrentState;

                if (jsonProperty == null)
                    yield break;

                yield return jsonProperty;
            }
        }


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
            reader = new Utf8JsonReader(buffer, isFinalBlock: bytesRead == 0, reader.CurrentState);
        }




        //// Use an action instead
        //public void ReadProperties(Action<JsonProperty> onJsonPropRetrieved)
        //{
        //    ArgumentNullException.ThrowIfNull(onJsonPropRetrieved);

        //    var jsonReaderstate = new JsonReaderState(new JsonReaderOptions { AllowTrailingCommas = true });

        //    if (this.Stream.Position > 0)
        //        this.Stream.Seek(0, SeekOrigin.Begin);

        //    Stream.Read(buffer);
        //    var reader = new Utf8JsonReader(buffer, isFinalBlock: false, state: jsonReaderstate);

        //    JsonProperty jsonProperty;
        //    while ((jsonProperty = InnerRead(ref reader)) != null)
        //    {
        //        onJsonPropRetrieved?.Invoke(jsonProperty);
        //        reader = new Utf8JsonReader(buffer, isFinalBlock: false, reader.CurrentState);
        //    }
        //}

    }



    public class JsonReaderValue
    {
        public string Name { get; set; }
        public JsonValue Value { get; set; }
        public JsonTokenType TokenType { get; set; }
    }
}
