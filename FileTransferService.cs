using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FileTransferTool
{
    public class FileTransferService
    {
        public async Task TransferAsync(string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken) {
            const int chunkSize = 1024 * 1024;
            var buffer = new byte[chunkSize];

            using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            using var sourceFileStream = 
                new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, 
                FileShare.Read, bufferSize: 4096, useAsync: true);

            using var destinationFileStream = 
                new FileStream(destinationFilePath, FileMode.Create, FileAccess.ReadWrite, 
                FileShare.Read, bufferSize:4096, useAsync: true);

            long totalBytes = sourceFileStream.Length;
            long offset = 0;
            int chunkIndex = 0;
            int maxRetries = 3;

            while (offset < totalBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int chunkLength = (int)Math.Min(chunkSize, totalBytes - offset);

                await sourceFileStream.ReadExactlyAsync(buffer.AsMemory(0, chunkLength), cancellationToken);

                ReadOnlySpan<byte> currentChunk = new(buffer, 0, chunkLength);
                byte[] hashBytes = MD5.HashData(currentChunk);
                string currentSourceChunkMd5Hash = Convert.ToHexString(hashBytes);
                fileHasher.AppendData(buffer, 0, chunkLength);

                Console.WriteLine($"Current source chunk index: {chunkIndex}, current source chunk hash: {currentSourceChunkMd5Hash}");
                Console.WriteLine($"Chunk position: {offset}, chunk size: {chunkLength}");

                for (int retry = 0; retry <= maxRetries; retry++) {
                    // Write the chunk to the destination file
                    await destinationFileStream.WriteAsync(buffer.AsMemory(0, chunkLength), cancellationToken);
                    await destinationFileStream.FlushAsync(cancellationToken);

                    // Read back the chunk from the destination file to verify integrity
                    destinationFileStream.Position = offset;
                    await destinationFileStream.ReadExactlyAsync(buffer.AsMemory(0, chunkLength), cancellationToken);
                    string destinationChunkMd5Hash = Convert.ToHexString(MD5.HashData(buffer.AsSpan(0, chunkLength)));

                    if (string.Equals(currentSourceChunkMd5Hash, destinationChunkMd5Hash))
                    {
                        break;
                    }
                    else {
                        Console.WriteLine($"Chunk mismatch detected at index {chunkIndex}, retrying... {retry} of {maxRetries}");
                        if (retry == maxRetries) {
                            throw new IOException($"Failed to transfer chunk at index {chunkIndex} (offset {offset}) after {maxRetries} retries"
                                + $"due to hash mismatch: Expected {currentSourceChunkMd5Hash}, got {destinationChunkMd5Hash}");
                        }
                    }
                }

                offset += chunkLength;
                chunkIndex++;
            }
            byte[] fileHashBytes = fileHasher.GetHashAndReset();
            string fileHash = Convert.ToHexString(fileHashBytes);
            Console.WriteLine($"File transfer completed. File hash: {fileHash}");
        }
    }
}
