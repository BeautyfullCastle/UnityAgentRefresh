---
name: unity-refresh
description: Refresh Unity Editor's AssetDatabase externally. Use after creating/modifying .cs, .uxml, .uss, .asset files in Unity projects.
---

# Unity Editor Refresh

A skill for refreshing Unity Editor's AssetDatabase from external tools.

## When to Use

- After creating/modifying/deleting `.uxml`, `.uss`, `.cs`, `.asset` files in Unity projects
- When Unity Editor needs to generate `.meta` files
- When AssetDatabase update is required
- When agents need to continue work after modifying Unity asset files

## Prerequisites

Unity Editor must be open and `AgentRefreshServer` must be running.
(Check for `[AgentRefreshServer] Started on http://localhost:7788/` message in Unity Console)

## Usage

### Trigger Refresh

```bash
curl -X POST http://localhost:7788/refresh -H "Content-Length: 0"
```

**Response (success):**
```json
{"success": true, "message": "Refresh triggered"}
```

### Check Server Status

```bash
curl http://localhost:7788/status
```

**Response:**
```json
{"running": true, "port": 7788}
```

## Workflow Example

Always call refresh after creating/modifying Unity asset files:

```bash
# 1. Create file (using Write tool)
# ... create .uxml or .uss file ...

# 2. Call Unity refresh
curl -X POST http://localhost:7788/refresh -H "Content-Length: 0"

# 3. Wait briefly (for Unity to generate .meta files)
sleep 1

# 4. Continue with next task
```

## Error Handling

| Response | Meaning | Solution |
|----------|---------|----------|
| `Connection refused` | Unity Editor is closed or server not running | Open Unity Editor |
| `Length Required` | Missing Content-Length header | Add `-H "Content-Length: 0"` |
| `{"success": true, ...}` | Success | Normal |

## Notes

- Refresh executes asynchronously. Even after receiving an HTTP response, Unity's internal processing may take some time to complete.
- When creating many files at once, consider adding a `sleep 2` wait time.
