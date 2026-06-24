using System;
using System.Collections.Generic;
using System.Text;

namespace FileTransferTool
{
    public record ChunkData
    {
        public int Index { get; init; }
        public long Offset { get; init; }
        public int Size { get; init; }
        public string Md5Hash { get; init; } = string.Empty;
        public int RetryCount { get; set; } = 0;
    }
}
