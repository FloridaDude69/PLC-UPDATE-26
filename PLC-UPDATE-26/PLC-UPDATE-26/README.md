# PLC Update Program - File Timestamp System

Production-ready file timestamp comparison and repository management system for distributed PLC environments.

## Quick Start

```bash
cd C:\Users\634647\source\repos\PLC-UPDATE-26\PLC-UPDATE-26
dotnet run
```

Navigate to: `http://localhost:5034/file-updates`

## Features

- **File Synchronization** - Track file versions across multiple host computers
- **Centralized Repository** - Single source of truth for latest file versions
- **Version Management** - Complete audit trail of file changes
- **REST API** - 8 endpoints for automation and integration
- **Web Interface** - 4-tab Blazor UI for manual operations

## Documentation

- **QUICKSTART.md** - Get running in 5 minutes
- **USAGE-GUIDE.md** - Detailed workflows and examples
- **README-FileTimestampSystem.md** - Technical reference
- **DEPLOYMENT-GUIDE.md** - Automation setup

## API Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/fileupdate/sync` | Sync files from host |
| GET | `/api/fileupdate/all` | Get all repository files |
| GET | `/api/fileupdate/host/{hostName}` | Get host's files |
| GET | `/api/fileupdate/latest/{fileName}` | Get latest version |
| GET | `/api/fileupdate/distribute/{hostName}` | Get updates for host |
| GET | `/api/fileupdate/updates-required` | Get all latest versions |
| GET | `/api/fileupdate/report` | Generate report |
| POST | `/api/fileupdate/analyze` | Analyze single file |

## Corporate Environment

Designed for locked-down corporate environments:
- ✅ No external dependencies or CDNs
- ✅ Self-contained with minimal footprint
- ✅ Text-based data storage
- ✅ SHA256 integrity verification
- ✅ Comprehensive audit logging

## System Requirements

- .NET 10.0 or later
- Windows, Linux, or macOS
- File system access for repository
- Network access for host computers
