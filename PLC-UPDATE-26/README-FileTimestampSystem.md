# PLC Update Program - File Timestamp Comparison System

## Overview

This system enables the PLC update program to compare file timestamps across multiple host computers and maintain a centralized file repository. It tracks the most current version of each file and distributes updates to host computers.

## Architecture

### Core Components

1. **FileTimestampInfo** (Model)
   - Stores metadata about files including timestamp, size, and SHA256 hash
   - Serializable to/from text format for storage

2. **FileRepository** (Service)
   - Manages persistent storage of file metadata in `Data/Repository/file_timestamps.db`
   - Supports querying by filename, host, and timestamp
   - Generates comprehensive reports

3. **FileTimestampComparer** (Service)
   - Analyzes individual files to extract metadata
   - Computes SHA256 hashes for file integrity verification
   - Compares file versions between hosts and repository

4. **FileUpdateManager** (Service)
   - Orchestrates the sync process from host computers
   - Determines which files need updates
   - Prepares distribution packages for hosts

5. **FileUpdateController** (API)
   - REST API endpoints for external host computers
   - Allows remote file synchronization and update retrieval

## Key Features

### 1. File Timestamp Comparison
- Compares file modification timestamps across all hosts
- Identifies newer versions of files
- Tracks version history with host origin information

### 2. Repository Management
- Centralized storage of file metadata (not the files themselves)
- Persistent storage in `Data/Repository/file_timestamps.db`
- Quick lookup of latest version for any file

### 3. File Distribution
- Identifies files that need updating on specific hosts
- Provides manifest of files to distribute
- Includes file metadata for validation on receiving hosts

### 4. Audit Trail
- Records which host provided the latest version
- Tracks repository update timestamps
- Maintains complete history of file versions

## API Endpoints

### Get Latest Timestamp
```
GET /api/fileupdate/latest/{fileName}
```
Returns the most recent timestamp for a specific file.

**Response:**
```json
{
  "fileName": "config.ini",
  "filePath": "C:\\Program Files\\MyApp\\config.ini",
  "fileSize": 2048,
  "lastModifiedUtc": "2024-01-15T10:30:00Z",
  "sha256Hash": "abc123def456...",
  "hostName": "MACHINE-01",
  "repositoryLastUpdated": "2024-01-15T10:31:00Z"
}
```

### Get All Repository Entries
```
GET /api/fileupdate/all
```
Returns all file timestamps in the repository.

### Get Host's File Entries
```
GET /api/fileupdate/host/{hostName}
```
Returns all files from a specific host computer.

### Sync Files from Host
```
POST /api/fileupdate/sync?hostName={hostName}
Content-Type: application/json

["C:\\path\\to\\file1.exe", "C:\\path\\to\\file2.ini"]
```
Analyzes files from a host computer and updates the repository if newer versions are found.

**Response:** List of updated FileTimestampInfo objects.

### Get Updates Required
```
GET /api/fileupdate/updates-required
```
Returns the latest version of each file in the repository.

### Get Distribution Package
```
GET /api/fileupdate/distribute/{hostName}
```
Returns which files need to be updated on a specific host.

**Response:**
```json
{
  "config.ini": {
    "fileName": "config.ini",
    "filePath": "C:\\Program Files\\MyApp\\config.ini",
    "fileSize": 2048,
    "lastModifiedUtc": "2024-01-15T10:30:00Z",
    "sha256Hash": "abc123def456...",
    "hostName": "MACHINE-01",
    "repositoryLastUpdated": "2024-01-15T10:31:00Z"
  }
}
```

### Generate Report
```
GET /api/fileupdate/report
```
Returns formatted text report of repository contents.

### Analyze Single File
```
POST /api/fileupdate/analyze?filePath={path}&hostName={hostName}
```
Analyzes a single file and returns its metadata without updating the repository.

## Blazor UI

Access the web interface at `/file-updates` for:

1. **Sync Files Tab**
   - Add files from a host computer
   - Batch sync multiple files
   - View sync results with file details

2. **Repository Tab**
   - View all entries in the repository
   - See file history from all hosts
   - Track file sizes and modification times

3. **Distribute Updates Tab**
   - Specify a target host computer
   - View files that need updating
   - See what version will be distributed

4. **Repository Report Tab**
   - Generate comprehensive text report
   - Shows latest version of each file
   - Displays file metadata and repository update times

## Data Storage

### Repository File Format
Location: `Data/Repository/file_timestamps.db`

Text-based format (pipe-delimited):
```
fileName|filePath|fileSize|lastModifiedUtc|sha256Hash|hostName|repositoryLastUpdated
```

Example:
```
config.ini|C:\Program Files\MyApp\config.ini|2048|2024-01-15T10:30:00.0000000Z|abc123def456...|MACHINE-01|2024-01-15T10:31:00.0000000Z
```

## Usage Scenarios

### Scenario 1: Initial Repository Setup
1. Configure source paths on host computers
2. Use Sync Files tab or `/api/fileupdate/sync` endpoint
3. Upload all files from each host
4. Repository now contains baseline versions

### Scenario 2: Detect File Changes
1. Run periodic sync from host computers
2. System automatically identifies newer versions
3. Updates repository with newer timestamps
4. Maintains audit trail of which host has the latest version

### Scenario 3: Distribute Updates
1. Use `/api/fileupdate/distribute/{hostName}`
2. Identify which files need updating on target host
3. Retrieve files from paths specified in metadata
4. Deploy updated files to host
5. Verify with SHA256 hash provided in metadata

### Scenario 4: Audit and Reporting
1. Generate repository report
2. Review which hosts contributed to latest versions
3. Track file update history
4. Identify files that haven't changed across hosts

## Configuration

### Add Services in Program.cs
```csharp
builder.Services.AddScoped<IFileRepository, FileRepository>();
builder.Services.AddScoped<IFileTimestampComparer, FileTimestampComparer>();
builder.Services.AddScoped<IFileUpdateManager, FileUpdateManager>();
```

### Repository Path
Default: `{ContentRootPath}/Data/Repository/`

To customize, modify the `FileRepository` constructor.

## Performance Considerations

- **File Analysis**: SHA256 hashing is performed asynchronously to avoid blocking
- **Repository Storage**: Text-based format for easy debugging and manual inspection
- **Query Performance**: Consider indexing by filename if repository grows very large
- **Batch Operations**: Use `/sync` endpoint with multiple files for efficiency

## Error Handling

- File not found errors return HTTP 404
- Invalid parameters return HTTP 400
- Server errors return HTTP 500 with descriptive messages
- Logging includes all operations for auditing

## Security Notes

- This system tracks timestamps and hashes but does **NOT** store actual file contents
- Actual file storage and distribution should be handled separately
- Consider implementing:
  - Authentication for API endpoints
  - HTTPS for all communications
  - Access controls for specific host computers
  - Audit logging of distribution operations

## Future Enhancements

- Database backend (SQL Server, PostgreSQL) for better performance
- File content storage with deduplication
- Delta/diff operations for partial file updates
- Compression for efficient distribution
- Automated sync scheduling
- Alert system for critical file changes
- Multi-site repository replication
