using SongGrabber.Handlers;
using System.Text;

namespace SongGrabber.Grabbing
{
    public sealed class MetadataStream : Stream
    {
        private readonly Stream _sourceStream;
        private string _metadata;
        private int _dataCount;

        public MetadataStream(Stream sourceStream, int icyMetaInt)
        {
            _sourceStream = sourceStream ?? throw new ArgumentNullException(nameof(sourceStream));
            IcyMetaInt = icyMetaInt >= 0
                ? icyMetaInt
                : throw new ArgumentException(
                    $"{nameof(icyMetaInt)} must be greater than or equal to zero",
                    nameof(icyMetaInt));
        }

        public event EventHandler<MetadataEventArgs> MetadataDiscovered;
        public event EventHandler<MetadataChangedEventArgs> MetadataChanged;

        public int IcyMetaInt { get; init; }

        public string Metadata
        {
            get { return _metadata; }
            private set
            {
                if (_metadata != value)
                {
                    MetadataChanged?.Invoke(this, new MetadataChangedEventArgs(_metadata, value));
                    _metadata = value;
                }
            }
        }

        public override bool CanRead => _sourceStream.CanRead;

        public override bool CanSeek => _sourceStream.CanSeek;

        public override bool CanWrite => _sourceStream.CanWrite;

        public override long Length => 4096;

        public override long Position
        {
            get => _sourceStream.Position;
            set => _sourceStream.Position = value;
        }

        public override void Flush()
        {
            _sourceStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (count + offset > buffer.Length)
                throw new ArgumentException(
                    "Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection");
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            return ReadToSpanOrArray(null, buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            return ReadToSpanOrArray(buffer, null, 0, buffer.Length);
        }

        /// <summary>
        /// Span<byte> is a ref struct and therefore has limitations (no interfaces, no member vars, no generics).
        /// So I have to use this awkward method to avoid code duplication
        /// </summary>
        private int ReadToSpanOrArray(Span<byte> span, byte[] array, int offset, int count)
        {
            if (span == null && array == null)
                throw new ArgumentNullException(nameof(span));

            int result = 0;
            int countToRead = count;
            while (result < count)
            {
                if (IcyMetaInt > 0 && _dataCount == IcyMetaInt)
                {
                    ReadMetadata();
                    _dataCount = 0;
                }

                var rest = IcyMetaInt > 0 ? IcyMetaInt - _dataCount : int.MaxValue;
                var c = Math.Min(rest, countToRead);
                // Read to span or array
                var readed = (span != null)
                    ? _sourceStream.Read(span.Slice(offset, c))
                    : _sourceStream.Read(array, offset, c);
                result += readed;
                _dataCount += readed;
                offset += readed;
                countToRead -= readed;

                if (readed < c)
                {
                    // End of stream
                    return result;
                }
            }

            return result;
        }

        private void ReadMetadata()
        {
            // First byte contains size of metada in bytes / 16
            var firstByte = _sourceStream.ReadByte();
            if (firstByte == -1) // end of stream
                return;

            int size = firstByte * 16;
            var bytes = new byte[size];
            if (_sourceStream.Read(bytes, 0, size) < size)
                return;  // end of stream

            var metadata = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
            MetadataDiscovered?.Invoke(this, new MetadataEventArgs(metadata));
            Metadata = metadata;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _sourceStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _sourceStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _sourceStream.Write(buffer, offset, count);
        }
    }
}
