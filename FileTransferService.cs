using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FileTransferTool
{
    public class FileTransferService
    {
        private readonly TransferData _transferData;

        public FileTransferService(TransferData transferData)
        {
            _transferData = transferData;
        }

        public async Task TransferAsync(CancellationToken cancellationToken) {
            using var sourceFileHasher = HashUtil.CreateSha256Hash();
            var fileChunks = await TransferChunksAsync(sourceFileHasher, cancellationToken);
            LogFileChunksData(fileChunks);
            await CompareAndLogFileHashes(sourceFileHasher, _transferData.DestinationFilePath, cancellationToken);
        }

        private async Task<List<ChunkData>> TransferChunksAsync(IncrementalHash sourceFileHasher, CancellationToken cancellationToken) {
            var sourceBuffer = new byte[_transferData.FileChunkSize];
            var destinationBuffer = new byte[_transferData.FileChunkSize];
            var fileChunks = new List<ChunkData>();

            await using var sourceFileStream = 
                new FileStream(_transferData.SourceFilePath, FileMode.Open, FileAccess.Read, 
                FileShare.Read, bufferSize: 4096, useAsync: true);

            await using var destinationFileStream = 
                new FileStream(_transferData.DestinationFilePath, FileMode.Create, FileAccess.ReadWrite, 
                FileShare.Read, bufferSize:4096, useAsync: true);

            long totalBytes = sourceFileStream.Length;
            long offset = 0;
            int chunkIndex = 0;

            while (offset < totalBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int chunkLength = (int)Math.Min(_transferData.FileChunkSize, totalBytes - offset);

                await sourceFileStream.ReadExactlyAsync(sourceBuffer.AsMemory(0, chunkLength), cancellationToken);
                string currentSourceChunkMd5Hash = HashUtil.GenerateMd5Hash(sourceBuffer.AsSpan(0, chunkLength));
                sourceFileHasher.AppendData(sourceBuffer.AsSpan(0, chunkLength));

                int retryCount = await WriteChunkWithRetryAsync(
                    destinationFileStream, sourceBuffer, destinationBuffer,
                    new ChunkContext(chunkIndex, offset, chunkLength, currentSourceChunkMd5Hash), 
                    cancellationToken);

                fileChunks.Add(new ChunkData
                {
                    Index = chunkIndex,
                    Offset = offset,
                    Size = chunkLength,
                    Md5Hash = currentSourceChunkMd5Hash,
                    RetryCount = retryCount
                });

                offset += chunkLength;
                chunkIndex++;
            }
            return fileChunks;
        }

        public async Task<int> WriteChunkWithRetryAsync(
            Stream destinationFileStream,
            byte[] sourceBuffer, 
            byte[] destinationBuffer,
            ChunkContext chunk,
            CancellationToken cancellationToken)
        {
            string destinationChunkMd5Hash = string.Empty;
            for (int retry = 0; retry <= _transferData.MaxRetries; retry++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try {
                    // Sets current position before writing, position moves forward when retry occurs
                    destinationFileStream.Position = chunk.Offset;

                    // Write the chunk to the destination file
                    await destinationFileStream.WriteAsync(
                        sourceBuffer.AsMemory(0, chunk.Length),
                        cancellationToken);

                    await destinationFileStream.FlushAsync(cancellationToken);

                    // Sets current position before reading back, position moves forward when retry occurs
                    destinationFileStream.Position = chunk.Offset;

                    // Read back the chunk from the destination file to verify integrity
                    await destinationFileStream.ReadExactlyAsync(
                        destinationBuffer.AsMemory(0, chunk.Length),
                        cancellationToken);

                    destinationChunkMd5Hash = HashUtil.GenerateMd5Hash(destinationBuffer.AsSpan(0, chunk.Length));

                    if (string.Equals(chunk.ExpectedMd5Hash, destinationChunkMd5Hash))
                    {
                        return retry;
                    }

                    if (retry < _transferData.MaxRetries)
                    {
                        Console.WriteLine($"Chunk mismatch detected at index {chunk.Index}, " +
                            $"retrying... {retry + 1} of {_transferData.MaxRetries}");
                    }
                }
                catch (IOException ex) when (retry < _transferData.MaxRetries) { 
                    Console.WriteLine($"I/O error occurred while transferring chunk at index: {chunk.Index}, Error: {ex.Message} " +
                        $"retrying... {retry + 1} of {_transferData.MaxRetries}.");
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }
            }

            throw new IOException(
                $"Failed to transfer chunk at index: {chunk.Index} offset: {chunk.Offset} after: {_transferData.MaxRetries} retries "
                + $"due to hash mismatch: Expected {chunk.ExpectedMd5Hash}, got {destinationChunkMd5Hash}");
        }

        private async Task CompareAndLogFileHashes(IncrementalHash sourceFileHasher, string destinationFilePath, CancellationToken cancellationToken) {
            string sourceFileHash = HashUtil.GetSha256Hash(sourceFileHasher);
            string destinationFileHash = await GenerateFileSha256Hash(destinationFilePath, cancellationToken);

            Console.WriteLine($"Source file hash: {sourceFileHash}");
            Console.WriteLine($"Destination file hash: {destinationFileHash}");

            if (sourceFileHash == destinationFileHash)
            {
                Console.WriteLine("Source and destination file hashes match. File transfer integrity verified.");
            }
            else {
                Console.WriteLine("Source and destination file hashes do not match. File transfer integrity verification failed.");
            }
        }

        private async Task<string> GenerateFileSha256Hash(string filePath, CancellationToken cancellationToken) { 
            int bufferSize = 1024 * 1024;

            await using var fileStream =
                new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: bufferSize, useAsync: true);

            using var fileHasher = HashUtil.CreateSha256Hash();
            var buffer = new byte[bufferSize];
            long offset = 0;
            long totalBytes = fileStream.Length;

            while (offset < totalBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int chunkLength = (int)Math.Min(bufferSize, totalBytes - offset);
                await fileStream.ReadExactlyAsync(buffer.AsMemory(0, chunkLength), cancellationToken);
                fileHasher.AppendData(buffer.AsSpan(0, chunkLength));
                offset += chunkLength;
            }

            return HashUtil.GetSha256Hash(fileHasher);
        }

        private static void LogFileChunksData(List<ChunkData> fileChunks) { 
            foreach (var chunk in fileChunks)
            {
                Console.WriteLine($"Chunk index: {chunk.Index}, offset: {chunk.Offset}, size: {chunk.Size}, " +
                    $"md5 hash: {chunk.Md5Hash}, retries: {chunk.RetryCount}");
            }
        }

        public readonly record struct ChunkContext(int Index, long Offset, int Length, string ExpectedMd5Hash);
    }
}
