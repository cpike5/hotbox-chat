# Configuration Reference

Authoritative reference for all HotBox configuration sections. All configuration uses the ASP.NET Core Options pattern with classes in `HotBox.Core/Options/`.

## Configuration Sources

Configuration is loaded from (in priority order):

1. Environment variables (using `__` as section separator, e.g., `Server__ServerName`)
2. `appsettings.{Environment}.json`
3. `appsettings.json`

## Sections

### Server

- **Options class**: `ServerOptions` (`HotBox.Core/Options/ServerOptions.cs`)
- **Config section**: `Server`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServerName` | `string` | `"HotBox"` | Display name of the server instance |
| `Port` | `int` | `5000` | Port number for the application to listen on |
| `RegistrationMode` | `RegistrationMode` | `InviteOnly` | User registration mode: `Open`, `InviteOnly`, or `Closed` |

**Valid RegistrationMode values**:
- `Open` - Anyone can create an account
- `InviteOnly` - Users need an invite code to register
- `Closed` - Registration is disabled

### Database

- **Options class**: `DatabaseOptions` (`HotBox.Core/Options/DatabaseOptions.cs`)
- **Config section**: `Database`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Provider` | `string` | `"sqlite"` | Database provider: `sqlite`, `postgresql`, `mysql`, or `mariadb` |
| `ConnectionString` | `string` | `""` | ADO.NET connection string for the selected provider |

**Example connection strings**:
- SQLite: `Data Source=hotbox.db`
- PostgreSQL: `Host=localhost;Port=5432;Database=hotbox;Username=hotbox;Password=secret`
- MySQL/MariaDB: `Server=localhost;Port=3306;Database=hotbox;User=hotbox;Password=secret`

### Jwt

- **Options class**: `JwtOptions` (`HotBox.Core/Options/JwtOptions.cs`)
- **Config section**: `Jwt`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Secret` | `string` | `""` | Secret key for signing JWT tokens (must be at least 32 characters) |
| `Issuer` | `string` | `"HotBox"` | JWT issuer claim |
| `Audience` | `string` | `"HotBox"` | JWT audience claim |
| `AccessTokenExpiration` | `TimeSpan` | `00:15:00` | Access token lifetime (default: 15 minutes) |
| `RefreshTokenExpiration` | `TimeSpan` | `7.00:00:00` | Refresh token lifetime (default: 7 days) |

**TimeSpan format**: `d.hh:mm:ss` or `hh:mm:ss` (e.g., `7.00:00:00` = 7 days, `00:15:00` = 15 minutes)

**Security note**: The `Secret` must be a strong random string of at least 32 characters in production. Generate with:
```bash
openssl rand -base64 48
```

### OAuth

- **Options class**: `OAuthOptions` and `OAuthProviderOptions` (`HotBox.Core/Options/OAuthOptions.cs`)
- **Config section**: `OAuth`

#### OAuth.Google

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ClientId` | `string` | `""` | Google OAuth 2.0 client ID |
| `ClientSecret` | `string` | `""` | Google OAuth 2.0 client secret |
| `Enabled` | `bool` | `false` | Enable Google OAuth authentication |

#### OAuth.Microsoft

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ClientId` | `string` | `""` | Microsoft OAuth 2.0 client ID |
| `ClientSecret` | `string` | `""` | Microsoft OAuth 2.0 client secret |
| `Enabled` | `bool` | `false` | Enable Microsoft OAuth authentication |

### IceServers

- **Options class**: `IceServerOptions` (`HotBox.Core/Options/IceServerOptions.cs`)
- **Config section**: `IceServers`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `StunUrls` | `string[]` | `["stun:stun.l.google.com:19302"]` | STUN server URLs for WebRTC NAT traversal |
| `TurnUrl` | `string` | `""` | TURN server URL for relay (when direct connections fail) |
| `TurnUsername` | `string` | `""` | TURN server username |
| `TurnCredential` | `string` | `""` | TURN server credential/password |

**Note**: STUN is sufficient for most deployments. TURN is only needed if users are behind restrictive NATs/firewalls that block peer-to-peer connections.

### Observability

- **Options class**: `ObservabilityOptions` (`HotBox.Core/Options/ObservabilityOptions.cs`)
- **Config section**: `Observability`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `SeqUrl` | `string` | `"http://localhost:5341"` | Seq log aggregation server URL |
| `OtlpEndpoint` | `string` | `""` | OpenTelemetry Protocol (OTLP) endpoint for traces/metrics |
| `LogLevel` | `string` | `"Information"` | Serilog minimum log level: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |

### AdminSeed

- **Options class**: `AdminSeedOptions` (`HotBox.Core/Options/AdminSeedOptions.cs`)
- **Config section**: `AdminSeed`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Email` | `string` | `""` | Email for the default admin user (created on first run if provided) |
| `Password` | `string` | `""` | Password for the default admin user |
| `DisplayName` | `string` | `""` | Display name for the default admin user |

**Note**: If all three values are provided and no admin user exists, one will be created during application startup. Leave blank to skip admin seeding.

### Search

- **Options class**: `SearchOptions` (`HotBox.Core/Options/SearchOptions.cs`)
- **Config section**: `Search`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `MaxResults` | `int` | `50` | Maximum number of search results to return |
| `DefaultLimit` | `int` | `20` | Default page size for search results |
| `MinQueryLength` | `int` | `2` | Minimum character length for a search query |
| `SnippetLength` | `int` | `150` | Length of text snippets in search results (characters) |

**Note**: This section is optional in `appsettings.json`. If omitted, the defaults from `SearchOptions` are used.

## Environment Variable Examples

All configuration values can be overridden via environment variables using `__` (double underscore) as the section delimiter.

### Basic Configuration

```bash
# Server settings
Server__ServerName="My HotBox Server"
Server__Port=5000
Server__RegistrationMode=Open

# Database (PostgreSQL)
Database__Provider=postgresql
Database__ConnectionString="Host=localhost;Port=5432;Database=hotbox;Username=hotbox;Password=secret"

# JWT (REQUIRED in production)
Jwt__Secret="your-super-secret-key-minimum-32-characters-long"
Jwt__Issuer="HotBox"
Jwt__Audience="HotBox"
Jwt__AccessTokenExpiration="00:30:00"
Jwt__RefreshTokenExpiration="14.00:00:00"
```

### OAuth Configuration

```bash
# Google OAuth
OAuth__Google__ClientId="123456789-abc.apps.googleusercontent.com"
OAuth__Google__ClientSecret="GOCSPX-..."
OAuth__Google__Enabled=true

# Microsoft OAuth
OAuth__Microsoft__ClientId="12345678-1234-1234-1234-123456789abc"
OAuth__Microsoft__ClientSecret="abc123..."
OAuth__Microsoft__Enabled=true
```

### WebRTC Configuration

```bash
# STUN only (most deployments)
IceServers__StunUrls__0="stun:stun.l.google.com:19302"
IceServers__StunUrls__1="stun:stun1.l.google.com:19302"

# TURN server (restrictive networks)
IceServers__TurnUrl="turn:turn.example.com:3478"
IceServers__TurnUsername="turnuser"
IceServers__TurnCredential="turnpass"
```

### Observability

```bash
# Seq for structured logs
Observability__SeqUrl="http://seq.example.com:5341"

# OTLP for traces/metrics (Jaeger, Tempo, etc.)
Observability__OtlpEndpoint="http://jaeger:4317"

# Log level
Observability__LogLevel=Debug
```

### Admin Seeding

```bash
# Create admin user on first run
AdminSeed__Email="admin@example.com"
AdminSeed__Password="SecurePassword123!"
AdminSeed__DisplayName="Admin"
```

### Search Configuration

```bash
Search__MaxResults=100
Search__DefaultLimit=25
Search__MinQueryLength=3
Search__SnippetLength=200
```

## Docker Compose Environment Example

The `docker-compose.yml` file demonstrates production environment variable usage:

```yaml
services:
  hotbox:
    environment:
      - ASPNETCORE_ENVIRONMENT=Production

      # Database (PostgreSQL)
      - Database__Provider=postgresql
      - Database__ConnectionString=Host=db;Port=5432;Database=hotbox;Username=hotbox;Password=${DB_PASSWORD:-changeme}

      # JWT (required)
      - Jwt__Secret=${JWT_SECRET:-super-secret-key-change-in-production-minimum-32-chars}

      # Observability
      - Observability__SeqUrl=http://seq:5341

      # Admin seed
      - AdminSeed__Email=${ADMIN_EMAIL:-admin@hotbox.local}
      - AdminSeed__Password=${ADMIN_PASSWORD:-Admin123!}
      - AdminSeed__DisplayName=${ADMIN_DISPLAY_NAME:-Admin}
```

**Note**: Values like `${JWT_SECRET:-default}` use shell variable substitution with fallback defaults. In production, set these as environment variables or in a `.env` file:

```bash
# .env file for docker-compose
DB_PASSWORD=strong-db-password
JWT_SECRET=your-strong-jwt-secret-minimum-32-characters
ADMIN_EMAIL=admin@yourserver.com
ADMIN_PASSWORD=SecureAdminPassword123!
ADMIN_DISPLAY_NAME=ServerAdmin
```

## Array Configuration in Environment Variables

For array properties (like `IceServers__StunUrls`), use indexed syntax:

```bash
IceServers__StunUrls__0="stun:stun.l.google.com:19302"
IceServers__StunUrls__1="stun:stun1.l.google.com:19302"
IceServers__StunUrls__2="stun:stun2.l.google.com:19302"
```

In JSON configuration files, use standard JSON arrays:

```json
{
  "IceServers": {
    "StunUrls": [
      "stun:stun.l.google.com:19302",
      "stun:stun1.l.google.com:19302"
    ]
  }
}
```

## Validation Notes

- **Jwt.Secret** must be set and at least 32 characters for production (enforced at startup)
- **Database.ConnectionString** must be valid for the selected provider
- **TimeSpan** values can be specified as strings in JSON (`"00:15:00"`) or as environment variables (`Jwt__AccessTokenExpiration=00:15:00`)
- **RegistrationMode** enum values are case-insensitive when loaded from configuration

## Configuration Precedence Example

Given:
1. `appsettings.json`: `Server__Port = 5000`
2. `appsettings.Production.json`: `Server__Port = 8080`
3. Environment variable: `Server__Port = 9000`

Result: Application uses port `9000` (environment variables have highest priority).
