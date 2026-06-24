# File Transfer Tool
 
The goal of this console application is to provide functionality to copy large file from a given source path to a destination folder by utilizing transfer in chunks.
File integrity validation is done by comparing MD5 hashed chunks at source to destination and overall file SHA256 hash comparison.
Language: C#
Framework .NET 10
 
## Requirements addressed
 
1. Option to provide source file path at the console.
2. Option to provide destination folder path at the console, the destination filename is derived from the source path.
3. Transfers the file in fixed-size chunks (1 MB by default).
4. Each chunk is MD5-hashed at the source, written to the destination, read back, and re-hashed. Hash mismatches are re-submitted up to a configurable maximum (default=3).
5. File-level SHA256 hash is computed on both the source during the transfer, and the destination after the transfer is complete. Both checksums are printed and compared.
6. Chunk's info: index, offset, size, MD5, and retry count are logged after the transfer completes.
7. Streams data through fixed-size buffers.
8. Concurrency — not implemented in this submission.

## How to run & try out
 
```bash
dotnet run
```

Then enter source and destination paths when prompted, for example:
 
```
Enter source file path: D:\transfer_test\source\test_1gb.bin
Enter destination folder path: D:\transfer_test\destination
```

## Project structure
 
```
FileTransferTool/
├── Program.cs                  Entry point, handles user input
├── TransferData.cs             Transfer specifics (paths, chunk size, retry count)
├── ChunkData.cs                Holds info per chunk (index, offset, size, hash, retries)
├── HashUtil.cs                 Conversion to MD5 and SHA256 hash helpers
└── FileTransferService.cs      Core implementation (transfer methods, retry logic, checksums verification)
```
## Notes
- A chunk size of 1MB is the default to be able to avoid large log volumes.
- FileStream class & its methods like ReadAsync, WriteAsync are used to perform asynchronous operations without blocking the main thread.
- A chunk is read from the source stream and a hash is computed, written at the destination, and then read back from the destination
  and hashed again to be able to simulate an over the network transfer.
- The source file hash is built incrementally as chunks are processed. To be able to correctly verify the destination file hash, the same process is applied.
- A retry mechanism is added so that hash mismatch and I/O failures are handled.
- The stream Position is re-set to the offset before both the write & read ops since in case of a retry it would've already moved to the next chunk.
  
