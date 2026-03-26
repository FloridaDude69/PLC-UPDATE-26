using PLC_UPDATE_26.Models;
using PLC_UPDATE_26.Services;

namespace PLC_UPDATE_26.Services;

public interface IFileUpdateManager
{
    Task<List<FileTimestampInfo>> SyncHostFilesAsync(string hostName, List<string> filePaths);
    Task<List<FileTimestampInfo>> GetUpdatesRequiredAsync();
    Task<Dictionary<string, FileTimestampInfo>> DistributeLatestFilesAsync(string targetHostName);
}

public class FileUpdateManager : IFileUpdateManager
{
    private readonly IFileRepository _repository;
    private readonly IFileTimestampComparer _comparer;
    private readonly ILogger<FileUpdateManager> _logger;

    public FileUpdateManager(
        IFileRepository repository,
        IFileTimestampComparer comparer,
        ILogger<FileUpdateManager> logger)
    {
        _repository = repository;
        _comparer = comparer;
        _logger = logger;
    }

    /// <summary>
    /// Sync files from a host computer and update the repository with the latest versions
    /// </summary>
    public async Task<List<FileTimestampInfo>> SyncHostFilesAsync(string hostName, List<string> filePaths)
    {
        var syncedFiles = new List<FileTimestampInfo>();

        foreach (var filePath in filePaths)
        {
            try
            {
                var hostFile = await _comparer.AnalyzeFileAsync(filePath, hostName);
                var repositoryFile = await _repository.GetLatestTimestampAsync(hostFile.FileName);

                var (newer, _, isDifferent) = await _comparer.CompareWithRepositoryAsync(hostFile, repositoryFile);

                if (isDifferent)
                {
                    await _repository.SaveTimestampAsync(newer);
                    syncedFiles.Add(newer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing file {FilePath} from host {HostName}", filePath, hostName);
            }
        }

        _logger.LogInformation("Synced {Count} files from host {HostName}", syncedFiles.Count, hostName);
        return syncedFiles;
    }

    /// <summary>
    /// Get list of files that need to be updated on host computers
    /// </summary>
    public async Task<List<FileTimestampInfo>> GetUpdatesRequiredAsync()
    {
        var allTimestamps = await _repository.GetAllTimestampsAsync();

        // Group by filename and get only the latest version of each file
        var latestVersions = allTimestamps
            .GroupBy(f => f.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(f => f.LastModifiedUtc).First())
            .ToList();

        return latestVersions;
    }

    /// <summary>
    /// Get the latest versions of all files that should be distributed to a host
    /// </summary>
    public async Task<Dictionary<string, FileTimestampInfo>> DistributeLatestFilesAsync(string targetHostName)
    {
        var updates = await GetUpdatesRequiredAsync();
        var hostUpdates = new Dictionary<string, FileTimestampInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in updates)
        {
            // Don't include files that already exist on the host with the same timestamp
            var hostVersion = (await _repository.GetTimestampsForHostAsync(targetHostName))
                .FirstOrDefault(f => f.FileName.Equals(file.FileName, StringComparison.OrdinalIgnoreCase));

            if (hostVersion == null || hostVersion.LastModifiedUtc < file.LastModifiedUtc)
            {
                hostUpdates[file.FileName] = file;
            }
        }

        _logger.LogInformation("Host {HostName} requires {Count} file updates", targetHostName, hostUpdates.Count);
        return hostUpdates;
    }
}
