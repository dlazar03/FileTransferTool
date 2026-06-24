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
            string sourceFileHash = await TransferChunksAsync(cancellationToken);
            await CompareFileHashes(sourceFileHash, _transferData.DestinationFilePath, cancellationToken);
        }

        public async Task<string> TransferChunksAsync(CancellationToken cancellationToken) {
            var buffer = new byte[_transferData.FileChunkSize];
            using var fileHasher = HashUtil.InitializeSha256Hash();

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

                await sourceFileStream.ReadExactlyAsync(buffer.AsMemory(0, chunkLength), cancellationToken);
                string currentSourceChunkMd5Hash = HashUtil.GenerateMd5Hash(buffer.AsSpan(0, chunkLength));
                fileHasher.AppendData(buffer, 0, chunkLength);

                Console.WriteLine($"Current source chunk index: {chunkIndex}, current source chunk hash: {currentSourceChunkMd5Hash}");
                Console.WriteLine($"Chunk position: {offset}, chunk size: {chunkLength}");

                for (int retry = 0; retry <= _transferData.MaxRetries; retry++) {
                    // Write the chunk to the destination file
                    await destinationFileStream.WriteAsync(buffer.AsMemory(0, chunkLength), cancellationToken);
                    await destinationFileStream.FlushAsync(cancellationToken);

                    // Read back the chunk from the destination file to verify integrity
                    destinationFileStream.Position = offset;
                    await destinationFileStream.ReadExactlyAsync(buffer.AsMemory(0, chunkLength), cancellationToken);
                    string destinationChunkMd5Hash = HashUtil.GenerateMd5Hash(buffer.AsSpan(0, chunkLength));

                    if (string.Equals(currentSourceChunkMd5Hash, destinationChunkMd5Hash))
                    {
                        break;
                    }
                    else {
                        Console.WriteLine($"Chunk mismatch detected at index {chunkIndex}, retrying... {retry} of {_transferData.MaxRetries}");
                        if (retry == _transferData.MaxRetries) {
                            throw new IOException($"Failed to transfer chunk at index {chunkIndex} (offset {offset}) after {_transferData.MaxRetries} retries"
                                + $"due to hash mismatch: Expected {currentSourceChunkMd5Hash}, got {destinationChunkMd5Hash}");
                        }
                    }
                }

                offset += chunkLength;
                chunkIndex++;
            }
            string fileHash = HashUtil.GenerateSha256Hash(fileHasher);
            Console.WriteLine($"File transfer completed. File hash: {fileHash}");
            return fileHash;
        }

        private async Task CompareFileHashes(string sourceFileHash, string destinationFilePath, CancellationToken cancellationToken) {
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

            using var fileHasher = HashUtil.InitializeSha256Hash();
            var buffer = new byte[bufferSize];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int bytesRead = await fileStream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }
                fileHasher.AppendData(buffer, 0, bytesRead);
            }
            return HashUtil.GenerateSha256Hash(fileHasher);
        }
    }
}
