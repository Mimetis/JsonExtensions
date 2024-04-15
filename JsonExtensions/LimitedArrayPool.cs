using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonExtensions
{
    internal sealed class LimitArrayPoolWriteStream : Stream
    {
        private const int InitialLength = 256;

        private readonly int maxBufferSize;
        private byte[] buffer;
        private int length;

        public LimitArrayPoolWriteStream(int maxBufferSize) : this(maxBufferSize, InitialLength) { }


        public LimitArrayPoolWriteStream(int maxBufferSize, long capacity)
        {
            if (capacity < InitialLength)
            {
                capacity = InitialLength;
            }
            else if (capacity > maxBufferSize)
            {
                throw new Exception("Capacity can't be > max buffer size.");
            }

            this.maxBufferSize = maxBufferSize;
            buffer = ArrayPool<byte>.Shared.Rent((int)capacity);
        }

        protected override void Dispose(bool disposing)
        {
            Debug.Assert(buffer != null);

            ArrayPool<byte>.Shared.Return(buffer);
            buffer = null!;

            base.Dispose(disposing);
        }

        public ArraySegment<byte> GetBuffer() => new(buffer, 0, length);

        public byte[] ToArray()
        {
            var arr = new byte[length];
            Buffer.BlockCopy(buffer, 0, arr, 0, length);
            return arr;
        }

        private void EnsureCapacity(int value)
        {
            if ((uint)value > (uint)maxBufferSize) // value cast handles overflow to negative as well
                throw new Exception(maxBufferSize.ToString());
            else if (value > buffer.Length)
                Grow(value);
        }

        private void Grow(int value)
        {
            Debug.Assert(value > buffer.Length);

            // Extract the current buffer to be replaced.
            byte[] currentBuffer = buffer;
            buffer = null!;

            // Determine the capacity to request for the new buffer. It should be
            // at least twice as long as the current one, if not more if the requested
            // value is more than that.  If the new value would put it longer than the max
            // allowed byte array, than shrink to that (and if the required length is actually
            // longer than that, we'll let the runtime throw).
            uint twiceLength = 2 * (uint)currentBuffer.Length;
            int newCapacity = twiceLength > Array.MaxLength ?
                Math.Max(value, Array.MaxLength) :
                Math.Max(value, (int)twiceLength);

            // Get a new buffer, copy the current one to it, return the current one, and
            // set the new buffer as current.
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newCapacity);
            Buffer.BlockCopy(currentBuffer, 0, newBuffer, 0, length);
            ArrayPool<byte>.Shared.Return(currentBuffer);
            buffer = newBuffer;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Debug.Assert(buffer != null);
            Debug.Assert(offset >= 0);
            Debug.Assert(count >= 0);

            EnsureCapacity(length + count);
            Buffer.BlockCopy(buffer, offset, this.buffer, length, count);
            length += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacity(length + buffer.Length);
            buffer.CopyTo(new Span<byte>(this.buffer, length, buffer.Length));
            length += buffer.Length;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return default;
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? asyncCallback, object? asyncState) =>
            TaskToAsyncResult.Begin(WriteAsync(buffer, offset, count, CancellationToken.None), asyncCallback, asyncState);

        public override void EndWrite(IAsyncResult asyncResult) =>
            TaskToAsyncResult.End(asyncResult);

        public override void WriteByte(byte value)
        {
            int newLength = length + 1;
            EnsureCapacity(newLength);
            buffer[length] = value;
            length = newLength;
        }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override long Length => length;
        public override bool CanWrite => true;
        public override bool CanRead => false;
        public override bool CanSeek => false;

        public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
    }
}
