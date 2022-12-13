using System.Text;

namespace SongGrabber.Grabbing
{
    internal class ResultImpl : IResult
    {
        private readonly List<Song> _songs = new();

        public ResultImpl(Uri uri, string folder)
        {
            Uri = uri;
            Folder = folder;
        }

        public Uri Uri { get; init; }
        public string Error { get; set; }
        public string Folder { get; init; }
        IReadOnlyList<Song> IResult.Songs => _songs;
        public List<Song> Songs => _songs;

        public override string ToString()
        {
            var builder = new StringBuilder(Uri.ToString());

            builder.Append(Folder);
            builder.Append(Environment.NewLine);

            if (!((IResult)this).IsSuccess)
            {
                builder.Append($"Error: {Error}");
                builder.Append(Environment.NewLine);
            }

            foreach (var s in Songs)
            {
                builder.Append(s.ToString());
                builder.Append(Environment.NewLine);
            }

            return builder.ToString();
        }
    }
}
