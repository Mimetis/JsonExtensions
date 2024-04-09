using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace ConsoleApp2
{

    public class JsonReader
    {
        public Stream Stream { get; }
        private byte[] buffer;

        public JsonReader(Stream stream, int bufferSize = 1024)
        {
            this.Stream = stream;
            this.buffer = new byte[bufferSize];

            if (!this.Stream.CanRead)
                throw new Exception("Stream is not readable");
        }


        // Can't use a yield return here because the reader is being passed by reference

        // public IEnumerable<JsonProperty> EnumerateProperties()
        // {

        //     var jsonReaderstate = new JsonReaderState(new JsonReaderOptions { AllowTrailingCommas = true });

        //     if (this.Stream.Position > 0)
        //         this.Stream.Seek(0, SeekOrigin.Begin);

        //     Stream.Read(buffer);
        //     var reader = new Utf8JsonReader(buffer, isFinalBlock: false, state: jsonReaderstate);

        //     JsonProperty jsonProperty;
        //     while ((jsonProperty = InnerRead(ref reader)) != null)
        //         yield return jsonProperty;

        // }

        // Use an action instead
        public void ReadProperties(Action<JsonProperty> onJsonPropRetrieved)
        {
            ArgumentNullException.ThrowIfNull(onJsonPropRetrieved);

            var jsonReaderstate = new JsonReaderState(new JsonReaderOptions { AllowTrailingCommas = true });

            if (this.Stream.Position > 0)
                this.Stream.Seek(0, SeekOrigin.Begin);

            Stream.Read(buffer);
            var reader = new Utf8JsonReader(buffer, isFinalBlock: false, state: jsonReaderstate);

            JsonProperty jsonProperty;
            while ((jsonProperty = InnerRead(ref reader)) != null)
                onJsonPropRetrieved?.Invoke(jsonProperty);

        }


        private JsonProperty InnerRead(ref Utf8JsonReader reader)
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



    public class JsonProperty
    {
        public string Name { get; set; }
        public Object Value { get; set; }
        public JsonTokenType TokenType { get; set; }
    }
}
