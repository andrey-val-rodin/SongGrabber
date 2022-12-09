using SongGrabber.Grabbing;

namespace SongGrabber
{
    public class ConsoleImpl : IConsole
    {
        public void Write(string value)
        {
            Console.Write(value);
        }

        public void WriteLine(string value)
        {
            Console.WriteLine(value);
        }
    }
}
