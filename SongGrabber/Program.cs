// See https://aka.ms/new-console-template for more information
using SongGrabber;
using SongGrabber.Grabbing;

var url = args.FirstOrDefault();
while (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
{
    if (!string.IsNullOrEmpty(url))
        Console.WriteLine($"Invalid URL: {url}");
    Console.Write("Enter URL: ");
    url = Console.ReadLine();
}

var grabber = new Grabber(new ConsoleImpl()) { NumberSongs = true };
var error = await grabber.GrabAsync(new Uri(url));
if (error != null)
    Console.WriteLine(error);
