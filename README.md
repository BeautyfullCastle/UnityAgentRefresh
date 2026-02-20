# Unity Agent Refresh

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

HTTP bridge for external AI agents to trigger Unity's `AssetDatabase.Refresh()`. Perfect for AI coding assistants like **Gemini CLI**, **Claude Code**, **opencode**, and other automation tools.

## Why?

When AI agents create or modify Unity asset files (`.cs`, `.uxml`, `.uss`, `.asset`), Unity needs to refresh its AssetDatabase to recognize the changes and generate `.meta` files. This package provides a simple HTTP endpoint to trigger that refresh programmatically.

## Installation

### Unity Package (via UPM)

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.beautyfullcastle.agent-refresh": "https://github.com/BeautyfullCastle/UnityAgentRefresh.git?path=unity-package"
  }
}
```

Or via Unity Package Manager:
1. Open **Window > Package Manager**
2. Click **+** > **Add package from git URL...**
3. Enter: `https://github.com/BeautyfullCastle/UnityAgentRefresh.git?path=unity-package`

### AI Agent Skill

#### For opencode

Copy the skill to your opencode skills directory:

```bash
# Windows
copy skill\unity-refresh\SKILL.md %USERPROFILE%\.config\opencode\skills\unity-refresh\

# macOS/Linux
cp skill/unity-refresh/SKILL.md ~/.config/opencode/skills/unity-refresh/
```

#### For Gemini CLI

Add to your `.gemini/settings.json`:

```json
{
  "mcpServers": {
    "unity-refresh": {
      "command": "curl",
      "args": ["-X", "POST", "http://localhost:7788/refresh", "-H", "Content-Length: 0"]
    }
  }
}
```

Or use the skill file directly in your custom instructions.

#### For Claude Code

Add the skill content to your Claude Code project instructions or use the MCP configuration.

## Usage

### Verify Server is Running

Open Unity Editor and check the Console for:
```
[AgentRefreshServer] Started on http://localhost:7788/
```

### Trigger Refresh

```bash
curl -X POST http://localhost:7788/refresh -H "Content-Length: 0"
```

Response:
```json
{"success": true, "message": "Refresh triggered"}
```

### Check Status

```bash
curl http://localhost:7788/status
```

Response:
```json
{"running": true, "port": 7788}
```

## Features

- **Auto-start**: Server starts automatically when Unity Editor opens
- **Cross-platform**: Windows and macOS support with intelligent focus management
- **Focus preservation**: Returns focus to your previous window after refresh
- **Thread-safe**: Properly queues refresh on Unity's main thread

## Workflow Example

```bash
# 1. AI agent creates a new script
# ... Write tool creates Assets/Scripts/NewFeature.cs ...

# 2. Trigger Unity refresh
curl -X POST http://localhost:7788/refresh -H "Content-Length: 0"

# 3. Wait for Unity to process
sleep 1

# 4. Continue with next task (Unity has now generated .meta files)
```

## Requirements

- Unity 2021.3 or later
- .NET Standard 2.1

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

Issues and pull requests are welcome at [GitHub](https://github.com/BeautyfullCastle/UnityAgentRefresh).
