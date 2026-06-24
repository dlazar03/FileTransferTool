using System;
using System.Collections.Generic;
using System.Text;

namespace FileTransferTool.Tests
{
    internal class TempFile : IDisposable
    {
        public string Path { get; }

        public TempFile(byte[]? content = null)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fttest_{Guid.NewGuid():N}.bin");
            if (content != null)
                File.WriteAllBytes(Path, content);
        }

        public void Dispose()
        {
            try { File.Delete(Path); } catch {}
        }
    }

    internal class TempDirectory : IDisposable
    {
        public string Path { get; }

        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"fttest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch {}
        }
    }

    internal class FailingWriteStream : MemoryStream 
    {
        public int WriteAttempts { get; private set; }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteAttempts++;
            throw new IOException("Simulated write failure");
        }
    }

    internal class RecoveredWriteStream : MemoryStream
    {
        private readonly int _failuresBeforeSuccessCount;
        public int WriteAttempts { get; private set; }

        public RecoveredWriteStream(int failuresBeforeSuccessCount)
        {
            _failuresBeforeSuccessCount = failuresBeforeSuccessCount;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteAttempts++;

            if (WriteAttempts <= _failuresBeforeSuccessCount)
            {
                throw new IOException("Simulated write failure");
            }
            return base.WriteAsync(buffer, cancellationToken);
        }
    }

    internal class CountWriteStream : MemoryStream
    {
        public int WriteAttempts { get; private set; }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteAttempts++;
            return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
