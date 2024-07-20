namespace InstallerCommons.ZipHelper
{
    public class StreamWithProgress(Stream stream, IProgress<int> readProgress, IProgress<int> writeProgress) : Stream
    {
        private readonly Stream _stream = stream;
        private readonly IProgress<int> _readProgress = readProgress;
        private readonly IProgress<int> _writeProgress = writeProgress;

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;
        public override long Position
        {
            get { return _stream.Position; }
            set { _stream.Position = value; }
        }

        public override void Flush() { _stream.Flush(); }
        public override long Seek(long offset, SeekOrigin origin) { return _stream.Seek(offset, origin); }
        public override void SetLength(long value) { _stream.SetLength(value); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = _stream.Read(buffer, offset, count);

            _readProgress?.Report(bytesRead);
            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
            _writeProgress?.Report(count);
        }
    }

}
