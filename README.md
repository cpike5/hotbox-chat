# HotBox

A self-hosted, open-source alternative to Discord. Built for small friend groups who want a private, lightweight chat platform they fully control.

## Why?

Discord is increasingly hostile to user privacy (facial ID / age verification), ships a bloated Electron-based client, and delivers a clunky, buggy UI. HotBox is the antidote: simple, fast, private, and self-hosted.

## Features

- **Text Channels** — Real-time messaging with SignalR, organized chat rooms
- **Voice Channels** — Always-on, drop-in/drop-out voice rooms via WebRTC P2P
- **Direct Messages** — Private 1-on-1 conversations
- **Message Search** — Full-text search with native database FTS (PostgreSQL `tsvector`, MySQL `FULLTEXT`, SQLite FTS5)
- **Authentication** — Email/password with optional OAuth (Google, Microsoft)
- **Roles & Permissions** — Admin, moderator, member with fine-grained permissions
- **Admin Panel** — Server settings, user/channel/invite management
- **Presence Tracking** — Online/idle/offline status
- **Notifications** — Desktop/browser notifications for mentions and messages
- **Observability** — Structured logging (Serilog), tracing and metrics (OpenTelemetry)

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core + SignalR (real-time text) + WebRTC (peer-to-peer voice) |
| Web Client | Blazor WASM (no JavaScript on server) |
| Database | SQLite (dev) / PostgreSQL, MySQL, MariaDB (prod) |
| ORM | Entity Framework Core with multi-provider support |
| Auth | ASP.NET Identity + OAuth (Google, Microsoft) |
| Logging | Serilog → Seq (dev), Elasticsearch (prod) |
| Observability | OpenTelemetry (traces + metrics) |
| Deployment | Docker / Docker Compose |

## Architecture

Three-layer architecture:

```
Core (domain models, interfaces, business logic)
  └── Infrastructure (EF Core data access, external services)
        └── Application (Blazor WASM UI, ASP.NET Core API)
```

## Quick Start

### Docker (Recommended)

Starts HotBox with PostgreSQL and Seq (log viewer):

```bash
git clone https://github.com/cpike5/hotbox-chat.git
cd hotbox-chat
docker-compose up
```

**Access:**
- HotBox: http://localhost:8080
- Seq (logs): http://localhost:5341
- Health check: http://localhost:8080/health

**First Run:**
The first registered user becomes the admin. To seed an admin user, set environment variables in `docker-compose.yml`:

```yaml
environment:
  - AdminSeed__Email=admin@example.com
  - AdminSeed__Password=YourSecurePassword
  - AdminSeed__DisplayName=Admin
```

**Production:**
- Generate a secure JWT secret: `Jwt__Secret=<your-random-secret>`
- Use a strong database password
- Enable TLS with a reverse proxy (Nginx/Caddy)

See [docs/deployment/docker.md](docs/deployment/docker.md) for full configuration, MySQL/MariaDB setup, backups, and TLS.

### Development Setup

**Prerequisites:**
- [.NET 8 SDK](https://dotnet.microsoft.com/download)

```bash
git clone https://github.com/cpike5/hotbox-chat.git
cd hotbox-chat
dotnet restore
dotnet run --project src/HotBox.Application
```

**Access:** http://localhost:5000

**Development Database:**
Uses SQLite by default (`hotbox.db` in project root). No database setup required.

**Run Tests:**

```bash
dotnet test
```

**Hot Reload:**
Blazor WASM supports hot reload for UI changes. Backend changes require restart.

### Bare Metal (Linux VPS)

```bash
git clone https://github.com/cpike5/hotbox-chat.git
cd hotbox-chat
sudo ./deploy/bare-metal-deploy.sh install
```

Installs to `/opt/hotbox` with a systemd service listening on `http://localhost:5000`.

See [docs/deployment/bare-metal.md](docs/deployment/bare-metal.md) for PostgreSQL setup, Nginx reverse proxy, TLS, and firewall configuration.

## Configuration

HotBox is configured via `appsettings.json` and environment variables. Environment variables override `appsettings.json` using double-underscore syntax (e.g., `Database__Provider`).

### Key Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `Server__ServerName` | `"HotBox"` | Server display name |
| `Server__Port` | `5000` | HTTP port (dev only; Docker uses 8080) |
| `Server__RegistrationMode` | `"Open"` | `Open`, `InviteOnly`, `Closed` |
| `Database__Provider` | `"sqlite"` | `sqlite`, `postgresql`, `mysql`, `mariadb` |
| `Database__ConnectionString` | `"Data Source=hotbox.db"` | Database connection string |
| `Jwt__Secret` | `""` | **Required for production** — JWT signing secret |
| `Jwt__AccessTokenExpiration` | `"00:15:00"` | Access token lifetime |
| `Jwt__RefreshTokenExpiration` | `"7.00:00:00"` | Refresh token lifetime |
| `OAuth__Google__Enabled` | `false` | Enable Google OAuth |
| `OAuth__Google__ClientId` | `""` | Google OAuth client ID |
| `OAuth__Google__ClientSecret` | `""` | Google OAuth client secret |
| `OAuth__Microsoft__Enabled` | `false` | Enable Microsoft OAuth |
| `OAuth__Microsoft__ClientId` | `""` | Microsoft OAuth client ID |
| `OAuth__Microsoft__ClientSecret` | `""` | Microsoft OAuth client secret |
| `IceServers__StunUrls` | `["stun:stun.l.google.com:19302"]` | STUN servers for WebRTC |
| `IceServers__TurnUrl` | `""` | TURN server URL (optional, for NAT traversal) |
| `IceServers__TurnUsername` | `""` | TURN username |
| `IceServers__TurnCredential` | `""` | TURN credential |
| `Observability__SeqUrl` | `"http://localhost:5341"` | Seq log aggregation URL |
| `Observability__OtlpEndpoint` | `""` | OpenTelemetry OTLP endpoint (optional) |
| `Observability__LogLevel` | `"Information"` | Minimum log level |
| `AdminSeed__Email` | `""` | Seed admin email (first run only) |
| `AdminSeed__Password` | `""` | Seed admin password |
| `AdminSeed__DisplayName` | `""` | Seed admin display name |

### Registration Modes

- **Open** — Anyone can register (default for development)
- **InviteOnly** — Users must have an invite code (recommended for private communities)
- **Closed** — No registration allowed; admins create users manually

### Database Connection Strings

**PostgreSQL:**
```
Host=db;Port=5432;Database=hotbox;Username=hotbox;Password=hotbox_password
```

**MySQL/MariaDB:**
```
Server=db;Port=3306;Database=hotbox;User=hotbox;Password=hotbox_password
```

**SQLite:**
```
Data Source=hotbox.db
```

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Read the docs** — Check `CLAUDE.md` and `docs/ownership-domains.md` for architecture and domain boundaries
2. **Open an issue first** — Discuss features or major changes before implementing
3. **Follow conventions** — Match existing code style, use `IServiceCollection` extension methods for DI, Options pattern for configuration
4. **Write tests** — Unit tests for business logic, integration tests for API endpoints
5. **Update docs** — Keep `docs/` in sync with code changes
6. **Commit messages** — Use conventional commits (e.g., `feat:`, `fix:`, `docs:`)

### Development Workflow

```bash
# Create feature branch
git checkout -b feature/your-feature

# Make changes, write tests
dotnet test

# Commit and push
git add .
git commit -m "feat: add your feature"
git push origin feature/your-feature

# Open pull request on GitHub
```

## Design Target

- ~100 concurrent users max (small friend groups)
- Dark mode by default
- Fast, responsive, not clunky — no Electron-like sluggishness

## Non-Goals

- Scaling beyond ~100 concurrent users
- JavaScript on the server (non-negotiable)
- Electron-based desktop client
- Per-voice-channel text chat

## License

MIT License — See [LICENSE](LICENSE) for details.
