// See https://aka.ms/new-console-template for more information
using LibVLCSharp.Shared;
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

// Initialization
Core.Initialize();
// var libVLC = new LibVLC("--quiet");
var libVLC = new LibVLC(enableDebugLogs: true);

var grabber = new Grabber(libVLC, new ConsoleImpl()) { NumberSongs = true };
CancellationTokenSource tokenSource = new();
var grabTask = grabber.GrabAsync(new Uri(url), 5, tokenSource.Token);
var readKeyTask = new Task(() =>
{
    ConsoleKeyInfo key = new();
    while (!Console.KeyAvailable && key.Key != ConsoleKey.Escape)
    {
        key = Console.ReadKey(true);
    }

    tokenSource.Cancel();
});
readKeyTask.Start();
Console.WriteLine("Press 'Esc' to quit");

var tasks = new[] { grabTask, readKeyTask };
Task.WaitAny(tasks);

if (!grabTask.IsCompleted)
    await grabTask;

var result = grabTask.Result;
if (result != null)
{
    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine(result.ToString());
}
