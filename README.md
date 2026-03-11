<p align="center">
  <img src="src/HotBox.Client/wwwroot/logo.svg" alt="HotBox" width="120" />
</p>

<h1 align="center">HotBox</h1>

<p align="center">
  Self-hosted chat for your crew. Text channels, voice rooms, direct messages — all on your own server.
</p>

<p align="center">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4" />
  <img alt="Docker" src="https://img.shields.io/badge/Docker-ready-2496ED" />
  <img alt="PRs Welcome" src="https://img.shields.io/badge/PRs-welcome-brightgreen.svg" />
</p>

<!-- TODO: Add hero screenshot (dark mode, realistic data) → docs/screenshots/hero-dark.png -->

## Quick Start

**Docker (recommended):**

```bash
git clone https://github.com/cpike5/hotbox-chat.git
cd hotbox-chat
cp .env.example .env   # edit .env with your secrets
docker compose up -d
```

Open **http://localhost:7200** — an admin account is created from the `ADMIN_*` values in your `.env` file.

See [Docker deployment docs](docs/deployment/docker.md) for production setup with the pre-built GHCR image, database options, TLS, and backups.

## Features

- **Text Channels** — Organized chat rooms with real-time messaging via SignalR
- **Voice Channels** — Drop into always-on voice rooms with peer-to-peer WebRTC audio
- **Direct Messages** — Private 1-on-1 conversations with conversation list and unread tracking
- **Message Search** — Full-text search across channels and DMs, powered by native database FTS (PostgreSQL `tsvector`, MySQL `FULLTEXT`, SQLite FTS5)
- **Authentication** — Email/password with optional OAuth (Google, Microsoft)
- **Roles** — Admin, Moderator, and Member roles with role-based access control
- **Admin Panel** — Manage server settings, users, channels, invites, and API keys
- **Invites** — Generate invite codes for controlled registration (open, invite-only, or closed modes)
- **Presence** — See who's online, idle, or offline in real time
- **Typing Indicators** — See when someone is composing a message
- **User Profiles** — Display name, avatar, bio, pronouns, and custom status
- **Notifications** — Desktop and browser notifications for mentions and direct messages
- **Agent / Bot API** — API key authentication and agent accounts for programmatic access via the MCP server
- **Observability** — Structured logging with Serilog, Seq, OpenTelemetry, and Elasticsearch support

## Why Self-Host?

- **You own the data** — Conversations stay on your hardware, not someone else's cloud
- **No telemetry, no tracking** — Zero data collection, ever
- **Lightweight** — Runs comfortably on a $5/month VPS for ~100 users
- **Your rules** — Open registration, invite-only, or fully closed — you decide who gets in
- **No bloat** — Blazor WASM client, no Electron, no 300 MB desktop app

## How It Compares

| | HotBox | Rocket.Chat | Element (Matrix) | Revolt | Mattermost |
|---|---|---|---|---|---|
| Self-hosted | Yes | Yes | Yes | Yes | Yes |
| Open source | Yes | MIT | Apache 2.0 | AGPL | AGPL / Proprietary |
| Voice chat | Built-in (WebRTC) | Jitsi plugin | Jitsi / native | Built-in | Plugin |
| Tech stack | .NET + Blazor WASM | Node.js + Meteor | Python / TypeScript | Rust / TypeScript | Go + React |
| Lightweight | Yes (~100 users) | No (enterprise-scale) | Moderate | Yes | No (enterprise-scale) |
| Desktop client | Browser-only | Electron | Electron | Electron | Electron |

HotBox is purpose-built for small groups. If you need enterprise features, federation, or 10,000-user scale, the others are better fits. If you want something simple and fast for your friend group, this is it.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core + SignalR + WebRTC |
| Web Client | Blazor WASM |
| Database | SQLite (dev) / PostgreSQL, MySQL, MariaDB (prod) |
| ORM | Entity Framework Core (multi-provider) |
| Auth | ASP.NET Identity + JWT + OAuth |
| Observability | Serilog + Seq + OpenTelemetry + Elasticsearch |
| Deployment | Docker / Docker Compose |

## Architecture

```
Core (domain models, interfaces, business logic)
  └── Infrastructure (EF Core data access, external services)
        └── Application (Blazor WASM UI, ASP.NET Core API)
```

## Configuration

Configured via `appsettings.json` or environment variables (double-underscore syntax, e.g. `Database__Provider`).

| Setting | Default | Description |
|---------|---------|-------------|
| `Server__ServerName` | `"HotBox"` | Server display name |
| `Server__RegistrationMode` | `"InviteOnly"` | `Open`, `InviteOnly`, or `Closed` |
| `Database__Provider` | `"sqlite"` | `sqlite`, `postgresql`, `mysql`, `mariadb` |
| `Database__ConnectionString` | `"Data Source=hotbox.db"` | Database connection string |
| `Jwt__Secret` | `""` | **Required for production** — JWT signing secret |
| `AdminSeed__Email` | `""` | Seed admin email (first run only) |
| `AdminSeed__Password` | `""` | Seed admin password |
| `AdminSeed__DisplayName` | `""` | Seed admin display name |

See [docs/deployment/docker.md](docs/deployment/docker.md) for all configuration options including OAuth, TURN/STUN, and observability.

## Development

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download)

```bash
git clone https://github.com/cpike5/hotbox-chat.git
cd hotbox-chat
dotnet restore
dotnet run --project src/HotBox.Application
```

Open **http://localhost:5000** — uses SQLite by default, no database setup needed.

```bash
dotnet test          # run tests
```

### Other Deployment Options

**Bare Metal (Linux VPS):**

```bash
sudo ./deploy/bare-metal-deploy.sh install
```

Installs to `/opt/hotbox` as a systemd service. See [docs/deployment/bare-metal.md](docs/deployment/bare-metal.md) for reverse proxy, TLS, and firewall setup.

### Agent / Bot Integration

HotBox includes an MCP (Model Context Protocol) server that lets LLMs create agent accounts, send messages, and read conversations. See [docs/mcp-server-setup.md](docs/mcp-server-setup.md) and [docs/api-key-management.md](docs/api-key-management.md) for setup.

## Contributing

Contributions welcome! Open an issue first for major changes.

1. Check `docs/ownership-domains.md` for architecture and domain boundaries
2. Follow existing code conventions and use conventional commits (`feat:`, `fix:`, `docs:`)
3. Write tests and keep docs in sync

```bash
git checkout -b feature/your-feature
dotnet test
git commit -m "feat: add your feature"
git push origin feature/your-feature
```

---

<p align="center">
  Give HotBox a try — clone it, <code>docker compose up</code>, and you're chatting in minutes.<br />
  If you find it useful, star the repo to help others discover it.
</p>
