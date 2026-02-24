# CLAUDE.md

## Project Overview

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

## Core MVP Features

- Text channels (flat list, plain text, no markdown/reactions/threads)
- Voice channels (drop-in/drop-out, always-on rooms)
- Direct messages (1-on-1)
- Message search (native database FTS — PostgreSQL `tsvector`, MySQL `FULLTEXT`, SQLite FTS5)
- Auth with configurable registration modes (open, invite-only, closed)
- Basic roles (admin, moderator, member)
- Desktop/browser notifications

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
