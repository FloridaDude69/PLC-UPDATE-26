using System.Security.Cryptography;
using System.Text.Json;
using PLC_UPDATE_26.Models;

namespace PLC_UPDATE_26.Services;

public interface IFileRepository
{
    Task<FileTimestampInfo?> GetLatestTimestampAsync(string fileName);
    Task<List<FileTimestampInfo>> GetAllTimestampsAsync();
    Task<List<FileTimestampInfo>> GetTimestampsForHostAsync(string hostName);
    Task SaveTimestampAsync(FileTimestampInfo fileInfo);
    Task UpdateRepositoryAsync(IEnumerable<FileTimestampInfo> fileInfos);
    Task<string> GenerateRepositoryReportAsync();
}

public class FileRepository : IFileRepository
{
    private readonly string _repositoryPath;
    private readonly ILogger<FileRepository> _logger;
    private const string RepositoryFileName = "file_timestamps.db";

    public FileRepository(ILogger<FileRepository> logger, IWebHostEnvironment env)
    {
        _logger = logger;
        _repositoryPath = Path.Combine(env.ContentRootPath, "Data", "Repository");
        Directory.CreateDirectory(_repositoryPath);
    }

    public async Task<FileTimestampInfo?> GetLatestTimestampAsync(string fileName)
    {
        var entries = await LoadAllEntriesAsync();
        return entries
            .Where(e => e.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.LastModifiedUtc)
            .FirstOrDefault();
    }

    public async Task<List<FileTimestampInfo>> GetAllTimestampsAsync()
    {
        return await LoadAllEntriesAsync();
    }

    public async Task<List<FileTimestampInfo>> GetTimestampsForHostAsync(string hostName)
    {
        var entries = await LoadAllEntriesAsync();
        return entries
            .Where(e => e.HostName.Equals(hostName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.LastModifiedUtc)
            .ToList();
    }

    public async Task SaveTimestampAsync(FileTimestampInfo fileInfo)
    {
        if (fileInfo == null)
            throw new ArgumentNullException(nameof(fileInfo));

        var entries = await LoadAllEntriesAsync();

        // Remove older entries for the same file from the same host
        entries.RemoveAll(e =>
            e.FileName.Equals(fileInfo.FileName, StringComparison.OrdinalIgnoreCase) &&
            e.HostName.Equals(fileInfo.HostName, StringComparison.OrdinalIgnoreCase) &&
            e.LastModifiedUtc <= fileInfo.LastModifiedUtc);

        entries.Add(fileInfo);

        await SaveEntriesToDiskAsync(entries);
        _logger.LogInformation("Saved timestamp for {FileName} from {HostName}", fileInfo.FileName, fileInfo.HostName);
    }

    public async Task UpdateRepositoryAsync(IEnumerable<FileTimestampInfo> fileInfos)
    {
        var fileList = fileInfos.ToList();
        if (!fileList.Any())
            return;

        foreach (var fileInfo in fileList)
        {
            fileInfo.RepositoryLastUpdated = DateTime.UtcNow;
            await SaveTimestampAsync(fileInfo);
        }

        _logger.LogInformation("Updated repository with {Count} file timestamps", fileList.Count);
    }

    public async Task<string> GenerateRepositoryReportAsync()
    {
        var entries = await LoadAllEntriesAsync();

        var report = new System.Text.StringBuilder();
        report.AppendLine("=== PLC UPDATE REPOSITORY REPORT ===");
        report.AppendLine($"Generated: {DateTime.UtcNow:O}");
        report.AppendLine($"Total Entries: {entries.Count}");
        report.AppendLine();

        var groupedByFile = entries
            .GroupBy(e => e.FileName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key);

        foreach (var fileGroup in groupedByFile)
        {
            report.AppendLine($"File: {fileGroup.Key}");
            var latest = fileGroup.OrderByDescending(e => e.LastModifiedUtc).First();
            report.AppendLine($"  Latest Timestamp: {latest.LastModifiedUtc:O}");
            report.AppendLine($"  Latest From Host: {latest.HostName}");
            report.AppendLine($"  File Size: {latest.FileSize} bytes");
            report.AppendLine($"  Hash: {latest.Sha256Hash}");
            report.AppendLine($"  Repository Updated: {latest.RepositoryLastUpdated:O}");
            report.AppendLine();
        }

        return report.ToString();
    }

    private async Task<List<FileTimestampInfo>> LoadAllEntriesAsync()
    {
        var filePath = Path.Combine(_repositoryPath, RepositoryFileName);

        if (!File.Exists(filePath))
            return new List<FileTimestampInfo>();

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var entries = new List<FileTimestampInfo>();

            foreach (var line in content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                var entry = FileTimestampInfo.FromString(line);
                if (entry != null)
                    entries.Add(entry);
            }

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading repository entries");
            return new List<FileTimestampInfo>();
        }
    }

    private async Task SaveEntriesToDiskAsync(List<FileTimestampInfo> entries)
    {
        var filePath = Path.Combine(_repositoryPath, RepositoryFileName);

        try
        {
            var content = string.Join(Environment.NewLine, entries.Select(e => e.ToString()));
            await File.WriteAllTextAsync(filePath, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving repository entries");
            throw;
        }
    }
}
