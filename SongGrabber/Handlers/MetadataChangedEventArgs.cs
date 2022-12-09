using System.Text.RegularExpressions;

namespace SongGrabber.Handlers
{
    public class MetadataChangedEventArgs : EventArgs
    {
        public MetadataChangedEventArgs(string oldMetadata, string newMetadata)
        {
            NewMetadata = newMetadata;
            NewStreamTitle = Regex.Match(NewMetadata, "(StreamTitle=')(.*)(';)").Groups[2].Value.Trim();
            OldMetadata = oldMetadata;
        }

        public string NewMetadata
        {
            get;
            private set;
        }

        public string NewStreamTitle
        {
            get;
            private set;
        }

        public string OldMetadata
        {
            get;
            private set;
        }
    }
}
