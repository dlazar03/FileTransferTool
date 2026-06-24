using System;
using System.Collections.Generic;
using System.Text;

namespace FileTransferTool
{
    public record TransferData
    {
        public required string SourceFilePath { get; init; }

        public required string DestinationFilePath { get; init; }

        public int FileChunkSize { get; init; } = 1024 * 1024;

        public int MaxRetries { get; init; } = 3;
    }
}
