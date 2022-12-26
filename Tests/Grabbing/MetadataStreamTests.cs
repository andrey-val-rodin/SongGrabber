using SongGrabber.Grabbing;
using System.Text;

namespace Tests.Grabbing
{
    public class MetadataStreamTests
    {
        [Fact]
        public void MetadataStream_SourceStreamIsNull_Exception()
        {
            Assert.Throws<ArgumentNullException>("sourceStream", () => new MetadataStream(null, 8000));
        }

        [Fact]
        public void MetadataStream_IcyMetaintIsLessThanZero_Exception()
        {
            Assert.Throws<ArgumentException>("icyMetaInt", () => new MetadataStream(new MemoryStream(), -1));
        }

        [Fact]
        public void ReadToBuffer_IcyMetaintIsZero_Success()
        {
            Tester.VerifyReadToBuffer(totalCount: 999, icyMetaInt: 0, readCount: 1000);
        }

        [Fact]
        public void ReadToBuffer_Variant0_Success()
        {
            Tester.VerifyReadToBuffer(totalCount: 2000, icyMetaInt: 1000, readCount: 1000);
        }

        [Fact]
        public void ReadToBuffer_Variant1_Success()
        {
            Tester.VerifyReadToBuffer(totalCount: 123, icyMetaInt: 1, readCount: 11);
        }

        [Fact]
        public void ReadToBuffer_Variant3_Success()
        {
            Tester.VerifyReadToBuffer(totalCount: 876, icyMetaInt: 1000, readCount: 1000);
        }

        [Fact]
        public void ReadToBuffer_Variant4_Success()
        {
            Tester.VerifyReadToBuffer(totalCount: 10000, icyMetaInt: 16000, readCount: 4096);
        }

        [Fact]
        public void ReadToSpan_IcyMetaintIsZero_Success()
        {
            Tester.VerifyReadToSpan(totalCount: 999, icyMetaInt: 0, readCount: 1000);
        }

        [Fact]
        public void ReadToSpan_Variant0_Success()
        {
            Tester.VerifyReadToSpan(totalCount: 2000, icyMetaInt: 1000, readCount: 1000);
        }

        [Fact]
        public void ReadToSpan_Variant1_Success()
        {
            Tester.VerifyReadToSpan(totalCount: 123, icyMetaInt: 1, readCount: 11);
        }

        [Fact]
        public void ReadToSpan_Variant3_Success()
        {
            Tester.VerifyReadToSpan(totalCount: 876, icyMetaInt: 1000, readCount: 1000);
        }

        [Fact]
        public void ReadToSpan_Variant4_Success()
        {
            Tester.VerifyReadToSpan(totalCount: 10000, icyMetaInt: 16000, readCount: 4096);
        }
    }

    #region Helpers
    internal static class Tester
    {
        private static readonly byte[] _pattern = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        private const string _metadata = "First metadata  "; // one 16-bytes block
        private static int _metadataCount;

        public static void VerifyReadToBuffer(int totalCount, int icyMetaInt, int readCount)
        {
            var stream = CreateStream(totalCount, icyMetaInt);
            int count = 0, c;
            int patternPos = 0;
            var buffer = new byte[readCount];
            while (true)
            {
                var countToRead = Math.Min(readCount, totalCount - count);
                if (countToRead == 0)
                    break;

                c = stream.Read(buffer, 0, countToRead);
                CheckBuffer(buffer, c, ref patternPos);
                count += c;
            }

            // Try for last metadata at the end
            Assert.Equal(0, stream.Read(buffer, 0, 1));

            Assertions(totalCount, icyMetaInt, count);
        }

        private static void CheckBuffer(byte[] buffer, int count, ref int patternPos)
        {
            for (int i = 0; i < count; i++)
            {
                // Data must be cyclic values 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 1, 2, 3...
                Assert.Equal(_pattern[patternPos], buffer[i]);

                patternPos = IncPatternPos(patternPos);
            }
        }

        public static void VerifyReadToSpan(int totalCount, int icyMetaInt, int readCount)
        {
            var stream = CreateStream(totalCount, icyMetaInt);
            int count = 0, c;
            int patternPos = 0;
            var buffer = new byte[readCount];
            var span = new Span<byte>(buffer);
            while (true)
            {
                var countToRead = Math.Min(readCount, totalCount - count);
                if (countToRead == 0)
                    break;

                c = stream.Read(span);
                CheckSpan(span.Slice(0, c), ref patternPos);
                count += c;
            }

            // Try for last metadata at the end
            Assert.Equal(0, stream.Read(buffer, 0, 1));

            Assertions(totalCount, icyMetaInt, count);
        }

        private static void Assertions(int totalCount, int icyMetaInt, int count)
        {
            if (icyMetaInt > 0)
            {
                // Verify exact metadata count
                Assert.Equal(totalCount / icyMetaInt, _metadataCount);
            }

            // Entire stream must be read
            Assert.Equal(totalCount, count);
        }

        private static Stream CreateStream(int totalCount, int icyMetaInt)
        {
            bool firstMetadata = true;
            var stream = new MetadataStream(Generate(totalCount, icyMetaInt), icyMetaInt);
            _metadataCount = 0;
            stream.MetadataDiscovered += (sender, args) =>
            {
                if (firstMetadata)
                {
                    // First metadata must be string "First metadata  "
                    Assert.Equal(_metadata, args.Metadata);
                    firstMetadata = false;
                }
                else
                {
                    // All other metadata must be empty
                    Assert.Equal(string.Empty, args.Metadata);
                }

                _metadataCount++;
            };

            return stream;
        }

        private static Stream Generate(int totalCount, int icyMetaInt)
        {
            var stream = new ChunkedStream();
            bool first = true;
            int count = 0, pos = 0, patternPos = 0;
            while (count < totalCount)
            {
                if (icyMetaInt > 0 && pos == icyMetaInt)
                {
                    if (first)
                    {
                        WriteMetadata(stream);
                        first = false;
                    }
                    else
                        WriteEmptyMetadata(stream);

                    pos = 0;
                }
                else
                {
                    stream.WriteByte(_pattern[patternPos]);

                    pos++;
                    patternPos = IncPatternPos(patternPos);
                    count++;
                }
            }

            if (icyMetaInt > 0 && pos == icyMetaInt)
            {
                // Write last metadata
                WriteEmptyMetadata(stream);
            }

            stream.Position = 0;
            return stream;
        }

        private static int IncPatternPos(int patternPos)
        {
            return patternPos < _pattern.Length - 1
                ? patternPos + 1
                : 0;
        }

        private static void WriteMetadata(MemoryStream stream)
        {
            var bytes = Encoding.UTF8.GetBytes(_metadata);
            // one 16-bytes block
            stream.WriteByte(1);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteEmptyMetadata(MemoryStream stream)
        {
            stream.WriteByte(0);
        }

        private static void CheckSpan(Span<byte> span, ref int patternPos)
        {
            for (int i = 0; i < span.Length; i++)
            {
                // Data must be cyclic values 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 1, 2, 3...
                Assert.Equal(_pattern[patternPos], span[i]);

                patternPos = IncPatternPos(patternPos);
            }
        }
    }

    /// <summary>
    /// HttpContent.ReadAsStream() returns ChunkedEncodingReadStream
    /// This stream may sometimes return fewer bytes than requested in the Read method
    /// Class ChunkedStream emulates such behavior
    /// Tests use this class because MetadataStream must always return requested number of bytes
    /// </summary>
    internal class ChunkedStream : MemoryStream
    {
        private bool _returnPartOnly = true;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count < 100 || !_returnPartOnly)
                return base.Read(buffer, offset, count);

            float part = count * 0.8f;
            _returnPartOnly = false;
            return base.Read(buffer, offset, (int) part);
        }
    }
    #endregion
}