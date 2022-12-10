using LibVLCSharp.Shared;
using Microsoft.VisualBasic;
using NAudio.Wave;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;

namespace SongGrabber.Grabbing
{
    public class Grabber
    {
        private readonly IConsole _console;
        private readonly HttpClient _httpClient = new();
        private int _icyMetaInt;
        private byte[] _buffer = new byte[65536];
        private WaveOutEvent _outputDevice;
        private WaveFormat _waveFormat;
        private BufferedWaveProvider _waveProvider;
        private WaveFileWriter _writer;

        public Grabber() : this(null)
        {
        }

        public Grabber(IConsole console)
        {
            Core.Initialize();

            Status = Status.Idle;
            _console = console;
        }

        public Status Status { get; private set; }
        public int RecordedSongCount { get; private set; }
        public bool NumberSongs { get; set; }

        public async Task<string> GrabAsync(
            Uri uri, int songCount = int.MaxValue, CancellationToken token = default)
        {
            Status = Status.PreparingStream;
            RecordedSongCount = 0;
            _icyMetaInt = 0;
            _console?.WriteLine(uri.ToString());

            try
            {
                _console?.WriteLine("Accessing audiostream...");
                await using var stream = await CreateStreamAsync(uri, token);
                if (stream == null)
                    return "Site does not support metadata";

                // TODO: remove "enableDebugLogs" ->
//                using var libVLC = new LibVLC();
//                using var libVLC = new LibVLC("--quiet");
                using var libVLC = new LibVLC(enableDebugLogs: true);
                using var media = new Media(libVLC, new StreamMediaInput(stream));
                using var mediaPlayer = new MediaPlayer(media);

                using var outputDevice = new WaveOutEvent();
                _outputDevice = outputDevice;
                _waveFormat = new WaveFormat(44100, 16, 2);
                _waveProvider = new BufferedWaveProvider(_waveFormat);
                outputDevice.Init(_waveProvider);

                mediaPlayer.SetAudioFormatCallback(AudioSetup, AudioCleanup);
                mediaPlayer.SetAudioCallbacks(PlayAudio, PauseAudio, ResumeAudio, FlushAudio, DrainAudio);
                stream.MetadataChanged += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.NewStreamTitle))
                    {
                        Status = Status.Recording;
                        var num = RecordedSongCount + 1;
                        var numStr = NumberSongs ? $"{num.ToString("D2")} " : "";
                        var filename = $"{numStr}{ValidateFilename(e.NewStreamTitle)}.wav";
                        _console?.WriteLine($"Writing {filename}...");
                        if (_writer != null)
                        {
                            _writer.Close();
                            _writer = null;
                            RecordedSongCount++;
                        }

                        if (RecordedSongCount < songCount)
                            _writer = new WaveFileWriter(filename, _waveFormat);
                    }
                };

                Status = Status.WaitingForNewSong;
                _console?.WriteLine("Waiting for the next song...");
                mediaPlayer.Play();
                outputDevice.Play();

                // TODO
                // Remove cycle, replace by semaphore
                Console.WriteLine("Press 'q' to quit. Press any other key to pause/play.");
                while (true)
                {
                    if (Console.ReadKey().KeyChar == 'q')
                        break;

                    if (RecordedSongCount >= songCount)
                        break;

                    if (mediaPlayer.IsPlaying)
                        mediaPlayer.Pause();
                    else
                        mediaPlayer.Play();
                }
                //TODO
                //What if MetadataChanged does't fired for very long time?
                _writer?.Close();
                _writer = null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
            finally
            {
                Status = Status.Idle;
            }

            return null;
        }

        async Task<MetadataStream> CreateStreamAsync(Uri uri, CancellationToken token)
        {
            var request = new HttpRequestMessage
            {
                RequestUri = uri,
                Method = HttpMethod.Get
            };

            request.Headers.Add("Icy-MetaData", "1");
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            System.Diagnostics.Debug.WriteLine(response.Headers.ToString());
            _icyMetaInt = GetIcyMetaInt(response.Headers);
            if (_icyMetaInt == 0)
                return null;

            var sourceStream = response.Content.ReadAsStream(token);
            return new MetadataStream(sourceStream, _icyMetaInt, 4096);
        }

        protected static int GetIcyMetaInt(HttpResponseHeaders headers)
        {
            const string header = "icy-metaint";
            if (!headers.Contains(header))
                return 0;

            int res = 0;
            try
            {
                var values = headers.GetValues(header);
                res = int.Parse(values.FirstOrDefault());
            }
            catch { }

            return res;
        }

        protected static string ValidateFilename(string filename)
        {
            filename = filename.Replace('\"', '\'');
            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                filename = string.Join("", filename.Split(Path.GetInvalidFileNameChars()));

                if (string.IsNullOrEmpty(filename))
                    filename = "noname";
            }

            return filename;
        }

        private byte[] GetBuffer(int size)
        {
            if (size > _buffer.Length)
                _buffer = new byte[size];

            return _buffer;
        }

        #region Callbacks
        void PlayAudio(IntPtr data, IntPtr samples, uint count, long pts)
        {
            int bytes = (int)count * 4; // (16 bit, 2 channels)
            var buffer = GetBuffer(bytes);
            Marshal.Copy(samples, buffer, 0, bytes);

            _waveProvider.AddSamples(buffer, 0, bytes);
            _writer?.Write(buffer, 0, bytes);
        }

        int AudioSetup(ref IntPtr opaque, ref IntPtr format, ref uint rate, ref uint channels)
        {
            channels = (uint)_waveFormat.Channels;
            rate = (uint)_waveFormat.SampleRate;
            return 0;
        }

        void DrainAudio(IntPtr data)
        {
            _writer?.Flush();
        }

        void FlushAudio(IntPtr data, long pts)
        {
            _writer?.Flush();
            _waveProvider.ClearBuffer();
        }

        void ResumeAudio(IntPtr data, long pts)
        {
            _outputDevice.Play();
        }

        void PauseAudio(IntPtr data, long pts)
        {
            _outputDevice.Pause();
        }

        void AudioCleanup(IntPtr opaque)
        {
        }
        #endregion
    }
}
