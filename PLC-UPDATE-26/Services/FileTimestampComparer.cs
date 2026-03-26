using System.Security.Cryptography;
using PLC_UPDATE_26.Models;

namespace PLC_UPDATE_26.Services;

public interface IFileTimestampComparer
{
    Task<FileTimestampInfo> AnalyzeFileAsync(string filePath, string hostName);
    Task<List<FileTimestampInfo>> AnalyzeDirectoryAsync(string directoryPath, string hostName, string? pattern = null);
    Task<(FileTimestampInfo newer, FileTimestampInfo older, bool isDifferent)> CompareWithRepositoryAsync(
        FileTimestampInfo hostFile, FileTimestampInfo? repositoryFile);
}

public class FileTimestampComparer : IFileTimestampComparer
{
    private readonly ILogger<FileTimestampComparer> _logger;

    public FileTimestampComparer(ILogger<FileTimestampComparer> logger)
    {
        _logger = logger;
    }

    public async Task<FileTimestampInfo> AnalyzeFileAsync(string filePath, string hostName)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var fileInfo = new FileInfo(filePath);
        var hash = await ComputeFileSha256Async(filePath);

        _logger.LogInformation("Analyzed file {FilePath} from host {HostName}", filePath, hostName);

        return new FileTimestampInfo
        {
            FileName = fileInfo.Name,
            FilePath = filePath,
            FileSize = fileInfo.Length,
            LastModifiedUtc = fileInfo.LastWriteTimeUtc,
            Sha256Hash = hash,
            HostName = hostName,
            RepositoryLastUpdated = DateTime.UtcNow
        };
    }

    public async Task<List<FileTimestampInfo>> AnalyzeDirectoryAsync(
        string directoryPath, string hostName, string? pattern = null)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var directory = new DirectoryInfo(directoryPath);
        var searchPattern = pattern ?? "*.*";
        var files = directory.GetFiles(searchPattern, SearchOption.AllDirectories);

        var results = new List<FileTimestampInfo>();

        foreach (var file in files)
        {
            try
            {
                var info = await AnalyzeFileAsync(file.FullName, hostName);
                results.Add(info);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze file {FilePath}", file.FullName);
            }
        }

        _logger.LogInformation("Analyzed {Count} files from directory {DirectoryPath}", results.Count, directoryPath);
        return results;
    }

    public async Task<(FileTimestampInfo newer, FileTimestampInfo older, bool isDifferent)> CompareWithRepositoryAsync(
        FileTimestampInfo hostFile, FileTimestampInfo? repositoryFile)
    {
        if (hostFile == null)
            throw new ArgumentNullException(nameof(hostFile));

        if (repositoryFile == null)
        {
            _logger.LogInformation("No repository entry for {FileName}. Host file is newer.", hostFile.FileName);
            return (hostFile, null!, true);
        }

        var isDifferent = hostFile.LastModifiedUtc != repositoryFile.LastModifiedUtc ||
                         hostFile.Sha256Hash != repositoryFile.Sha256Hash;

        if (!isDifferent)
        {
            _logger.LogDebug("File {FileName} unchanged since repository", hostFile.FileName);
            return (hostFile, repositoryFile, false);
        }

        var newer = hostFile.IsNewer(repositoryFile) ? hostFile : repositoryFile;
        var older = hostFile.IsNewer(repositoryFile) ? repositoryFile : hostFile;

        _logger.LogInformation("File {FileName} differs. Newer version from {Host} at {Timestamp}",
            hostFile.FileName, newer.HostName, newer.LastModifiedUtc);

        return (newer, older, true);
    }

    private async Task<string> ComputeFileSha256Async(string filePath)
    {
        using (var sha256 = SHA256.Create())
        using (var stream = File.OpenRead(filePath))
        {
            var hash = await Task.Run(() => sha256.ComputeHash(stream));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
