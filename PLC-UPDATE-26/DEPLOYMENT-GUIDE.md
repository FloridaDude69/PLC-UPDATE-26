# PLC Update Program - Configuration & Deployment Examples

## Setting Up Automated File Synchronization

This document provides configuration examples for deploying automated file synchronization on host computers.

## Option 1: PowerShell Scheduled Task (Windows)

### Create the sync script: `sync-files.ps1`

```powershell
# Configuration
$ServerUrl = "https://your-plc-update-server"
$HostName = $env:COMPUTERNAME
$FilesToSync = @(
    "C:\Program Files\MyApp\application.exe",
    "C:\Program Files\MyApp\config.ini",
    "C:\Program Files\MyApp\database.db",
    "C:\ProgramData\MyApp\settings.xml"
)
$LogPath = "C:\Logs\plc-sync"
$LogFile = Join-Path $LogPath "sync_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"

# Create log directory if it doesn't exist
if (-not (Test-Path $LogPath)) {
    New-Item -ItemType Directory -Path $LogPath -Force | Out-Null
}

# Logging function
function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    "$timestamp - $Message" | Tee-Object -FilePath $LogFile -Append
}

Write-Log "Starting file sync for host: $HostName"

try {
    # Build the API URL
    $ApiUrl = "$ServerUrl/api/fileupdate/sync?hostName=$([System.Uri]::EscapeDataString($HostName))"

    # Verify files exist
    $validFiles = @()
    foreach ($file in $FilesToSync) {
        if (Test-Path $file) {
            $validFiles += $file
            Write-Log "Found file: $file"
        } else {
            Write-Log "WARNING: File not found: $file"
        }
    }

    if ($validFiles.Count -eq 0) {
        Write-Log "ERROR: No valid files found to sync"
        exit 1
    }

    # Call the API
    Write-Log "Syncing $($validFiles.Count) files to $ServerUrl"
    $response = Invoke-RestMethod -Uri $ApiUrl -Method Post `
        -Body ($validFiles | ConvertTo-Json) `
        -ContentType "application/json" `
        -TimeoutSec 300 `
        -SkipCertificateCheck  # Remove if using valid certificate

    # Log results
    Write-Log "Sync completed successfully"
    Write-Log "Response: $($response | ConvertTo-Json -Depth 2)"

    # Count updated files
    if ($response -is [array]) {
        Write-Log "Updated files: $($response.Count)"
    } else {
        Write-Log "Updated files: 1"
    }
}
catch {
    Write-Log "ERROR: $($_.Exception.Message)"
    exit 1
}

Write-Log "Sync operation finished"
```

### Schedule the task

```powershell
# Run as administrator

# Define the task parameters
$TaskName = "PLC-File-Sync"
$ScriptPath = "C:\Scripts\sync-files.ps1"
$RunAsUser = "DOMAIN\ServiceAccount"  # Or your user account
$RunAsPassword = Read-Host "Enter password for $RunAsUser" -AsSecureString

# Create a trigger for daily at 2 AM
$Trigger = New-ScheduledTaskTrigger -Daily -At 2am

# Create an action to run the script
$Action = New-ScheduledTaskAction -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$ScriptPath`""

# Register the task
Register-ScheduledTask -TaskName $TaskName `
    -Trigger $Trigger `
    -Action $Action `
    -User $RunAsUser `
    -Password $RunAsPassword `
    -RunLevel Highest `
    -Description "Synchronize files with PLC Update server"

Write-Host "Scheduled task '$TaskName' created successfully"

# To test immediately:
# Start-ScheduledTask -TaskName $TaskName
```

## Option 2: Windows Service Implementation

### Create a Windows Service project

```csharp
// Program.cs
using PLC_UPDATE_26.Services;
using PLC_UPDATE_26.Utilities;

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()  // Run as Windows Service
    .ConfigureServices(services => {
        services.AddSingleton<IFileTimestampComparer, FileTimestampComparer>();
        services.AddScoped<FileUpdateClient>(sp => {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<FileUpdateClient>>();
            var serverUrl = config["FileSync:ServerUrl"] ?? "https://localhost:7000";
            return new FileUpdateClient(serverUrl, logger);
        });
        services.AddScoped<IHostSyncService, HostSyncService>();
        services.AddHostedService<FileSyncWorker>();
    })
    .ConfigureLogging(logging => {
        logging.AddEventLog();
        logging.AddFile("Logs/plc-sync-service-{Date}.txt");
    })
    .Build();

await host.RunAsync();
```

### Create the hosted service

```csharp
// FileSyncWorker.cs
public class FileSyncWorker : BackgroundService
{
    private readonly IHostSyncService _syncService;
    private readonly IConfiguration _config;
    private readonly ILogger<FileSyncWorker> _logger;

    public FileSyncWorker(
        IHostSyncService syncService,
        IConfiguration config,
        ILogger<FileSyncWorker> logger)
    {
        _syncService = syncService;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("File Sync Worker starting");

        var interval = TimeSpan.FromHours(
            _config.GetValue("FileSync:IntervalHours", 24));
        var filesToSync = _config.GetSection("FileSync:FilePaths")
            .Get<List<string>>() ?? new();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Running file sync check");

                var result = await _syncService.SyncWithRepositoryAsync(
                    Environment.MachineName,
                    filesToSync);

                _logger.LogInformation(
                    "Sync completed: {UploadedCount} uploaded, {UnchangedCount} unchanged, {FailedCount} failed",
                    result.UploadedFiles.Count,
                    result.UnchangedFiles.Count,
                    result.FailedFiles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during file sync");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("File Sync Worker stopping");
    }
}
```

### appsettings.json configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "FileSync": {
    "ServerUrl": "https://your-plc-update-server",
    "IntervalHours": 24,
    "FilePaths": [
      "C:\\Program Files\\MyApp\\application.exe",
      "C:\\Program Files\\MyApp\\config.ini",
      "C:\\ProgramData\\MyApp\\database.db"
    ]
  }
}
```

### Install as Windows Service

```powershell
# As Administrator
cd C:\path\to\Windows.Service

# Install
sc create "PLC-File-Sync" binPath= "C:\path\to\service\FileSyncService.exe"

# Configure to auto-start
sc config "PLC-File-Sync" start= auto

# Start the service
net start "PLC-File-Sync"

# Check status
Get-Service "PLC-File-Sync"

# Uninstall when needed
net stop "PLC-File-Sync"
sc delete "PLC-File-Sync"
```

## Option 3: Console Application with Timer

```csharp
// Simple console app that runs periodically
using PLC_UPDATE_26.Services;
using PLC_UPDATE_26.Utilities;

class Program
{
    static async Task Main()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();

        var loggerFactory = LoggerFactory.Create(builder => {
            builder.AddConsole();
            builder.AddFile("Logs/sync-{Date}.txt");
        });

        var logger = loggerFactory.CreateLogger<Program>();
        var serverUrl = config["FileSync:ServerUrl"];
        var filesToSync = config.GetSection("FileSync:FilePaths").Get<List<string>>();
        var syncIntervalSeconds = config.GetValue("FileSync:IntervalSeconds", 86400);

        var client = new FileUpdateClient(serverUrl, 
            loggerFactory.CreateLogger<FileUpdateClient>());
        var comparer = new FileTimestampComparer(
            loggerFactory.CreateLogger<FileTimestampComparer>());
        var syncService = new HostSyncService(client, comparer,
            loggerFactory.CreateLogger<HostSyncService>());

        var timer = new Timer(async _ => {
            try
            {
                logger.LogInformation("Running scheduled file sync");
                var result = await syncService.SyncWithRepositoryAsync(
                    Environment.MachineName,
                    filesToSync);
                logger.LogInformation($"Sync result: {result}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync failed");
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(syncIntervalSeconds));

        logger.LogInformation("File sync agent started. Press Ctrl+C to stop.");
        await Task.Delay(Timeout.Infinite);
    }
}
```

## Monitoring and Alerts

### Check sync success from the server

```powershell
# Get all files synced from a specific host
Invoke-RestMethod -Uri "https://your-server/api/fileupdate/host/MACHINE-01" `
    -SkipCertificateCheck | ForEach-Object { $_.LastModifiedUtc }
```

### Send email on sync failure

```powershell
# Add to sync-files.ps1
$EmailParams = @{
    SmtpServer = "your-smtp-server"
    From = "noreply@your-company.com"
    To = "admin@your-company.com"
    Subject = "PLC File Sync Failed on $HostName"
    Body = "Error: $(Get-Content $LogFile | Select -Last 10)"
}

if ($LASTEXITCODE -ne 0) {
    Send-MailMessage @EmailParams
}
```

## Disaster Recovery

### Backup strategy

```powershell
# Daily backup of repository to network location
$RepositoryPath = "C:\your-app\Data\Repository\file_timestamps.db"
$BackupPath = "\\backup-server\plc-updates"
$BackupFile = Join-Path $BackupPath "file_timestamps_$(Get-Date -Format 'yyyyMMdd').db"

Copy-Item -Path $RepositoryPath -Destination $BackupFile -Force

# Keep only last 30 days
Get-ChildItem -Path $BackupPath -Filter "file_timestamps_*.db" | 
    Where-Object { $_.CreationTime -lt (Get-Date).AddDays(-30) } |
    Remove-Item -Force
```

### Restore from backup

```powershell
# If repository is corrupted, restore from backup
$BackupFile = "\\backup-server\plc-updates\file_timestamps_20240115.db"
$RepositoryPath = "C:\your-app\Data\Repository\file_timestamps.db"

Copy-Item -Path $BackupFile -Destination $RepositoryPath -Force
# Restart the application
```

## Monitoring Dashboard Setup

Add to your central server's startup code:

```csharp
app.MapGet("/api/status/host-sync", async (IFileRepository repo, HttpContext context) =>
{
    var allFiles = await repo.GetAllTimestampsAsync();
    var hostGroups = allFiles.GroupBy(f => f.HostName);

    var status = hostGroups.Select(g => new {
        Host = g.Key,
        FileCount = g.Select(f => f.FileName).Distinct().Count(),
        LastSync = g.Max(f => f.RepositoryLastUpdated),
        FilesUpdated = g.Max(f => f.LastModifiedUtc)
    });

    return Results.Ok(status);
});
```

Access status at: `https://your-server/api/status/host-sync`

## Troubleshooting Deployment Issues

### Issue: Service won't start
```powershell
# Check Windows event viewer
Get-EventLog -LogName Application -Source "PLC-File-Sync" -Newest 10

# Check service status
Get-Service "PLC-File-Sync" | Select-Object Status, StartType
```

### Issue: Files not syncing
```powershell
# Verify script can access files
Test-Path "C:\Program Files\MyApp\application.exe"

# Test API connectivity
Invoke-RestMethod -Uri "https://your-server/api/fileupdate/all" -SkipCertificateCheck
```

### Issue: Permission denied errors
```powershell
# Run sync with elevated privileges
# Or configure service to run as a user with file access permissions
```
