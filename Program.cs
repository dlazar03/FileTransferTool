
using FileTransferTool;

try
{
    Console.Write("Enter source file path: ");
    var sourceFilePath = (Console.ReadLine() ?? string.Empty).Trim();

    if (string.IsNullOrWhiteSpace(sourceFilePath)) {
        throw new ArgumentException("Source file path is required.");
    }

    if (!File.Exists(sourceFilePath)) {
        throw new FileNotFoundException($"Source file under {sourceFilePath} could not be found.");
    }

    Console.Write("Enter destination folder path: ");
    var destinationFolderPath = (Console.ReadLine() ?? string.Empty).Trim();
    
    if (string.IsNullOrWhiteSpace(destinationFolderPath)) {
        throw new ArgumentException("Destination file path is required.");
    }

    Directory.CreateDirectory(destinationFolderPath);
    var mergedDestinationPath = Path.Combine(destinationFolderPath, Path.GetFileName(sourceFilePath));

    var fullSourcePath = Path.GetFullPath(sourceFilePath);
    var fullDestinationPath = Path.GetFullPath(mergedDestinationPath);

    if (string.Equals(fullSourcePath, fullDestinationPath, StringComparison.OrdinalIgnoreCase)) {
        throw new InvalidOperationException("Source and destination file paths are the same file, provide different path.");
    }

    var fileService = new FileTransferService();
    var cancellationToken = new CancellationTokenSource();
    await fileService.TransferAsync(fullSourcePath, fullDestinationPath, cancellationToken.Token);

    return 0;
}
catch (Exception ex) {
    Console.Error.WriteLine($"Error occured: {ex.Message}");
    return 1;
}