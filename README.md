# file-share-backend

.NET 10 backend for the file-share platform. Exposes a REST API and a SignalR hub for real-time updates.

## Tech Stack

- .NET 10 / ASP.NET Core
- [WolverineFx](https://wolverine.netlify.app/) 5.16.2 — HTTP endpoints and messaging
- Entity Framework Core 10 + SQLite
- Ardalis.Specification 9.3.1 — repository pattern
- SignalR — real-time push to admin dashboard

## Features

| Domain | Endpoints |
|--------|-----------|
| Files | `GET /api/v1/files` — list files with directory tree |
| Directories | `GET /api/v1/directories` — directory tree structure |
| Shares | `POST /api/v1/shares` — create share link; `GET /api/v1/shares` — list shares |
| Download | `POST /api/v1/download/:token` — resolve and stream file |
| System | `GET /api/v1/system/stats` — CPU, memory, disk metrics |

**Background services:**
- `FileSystemWatcherService` — monitors `MonitoredFolder` for file changes
- `PollingService` — periodic file sync
- `CleanupBackgroundService` — removes expired share links automatically

## Development

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Run

```bash
cd src/backend
dotnet run
```

API available at `http://localhost:5067`. OpenAPI UI at `/openapi/v1.json` (development only).

### Tests

```bash
cd tests/FileShare.Tests
dotnet test
```

## Docker

```bash
docker build -t file-share-backend ./src/backend
docker run -p 8080:8080 \
  -v $(pwd)/shared-files:/app/shared-files \
  -v $(pwd)/data:/app/data \
  file-share-backend
```

## Configuration

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=data/fileshare.db"
  },
  "MonitoredFolder": "/app/shared-files",
  "AllowedOrigins": ["http://your-domain.com"]
}
```

## CI/CD

On push to `main`, GitHub Actions builds and pushes the Docker image to `ghcr.io/raphaelm22/file-share-backend:latest`.
