# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

HotBox is an open-source, self-hosted alternative to Discord. Built for small friend groups (~10 people) who want a private, lightweight chat platform they fully control. Design target is ~100 concurrent users max.

**Status**: Early development — requirements are defined, a UI prototype exists, but `src/` has no application code yet.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core (API + SignalR for real-time text) |
| Voice | WebRTC (implementation approach TBD) |
| Web Client | Blazor WASM (no JS frameworks, no JS on the server — ever) |
| Database | SQLite (dev), PostgreSQL/MySQL/MariaDB (prod) via EF Core |
| Auth | ASP.NET Identity + optional OAuth (Google, Microsoft) |
| Logging | Serilog → Seq (dev), Elasticsearch (prod) |
| Observability | OpenTelemetry for tracing and metrics |
| Containerization | Docker / Docker Compose |

## Architecture

Three-layer architecture:
- **Core** — Domain models, interfaces, business logic. No infrastructure dependencies.
- **Infrastructure** — Data access (EF Core), external services. Implements Core interfaces.
- **Application** — UI (Blazor WASM) and API (ASP.NET Core). References both Core and Infrastructure.

Key patterns:
- `IServiceCollection` extension methods for DI registration
- Options pattern (`IOptions<T>`) for configuration
- Configuration via `appsettings.json` and environment variables
- Docker-first deployment

## Project Structure

```
docs/
  requirements/    # Product requirements
  architecture/    # Architecture decisions (empty, to be populated)
  designs/         # Design documents (empty, to be populated)
  research/        # Technical research (empty, to be populated)
prototypes/        # HTML prototypes for UI exploration
src/               # Application source code (not yet scaffolded)
temp/              # Drafts — move to docs/ when finalized
```

## Core MVP Features

- Text channels (flat list, plain text, no markdown/reactions/threads)
- Voice channels (drop-in/drop-out, always-on rooms)
- Direct messages (1-on-1)
- Auth with configurable registration modes (open, invite-only, closed)
- Basic roles (admin, moderator, member)
- Desktop/browser notifications

## Design Direction

- Dark mode by default
- Fast and responsive — no loading screens, no Electron-like sluggishness
- Cleaner and less noisy than Discord
- UI prototype is at `prototypes/main-ui-proposal.html` — design tokens and CSS custom properties are defined there

## Explicit Constraints

- No JavaScript on the server (non-negotiable)
- No Electron-based desktop client (native Avalonia client is a future goal)
- No per-voice-channel text chat
- Observability (Serilog + OpenTelemetry) must be built in from day one, not bolted on later

## Domain Agents

The project is organized into five ownership domains, each with a dedicated sub-agent defined in `.claude/agents/`. Each agent has full context about its domain — what it owns, what it doesn't, coordination points, and docs it maintains.

| Agent | Domain | Invoke With |
|-------|--------|-------------|
| `platform` | Infrastructure, config, Docker, observability, database, Core layer | Task tool with `.claude/agents/platform.md` |
| `auth-security` | Identity, JWT, OAuth, roles, permissions, registration | Task tool with `.claude/agents/auth-security.md` |
| `messaging` | Text channels, DMs, ChatHub, message persistence | Task tool with `.claude/agents/messaging.md` |
| `realtime-media` | Voice channels, WebRTC, VoiceSignalingHub, audio | Task tool with `.claude/agents/realtime-media.md` |
| `client-experience` | Blazor WASM shell, layout, design system, theming, notifications UI | Task tool with `.claude/agents/client-experience.md` |

See `docs/ownership-domains.md` for full domain boundaries, code ownership, and coordination rules.
