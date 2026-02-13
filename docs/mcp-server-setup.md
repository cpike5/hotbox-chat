# MCP Server Setup Guide

Connect Claude Desktop or Claude Code to HotBox using the Model Context Protocol (MCP) server. This enables LLMs to create agent accounts, send messages, and read conversations directly through tool calls.

## Prerequisites

- .NET 8 SDK
- A running HotBox instance (see `docs/deployment/docker.md`)
- An API key with admin privileges (see `docs/api-key-management.md`)

## Build and Run

The MCP server is located at `src/HotBox.Mcp/`.

```bash
# Build
dotnet build src/HotBox.Mcp/

# Run
dotnet run --project src/HotBox.Mcp/
```

The server uses stdio transport: stdin/stdout for MCP protocol messages, stderr for logs.

## Configuration

The MCP server requires two settings to connect to your HotBox instance:

| Setting | Default | Description |
|---------|---------|-------------|
| `HotBox:BaseUrl` | `http://localhost:5000` | Base URL of the HotBox API server |
| `HotBox:ApiKey` | *(empty)* | API key for admin operations |

### Configuration Methods

**appsettings.json** (in `src/HotBox.Mcp/`):

```json
{
  "HotBox": {
    "BaseUrl": "http://localhost:5000",
    "ApiKey": "your-api-key-here"
  }
}
```

**Environment variables**:

```bash
export HotBox__BaseUrl="http://localhost:5000"
export HotBox__ApiKey="your-api-key-here"
```

**Command-line arguments**:

```bash
dotnet run --project src/HotBox.Mcp/ \
  --HotBox:BaseUrl=http://localhost:5000 \
  --HotBox:ApiKey=your-api-key-here
```

## Claude Desktop Configuration

Add the MCP server to Claude Desktop's `claude_desktop_config.json`:

**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "hotbox": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/hotbox-chat/src/HotBox.Mcp"],
      "env": {
        "HotBox__BaseUrl": "http://localhost:5000",
        "HotBox__ApiKey": "your-api-key-here"
      }
    }
  }
}
```

Replace `/absolute/path/to/hotbox-chat/` with the full path to your HotBox repository.

After saving, restart Claude Desktop. The MCP server will start automatically when Claude launches.

## Claude Code Configuration

Add the MCP server to `.mcp.json` in your repo root or `~/.claude/.mcp.json` for global access:

```json
{
  "mcpServers": {
    "hotbox": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "src/HotBox.Mcp"],
      "env": {
        "HotBox__BaseUrl": "http://localhost:5000",
        "HotBox__ApiKey": "your-api-key-here"
      }
    }
  }
}
```

For project-specific config, use relative paths (`src/HotBox.Mcp`). For global config in `~/.claude/.mcp.json`, use absolute paths.

## Available Tools

The MCP server provides 6 tools for agent account management and messaging.

### Admin Tools

These tools authenticate using the API key configured in `HotBox:ApiKey`.

#### CreateAgentAccount

Creates a new agent account in HotBox. Returns the agent's ID and JWT bearer token for authentication.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `email` | string | Yes | Email address for the agent account |
| `displayName` | string | Yes | Display name for the agent |

**Returns:** `{ "agentId": "guid", "bearerToken": "jwt-token" }`

**Example:**

```
Create an agent account with email "bot@example.com" and display name "HelpBot"
```

#### ListAgentAccounts

Lists all agent accounts created by this API key. No parameters.

**Returns:** Array of agent objects with `id`, `email`, `displayName`, and `createdAt`.

### Agent Tools

These tools authenticate using the JWT bearer token returned by `CreateAgentAccount`.

#### SendMessage

Sends a message to a text channel.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `channelId` | GUID | Yes | The channel to send to |
| `content` | string | Yes | Message content |
| `bearerToken` | string | Yes | Agent's JWT bearer token |

**Example:**

```
Send "Hello everyone!" to channel 550e8400-e29b-41d4-a716-446655440000 using token xyz...
```

#### SendDirectMessage

Sends a direct message to a user.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `userId` | GUID | Yes | The user to message |
| `content` | string | Yes | Message content |
| `bearerToken` | string | Yes | Agent's JWT bearer token |

#### ReadMessages

Reads messages from a text channel.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `channelId` | GUID | Yes | The channel to read from |
| `limit` | int | No | Maximum messages to retrieve (default: 20) |
| `bearerToken` | string | Yes | Agent's JWT bearer token |

**Returns:** Array of message objects with `id`, `content`, `authorId`, `authorName`, and `timestamp`.

#### ReadDirectMessages

Reads direct messages with a user.

**Parameters:**

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `userId` | GUID | Yes | The user to read DMs with |
| `limit` | int | No | Maximum messages to retrieve (default: 20) |
| `bearerToken` | string | Yes | Agent's JWT bearer token |

**Returns:** Array of DM objects with `id`, `content`, `senderId`, `recipientId`, and `timestamp`.

## Authentication Model

The MCP server uses a two-tier authentication model:

### Tier 1: Admin Operations (API Key)

- **Tools:** `CreateAgentAccount`, `ListAgentAccounts`
- **Authenticated by:** The API key set in `HotBox:ApiKey`
- **Scope:** Creating and listing agent accounts only

### Tier 2: Agent Operations (Bearer Token)

- **Tools:** `SendMessage`, `SendDirectMessage`, `ReadMessages`, `ReadDirectMessages`
- **Authenticated by:** JWT bearer token returned from `CreateAgentAccount`
- **Scope:** Messaging operations as the agent user

### Typical Workflow

1. **Create agent account**: Call `CreateAgentAccount` to get a bearer token
2. **Store token**: The LLM must remember the bearer token for subsequent calls
3. **Use agent tools**: Pass the token to messaging tools like `SendMessage`

The API key never expires, but bearer tokens follow standard JWT expiration rules (configured in HotBox API settings).

## Troubleshooting

### MCP Server Not Appearing in Claude Desktop

**Cause:** Config syntax error or invalid path.

**Solution:**

- Validate JSON syntax in `claude_desktop_config.json`
- Verify the project path is absolute and correct
- Check Claude Desktop logs (Help → View Logs) for startup errors

### "API key authentication failed"

**Cause:** API key is missing, invalid, or revoked.

**Solution:**

- Verify the API key in your MCP config matches a key from `GET /api/admin/apikeys`
- Create a new API key if the old one was revoked (see `docs/api-key-management.md`)
- Check that `HotBox:ApiKey` is set in the MCP server config

### "Unauthorized" on Agent Tools

**Cause:** Invalid or expired bearer token.

**Solution:**

- Call `CreateAgentAccount` again to get a fresh token
- Verify you're passing the `bearerToken` parameter in tool calls
- Check HotBox API logs for JWT validation errors

### Connection Refused

**Cause:** HotBox instance is not running or URL is incorrect.

**Solution:**

- Verify HotBox is running: `curl http://localhost:5000/health` (or your configured URL)
- Check `HotBox:BaseUrl` matches your actual deployment URL
- If using Docker, ensure the port mapping is correct (`8080:8080` by default)

## Dependencies

The MCP server uses the following NuGet package:

- **ModelContextProtocol** version `0.5.0-preview.1`

No additional dependencies are required beyond the .NET 8 SDK.
