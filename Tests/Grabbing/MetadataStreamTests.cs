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
        public void Read_IcyMetaintIsZero_Success()
        {
            Tester.Verify(999, 0, 1000);
        }

        [Fact]
        public void Read_Variant0_Success()
        {
            Tester.Verify(2000, 1000, 1000);
        }

        [Fact]
        public void Read_Variant1_Success()
        {
            Tester.Verify(123, 1, 11);
        }

        [Fact]
        public void Read_Variant3_Success()
        {
            Tester.Verify(876, 1000, 1000);
        }

        [Fact]
        public void Read_Variant4_Success()
        {
            Tester.Verify(10000, 16000, 4096);
        }
    }

    internal static class Tester
    {
        private static readonly byte[] _pattern = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        private const string _metadata = "First metadata  "; // one 16-bytes block

        public static void Verify(int totalCount, int icyMetaInt, int readCount)
        {
            var stream = new MetadataStream(Generate(totalCount, icyMetaInt), icyMetaInt);
            bool first = true;
            int metadataCount = 0;
            stream.MetadataDiscovered += (sender, args) =>
            {
                if (first)
                {
                    // First metadata must be string "First metadata"
                    Assert.Equal(_metadata, args.Metadata);
                    first = false;
                }
                else
                {
                    // All other metadata must be empty
                    Assert.Equal(string.Empty, args.Metadata);
                }

                metadataCount++;
            };

            int count = 0, c = 0;
            int patternPos = 0;
            var buffer = new byte[readCount];
            while ((c = stream.Read(buffer, 0, readCount)) > 0)
            {
                CheckBuffer(buffer, c, ref patternPos);
                count += c;
            }

            if (icyMetaInt > 0)
            {
                // Verify exact metadata count
                Assert.Equal(totalCount / icyMetaInt, metadataCount);
            }

            // Entire stream must be read
            Assert.Equal(totalCount, count);
        }

        private static MemoryStream Generate(int totalCount, int icyMetaInt)
        {
            var stream = new MemoryStream();
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

        private static void CheckBuffer(byte[] buffer, int count, ref int patternPos)
        {
            for (int i = 0; i < count; i++)
            {
                // Data must be cyclic values 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 1, 2, 3...
                Assert.Equal(_pattern[patternPos], buffer[i]);
                
                patternPos = IncPatternPos(patternPos);
            }
        }
    }
}