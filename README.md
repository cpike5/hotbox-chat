# HotBox

A self-hosted, open-source alternative to Discord. Built for small friend groups who want a private, lightweight chat platform they fully control.

## Why?

Discord is increasingly hostile to user privacy (facial ID / age verification), ships a bloated Electron-based client, and delivers a clunky, buggy UI. HotBox is the antidote: simple, fast, private, and self-hosted.

## Features (MVP)

- **Text Channels** — Organized chat rooms (general, gaming, etc.)
- **Voice Channels** — Always-on, drop-in/drop-out voice rooms via WebRTC
- **Direct Messages** — Private 1-on-1 conversations
- **Authentication** — Email/password with optional OAuth (Google, Microsoft)
- **Roles & Permissions** — Admin, moderator, member
- **Notifications** — Desktop/browser notifications for mentions and messages
- **Observability** — Structured logging (Serilog), tracing and metrics (OpenTelemetry)

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core + SignalR |
| Web Client | Blazor WASM |
| Database | SQLite (dev) / PostgreSQL, MySQL, MariaDB (prod) |
| ORM | Entity Framework Core |
| Auth | ASP.NET Identity + OAuth |
| Logging | Serilog → Seq (dev), Elasticsearch (prod) |
| Observability | OpenTelemetry |
| Deployment | Docker / Docker Compose |

## Architecture

Three-layer architecture:

```
Core (domain models, interfaces, business logic)
  └── Infrastructure (EF Core data access, external services)
        └── Application (Blazor WASM UI, ASP.NET Core API)
```

## Getting Started

> **Note**: The project is in early development. Source code has not been scaffolded yet. See `docs/requirements/requirements.md` for the full product specification and `prototypes/` for UI explorations.

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download) (or latest)
- [Docker](https://www.docker.com/) (for containerized deployment)

## Configuration

HotBox is designed to be highly configurable via `appsettings.json` and environment variables:

- Port configuration
- Admin user seeding on first run
- Registration mode (open / invite-only / closed)
- OAuth provider settings (optional — login UI adapts dynamically)
- Database provider selection
- Role definitions

## Design Target

- ~100 concurrent users max
- Dark mode by default
- Fast, responsive, not clunky — no Electron-like sluggishness

## Non-Goals

- Scaling beyond ~100 concurrent users
- JavaScript on the server
- Electron-based desktop client
- Per-voice-channel text chat

## License

TBD
