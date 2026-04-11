using ObjLoader.Cache.Core;
using ObjLoader.Core.Models;
using System.IO;
using System.Numerics;
using System.Text;

namespace ObjLoader.Cache.Streaming
{
    public sealed class StreamingCacheWriter : IStreamingCacheWriter
    {
        private readonly string _cacheDir;
        private readonly string _tempDir;
        private readonly bool _isSplit;
        private readonly int _splitChunkSize;
        private Stream? _stream;
        private BinaryWriter? _writer;
        private bool _committed;
        private bool _disposed;

        public StreamingCacheWriter(string cacheDir, bool isSplit, int splitChunkSize = 256 * 1024)
        {
            _cacheDir = cacheDir;
            _isSplit = isSplit;
            _splitChunkSize = splitChunkSize;
            _tempDir = Path.Combine(cacheDir, ".tmp");
            _committed = false;

            if (Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, true); } catch { }
            }
            Directory.CreateDirectory(_tempDir);

            if (_isSplit)
            {
                _stream = new SplitWriteStream(_tempDir, _splitChunkSize);
            }
            else
            {
                string tempFile = Path.Combine(_tempDir, "model.bin");
                _stream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
            }
            _writer = new BinaryWriter(_stream);
        }

        public void WriteHeader(CacheHeader header)
        {
            EnsureNotDisposed();
            _writer!.Write(header.Signature);
            _writer.Write(header.Version);
            _writer.Write(header.Timestamp);
            _writer.Write(header.OriginalPath);
            _writer.Write(header.ParserId);
            _writer.Write(header.ParserVersion);
            _writer.Write(header.PluginVersion);
            _writer.Write(header.FileHash);
        }

        public void WriteThumbnail(byte[] thumbnail)
        {
            EnsureNotDisposed();
            _writer!.Write(thumbnail.Length);
            if (thumbnail.Length > 0)
            {
                _writer.Write(thumbnail);
            }
        }

        public void WriteMetadata(int vertexCount, int indexCount, List<ModelPart> parts, Vector3 center, float scale)
        {
            EnsureNotDisposed();
            _writer!.Write(vertexCount);
            _writer.Write(indexCount);
            _writer.Write(parts.Count);

            foreach (var part in parts)
            {
                var textureBytes = Encoding.UTF8.GetBytes(part.TexturePath ?? string.Empty);
                _writer.Write(textureBytes.Length);
                _writer.Write(textureBytes);
                _writer.Write(part.IndexOffset);
                _writer.Write(part.IndexCount);
                _writer.Write(part.BaseColor.X);
                _writer.Write(part.BaseColor.Y);
                _writer.Write(part.BaseColor.Z);
                _writer.Write(part.BaseColor.W);
            }

            _writer.Write(center.X);
            _writer.Write(center.Y);
            _writer.Write(center.Z);
            _writer.Write(scale);
        }

        public void WriteVertexChunk(ReadOnlySpan<byte> vertexData)
        {
            EnsureNotDisposed();
            _writer!.Flush();
            _stream!.Write(vertexData);
        }

        public void WriteIndexChunk(ReadOnlySpan<byte> indexData)
        {
            EnsureNotDisposed();
            _writer!.Flush();
            _stream!.Write(indexData);
        }

        public void Commit()
        {
            EnsureNotDisposed();
            if (_committed) return;

            _writer?.Flush();
            _stream?.Flush();
            _writer?.Dispose();
            _stream?.Dispose();
            _writer = null;
            _stream = null;

            try
            {
                if (_isSplit)
                {
                    CleanExistingSplitFiles(_cacheDir);
                    CleanExistingSingleFile(_cacheDir);

                    foreach (var tmpFile in Directory.GetFiles(_tempDir, "part.*.bin"))
                    {
                        string fileName = Path.GetFileName(tmpFile);
                        string dest = Path.Combine(_cacheDir, fileName);
                        File.Move(tmpFile, dest, true);
                    }

                    if (File.Exists(Path.Combine(_tempDir, "header.bin")))
                    {
                        File.Move(Path.Combine(_tempDir, "header.bin"), Path.Combine(_cacheDir, "header.bin"), true);
                    }
                }
                else
                {
                    string tmpFile = Path.Combine(_tempDir, "model.bin");
                    string destFile = Path.Combine(_cacheDir, "model.bin");
                    File.Move(tmpFile, destFile, true);

                    CleanExistingSplitFiles(_cacheDir);
                }

                _committed = true;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(_tempDir))
                    {
                        Directory.Delete(_tempDir, true);
                    }
                }
                catch { }
            }
        }

        public void Rollback()
        {
            try
            {
                _writer?.Dispose();
                _stream?.Dispose();
            }
            catch { }
            _writer = null;
            _stream = null;

            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }

        public long GetTotalSize()
        {
            if (_isSplit && _stream is SplitWriteStream sws)
            {
                return sws.TotalLength;
            }

            if (_stream != null)
            {
                return _stream.Length;
            }

            if (_committed)
            {
                return CalculateCommittedSize();
            }

            return 0;
        }

        public int GetPartsCount()
        {
            if (_isSplit && _stream is SplitWriteStream sws)
            {
                return sws.PartCount;
            }
            return 1;
        }

        private long CalculateCommittedSize()
        {
            long total = 0;
            try
            {
                if (_isSplit)
                {
                    foreach (var f in Directory.GetFiles(_cacheDir, "part.*.bin"))
                    {
                        total += new FileInfo(f).Length;
                    }
                }
                else
                {
                    string singleFile = Path.Combine(_cacheDir, "model.bin");
                    if (File.Exists(singleFile))
                    {
                        total = new FileInfo(singleFile).Length;
                    }
                }
            }
            catch { }
            return total;
        }

        private static void CleanExistingSplitFiles(string dir)
        {
            try
            {
                foreach (var f in Directory.GetFiles(dir, "part.*.bin"))
                {
                    File.Delete(f);
                }
            }
            catch { }
        }

        private static void CleanExistingSingleFile(string dir)
        {
            try
            {
                string singleFile = Path.Combine(dir, "model.bin");
                if (File.Exists(singleFile))
                {
                    File.Delete(singleFile);
                }
            }
            catch { }
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(StreamingCacheWriter));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (!_committed)
            {
                Rollback();
            }
            else
            {
                try
                {
                    _writer?.Dispose();
                    _stream?.Dispose();
                }
                catch { }
            }
        }

        private sealed class SplitWriteStream : Stream
        {
            private readonly string _dir;
            private readonly int _chunkSize;
            private int _partIndex;
            private FileStream? _currentStream;
            private long _totalLength;

            public long TotalLength => _totalLength;
            public int PartCount => _partIndex;

            public SplitWriteStream(string dir, int chunkSize)
            {
                _dir = dir;
                _chunkSize = chunkSize;
                NextPart();
            }

            private void NextPart()
            {
                _currentStream?.Dispose();
                string path = Path.Combine(_dir, $"part.{_partIndex}.bin");
                _currentStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                _partIndex++;
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => _totalLength;
            public override long Position { get => _totalLength; set => throw new NotSupportedException(); }

            public override void Flush() => _currentStream?.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count)
            {
                Write(new ReadOnlySpan<byte>(buffer, offset, count));
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                int written = 0;
                while (written < buffer.Length)
                {
                    if (_currentStream!.Length >= _chunkSize)
                    {
                        NextPart();
                    }

                    int remainingInChunk = (int)(_chunkSize - _currentStream!.Length);
                    int toWrite = Math.Min(remainingInChunk, buffer.Length - written);
                    _currentStream!.Write(buffer.Slice(written, toWrite));
                    written += toWrite;
                    _totalLength += toWrite;
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    try { _currentStream?.Dispose(); } catch { }
                }
                base.Dispose(disposing);
            }
        }
    }
}