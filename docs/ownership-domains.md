# Ownership Domains

**Version**: 1.1
**Date**: 2026-02-13

This document defines the five ownership domains for HotBox development. Each domain is owned by a dedicated agent (defined in `.claude/agents/`) responsible for implementation, maintenance, bug fixes, and documentation within their area.

---

## Overview

| Domain | Agent | Primary Scope |
|--------|-------|---------------|
| Platform | `platform` | Infrastructure, config, observability, Docker, database, CI/CD |
| Auth & Security | `auth-security` | Identity, JWT, OAuth, roles, permissions, registration |
| Messaging | `messaging` | Text channels, DMs, message persistence, ChatHub, message search |
| Real-time & Media | `realtime-media` | Voice channels, WebRTC, VoiceSignalingHub, audio |
| Client Experience | `client-experience` | Blazor WASM shell, layout, design system, theming, notifications UI, presence UI, search UI |

---

## Domain Details

### 1. Platform

**Agent**: `.claude/agents/platform.md`

**Owns**:
- Solution structure and project scaffolding
- EF Core DbContext, migrations, multi-provider strategy
- Configuration system (`appsettings.json`, Options classes, env var overrides)
- Observability pipeline (Serilog, OpenTelemetry, Elasticsearch, Seq)
- Docker (Dockerfile, docker-compose files)
- CI/CD pipeline (when added)
- Shared Core layer entities, enums, interfaces
- DI registration patterns (`IServiceCollection` extensions)
- `Program.cs` startup pipeline
- Database seeding infrastructure

**Maintains docs**:
- `docs/technical-spec.md` — Sections 1 (Architecture), 8 (Configuration), 9 (Observability), 10 (Docker)
- `docs/implementation-plan.md` — Phase 1 (Scaffolding), Phase 9 (Polish/Docker)
- `CLAUDE.md` — Tech stack and architecture sections

**Code paths**:
```
src/HotBox.Core/Entities/                 # AppUser.cs, Channel.cs, Message.cs, DirectMessage.cs, Invite.cs, ApiKey.cs, RefreshToken.cs, ServerSettings.cs
src/HotBox.Core/Enums/                    # All enums
src/HotBox.Core/Interfaces/               # All repository and service interfaces
src/HotBox.Core/Options/                  # ServerOptions.cs, DatabaseOptions.cs, JwtOptions.cs, OAuthOptions.cs, IceServerOptions.cs, ObservabilityOptions.cs, AdminSeedOptions.cs, SearchOptions.cs
src/HotBox.Infrastructure/Data/           # DbContext, configurations, migrations
src/HotBox.Infrastructure/DependencyInjection/ # InfrastructureServiceExtensions.cs
src/HotBox.Application/DependencyInjection/    # ApplicationServiceExtensions.cs, ObservabilityExtensions.cs
src/HotBox.Application/Middleware/         # Request logging, etc.
src/HotBox.Application/Program.cs
src/HotBox.Application/appsettings*.json
Dockerfile                                 # Multi-stage build
docker-compose*.yml                        # Compose files at repo root
tests/HotBox.Core.Tests/
tests/HotBox.Infrastructure.Tests/
```

**Coordinates with**:
- All other domains (provides the foundation they build on)
- Auth & Security (database seeding, identity configuration)
- Client Experience (design tokens in CSS, shared DTOs)

---

### 2. Auth & Security

**Agent**: `.claude/agents/auth-security.md`

**Owns**:
- ASP.NET Identity configuration
- JWT token generation, validation, refresh flow
- OAuth provider integration (Google, Microsoft, future providers)
- Registration modes (Open, InviteOnly, Closed)
- Role and permission system
- Admin user and role seeding
- Invite system (generation, validation, revocation)
- Auth-related API endpoints
- Authorization enforcement (API + SignalR hubs)

**Maintains docs**:
- `docs/technical-spec.md` — Section 5 (Authentication and Authorization)
- `docs/implementation-plan.md` — Phase 2 (Auth Backend)

**Code paths**:
```
src/HotBox.Core/Entities/AppUser.cs       # User entity
src/HotBox.Core/Entities/Invite.cs        # Invite entity
src/HotBox.Core/Entities/ApiKey.cs        # API key entity
src/HotBox.Core/Entities/RefreshToken.cs  # Refresh token entity
src/HotBox.Core/Interfaces/ITokenService.cs
src/HotBox.Core/Interfaces/IInviteService.cs
src/HotBox.Core/Options/JwtOptions.cs
src/HotBox.Core/Options/OAuthOptions.cs
src/HotBox.Core/Options/AdminSeedOptions.cs
src/HotBox.Infrastructure/Identity/       # Identity configuration
src/HotBox.Infrastructure/Data/Seeding/   # DatabaseSeeder
src/HotBox.Infrastructure/Services/TokenService.cs
src/HotBox.Infrastructure/Services/InviteService.cs
src/HotBox.Application/Controllers/AuthController.cs
src/HotBox.Application/Controllers/AdminController.cs
src/HotBox.Application/DependencyInjection/AuthenticationExtensions.cs
tests/HotBox.Application.Tests/           # Auth-related tests
```

**Coordinates with**:
- Platform (identity tables in DbContext, seeding infrastructure)
- Client Experience (login/register pages, OAuth buttons, route guards)
- Messaging & Real-time (authorization on SignalR hubs)

---

### 3. Messaging

**Agent**: `.claude/agents/messaging.md`

**Owns**:
- Text channel CRUD (service + controller)
- Message persistence and retrieval (paginated)
- Direct messages (service + controller)
- ChatHub SignalR hub (real-time message delivery, typing indicators)
- Message search (provider-aware FTS: PostgreSQL `tsvector`, MySQL `FULLTEXT`, SQLite FTS5)
- Search controller and search service
- Message-related repository implementations
- Channel and message business logic

**Maintains docs**:
- `docs/technical-spec.md` — Sections 2.3 (API: Channels, Messages, DMs), 2.4 (ChatHub), 4 (Database: Message/Channel/DM entities), 7 (Real-Time: message history, pagination)
- `docs/implementation-plan.md` — Phase 3 (Text Channels), Phase 4 (DMs), Phase 4.5 (Message Search)

**Code paths**:
```
src/HotBox.Core/Entities/Channel.cs
src/HotBox.Core/Entities/Message.cs
src/HotBox.Core/Entities/DirectMessage.cs
src/HotBox.Core/Interfaces/IChannelRepository.cs
src/HotBox.Core/Interfaces/IMessageRepository.cs
src/HotBox.Core/Interfaces/IDirectMessageRepository.cs
src/HotBox.Core/Interfaces/IChannelService.cs
src/HotBox.Core/Interfaces/IMessageService.cs
src/HotBox.Core/Interfaces/IDirectMessageService.cs
src/HotBox.Core/Interfaces/ISearchService.cs
src/HotBox.Core/Options/SearchOptions.cs
src/HotBox.Infrastructure/Repositories/ChannelRepository.cs
src/HotBox.Infrastructure/Repositories/MessageRepository.cs
src/HotBox.Infrastructure/Repositories/DirectMessageRepository.cs
src/HotBox.Infrastructure/Services/ChannelService.cs
src/HotBox.Infrastructure/Services/MessageService.cs
src/HotBox.Infrastructure/Services/DirectMessageService.cs
src/HotBox.Infrastructure/Services/Search/PostgresSearchService.cs
src/HotBox.Infrastructure/Services/Search/MySqlSearchService.cs
src/HotBox.Infrastructure/Services/Search/SqliteSearchService.cs
src/HotBox.Infrastructure/Services/Search/FallbackSearchService.cs
src/HotBox.Application/Controllers/ChannelsController.cs
src/HotBox.Application/Controllers/MessagesController.cs
src/HotBox.Application/Controllers/DirectMessagesController.cs
src/HotBox.Application/Controllers/SearchController.cs
src/HotBox.Application/Hubs/ChatHub.cs
```

**Future ownership**:
- File/media sharing (attachments on messages)
- Message formatting (markdown, emoji reactions)
- Threaded replies

**Coordinates with**:
- Platform (entity definitions in Core, repository interfaces)
- Auth & Security (authorization on channel CRUD, message posting)
- Client Experience (chat UI components consume these services)
- Real-time & Media (shared SignalR patterns, ChatHub presence integration)

---

### 4. Real-time & Media

**Agent**: `.claude/agents/realtime-media.md`

**Owns**:
- VoiceSignalingHub (SignalR hub for WebRTC signaling)
- WebRTC P2P mesh implementation
- JSInterop bridge for WebRTC (`webrtc-interop.js`)
- Audio device management (`audio-interop.js`)
- ICE/STUN/TURN configuration
- Voice channel join/leave/mute/deafen logic
- Voice-related Blazor services and state

**Maintains docs**:
- `docs/technical-spec.md` — Section 6 (Voice Chat: WebRTC Architecture)
- `docs/implementation-plan.md` — Phase 6 (Voice Channels)

**Code paths**:
```
src/HotBox.Core/Options/IceServerOptions.cs
src/HotBox.Application/Hubs/VoiceSignalingHub.cs
src/HotBox.Client/wwwroot/js/webrtc-interop.js
src/HotBox.Client/wwwroot/js/audio-interop.js
src/HotBox.Client/Services/VoiceHubService.cs
src/HotBox.Client/Services/WebRtcService.cs
src/HotBox.Client/Services/VoiceConnectionManager.cs
src/HotBox.Client/State/VoiceState.cs
# Voice UI components TBD (not yet implemented in sidebar)
```

**Future ownership**:
- Push-to-talk
- Screen sharing (`getDisplayMedia()`)
- Video calls
- SFU migration (LiveKit integration)

**Coordinates with**:
- Platform (STUN/TURN config in appsettings, Docker coturn service)
- Messaging (voice channels share the Channel entity with text channels)
- Client Experience (voice UI components will integrate into channel list and top-bar layout)

---

### 5. Client Experience

**Agent**: `.claude/agents/client-experience.md`

**Owns**:
- Blazor WASM project shell (`MainLayout`, `AuthLayout`, `AdminLayout`, routing, `Program.cs`)
- Design system (CSS custom properties/tokens, `app.css`)
- Top-bar navigation and layout
- Channel list, DM list, members panel
- Chat components (message list, message input, channel header)
- User profile UI (popover, edit modal)
- Auth UI (login/register pages, OAuth callback, route guards)
- Admin panel UI (user/channel/invite/settings management)
- Search UI (Ctrl+K overlay, search results)
- Notification display (browser notification JSInterop)
- Presence UI (status dots, connection status)
- Client-side state management (`AppState`, `ChannelState`, `DirectMessageState`, `AuthState`, `PresenceState`, `SearchState`)
- `ApiClient` HTTP wrapper and `ChatHubService`
- Theming (dark mode, future light mode)
- Accessibility

**Maintains docs**:
- `docs/technical-spec.md` — Section 3 (Frontend Architecture), design tokens table
- `docs/implementation-plan.md` — Phase 7 (Auth UI), Phase 8 (Admin Panel)
- UI prototype at `temp/prototype.html`

**Code paths**:
```
src/HotBox.Client/Layout/MainLayout.razor
src/HotBox.Client/Layout/AuthLayout.razor
src/HotBox.Client/Layout/AdminLayout.razor
src/HotBox.Client/Pages/ChannelPage.razor
src/HotBox.Client/Pages/DirectMessagePage.razor
src/HotBox.Client/Pages/DmsPage.razor
src/HotBox.Client/Pages/LoginPage.razor
src/HotBox.Client/Pages/RegisterPage.razor
src/HotBox.Client/Pages/OAuthCallbackPage.razor
src/HotBox.Client/Pages/AdminPage.razor
src/HotBox.Client/Components/Chat/ChannelList.razor
src/HotBox.Client/Components/Chat/ChannelHeader.razor
src/HotBox.Client/Components/Chat/MessageList.razor
src/HotBox.Client/Components/Chat/MessageInput.razor
src/HotBox.Client/Components/Chat/DirectMessageList.razor
src/HotBox.Client/Components/Chat/DirectMessageMessageList.razor
src/HotBox.Client/Components/Chat/DirectMessageInput.razor
src/HotBox.Client/Components/Chat/MembersPanel.razor
src/HotBox.Client/Components/Chat/TypingIndicator.razor
src/HotBox.Client/Components/Admin/AdminUserManagement.razor
src/HotBox.Client/Components/Admin/AdminChannelManagement.razor
src/HotBox.Client/Components/Admin/AdminInviteManagement.razor
src/HotBox.Client/Components/Admin/AdminServerSettings.razor
src/HotBox.Client/Components/Admin/AdminApiKeyManagement.razor
src/HotBox.Client/Components/Profile/UserProfilePopover.razor
src/HotBox.Client/Components/Profile/EditProfileModal.razor
src/HotBox.Client/Components/SearchOverlay.razor
src/HotBox.Client/Components/ConnectionStatus.razor
src/HotBox.Client/Components/NewDmPicker.razor
src/HotBox.Client/Components/GlobalErrorBoundary.razor
src/HotBox.Client/Services/ApiClient.cs
src/HotBox.Client/Services/ChatHubService.cs
src/HotBox.Client/Services/NotificationService.cs
src/HotBox.Client/Services/JwtParser.cs
src/HotBox.Client/State/AppState.cs
src/HotBox.Client/State/ChannelState.cs
src/HotBox.Client/State/DirectMessageState.cs
src/HotBox.Client/State/AuthState.cs
src/HotBox.Client/State/PresenceState.cs
src/HotBox.Client/State/SearchState.cs
src/HotBox.Client/DependencyInjection/ClientServiceExtensions.cs
src/HotBox.Client/wwwroot/css/            # All stylesheets
src/HotBox.Client/wwwroot/js/notification-interop.js
src/HotBox.Client/Program.cs
src/HotBox.Client/_Imports.razor
tests/HotBox.Client.Tests/
```

**Future ownership**:
- Avalonia native desktop client
- Responsive/mobile layout
- Accessibility audit
- Light mode theme

**Coordinates with**:
- All other domains (provides the UI for every feature)
- Messaging (chat components render message data)
- Real-time & Media (voice components in sidebar)
- Auth & Security (auth state, route guards, OAuth buttons)
- Platform (design tokens derived from prototype, shared DTOs)

---

## Interaction Model

```
              Platform
             /    |    \
           Auth  Msg   RT&Media
             \    |    /
          Client Experience
```

- **Platform** provides the foundation (infrastructure, config, observability) — all domains depend on it
- **Auth & Security**, **Messaging**, and **Real-time & Media** are vertical feature domains with clear boundaries
- **Client Experience** stitches everything into a cohesive UI and owns the overall user-facing shell

## Invoking Agents

Agents are defined as sub-agent definitions in `.claude/agents/` and can be invoked by name:

```
/platform         — Infrastructure, config, Docker, observability work
/auth-security    — Identity, auth, permissions work
/messaging        — Text channels, DMs, ChatHub work
/realtime-media   — Voice, WebRTC, audio work
/client-experience — UI, layout, theming, shared components work
```

Each agent has full context about its domain: what it owns, what it doesn't, who to coordinate with, and what docs to maintain.

## Shared Ownership / Boundaries

Some code is touched by multiple domains. Rules of engagement:

| Code | Primary Owner | Others May |
|------|--------------|------------|
| Core entities/interfaces | Platform | Propose changes via Platform |
| `ChatHub.cs` | Messaging | Real-time & Media adds voice signaling patterns |
| Channel list components | Client Experience | Messaging and Real-time add their specific items |
| `appsettings.json` | Platform | Each domain adds their config sections |
| `Program.cs` (Application) | Platform | Each domain adds their DI registrations |
| `Program.cs` (Client) | Client Experience | Each domain adds their client services |
