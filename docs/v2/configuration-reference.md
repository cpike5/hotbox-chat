# HotBox v2 — Configuration Reference

**Version**: 2.0
**Date**: 2026-03-22
**Status**: Draft

---

## Overview

HotBox v2 uses the standard ASP.NET Core configuration system. Settings are defined in `appsettings.json` and can be overridden by environment-specific files (`appsettings.Production.json`) or environment variables.

**Precedence** (highest wins):
1. Environment variables
2. `appsettings.{Environment}.json`
3. `appsettings.json`
4. User secrets (development only)

**Environment variable convention**: Double-underscore (`__`) replaces `:` as section separator.
Example: `Jwt:Secret` → `Jwt__Secret`

---

## Connection Strings

| Key | Description | Default | Required |
|-----|------------|---------|----------|
| `ConnectionStrings:Postgres` | PostgreSQL connection string | — | Yes |
| `ConnectionStrings:Redis` | Redis connection string | `localhost:6379` | Yes |

```jsonc
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=hotbox;Username=hotbox;Password=changeme",
    "Redis": "localhost:6379"
  }
}
```

**PostgreSQL connection string format:**
```
Host=<host>;Port=<port>;Database=<db>;Username=<user>;Password=<pass>;SSL Mode=<mode>
```

**Redis connection string format:**
```
<host>:<port>,password=<pass>,ssl=<true|false>,abortConnect=false
```

---

## Server

| Key | Type | Description | Default |
|-----|------|------------|---------|
| `Server:ServerName` | string | Display name of the HotBox instance | `"HotBox"` |
| `Server:RegistrationMode` | enum | `Open`, `InviteOnly`, or `Closed` | `"Open"` |

```jsonc
{
  "Server": {
    "ServerName": "My Server",
    "RegistrationMode": "Open"
  }
}
```

### Registration Modes

| Mode | Behavior |
|------|----------|
| `Open` | Anyone can create an account |
| `InviteOnly` | Registration requires a valid invite code |
| `Closed` | Only admins can create accounts |

---

## JWT

| Key | Type | Description | Default |
|-----|------|------------|---------|
| `Jwt:Secret` | string | HMAC signing key (min 32 characters) | — (required) |
| `Jwt:Issuer` | string | Token issuer claim | `"HotBox"` |
| `Jwt:Audience` | string | Token audience claim | `"HotBox"` |
| `Jwt:AccessTokenExpiration` | int | Access token lifetime in minutes | `15` |
| `Jwt:RefreshTokenExpiration` | int | Refresh token lifetime in minutes | `10080` (7 days) |

```jsonc
{
  "Jwt": {
    "Secret": "your-secret-key-at-least-32-characters-long",
    "Issuer": "HotBox",
    "Audience": "HotBox",
    "AccessTokenExpiration": 15,
    "RefreshTokenExpiration": 10080
  }
}
```

**Security notes:**
- Generate a strong secret: `openssl rand -base64 48`
- Never commit the secret to source control
- Use environment variables or user secrets for the JWT secret

---

## OAuth Providers

Each provider has the same structure. All are optional and disabled by default.

| Key | Type | Description | Default |
|-----|------|------------|---------|
| `OAuth:{Provider}:ClientId` | string | OAuth client ID | `""` |
| `OAuth:{Provider}:ClientSecret` | string | OAuth client secret | `""` |
| `OAuth:{Provider}:Enabled` | bool | Enable this provider | `false` |

Supported providers: `Google`, `Microsoft`, `Discord`

```jsonc
{
  "OAuth": {
    "Google": {
      "ClientId": "your-google-client-id",
      "ClientSecret": "your-google-client-secret",
      "Enabled": true
    },
    "Microsoft": {
      "ClientId": "",
      "ClientSecret": "",
      "Enabled": false
    },
    "Discord": {
      "ClientId": "",
      "ClientSecret": "",
      "Enabled": false
    }
  }
}
```

---

## ICE Servers

WebRTC STUN/TURN servers for NAT traversal. At minimum, one STUN server is needed.

```jsonc
{
  "IceServers": [
    {
      "Urls": ["stun:stun.l.google.com:19302"]
    },
    {
      "Urls": ["turn:turn.example.com:3478"],
      "Username": "hotbox",
      "Credential": "turn-secret"
    }
  ]
}
```

| Key | Type | Description |
|-----|------|------------|
| `IceServers[n]:Urls` | string[] | STUN or TURN URLs |
| `IceServers[n]:Username` | string | TURN username (optional for STUN) |
| `IceServers[n]:Credential` | string | TURN credential (optional for STUN) |

---

## Observability

| Key | Type | Description | Default |
|-----|------|------------|---------|
| `Observability:SeqUrl` | string | Seq server URL for structured logs | `""` |
| `Observability:OtlpEndpoint` | string | OpenTelemetry OTLP endpoint | `""` |
| `Observability:ElasticsearchUrl` | string | Elasticsearch URL for log sink | `""` |
| `Observability:LogLevel` | string | Minimum log level | `"Information"` |

```jsonc
{
  "Observability": {
    "SeqUrl": "http://localhost:5341",
    "OtlpEndpoint": "http://localhost:4317",
    "ElasticsearchUrl": "",
    "LogLevel": "Information"
  }
}
```

### Log Levels

`Verbose` < `Debug` < `Information` < `Warning` < `Error` < `Fatal`

Development default: `Debug`
Production default: `Information`

---

## Admin Seed

First-run admin account creation. Only used if no admin user exists in the database.

| Key | Type | Description | Default |
|-----|------|------------|---------|
| `AdminSeed:Email` | string | Admin email address | `"admin@example.com"` |
| `AdminSeed:Password` | string | Admin password | — (required) |
| `AdminSeed:DisplayName` | string | Admin display name | `"Admin"` |

```jsonc
{
  "AdminSeed": {
    "Email": "admin@hotbox.local",
    "Password": "YourSecurePassword123",
    "DisplayName": "Admin"
  }
}
```

---

## Search

| Key | Type | Description | Default |
|-----|------|------------|---------|
| `Search:MaxResults` | int | Maximum results per search query | `100` |
| `Search:DefaultLimit` | int | Default page size for search results | `25` |
| `Search:MinQueryLength` | int | Minimum query length (characters) | `2` |
| `Search:SnippetLength` | int | Maximum snippet length in results | `200` |

```jsonc
{
  "Search": {
    "MaxResults": 100,
    "DefaultLimit": 25,
    "MinQueryLength": 2,
    "SnippetLength": 200
  }
}
```

---

## Complete Example

```jsonc
{
  "Server": {
    "ServerName": "HotBox",
    "RegistrationMode": "Open"
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=hotbox;Username=hotbox;Password=changeme",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Secret": "your-secret-key-at-least-32-characters-long",
    "Issuer": "HotBox",
    "Audience": "HotBox",
    "AccessTokenExpiration": 15,
    "RefreshTokenExpiration": 10080
  },
  "OAuth": {
    "Google": { "ClientId": "", "ClientSecret": "", "Enabled": false },
    "Microsoft": { "ClientId": "", "ClientSecret": "", "Enabled": false },
    "Discord": { "ClientId": "", "ClientSecret": "", "Enabled": false }
  },
  "IceServers": [
    { "Urls": ["stun:stun.l.google.com:19302"] }
  ],
  "Observability": {
    "SeqUrl": "http://localhost:5341",
    "OtlpEndpoint": "",
    "ElasticsearchUrl": "",
    "LogLevel": "Information"
  },
  "AdminSeed": {
    "Email": "admin@example.com",
    "Password": "ChangeMe123!",
    "DisplayName": "Admin"
  },
  "Search": {
    "MaxResults": 100,
    "DefaultLimit": 25,
    "MinQueryLength": 2,
    "SnippetLength": 200
  }
}
```

---

## Docker Environment Variables

For Docker deployments, all settings can be passed as environment variables:

```bash
# Required
ConnectionStrings__Postgres="Host=postgres;Port=5432;Database=hotbox;Username=hotbox;Password=${DB_PASSWORD}"
ConnectionStrings__Redis="redis:6379"
Jwt__Secret="${JWT_SECRET}"
AdminSeed__Email="${ADMIN_EMAIL}"
AdminSeed__Password="${ADMIN_PASSWORD}"
AdminSeed__DisplayName="${ADMIN_DISPLAY_NAME}"

# Optional
Server__ServerName="My HotBox"
Server__RegistrationMode="InviteOnly"
Observability__SeqUrl="http://seq:5341"
Observability__OtlpEndpoint="http://otel-collector:4317"
Observability__ElasticsearchUrl="http://elasticsearch:9200"
OAuth__Google__ClientId="..."
OAuth__Google__ClientSecret="..."
OAuth__Google__Enabled="true"
```

### .env File

Create a `.env` file alongside `docker-compose.yml`:

```bash
# Database
DB_PASSWORD=your-secure-password

# JWT
JWT_SECRET=your-jwt-secret-at-least-32-characters

# Admin
ADMIN_EMAIL=admin@example.com
ADMIN_PASSWORD=YourSecureAdminPassword
ADMIN_DISPLAY_NAME=Admin

# Seq (hashed password — see docs/v2/deployment/docker.md)
SEQ_PASSWORD=your-hashed-seq-password
```

**Never commit `.env` to source control.** The `.gitignore` should include `.env`.
