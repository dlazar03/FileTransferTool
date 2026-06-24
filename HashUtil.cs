using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FileTransferTool
{
    public static class HashUtil
    {
        public static string GenerateMd5Hash(ReadOnlySpan<byte> chunk) {
            byte[] hashBytes = MD5.HashData(chunk);
            string currentSourceChunkMd5Hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            return currentSourceChunkMd5Hash;
        }

        public static IncrementalHash InitializeSha256Hash() {
            var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            return fileHasher;
        }

        public static string GenerateSha256Hash(IncrementalHash hash) {
            byte[] fileHashBytes = hash.GetHashAndReset();
            string fileHash = Convert.ToHexString(fileHashBytes).ToLowerInvariant();
            return fileHash;
        }
    }
}
