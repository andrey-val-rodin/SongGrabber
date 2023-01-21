using SongGrabber.Grabbing;

namespace Tests.Grabbing
{
    public class QueueTests
    {
        private static readonly byte[] _pattern = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        private static readonly byte[] _buffer = new byte[10];

        [Fact]
        public async Task Dequeue_Default_Ok()
        {
            await CheckAsync();
        }

        [Fact]
        public async Task Dequeue_CapacityIs7_Ok()
        {
            await CheckAsync(1000, 7);
        }

        #region Helpers
        private static async Task CheckAsync(int bytes = default, int capacity = default)
        {
            if (bytes == default)
                bytes = GetDefaultCapacity();

            using var stream = CreateStream(bytes);
            using var queue = capacity != default ? new Queue(stream, capacity) : new Queue(stream);

            int count = 0;
            while (count < bytes)
            {
                int length = Math.Min(_buffer.Length, bytes - count);
                int readed = queue.Dequeue(new Span<byte>(_buffer, 0, length));
                while (readed < length)
                {
                    // Let queue load some data from stream
                    await Task.Delay(10);

                    // Load the rest
                    readed += queue.Dequeue(new Span<byte>(_buffer, readed, _buffer.Length - readed));
                }
                Assert.Equal(length, readed);
                Assert.Equal(
                    new ArraySegment<byte>(_pattern, 0, length),
                    new ArraySegment<byte>(_buffer, 0, length));

                count += length;
            }

            Assert.Equal(bytes, count);
        }

        private static int GetDefaultCapacity()
        {
            using var stream = CreateStream();
            using var queue = new Queue(stream);

            return queue.Capacity;
        }

        private static Stream CreateStream(int bytes = default)
        {
            var stream = new MemoryStream();
            if (bytes != default)
                FillStream(bytes, stream);
            return stream;
        }

        private static void FillStream(int bytes, MemoryStream stream)
        {
            int patternCount = bytes / _pattern.Length;
            for (int i = 0; i < patternCount; i++)
            {
                stream.Write(_pattern, 0, _pattern.Length);
            }

            // Finally add the rest
            int rest = bytes - (patternCount * _pattern.Length);
            stream.Write(new Span<byte>(_pattern, 0, rest));

            stream.Position = 0;
        }
        #endregion
    }
}
