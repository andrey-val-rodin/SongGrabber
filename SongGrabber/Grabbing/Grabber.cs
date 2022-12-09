using System.IO;
using System.Net.Http.Headers;

namespace SongGrabber.Grabbing
{
    public class Grabber
    {
        private readonly IConsole _console;
        private readonly HttpClient _httpClient = new();
        private int _icyMetaInt;
        private bool _isRecording;
        private int _recordedSongCount;
        private FileStream _filestream;
        byte[] _buffer = new byte[65536];

        public Grabber()
        {
        }

        public Grabber(IConsole console)
        {
            _console = console;
        }

        public bool IsGrabbing { get; private set; }

        public async Task<string> GrabAsync(
            Uri uri, int songCount = int.MaxValue, CancellationToken token = default)
        {
            _icyMetaInt = 0;
            _recordedSongCount = 0;
            _console.WriteLine(uri.ToString());

            try
            {
                _console?.WriteLine("Accessing audiostream...");
                await using var stream = await CreateStreamAsync(uri, token);
                if (stream == null)
                    return "Site does not support metadata";
                /*
                byte[] buffer = new byte[65536];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    _filestream.Write(buffer, 0, read);
                }
                */
            }
            catch (Exception e)
            {
                return e.Message;
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

            return new MetadataStream(response.Content.ReadAsStream(token), _icyMetaInt);
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
            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                filename = string.Join("", filename.Split(Path.GetInvalidFileNameChars()));

                if (string.IsNullOrEmpty(filename))
                    filename = "noname";
            }

            return filename;
        }

        protected byte[] GetBuffer(int size)
        {
            if (size > _buffer.Length)
                _buffer = new byte[size];

            return _buffer;
        }
    }
}
