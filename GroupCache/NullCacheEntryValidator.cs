using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GroupCache
{
    public class NullCacheEntryValidator : ICacheEntryValidator
    {
        public ValidationStream ValidateEntryPassThrough(string key, Stream sink)
        {
            return new NullValidationStream(sink);
        }
    }

    public class NullValidationStream : ValidationStream
    {
        private Stream _sink;
        public NullValidationStream(Stream sink)
        {
            _sink = sink;
        }
        public override bool CanRead => _sink.CanRead;

        public override bool CanSeek => _sink.CanSeek;

        public override bool CanWrite => _sink.CanWrite;

        public override long Length => _sink.Length;

        public override long Position { get => _sink.Position; set => _sink.Position = value; }

        public override void Flush()
        {
            _sink.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _sink.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _sink.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _sink.SetLength(value);
        }

        public override Task ValidateAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _sink.Write(buffer, offset, count);
        }
    }

}