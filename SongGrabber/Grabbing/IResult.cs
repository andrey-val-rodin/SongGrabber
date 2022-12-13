using System.Text;

namespace SongGrabber.Grabbing
{
    public interface IResult
    {
        Uri Uri { get; }
        bool IsSuccess { get => string.IsNullOrEmpty(Error); }
        string Error { get; }
        string Folder { get; }
        int DownloadedCount { get => Songs.Count(s => s.Status == SongStatus.Downloaded); }
        IReadOnlyList<Song> Songs { get; }
    }
}
