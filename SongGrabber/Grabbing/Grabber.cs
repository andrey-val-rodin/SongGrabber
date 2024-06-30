using LibVLCSharp.Shared;
using NAudio.Wave;
using SongGrabber.Handlers;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;

namespace SongGrabber.Grabbing
{
    public sealed class Grabber
    {
        private readonly IConsole _console;
        private int _songCount;
        private readonly LibVLC _libVLC;
        private MetadataStream _stream;
        private Media _media;
        private MediaPlayer _mediaPlayer;
        private WaveOutEvent _outputDevice;
        private readonly HttpClient _httpClient = new();
        private int _icyMetaInt;
        private byte[] _buffer = new byte[65536];
        private WaveFormat _waveFormat;
        private BufferedWaveProvider _waveProvider;
        private WaveFileWriter _writer;
        private readonly object _lockObj = new ();
        private ResultImpl _result;
        private CancellationTokenSource _tokenSource;

        public Grabber(LibVLC libVLC) : this(libVLC, null)
        {
        }

        public Grabber(LibVLC libVLC, IConsole console)
        {
            _libVLC = libVLC ?? throw new ArgumentNullException(nameof(libVLC));
            _console = console;
            Status = Status.Idle;
        }

        public Status Status { get; private set; }
        public int RecordedSongCount { get; private set; }
        public bool NumberSongs { get; set; }

        public async Task<IResult> GrabAsync(
            Uri uri, int songCount, CancellationToken token = default)
        {
            try
            {
                if (!await InitGrabbingAsync(uri, songCount, token))
                    return _result;

                _mediaPlayer.Play();
                _outputDevice.Play();

                // Wait until cancellation is requested
                _tokenSource = new CancellationTokenSource();
                token.Register(() => _tokenSource?.Cancel());
                await Task.Delay(TimeSpan.FromMilliseconds(-1), _tokenSource.Token);

                //TODO
                //What if MetadataChanged doesn't fired for very long time?
            }
            catch (TaskCanceledException)
            {
                if (token.IsCancellationRequested)
                {
                    // Canceled from outside
                    _result.Error = "Canceled";
                    RemoveLastSong("Deleted due to cancellation");
                }
            }
            catch (Exception e)
            {
                _result.Error = e.Message;
                RemoveLastSong("Deleted due to error");
                _tokenSource?.Cancel();
            }
            finally
            {
                await FinishGrabbingAsync();
            }

            return _result;
        }

        private async Task<bool> InitGrabbingAsync(Uri uri, int songCount, CancellationToken token)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            _songCount = songCount > 0
                ? songCount
                : throw new ArgumentException($"{songCount} must be greater than zero");

            _result = new ResultImpl(uri, Directory.GetCurrentDirectory());
            Status = Status.PreparingStream;
            RecordedSongCount = 0;
            _icyMetaInt = 0;
            _console?.WriteLine(uri.ToString());

            _console?.WriteLine("Accessing audio stream...");
            _stream = await CreateStreamAsync(uri, token);
            if (_stream == null)
            {
                _result.Error = "Site does not support metadata";
                return false;
            }

            _stream.MetadataChanged += MetadataChangedHandler;

            _media = new Media(_libVLC, new StreamMediaInput(_stream), ":no-video");
            _mediaPlayer = new MediaPlayer(_media);

            _outputDevice = new WaveOutEvent();
            _waveFormat = new WaveFormat(44100, 16, 2);
            _waveProvider = new BufferedWaveProvider(_waveFormat);
            _outputDevice.Init(_waveProvider);

            _mediaPlayer.SetAudioFormatCallback(AudioSetup, null);
            _mediaPlayer.SetAudioCallbacks(PlayAudio, null, null, FlushAudio, DrainAudio);

            return true;
        }

        private async Task FinishGrabbingAsync()
        {
            if (Status == Status.Idle)
                return; // already finished

            await Task.Yield();

            _mediaPlayer?.Stop();
            _outputDevice?.Stop();
            
            _outputDevice?.Dispose();
            _mediaPlayer?.Dispose();
            _media?.Dispose();
            _writer?.Close();
            _stream?.Dispose();
            _tokenSource?.Dispose();

            _tokenSource = null;
            _writer = null;
            _mediaPlayer = null;
            _media = null;
            _stream = null;

            Status = Status.Idle;
        }

        private void RemoveLastSong(string cause)
        {
            bool __lockWasTaken = false;
            try
            {
                Monitor.Enter(_lockObj, ref __lockWasTaken);

                if (_writer != null)
                {
                    _writer.Close();
                    _writer = null;
                }

                var lastSong = _result.Songs.LastOrDefault();
                if (lastSong != null)
                {
                    File.Delete(lastSong.Filename);
                    lastSong.Status = SongStatus.Deleted;
                    lastSong.Notes = cause;
                }
            }
            catch (Exception e)
            {
                _result.Error = e.Message;
            }
            finally
            {
                if (__lockWasTaken)
                    Monitor.Exit(_lockObj);
            }
        }

        private async Task<MetadataStream> CreateStreamAsync(Uri uri, CancellationToken token)
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
            return new MetadataStream(sourceStream, _icyMetaInt);
        }

        private static int GetIcyMetaInt(HttpResponseHeaders headers)
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

        private string GetFileName(string streamTitle)
        {
            var num = RecordedSongCount + 1;
            var numStr = NumberSongs ? $"{num:D2} " : "";
            var filename = $"{numStr}{ValidateFilename(streamTitle)}.wav";
            return filename;
        }

        private static string ValidateFilename(string filename)
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

        #region Callbacks and handlers
        private void MetadataChangedHandler(object sender, MetadataChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.NewStreamTitle))
            {
                if (Status == Status.PreparingStream)
                {
                    // Skip currently playing song
                    Status = Status.WaitingForNewSong;
                    _console?.WriteLine("Waiting for the next song...");
                    return;
                }

                Status = Status.Recording;
                bool __lockWasTaken = false;
                try
                {
                    Monitor.Enter(_lockObj, ref __lockWasTaken);

                    if (_writer != null)
                    {
                        // Close previous song
                        RecordedSongCount++;

                        _writer.Close();
                        _writer = null;

                        var lastSong = _result.Songs.LastOrDefault();
                        lastSong.Status = SongStatus.Downloaded;
                    }

                    if (RecordedSongCount < _songCount)
                    {
                        // Start new song
                        var filename = GetFileName(e.NewStreamTitle);
                        _console?.WriteLine($"Writing {filename}...");

                        _writer = new WaveFileWriter(filename, _waveFormat);
                        _result.Songs.Add(new Song()
                        {
                            Filename = filename,
                            Status = SongStatus.Downloading
                        });
                    }
                }
                catch (Exception ex)
                {
                    _result.Error = ex.Message;
                    RemoveLastSong("Deleted due to error");
                    _tokenSource?.Cancel();
                }
                finally
                {
                    if (__lockWasTaken)
                        Monitor.Exit(_lockObj);

                    if (RecordedSongCount >= _songCount)
                        _tokenSource?.Cancel();
                }
            }
        }

        private int AudioSetup(ref IntPtr opaque, ref IntPtr format, ref uint rate, ref uint channels)
        {
            channels = (uint)_waveFormat.Channels;
            rate = (uint)_waveFormat.SampleRate;
            return 0;
        }

        private void PlayAudio(IntPtr data, IntPtr samples, uint count, long pts)
        {
            int bytes = (int)count * 4; // (16 bit, 2 channels)
            var buffer = GetBuffer(bytes);
            Marshal.Copy(samples, buffer, 0, bytes);

            bool __lockWasTaken = false;
            try
            {
                Monitor.Enter(_lockObj, ref __lockWasTaken);

                _waveProvider.AddSamples(buffer, 0, bytes);
                _writer?.Write(buffer, 0, bytes);
            }
            catch (Exception e)
            {
                _result.Error = e.Message;
                RemoveLastSong("Deleted due to error");
                _tokenSource?.Cancel();
            }
            finally
            {
                if (__lockWasTaken)
                    Monitor.Exit(_lockObj);
            }
        }

        private void DrainAudio(IntPtr data)
        {
            _writer?.Flush();
        }

        private void FlushAudio(IntPtr data, long pts)
        {
            _writer?.Flush();
            _waveProvider.ClearBuffer();
        }
        #endregion
    }
}
