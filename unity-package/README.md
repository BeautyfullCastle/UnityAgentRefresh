# Agent Refresh Server

A lightweight HTTP server for Unity Editor that allows external AI agents to trigger `AssetDatabase.Refresh()`.

## Features

- **Automatic startup**: Server starts automatically when Unity Editor opens
- **HTTP API**: Simple REST endpoints for refresh and status
- **Cross-platform**: Supports Windows and macOS with intelligent focus management
- **Focus preservation**: Returns focus to your previous window after refresh

## API Endpoints

### POST /refresh

Triggers `AssetDatabase.Refresh()` on the main thread.

```bash
curl -X POST http://localhost:7788/refresh -H "Content-Length: 0"
```

Response:
```json
{"success": true, "message": "Refresh triggered"}
```

### GET /status

Returns server status.

```bash
curl http://localhost:7788/status
```

Response:
```json
{"running": true, "port": 7788}
```

## How It Works

1. Server listens on `http://localhost:7788/`
2. On `/refresh` POST request:
   - Windows: Uses `AttachThreadInput` to bring Unity to foreground
   - macOS: Uses AppleScript to manage application focus
3. Queues `AssetDatabase.Refresh()` via `EditorApplication.delayCall`
4. Returns focus to the original foreground window

## Requirements

- Unity 2021.3 or later
- .NET Standard 2.1 compatible

## License

MIT License - see [LICENSE](../LICENSE) for details.
