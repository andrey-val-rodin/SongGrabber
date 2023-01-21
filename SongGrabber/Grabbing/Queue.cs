using System.Threading;

namespace SongGrabber.Grabbing
{
    public class Queue : IDisposable
    {
        const int Portion = 10000;

        private Stream _stream;
        private readonly int _capacity;
        private byte[] _circularBuffer;
        private readonly byte[] _portion = new byte[Portion];
        private int _start = 0;
        private int _count = 0;
        private ulong _pos = 0;
        private CancellationTokenSource _tokenSource = new();
        private readonly object _lock = new();
        private bool _disposedValue;

        public Queue(Stream stream) : this(stream, 16777216)
        {
        }

        public Queue(Stream stream, int capacity)
        {
            if (capacity < 1)
                throw new ArgumentException("Positive number required.", nameof(capacity));

            _capacity = capacity;
            _circularBuffer = new byte[_capacity];
            _stream = stream;

            Task.Run(async () =>
            {
                if (_tokenSource != null)
                    await EnqueueAsync(_tokenSource.Token);
            });
        }

        public int Capacity => _capacity;

        public int Count
        {
            get
            {
                lock(_lock)
                {
                    return _count;
                }
            }
        }

        private async Task EnqueueAsync(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    if (token.IsCancellationRequested)
                        return;

                    if (Count > _capacity - Portion * 2)
                    {
                        // Buffer is nearly full
                        // Wait for the client to read from queue
                        // await Task.Delay(10, token);
                        continue;
                    }

                    int readed = await ReadPortionAsync();
                    if (readed > 0)
                    {
                        lock (_lock)
                        {
                            int startIndex = (_start + _count) % _capacity;
                            int toRead = Math.Min(_capacity - startIndex, Portion);
                            Array.Copy(_portion, 0, _circularBuffer, startIndex, toRead);
                            if (toRead < Portion)
                                Array.Copy(_portion, toRead, _circularBuffer, 0, Portion - toRead);

                            _count += Portion;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task<int> ReadPortionAsync()
        {
            int result = 0;
            int index = 0;
            while (result < Portion)
            {
                int readed = _stream == null ? 0 : _stream.Read(_portion, index, _portion.Length - index);
//                if (readed == 0)
//                    await Task.Delay(1);

//                System.Diagnostics.Debug.WriteLine($"readed: {readed}");
                if (readed <= 0)
                    break;

                result += readed;
                index += readed;
            }

            return result;
        }

        public int Dequeue(Span<byte> buffer)
        {
            int length;
            lock (_lock)
            {
                length = Math.Min(buffer.Length, _count);
                if (length == 0)
                    return 0;

                if (_capacity - _start >= length)
                {
                    var span = new Span<byte>(_circularBuffer, _start, length);
                    span.CopyTo(buffer);
                }
                else
                {
                    var firstPart = new Span<byte>(_circularBuffer, _start, _capacity - _start);
                    firstPart.CopyTo(buffer);
                    buffer = buffer[firstPart.Length..];
                    var secondPart = new Span<byte>(_circularBuffer, 0, length - firstPart.Length);
                    secondPart.CopyTo(buffer);
                }

                _count -= length;
                _start += length;
                _start %= _capacity;
                _pos += (ulong)length;
            }

            return length;
        }

        public bool Seek(ulong offset)
        {
            ulong shift = _pos - offset;
            if (shift > 0 && (int)shift < _start)
            {
                lock (_lock)
                {
                    _start -= (int)shift;
                    _count += (int)shift;
                }

                return true;
            }

            return false;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _tokenSource.Cancel();
                    _tokenSource.Dispose();
                }

                _stream = null;
                _circularBuffer = null;
                _tokenSource = null;
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
