# PLC Update Program - Usage Guide

## Quick Start

### 1. Running the Central Repository Server

The Blazor application serves as the central file timestamp repository:

```bash
cd PLC-UPDATE-26
dotnet run
```

Access the web interface at `https://localhost:7xxx/file-updates`

### 2. Syncing Files from a Host Computer

#### Option A: Using the Blazor UI

1. Navigate to **Sync Files** tab
2. Enter the host computer name (e.g., `MACHINE-01`)
3. Add file paths to sync:
   - Click "Add File"
   - Enter file path (e.g., `C:\Program Files\MyApp\config.ini`)
   - Repeat for multiple files
4. Click "Sync X File(s)" button
5. View sync results showing which files were uploaded

#### Option B: Using the REST API

```bash
curl -X POST "https://localhost:7xxx/api/fileupdate/sync?hostName=MACHINE-01" \
  -H "Content-Type: application/json" \
  -d '["C:\\path\\to\\file1.exe", "C:\\path\\to\\file2.ini"]'
```

### 3. Checking Repository Contents

#### Via Web UI

1. Navigate to **Repository** tab
2. Click "Refresh" to load latest entries
3. Browse the table showing all files and their metadata

#### Via REST API

```bash
# Get all files in repository
curl "https://localhost:7xxx/api/fileupdate/all"

# Get files from specific host
curl "https://localhost:7xxx/api/fileupdate/host/MACHINE-01"

# Get latest version of a specific file
curl "https://localhost:7xxx/api/fileupdate/latest/config.ini"
```

### 4. Distributing Updates to a Host

#### Via Web UI

1. Navigate to **Distribute Updates** tab
2. Enter target host name
3. Click "Get Updates for Host"
4. View files that need updating

#### Via REST API

```bash
curl "https://localhost:7xxx/api/fileupdate/distribute/MACHINE-02"
```

Returns a dictionary of files with their metadata:
- File name and path
- Latest timestamp
- File size
- SHA256 hash for verification

### 5. Generating Reports

#### Via Web UI

1. Navigate to **Repository Report** tab
2. Click "Generate Report"
3. Review the formatted text report
4. Can copy/download the report text

#### Via REST API

```bash
curl "https://localhost:7xxx/api/fileupdate/report" > report.txt
```

## Common Workflows

### Workflow 1: Initial Repository Seeding

**Goal**: Set up the repository with baseline files from your production environment

**Steps**:

1. On a reference host computer, identify all critical files
2. Use the Sync API endpoint or UI to upload them:
   ```bash
   curl -X POST "https://localhost:7xxx/api/fileupdate/sync?hostName=REFERENCE-HOST" \
     -H "Content-Type: application/json" \
     -d '["C:\\path\\to\\app.exe", "C:\\path\\to\\config.ini", "C:\\path\\to\\data.db"]'
   ```
3. Verify in repository via UI or `GET /api/fileupdate/all`
4. Generate report to confirm all files are captured

### Workflow 2: Periodic File Change Detection

**Goal**: Detect when files change and update the repository

**Setup** (On each host computer):
1. Create a scheduled task to run daily/hourly
2. Call the sync endpoint with your monitored files:
   ```powershell
   $filePath = "https://your-server/api/fileupdate/sync?hostName=$env:COMPUTERNAME"
   $files = @(
     "C:\Program Files\MyApp\app.exe",
     "C:\Program Files\MyApp\config.ini"
   )
   Invoke-RestMethod -Uri $filePath -Method Post -Body ($files | ConvertTo-Json)
   ```

**Result**: 
- If files are unchanged, nothing happens
- If files are newer, they are updated in repository
- Older entries from same file remain for audit trail

### Workflow 3: Push Updates to Multiple Hosts

**Goal**: Identify and distribute updated files to all host computers

**Steps**:

1. Check what updates are available:
   ```bash
   curl "https://localhost:7xxx/api/fileupdate/updates-required"
   ```

2. For each host computer, get its required updates:
   ```bash
   curl "https://localhost:7xxx/api/fileupdate/distribute/MACHINE-01"
   ```

3. For each file in the response:
   - Copy from the file path specified
   - Verify SHA256 hash matches the provided value
   - Deploy to the host

4. After deployment, sync the host again to confirm:
   ```bash
   curl -X POST "https://localhost:7xxx/api/fileupdate/sync?hostName=MACHINE-01" \
     -H "Content-Type: application/json" \
     -d '["C:\\path\\to\\updated\\file"]'
   ```

### Workflow 4: Audit and Compliance Reporting

**Goal**: Generate compliance reports showing file versions across all hosts

**Steps**:

1. Generate repository report:
   ```bash
   curl "https://localhost:7xxx/api/fileupdate/report"
   ```

2. Report shows:
   - Latest timestamp for each file
   - Which host provided the latest version
   - File size and hash
   - When repository was updated

3. Query specific host history:
   ```bash
   curl "https://localhost:7xxx/api/fileupdate/host/MACHINE-01"
   ```

## Advanced Usage

### Using FileUpdateClient in Your Code

```csharp
// Setup
var logger = LoggerFactory.Create(builder => builder.AddConsole())
    .CreateLogger<FileUpdateClient>();
var client = new FileUpdateClient("https://your-server", logger);

// Sync files
var syncedFiles = await client.SyncFilesAsync("MACHINE-01", 
    new List<string> { "C:\\path\\to\\file.exe" });

// Get updates for a host
var updates = await client.GetRequiredUpdatesAsync("MACHINE-02");
foreach (var file in updates.Values)
{
    Console.WriteLine($"Update available: {file.FileName} ({file.LastModifiedUtc:O})");
}

// Get repository report
var report = await client.GetRepositoryReportAsync();
Console.WriteLine(report);

client.Dispose();
```

### Using HostSyncService for Automated Sync

```csharp
// Setup
var repository = new FileRepository(logger, env);
var comparer = new FileTimestampComparer(logger);
var httpClient = new HttpClient { BaseAddress = new Uri("https://your-server") };
var client = new FileUpdateClient("https://your-server", logger);
var syncService = new HostSyncService(client, comparer, logger);

// Perform sync
var syncResult = await syncService.SyncWithRepositoryAsync(
    Environment.MachineName,
    new List<string> 
    { 
        "C:\\Program Files\\MyApp\\app.exe",
        "C:\\Program Files\\MyApp\\config.ini"
    });

Console.WriteLine($"Sync Result: {syncResult}");
Console.WriteLine($"Uploaded: {syncResult.UploadedFiles.Count}");
Console.WriteLine($"Unchanged: {syncResult.UnchangedFiles.Count}");
Console.WriteLine($"Failed: {syncResult.FailedFiles.Count}");
```

## File Repository Storage

### Location
`{AppRoot}/Data/Repository/file_timestamps.db`

### Format
Text-based, pipe-delimited for easy inspection:
```
fileName|filePath|fileSize|lastModifiedUtc|sha256Hash|hostName|repositoryLastUpdated
```

### Example
```
app.exe|C:\Program Files\MyApp\app.exe|1048576|2024-01-15T10:30:00.0000000Z|a1b2c3d4e5f6...|MACHINE-01|2024-01-15T10:31:00.0000000Z
```

### Backing Up the Repository

```powershell
# Backup
Copy-Item -Path "C:\path\to\app\Data\Repository\file_timestamps.db" `
          -Destination "C:\backups\file_timestamps_$(Get-Date -Format 'yyyyMMdd').db"

# Restore
Copy-Item -Path "C:\backups\file_timestamps_20240115.db" `
          -Destination "C:\path\to\app\Data\Repository\file_timestamps.db"
```

## Troubleshooting

### Issue: "File not found in repository"
**Cause**: File hasn't been synced yet
**Solution**: Ensure the file exists on a host and run sync operation first

### Issue: "Hash or size mismatch"
**Cause**: File was modified without being synced
**Solution**: Run sync again from the host to update the repository

### Issue: Sync takes a long time
**Cause**: Large files or network latency
**Solution**: 
- Consider splitting large directories
- Use local network paths when possible
- Implement timeout handling in client code

### Issue: Repository file grows very large
**Cause**: Many file versions accumulated
**Solution**: 
- Archive old repository files periodically
- Implement retention policy (e.g., keep last 30 days)
- Consider database backend for better performance

## Performance Tips

1. **Batch Operations**: Sync multiple files in one call rather than individually
2. **Monitoring**: Use scheduled tasks instead of manual operations
3. **Network**: Use local network paths when possible to avoid internet latency
4. **Filtering**: Use file patterns to exclude unnecessary files
5. **Archival**: Move old repository backups offline to save space

## Security Considerations

1. **HTTPS Only**: Always use HTTPS for API calls
2. **Authentication**: Consider adding API key or Windows authentication
3. **Authorization**: Restrict which hosts can sync which files
4. **Audit Logging**: All sync operations are logged
5. **File Integrity**: SHA256 hashes verify files haven't been tampered with

## Next Steps

- Implement automated sync agents on host computers
- Set up scheduled distribution of updates
- Create monitoring dashboards for file versions
- Integrate with your PLC programming environment
- Develop policies for version management and rollback
