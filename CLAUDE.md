# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

You are primarily an orchestrator (unless the user specifies otherwise). Try to delegate your tasks to specialized sub-agents, domain agents, or generic agents, and run in parallel or in the background whenever possible. 

## Project Overview

HotBox is an open-source, self-hosted alternative to Discord. Built for small friend groups (~10 people) who want a private, lightweight chat platform they fully control. Design target is ~100 concurrent users max.

**Status**: Active development — Core/Infrastructure/Application/Client projects scaffolded with EF Core multi-provider DbContext. Implemented features: authentication (JWT + OAuth + API keys), text channels, direct messages, message search (FTS), admin panel, presence/online status, voice signaling (WebRTC P2P full mesh), user profiles, MCP agent accounts. Deployment scripts and guides in place.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core (API + SignalR for real-time text) |
| Voice | WebRTC P2P full mesh (SIPSorcery signaling via SignalR, JS interop for browser WebRTC) |
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
- Docker-first deployment (see `deploy/` scripts and `docs/deployment/`)

## Project Structure

```
deploy/              # Deployment scripts (docker-deploy.sh, bare-metal-deploy.sh)
docs/
  requirements/      # Product requirements
  deployment/        # Docker and bare-metal deployment guides
  architecture/      # Architecture decisions (empty, to be populated)
  designs/           # Design documents (empty, to be populated)
  research/          # Technical research (empty, to be populated)
prototypes/          # HTML prototypes for UI exploration
src/
  HotBox.Core/       # Domain models, interfaces, enums
  HotBox.Infrastructure/ # EF Core DbContext, configurations, DI
  HotBox.Application/    # ASP.NET Core host (API entry point)
  HotBox.Client/         # Blazor WASM client
tests/               # Unit/integration test projects
temp/                # Drafts — move to docs/ when finalized
Dockerfile           # Multi-stage build (SDK → runtime)
docker-compose.yml   # App + PostgreSQL
```

## Core MVP Features

- Text channels (flat list, plain text, no markdown/reactions/threads)
- Voice channels (drop-in/drop-out, always-on rooms)
- Direct messages (1-on-1)
- Message search (native database FTS — PostgreSQL `tsvector`, MySQL `FULLTEXT`, SQLite FTS5)
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

## GitHub CLI Quirk

`gh issue view {N}` (without `--json`) fails with a Projects (classic) deprecation error. Always use `gh issue view {N} --json title,body,labels,state` instead.

## Enum Serialization

All enums in `HotBox.Core/Enums/` must have `[JsonConverter(typeof(JsonStringEnumConverter))]`. The server (API controllers + SignalR) serializes enums as strings. The Blazor WASM client deserializes API responses with default `System.Text.Json` options — without the attribute on the enum itself, the client cannot parse string values like `"Text"` back into `ChannelType`. Adding `JsonStringEnumConverter` only on the server side is **not enough**; the attribute must live on the enum type in Core so both sides agree.
