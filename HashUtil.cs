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
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public static IncrementalHash CreateSha256Hash() {
            return IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        }

        public static string GetSha256Hash(IncrementalHash hash) {
            byte[] fileHashBytes = hash.GetHashAndReset();
            return Convert.ToHexString(fileHashBytes).ToLowerInvariant();
        }
    }
}
