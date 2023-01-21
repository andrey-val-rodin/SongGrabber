using LibVLCSharp.Shared;

namespace SongGrabber.Grabbing
{
    public class QueueMediaInput : MediaInput
    {
        private readonly Queue _queue;

        public QueueMediaInput(Queue queue)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
//            CanSeek = false;
        }

        public override bool Open(out ulong size)
        {
            size = 1000000;
            return true;
        }

        public unsafe override int Read(IntPtr buf, uint len)
        {
            try
            {
                var result = _queue.Dequeue(new Span<byte>(buf.ToPointer(), (int)Math.Min(len, 2147483647u)));
                System.Diagnostics.Debug.WriteLine($"*** Read {len} bytes. Readed: {result}. Queue.Count: {_queue.Count}");
                return result;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public override bool Seek(ulong offset)
        {
            return _queue.Seek(offset);
        }

        public override void Close()
        {
        }
    }
}
