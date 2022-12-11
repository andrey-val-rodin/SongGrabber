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
CancellationTokenSource tokenSource = new();
var grabTask = grabber.GrabAsync(new Uri(url), 5, tokenSource.Token);
var readKeyTask = new Task(ListenForEsc);
readKeyTask.Start();
Console.WriteLine("Press 'Esc' to quit");

var tasks = new[] { grabTask, readKeyTask };
Task.WaitAny(tasks);

if (grabTask.IsCompleted)
{
    var error = grabTask.Result;
    if (error != null)
        Console.WriteLine(error);
}

static void ListenForEsc()
{
    ConsoleKeyInfo key = new();
    while (!Console.KeyAvailable && key.Key != ConsoleKey.Escape)
    {
        key = Console.ReadKey(true);
    }
}
