# API Key Management Guide

## Overview

HotBox supports API key authentication for programmatic access by AI agents, bots, and automation tools. API keys provide a way to authenticate without using user credentials, and are designed for MCP (Model Context Protocol) agent tools and future integrations.

### Key Features

- **Secure Storage** — Keys are hashed with SHA-256 before storage; plaintext never persists in the database
- **One-Time Display** — Plaintext key is shown only once at creation time
- **One-to-Many Model** — A single API key can create and control multiple agent accounts
- **Permanent Revocation** — Revoked keys cannot authenticate, but existing agent accounts remain active
- **Admin-Only Management** — All API key operations require the Admin role

## Architecture

### Storage & Hashing

- Keys are 32 random bytes encoded as Base64 (44-character string)
- SHA-256 hash of the plaintext key is stored in the `ApiKeys` table
- Last 8 characters (keyPrefix) stored separately for display purposes (shown as `••••••••abcd1234`)
- Plaintext key is returned only in the `POST /api/admin/apikeys` response

### Authentication Flow

1. Client includes API key in request header: `X-Api-Key: <key>`
2. Server hashes the provided key and looks up the hash in the database
3. If found and not revoked, authentication succeeds with these claims:
   - `auth_method = api_key`
   - `api_key_id = <guid>`
   - `api_key_name = <name>`
4. If revoked or not found, request returns `401 Unauthorized`

### Dual Authentication

HotBox supports both JWT Bearer tokens (for human users) and API key authentication (for agents):

- Requests with `X-Api-Key` header use API key authentication
- Requests with `Authorization: Bearer <token>` use JWT authentication
- API key authentication is independent of user sessions

## API Reference

All endpoints require the `Admin` role via JWT Bearer token.

### Create API Key

Generate a new API key.

**Endpoint:**

```http
POST /api/admin/apikeys
Authorization: Bearer <jwt-token>
Content-Type: application/json
```

**Request Body:**

```json
{
  "name": "MCP Agent Key"
}
```

- `name` (required, max 100 chars) — Human-readable label for the key

**Response:**

```json
{
  "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
  "name": "MCP Agent Key",
  "key": "Y2F0cyBhcmUgbmljZSwgZG9ncyBhcmUgZ3JlYXQ=",
  "createdAtUtc": "2026-02-12T20:15:30Z"
}
```

- `key` — **This is the only time the plaintext key is returned.** Store it securely.

**Example (curl):**

```bash
curl -X POST http://localhost:8080/api/admin/apikeys \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"name": "MCP Agent Key"}'
```

### List API Keys

Retrieve all API keys (active and revoked).

**Endpoint:**

```http
GET /api/admin/apikeys
Authorization: Bearer <jwt-token>
```

**Response:**

```json
[
  {
    "id": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "name": "MCP Agent Key",
    "maskedKey": "••••••••YXQ=",
    "createdAtUtc": "2026-02-12T20:15:30Z",
    "revokedAtUtc": null,
    "revokedReason": null,
    "isActive": true
  },
  {
    "id": "a1b2c3d4-58cc-4372-a567-0e02b2c3d479",
    "name": "Revoked Key",
    "maskedKey": "••••••••1234",
    "createdAtUtc": "2026-01-10T10:00:00Z",
    "revokedAtUtc": "2026-02-01T12:00:00Z",
    "revokedReason": "Security concern",
    "isActive": false
  }
]
```

- Results are ordered by creation date (newest first)
- `maskedKey` shows the last 8 characters prefixed with `••••••••`
- `isActive` is `true` if the key has not been revoked

**Example (curl):**

```bash
curl -X GET http://localhost:8080/api/admin/apikeys \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

### Revoke API Key

Permanently revoke an API key. This action cannot be undone.

**Endpoint:**

```http
PUT /api/admin/apikeys/{id}/revoke
Authorization: Bearer <jwt-token>
Content-Type: application/json
```

**Request Body:**

```json
{
  "reason": "Key compromised"
}
```

- `reason` (optional, max 500 chars) — Explanation for revocation

**Response:**

```json
{
  "message": "API key revoked."
}
```

**Example (curl):**

```bash
curl -X PUT http://localhost:8080/api/admin/apikeys/f47ac10b-58cc-4372-a567-0e02b2c3d479/revoke \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"reason": "Key compromised"}'
```

## Using API Keys

### Authentication Header

Include the API key in the `X-Api-Key` header:

```http
GET /api/channels
X-Api-Key: Y2F0cyBhcmUgbmljZSwgZG9ncyBhcmUgZ3JlYXQ=
```

**Example (curl):**

```bash
curl -X GET http://localhost:8080/api/channels \
  -H "X-Api-Key: Y2F0cyBhcmUgbmljZSwgZG9ncyBhcmUgZ3JlYXQ="
```

### Agent Account Creation

API keys are used to create agent accounts (users with `IsAgent = true`). Each agent account is linked to the API key that created it via the `CreatedByApiKeyId` foreign key on the `AppUser` entity.

When listing users via `GET /api/admin/users`, agent accounts are marked with `"isAgent": true`.

### JWT Tokens for Agents

After creating an agent account, the API key can be used to obtain a JWT token for that agent. Agent JWTs work identically to human user JWTs and persist beyond the lifetime of the API key — revoking an API key does not invalidate existing agent sessions.

## Security Considerations

### Key Storage

- **Never commit API keys to version control**
- Store keys in environment variables or a secrets manager (e.g., HashiCorp Vault, AWS Secrets Manager)
- Treat API keys with the same sensitivity as passwords

### Revocation Behavior

- Revoked keys immediately fail authentication
- Agent accounts created by the key remain active and functional
- Existing JWT tokens issued to agent accounts continue working until expiry
- Revocation is permanent — keys cannot be un-revoked

### Rotation Strategy

To rotate an API key:

1. Create a new API key
2. Update clients/agents to use the new key
3. Revoke the old key once all clients have migrated

### Audit Trail

All API key operations are logged with the admin user ID and key details. Check application logs (Serilog → Seq in dev, Elasticsearch in prod) for:

- Key creation: `"Admin {UserId} created API key {ApiKeyId} ({ApiKeyName})"`
- Key revocation: `"Admin {UserId} revoked API key {ApiKeyId} ({ApiKeyName}). Reason: {RevokeReason}"`
- Authentication failures: `"API key authentication failed: key not found"` or `"API key authentication failed: key {ApiKeyId} is revoked"`

## Best Practices

1. **Use descriptive names** — Name keys after their purpose (e.g., "MCP Production Agent", "Testing Harness")
2. **Limit key scope** — Currently all keys have full agent access; scopes/permissions will be added in a future phase
3. **Revoke unused keys** — If a key is no longer needed, revoke it immediately
4. **Monitor usage** — Track which keys authenticate in logs; investigate unexpected activity
5. **Store securely** — Use a password manager or secrets manager, not plaintext files

## Integration with MCP Agent Tools

API keys are the foundation for the MCP (Model Context Protocol) server integration. The MCP server uses an API key to:

- Create agent accounts (`create_agent_account` tool)
- List agent accounts created by the key (`list_agent_accounts` tool)
- Authenticate agent actions (sending messages, reading messages, etc.)

See `docs/requirements/mcp-agent-tools.md` for full MCP integration details.

## Troubleshooting

### 401 Unauthorized with API Key

**Cause:** Key is invalid, revoked, or not found in the database.

**Solution:**

- Verify the key is correct (check for typos or whitespace)
- List all keys via `GET /api/admin/apikeys` and confirm the key is active
- If revoked, create a new key

### API Key Not Working After Creation

**Cause:** The plaintext key was not saved at creation time.

**Solution:**

- Revoke the old key
- Create a new key and immediately save the `key` value from the response

### Agent Account Shows Wrong API Key

**Cause:** Multiple API keys used to create agents, or the `CreatedByApiKeyId` foreign key is null.

**Solution:**

- Agent accounts created before API key infrastructure will have `CreatedByApiKeyId = null`
- Only agents created via API key authentication will have the foreign key set
- Check `GET /api/admin/users` to see which accounts are agents

## Future Enhancements

Planned improvements (not yet implemented):

- **Scopes/Permissions** — Limit what each key can do (e.g., read-only, specific channels)
- **Expiration** — Auto-revoke keys after a set time period
- **Rate Limiting** — Throttle requests per key to prevent abuse
- **Usage Metrics** — Track request counts and last-used timestamp per key

See `docs/requirements/mcp-agent-tools.md` for the full roadmap.
