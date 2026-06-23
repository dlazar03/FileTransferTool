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
            long readCount = 0;
            int chunkIndex = 0;

            while ((readCount = await sourceFileStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                long chunkPositionStart = sourceFileStream.Position - readCount;
                long chunkPositionEnd = sourceFileStream.Position;

                ReadOnlySpan<byte> currentChunk = new(buffer, 0, (int)readCount);
                byte[] hashBytes = MD5.HashData(currentChunk);
                string currentChunkHash = Convert.ToHexString(hashBytes);
                fileHasher.AppendData(buffer, 0, (int)readCount);

                Console.WriteLine($"Current chunk processing: {chunkIndex}, current chunk hash: {currentChunkHash}");
                Console.WriteLine($"Chunk start at byte: {chunkPositionStart} end at byte: {chunkPositionEnd}, chunk size: {readCount}");

                await destinationFileStream.WriteAsync(buffer.AsMemory(0, (int)readCount), cancellationToken);
                chunkIndex++;
            }
            byte[] fileHashBytes = fileHasher.GetHashAndReset();
            string fileHash = Convert.ToHexString(fileHashBytes);
            Console.WriteLine($"File transfer completed. File hash: {fileHash}");
        }
    }
}
