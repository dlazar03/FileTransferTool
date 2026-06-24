using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FileTransferTool.Tests
{
    public class FileTransferServiceTests
    {
        [Theory]
        [InlineData(0)]                          // empty file
        [InlineData(100)]                        // smaller than chunk
        [InlineData(1024 * 1024)]                // exactly one chunk
        [InlineData(1024 * 1024 + 1)]            // chunk + 1 byte
        [InlineData(3 * 1024 * 1024)]            // multiple full chunks
        [InlineData(5 * 1024 * 1024 + 12345)]    // multiple chunks + remainder
        public async Task TransferAsync_CopiesFileWithMatchingContent(int fileSize)
        {
            // Arrange
            var content = new byte[fileSize];
            new Random(42).NextBytes(content);

            using var source = new TempFile(content);
            using var destDir = new TempDirectory();
            var destPath = Path.Combine(destDir.Path, Path.GetFileName(source.Path));

            var service = new FileTransferService(new TransferData
            {
                SourceFilePath = source.Path,
                DestinationFilePath = destPath,
            });

            // Act
            await service.TransferAsync(CancellationToken.None);

            // Assert
            Assert.True(File.Exists(destPath));
            var copied = await File.ReadAllBytesAsync(destPath);
            Assert.Equal(content, copied);
        }

        [Fact]
        public async Task TransferAsync_ComputesMatchingSourceAndDestinationHashes()
        {
            // Arrange
            var content = new byte[2 * 1024 * 1024 + 500];
            new Random(7).NextBytes(content);

            using var source = new TempFile(content);
            using var destDir = new TempDirectory();
            var destPath = Path.Combine(destDir.Path, "out.bin");

            var service = new FileTransferService(new TransferData
            {
                SourceFilePath = source.Path,
                DestinationFilePath = destPath,
            });

            // Act
            await service.TransferAsync(CancellationToken.None);

            using var sha = SHA256.Create();
            await using var sourceStream = File.OpenRead(source.Path);
            var sourceHash = await sha.ComputeHashAsync(sourceStream);

            sha.Initialize();
            await using var destStream = File.OpenRead(destPath);
            var destHash = await sha.ComputeHashAsync(destStream);

            // Assert
            Assert.Equal(sourceHash, destHash);
        }

        [Fact]
        public async Task TransferAsync_HashMismatchRetriesAndThrows()
        {
            // Arrange
            var stream = new CountWriteStream();
            var sourceBuffer = Encoding.UTF8.GetBytes("Random Text");
            var destBuffer = new byte[sourceBuffer.Length];

            var chunk = new FileTransferService.ChunkContext(0, 0, sourceBuffer.Length, "invalid hash");
            var transferData = new TransferData { SourceFilePath = "", DestinationFilePath = "", MaxRetries = 3 };
            var service = new FileTransferService(transferData);

            // Act & Assert
            await Assert.ThrowsAsync<IOException>(
                () => service.WriteChunkWithRetryAsync(
                    stream,
                    sourceBuffer,
                    destBuffer,
                    chunk,
                    CancellationToken.None)); 

            Assert.Equal(transferData.MaxRetries + 1, stream.WriteAttempts);
        }

        [Fact]
        public async Task TransferAsync_RespectsCancellation()
        {
            // Arrange
            var content = new byte[50 * 1024 * 1024];
            new Random(99).NextBytes(content);

            using var source = new TempFile(content);
            using var destDir = new TempDirectory();
            var destPath = Path.Combine(destDir.Path, "out.bin");

            var service = new FileTransferService(new TransferData
            {
                SourceFilePath = source.Path,
                DestinationFilePath = destPath,
            });

            // Act & Assert
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(10));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.TransferAsync(cts.Token));
        }

        [Fact]
        public async Task TransferAsync_RetryAndThrowOnWriteFailure() 
        {
            // Arrange 
            var stream = new FailingWriteStream();

            var sourceBuffer = new byte[1024];
            var destBuffer = new byte[1024];

            var chunk = new FileTransferService.ChunkContext(0, 0, 1024, "dummyhash");
            var transferData = new TransferData { SourceFilePath = "", DestinationFilePath = "", MaxRetries = 3 };

            var service = new FileTransferService(transferData);

            // Act 
            var exception = await Assert.ThrowsAsync<IOException>(
                () => service.WriteChunkWithRetryAsync(
                    stream,
                    sourceBuffer,
                    destBuffer,
                    chunk,
                    CancellationToken.None));

            // Assert
            Assert.Equal(transferData.MaxRetries + 1, stream.WriteAttempts);
            Assert.Equal("Simulated write failure", exception.Message);
        }

        [Fact]
        public async Task TransferAsync_RetryOnWriteAndSucceed()
        {
            // Arrange
            var sourceBuffer = new byte[1024];
            var destBuffer = new byte[1024];
            
            var expectedMd5Hash = HashUtil.GenerateMd5Hash(sourceBuffer.AsSpan(0, sourceBuffer.Length));
            var chunk = new FileTransferService.ChunkContext(0, 0, sourceBuffer.Length, expectedMd5Hash);
            var service = new FileTransferService(new TransferData { SourceFilePath = "", DestinationFilePath = "", MaxRetries = 3 });
            var stream = new RecoveredWriteStream(2);

            // Act
            var retries = await service.WriteChunkWithRetryAsync(
                stream,
                sourceBuffer,
                destBuffer,
                chunk,
                CancellationToken.None);

            Assert.Equal(2, retries);
        }
    }
}
