using Microsoft.AspNetCore.Mvc;
using PLC_UPDATE_26.Models;
using PLC_UPDATE_26.Services;

namespace PLC_UPDATE_26.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FileUpdateController : ControllerBase
{
    private readonly IFileRepository _repository;
    private readonly IFileTimestampComparer _comparer;
    private readonly IFileUpdateManager _updateManager;
    private readonly ILogger<FileUpdateController> _logger;

    public FileUpdateController(
        IFileRepository repository,
        IFileTimestampComparer comparer,
        IFileUpdateManager updateManager,
        ILogger<FileUpdateController> logger)
    {
        _repository = repository;
        _comparer = comparer;
        _updateManager = updateManager;
        _logger = logger;
    }

    /// <summary>
    /// Get the latest timestamp for a specific file
    /// </summary>
    [HttpGet("latest/{fileName}")]
    public async Task<ActionResult<FileTimestampInfo>> GetLatestTimestamp(string fileName)
    {
        var timestamp = await _repository.GetLatestTimestampAsync(fileName);
        if (timestamp == null)
            return NotFound($"File '{fileName}' not found in repository");

        return Ok(timestamp);
    }

    /// <summary>
    /// Get all timestamps in the repository
    /// </summary>
    [HttpGet("all")]
    public async Task<ActionResult<List<FileTimestampInfo>>> GetAllTimestamps()
    {
        var timestamps = await _repository.GetAllTimestampsAsync();
        return Ok(timestamps);
    }

    /// <summary>
    /// Get all timestamps for a specific host
    /// </summary>
    [HttpGet("host/{hostName}")]
    public async Task<ActionResult<List<FileTimestampInfo>>> GetTimestampsForHost(string hostName)
    {
        var timestamps = await _repository.GetTimestampsForHostAsync(hostName);
        return Ok(timestamps);
    }

    /// <summary>
    /// Sync files from a host computer
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<List<FileTimestampInfo>>> SyncFiles(
        [FromQuery] string hostName,
        [FromBody] List<string> filePaths)
    {
        if (string.IsNullOrEmpty(hostName))
            return BadRequest("hostName is required");

        if (filePaths == null || !filePaths.Any())
            return BadRequest("filePaths cannot be empty");

        try
        {
            var syncedFiles = await _updateManager.SyncHostFilesAsync(hostName, filePaths);
            return Ok(syncedFiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file sync from host {HostName}", hostName);
            return StatusCode(500, $"Error during sync: {ex.Message}");
        }
    }

    /// <summary>
    /// Get files that need to be updated
    /// </summary>
    [HttpGet("updates-required")]
    public async Task<ActionResult<List<FileTimestampInfo>>> GetUpdatesRequired()
    {
        var updates = await _updateManager.GetUpdatesRequiredAsync();
        return Ok(updates);
    }

    /// <summary>
    /// Get the latest files that should be distributed to a specific host
    /// </summary>
    [HttpGet("distribute/{hostName}")]
    public async Task<ActionResult<Dictionary<string, FileTimestampInfo>>> GetDistributeFiles(string hostName)
    {
        if (string.IsNullOrEmpty(hostName))
            return BadRequest("hostName is required");

        try
        {
            var updates = await _updateManager.DistributeLatestFilesAsync(hostName);
            return Ok(updates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting updates for host {HostName}", hostName);
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate a repository report
    /// </summary>
    [HttpGet("report")]
    public async Task<ActionResult<string>> GenerateReport()
    {
        try
        {
            var report = await _repository.GenerateRepositoryReportAsync();
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report");
            return StatusCode(500, $"Error generating report: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyze a single file
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<FileTimestampInfo>> AnalyzeFile(
        [FromQuery] string filePath,
        [FromQuery] string hostName)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(hostName))
            return BadRequest("Both filePath and hostName are required");

        try
        {
            var fileInfo = await _comparer.AnalyzeFileAsync(filePath, hostName);
            return Ok(fileInfo);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing file {FilePath}", filePath);
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }
}
