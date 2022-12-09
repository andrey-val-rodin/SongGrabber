using System.Text.RegularExpressions;

namespace SongGrabber.Handlers
{
    public class MetadataEventArgs : EventArgs
    {
        public MetadataEventArgs(long position, int size, string metadata)
        {
            Position = position;
            Size = size;

            Metadata = metadata;
            // pattern: "StreamTitle='Performer - Song'; StreamUrl = ''"
            // example: "StreamTitle='Eric Clapton - Autumn Leaves';"
            StreamTitle = Regex.Match(Metadata, "(StreamTitle=')(.*)(';)").Groups[2].Value.Trim();
            StreamUrl = Regex.Match(Metadata, "(;StreamUrl=')(.*)(')").Groups[2].Value.Trim();
        }

        public long Position
        {
            get;
            private set;
        }

        public int Size
        {
            get;
            private set;
        }

        public string Metadata
        {
            get;
            private set;
        }

        public string StreamTitle
        {
            get;
            private set;
        }

        public string StreamUrl
        {
            get;
            private set;
        }
    }
}
