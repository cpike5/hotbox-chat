# HotBox v2 — Ownership Domains

**Version**: 2.0
**Date**: 2026-03-22
**Status**: Draft

---

## Overview

HotBox v2 development is organized into six ownership domains. Each domain has a dedicated sub-agent defined in `.claude/agents/` with full context about its scope, coordination points, and documentation responsibilities.

---

## Domain Map

| Domain | Agent File | Primary Scope |
|--------|-----------|---------------|
| **Platform** | `.claude/agents/platform.md` | Infrastructure, config, Docker, observability, database, Core layer, caching, health checks |
| **Auth & Security** | `.claude/agents/auth-security.md` | Identity, JWT, OAuth, roles, permissions, registration, API keys, FluentValidation (auth validators) |
| **Messaging** | `.claude/agents/messaging.md` | Text channels, DMs, ChatHub, message persistence, search, FluentValidation (message validators) |
| **Real-time & Media** | `.claude/agents/realtime-media.md` | Voice channels, WebRTC, VoiceSignalingHub, audio, presence |
| **Client Experience** | `.claude/agents/client-experience.md` | Blazor UI shell, MudBlazor theming, layout, components, Tailwind, notifications UI, render mode decisions |
| **Marketing** | `.claude/agents/marketing.md` | Branding, README, release notes, public-facing content, visual assets |

---

## Interaction Model

```
                        ┌─────────────┐
                        │  Marketing  │
                        │  (external  │
                        │   content)  │
                        └──────┬──────┘
                               │ writes about
          ┌────────────────────┼────────────────────┐
          │                    │                     │
   ┌──────┴──────┐   ┌────────┴───────┐   ┌────────┴───────┐
   │    Auth &    │   │   Messaging    │   │  Real-time &   │
   │   Security   │   │                │   │    Media       │
   │              │   │  (channels,    │   │  (voice,       │
   │  (identity,  │   │   DMs, search, │   │   WebRTC,      │
   │   JWT, OAuth,│   │   ChatHub)     │   │   presence)    │
   │   roles)     │   │                │   │                │
   └──────┬──────┘   └────────┬───────┘   └────────┬───────┘
          │                    │                     │
          └────────────────────┼─────────────────────┘
                               │ renders via
                        ┌──────┴──────┐
                        │   Client    │
                        │ Experience  │
                        │             │
                        │ (Blazor UI, │
                        │  MudBlazor, │
                        │  Tailwind)  │
                        └──────┬──────┘
                               │ builds on
                        ┌──────┴──────┐
                        │  Platform   │
                        │             │
                        │ (infra, DB, │
                        │  Redis,     │
                        │  caching,   │
                        │  Docker,    │
                        │  observability)│
                        └─────────────┘
```

---

## Domain Boundaries

### Platform

**Owns:**
- `HotBox.Core/` — all entities, interfaces, enums, options, models
- `HotBox.Infrastructure/Data/` — DbContext, migrations, entity configurations
- `HotBox.Infrastructure/DependencyInjection/` — `AddInfrastructure()` registration
- `HotBox.Application/DependencyInjection/ObservabilityExtensions.cs`
- `HotBox.Application/Program.cs` — startup pipeline, DI composition root
- Docker files (`Dockerfile`, `docker-compose.yml`)
- `appsettings.json` structure and Options classes
- Health check registration
- Redis connection and HybridCache configuration
- Database migrations and seeding infrastructure

**Does not own:**
- Business logic in service implementations (owned by feature domains)
- Blazor UI components (owned by Client Experience)
- Hub implementations (owned by Messaging / Real-time & Media)

**Coordination points:**
- Other domains add entities → Platform reviews and applies migration
- Other domains add Options classes → Platform ensures they're registered in DI
- Other domains add health checks → Platform registers them in `Program.cs`
- Other domains need Redis → Platform provides the connection; domain uses it

### Auth & Security

**Owns:**
- `HotBox.Infrastructure/Services/TokenService.cs`
- `HotBox.Application/Controllers/AuthController.cs`
- `HotBox.Application/Controllers/ApiKeysController.cs`
- `HotBox.Application/Authentication/` — JWT config, API key handler, OAuth setup
- Auth-related FluentValidation validators
- `HotBox.Application/Components/Pages/Auth/` — Login, Register pages
- Security middleware, CSRF, CSP headers

**Does not own:**
- `AppUser` entity definition (Platform/Core)
- Auth UI layout/styling (Client Experience)

**Coordination points:**
- Client Experience implements auth UI using auth state from this domain
- Messaging and Real-time use `[Authorize]` attributes defined by this domain
- Platform seeds the admin account using config from this domain

### Messaging

**Owns:**
- `HotBox.Infrastructure/Services/ChannelService.cs`, `MessageService.cs`, `DirectMessageService.cs`, `SearchService.cs`, `ReadStateService.cs`
- `HotBox.Infrastructure/Repositories/ChannelRepository.cs`, `MessageRepository.cs`, `DirectMessageRepository.cs`
- `HotBox.Application/Hubs/ChatHub.cs` (text messaging methods)
- `HotBox.Application/Controllers/ChannelsController.cs`, `MessagesController.cs`, `DirectMessagesController.cs`, `SearchController.cs`
- Message-related FluentValidation validators

**Does not own:**
- `ChatHub` voice-related methods (Real-time & Media)
- Channel list UI rendering (Client Experience)
- Message entity definition (Platform/Core)

**Coordination points:**
- Real-time & Media adds voice signaling methods to a separate hub
- Client Experience renders message list, composer, and channel sidebar
- Platform manages the search index infrastructure (GIN index, tsvector column)

### Real-time & Media

**Owns:**
- `HotBox.Application/Hubs/VoiceSignalingHub.cs`
- `HotBox.Infrastructure/Services/PresenceService.cs`
- Voice-related JavaScript interop
- WebRTC client-side logic
- ICE/STUN/TURN configuration

**Does not own:**
- `ChatHub` text messaging methods (Messaging)
- Voice UI controls layout/styling (Client Experience)
- Presence UI indicators (Client Experience)

**Coordination points:**
- Client Experience implements voice controls and presence indicators
- Messaging uses presence data for typing indicators and online status
- Platform provides SignalR backplane (Redis) configuration

### Client Experience

**Owns:**
- `HotBox.Application/Components/Layout/` — MainLayout, TopBar, ChannelSidebar, MembersList
- `HotBox.Application/Components/Pages/` — page-level components
- `HotBox.Client/Components/` — WASM-specific interactive components
- MudBlazor theme configuration
- Tailwind CSS configuration
- Design tokens, typography, color palette
- CSS isolation files (`.razor.css`)
- Render mode decisions per component
- Notification UI (toasts, notification panel)

**Does not own:**
- Service layer logic (feature domains)
- Auth flow logic (Auth & Security)
- SignalR hub implementations (Messaging, Real-time & Media)

**Coordination points:**
- All feature domains provide data contracts; Client Experience renders them
- Auth & Security provides auth state; Client Experience conditionally renders
- Real-time & Media provides voice state; Client Experience renders controls

### Marketing

**Owns:**
- `README.md`
- Release notes and changelogs
- Public-facing documentation (deployment guides, feature descriptions)
- Brand assets and visual identity

**Does not own:**
- Technical specification documents (Platform)
- Code or architecture (all other domains)

**Coordination points:**
- Reads from all domains to accurately describe features
- Client Experience provides screenshots and UI descriptions

---

## Shared Ownership Rules

### Core Entities & Interfaces
- **Defined by**: Platform (as code owner of `HotBox.Core/`)
- **Requested by**: Feature domains propose new entities/interfaces via PR
- **Migration**: Platform creates and reviews all EF Core migrations

### ChatHub vs VoiceSignalingHub
- **ChatHub**: Owned by Messaging (text methods, typing, read state)
- **VoiceSignalingHub**: Owned by Real-time & Media (signaling, voice participant tracking)
- Both hubs are separate classes — no shared hub ownership conflicts

### FluentValidation Validators
- Each feature domain owns validators for its input DTOs
- Validators live in `HotBox.Infrastructure/Validation/`
- Platform ensures `AddFluentValidationAutoValidation()` is registered

### Configuration Sections
- Each domain owns its configuration section:
  - Platform: `Server`, `ConnectionStrings`, `Observability`
  - Auth & Security: `Jwt`, `OAuth`, `AdminSeed`
  - Real-time & Media: `IceServers`
  - Messaging: `Search`
- Platform ensures all Options classes are bound in DI

### Health Checks
- Each domain may propose health checks
- Platform registers all health checks in `Program.cs`
- Built-in: database (Platform), Redis (Platform), SignalR (Real-time & Media)

### HybridCache
- Platform provides the cache infrastructure
- Feature domains use `HybridCache` via DI — each domain manages its own cache keys and invalidation

---

## v1 → v2 Domain Changes

| Change | Impact |
|--------|--------|
| PostgreSQL only (dropped MySQL/SQLite) | Messaging: single FTS implementation. Platform: single migration path |
| Redis backplane | Platform: new infrastructure. Real-time & Media: presence may evolve to Redis hash |
| HybridCache | Platform: new infrastructure. All domains: cache hot-path data |
| MudBlazor + Tailwind | Client Experience: complete UI rewrite using component library |
| Blazor hybrid render mode | Client Experience: render mode decisions per component. All domains: Blazor Server components call services directly |
| FluentValidation | All domains: add validators for input DTOs |
| Scalar (replaces Swashbuckle) | Platform: swap API docs middleware |
| Test projects | All domains: write tests for their services and integrations |
