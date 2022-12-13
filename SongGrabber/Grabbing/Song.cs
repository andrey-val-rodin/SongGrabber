namespace SongGrabber.Grabbing
{
    public class Song
    {
        public string Filename { get; set; }
        public SongStatus Status { get; set; }
        public string Notes { get; set; }
        public int ApproximateDuration { get; set; }

        public override string ToString()
        {
            var duration = ApproximateDuration <= 0 ? string.Empty : $", {ApproximateDuration} sec";
            var notes = string.IsNullOrEmpty(Notes) ? string.Empty : $", {Notes}";
            return $"{Filename}{duration}, {Status}{notes}";
        }
    }
}
