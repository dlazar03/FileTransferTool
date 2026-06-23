using System;
using System.Collections.Generic;
using System.Text;

namespace FileTransferTool
{
    public class FileTransferService
    {
        public async Task TransferAsync(string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken) {
            const int chunkSize = 1024 * 1024;
            var buffer = new byte[chunkSize];

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

                Console.WriteLine($"Current chunk processing: {chunkIndex}");
                Console.WriteLine($"Chunk start at byte: {chunkPositionStart} end at byte: {chunkPositionEnd}, chunk size: {readCount}");

                await destinationFileStream.WriteAsync(buffer, cancellationToken);
                chunkIndex++;
            }
            Console.WriteLine("Transfer completed.");
        }
    }
}
