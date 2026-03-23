# HotBox v2 — Implementation Plan

**Version**: 2.0
**Date**: 2026-03-22
**Status**: Draft

---

## Overview

This plan covers the phased implementation of HotBox v2 from an empty solution to a deployable, tested application. Each phase builds on the previous and ends with a working, demonstrable state.

---

## Phase 1: Foundation & Infrastructure

**Goal**: Empty solution → running app with database, Redis, health checks, and observability.

### 1.1 Solution Scaffolding

- [ ] Create solution: `HotBox.sln`
- [ ] Create projects:
  - `src/HotBox.Core` (Class Library, net9.0)
  - `src/HotBox.Infrastructure` (Class Library, net9.0)
  - `src/HotBox.Application` (ASP.NET Core Web, net9.0)
  - `src/HotBox.Client` (Blazor WebAssembly, net9.0)
  - `tests/HotBox.Core.Tests` (xUnit)
  - `tests/HotBox.Infrastructure.Tests` (xUnit)
  - `tests/HotBox.Application.Tests` (xUnit)
  - `tests/HotBox.Client.Tests` (xUnit + bUnit)
- [ ] Set up project references (Core ← Infrastructure ← Application, Core ← Client ← Application)
- [ ] Install NuGet packages per project (see `stack.md` package manifest)
- [ ] Create `.editorconfig`, `Directory.Build.props` for shared settings
- [ ] Create `global.json` pinning SDK 9.0

### 1.2 Core Layer

- [ ] Define `Options/` classes: `ServerOptions`, `JwtOptions`, `OAuthOptions`, `IceServerOptions`, `ObservabilityOptions`, `AdminSeedOptions`, `SearchOptions`
- [ ] Define `Enums/`: `ChannelType`, `UserRole`, `RegistrationMode`, `UserStatus`, `NotificationType` (all with `[JsonStringEnumConverter]`)
- [ ] Define `Entities/`: `AppUser`, `Channel`, `Message`, `DirectMessage`, `Invite`, `ApiKey`, `RefreshToken`, `ServerSettings`, `Notification`, `UserChannelRead`, `UserNotificationPreference`
- [ ] Define `Interfaces/`: repository and service interfaces
- [ ] Define `Models/`: DTOs, request/response types

### 1.3 Infrastructure Layer

- [ ] Create `HotBoxDbContext` with entity configurations (`IEntityTypeConfiguration<T>`)
- [ ] Configure PostgreSQL provider (Npgsql)
- [ ] Set up ASP.NET Core Identity with `AppUser` and `IdentityRole<Guid>`
- [ ] Create initial EF Core migration
- [ ] Create `DependencyInjection/ServiceCollectionExtensions.cs` for `AddInfrastructure()`
- [ ] Register Redis (`StackExchange.Redis`) connection
- [ ] Implement repository classes (empty bodies — interfaces wired up)

### 1.4 Application Layer

- [ ] Configure `Program.cs`:
  - Serilog bootstrap logger
  - `AddInfrastructure()`, `AddApplicationServices()`
  - HybridCache with Redis L2
  - Health checks (database, Redis)
  - Auto-migrate on startup
  - `MapHealthChecks("/health")`
- [ ] Set up `appsettings.json` and `appsettings.Development.json`
- [ ] Install and configure Scalar (replace Swashbuckle)
- [ ] Create `DependencyInjection/ObservabilityExtensions.cs` (Serilog + OpenTelemetry)

### 1.5 Docker Compose

- [ ] Create `Dockerfile` (multi-stage, non-root)
- [ ] Create `docker-compose.yml` (api, postgres, redis, seq)
- [ ] Create `.env.example`
- [ ] Verify: `docker compose up` → healthy API, `/health` returns OK

### 1.6 Testing Infrastructure

- [ ] Set up Testcontainers PostgreSQL helper
- [ ] Set up Respawn for test database cleanup
- [ ] Set up `WebApplicationFactory<Program>` for integration tests
- [ ] Write first test: health check returns 200

**Exit criteria**: `docker compose up` starts the full stack. `/health` returns healthy. Seq shows structured logs. OpenTelemetry traces visible.

---

## Phase 2: Authentication & Authorization

**Goal**: Users can register, login, and authenticate via JWT. OAuth and API keys work.

### 2.1 Auth Service Layer

- [ ] Implement `TokenService` (JWT generation, refresh token management)
- [ ] Implement registration logic (respecting `RegistrationMode`)
- [ ] Implement login with password verification and lockout
- [ ] Implement refresh token rotation (HttpOnly Secure cookie)
- [ ] Implement `DatabaseSeeder` for admin seed on first run

### 2.2 FluentValidation

- [ ] Create validators: `RegisterUserValidator`, `LoginValidator`
- [ ] Wire up `AddFluentValidationAutoValidation()` in DI
- [ ] Implement service-layer validation pattern for Blazor Server calls

### 2.3 API Controllers

- [ ] `AuthController` — register, login, refresh, logout, me
- [ ] JWT bearer authentication middleware
- [ ] API key authentication handler (`X-Api-Key` header)
- [ ] Role-based authorization policies (Admin, Moderator, Member)

### 2.4 OAuth

- [ ] Google OAuth configuration
- [ ] Microsoft OAuth configuration
- [ ] Discord OAuth configuration
- [ ] External login flow (create-or-link user)

### 2.5 Blazor Auth Pages

- [ ] `Auth/Login.razor` (Static SSR)
- [ ] `Auth/Register.razor` (Static SSR)
- [ ] Auth state provider for Blazor Server circuits

### 2.6 Tests

- [ ] Unit: token generation, password validation
- [ ] Integration: register → login → access protected endpoint
- [ ] Integration: refresh token rotation
- [ ] Integration: registration mode enforcement

**Exit criteria**: Full auth flow works. Admin seeded on first run. OAuth configurable. API keys functional.

---

## Phase 3: Text Channels & Real-Time Messaging

**Goal**: Users can create channels, send messages, and see them in real-time across multiple instances.

### 3.1 Service Layer

- [ ] Implement `ChannelService` + `ChannelRepository` (CRUD, ordering)
- [ ] Implement `MessageService` + `MessageRepository` (send, edit, delete, cursor pagination)
- [ ] Implement `ReadStateService` (track per-user, per-channel read position)
- [ ] Implement `SearchService` (PostgreSQL tsvector/tsquery, GIN index)
- [ ] FluentValidation: `CreateChannelValidator`, `SendMessageValidator`

### 3.2 Caching

- [ ] Cache channel list via HybridCache (invalidate on CRUD)
- [ ] Cache channel details via HybridCache

### 3.3 SignalR ChatHub

- [ ] Configure SignalR with Redis backplane
- [ ] Implement ChatHub server methods: `SendMessage`, `EditMessage`, `DeleteMessage`, `JoinChannel`, `LeaveChannel`, `StartTyping`, `StopTyping`, `MarkChannelRead`
- [ ] Implement ChatHub client broadcasts: `ReceiveMessage`, `MessageEdited`, `MessageDeleted`, `UserTyping`, `UnreadCountUpdated`
- [ ] Group management (SignalR groups per channel)

### 3.4 API Controllers

- [ ] `ChannelsController` — CRUD
- [ ] `MessagesController` — list (cursor paginated), send, edit, delete
- [ ] `SearchController` — full-text search

### 3.5 Blazor UI

- [ ] `MainLayout.razor` (InteractiveServer) — three-panel Discord-style layout using MudBlazor
- [ ] `ChannelSidebar.razor` — channel list with MudList, unread badges
- [ ] `Chat.razor` — message list with infinite scroll, typing indicators
- [ ] `MessageComposer.razor` (InteractiveWebAssembly) — input with send button
- [ ] `TopBar.razor` — channel name, search trigger, user menu
- [ ] MudBlazor theming (dark mode, custom palette)

### 3.6 Tests

- [ ] Unit: message validation, channel service logic
- [ ] Integration: send message via hub → persisted → broadcast to subscribers
- [ ] Integration: cursor pagination
- [ ] Integration: full-text search returns ranked results
- [ ] Integration: Redis backplane — verify message delivery across two hub instances

**Exit criteria**: Real-time messaging works. Messages persist. Search returns results. Unread tracking functional. Redis backplane verified.

---

## Phase 4: Direct Messages

**Goal**: Users can send 1-on-1 direct messages.

### 4.1 Service Layer

- [ ] Implement `DirectMessageService` + `DirectMessageRepository`
- [ ] Conversation list (distinct partners, last message preview)
- [ ] Cursor-based pagination per conversation
- [ ] FluentValidation: `SendDirectMessageValidator`

### 4.2 SignalR

- [ ] Add `SendDirectMessage` to ChatHub
- [ ] `ReceiveDirectMessage` client event
- [ ] DM-specific notifications

### 4.3 API

- [ ] `DirectMessagesController` — conversations list, messages, send

### 4.4 Blazor UI

- [ ] `DirectMessages.razor` page
- [ ] DM conversation list in sidebar
- [ ] DM chat view (reuse message components)

### 4.5 Tests

- [ ] Integration: send DM → recipient receives via SignalR
- [ ] Integration: conversation list ordering

**Exit criteria**: 1-on-1 DMs work end-to-end.

---

## Phase 5: Presence & Notifications

**Goal**: Online/idle/offline status tracking and push notifications.

### 5.1 Presence

- [ ] Implement `PresenceService` (in-memory `ConcurrentDictionary`)
- [ ] Multi-connection tracking (user online until all tabs close)
- [ ] Grace period (30s) to prevent flicker on refresh
- [ ] Idle detection (5 minutes)
- [ ] `UserStatusChanged` SignalR broadcast
- [ ] DoNotDisturb status (user-set)

### 5.2 Notifications

- [ ] Implement `NotificationService` + `NotificationRepository`
- [ ] `UserNotificationPreference` — mute channels, DND toggle
- [ ] Persist notifications to database
- [ ] Push via SignalR: `ReceiveNotification`
- [ ] MudSnackbar toast display
- [ ] Browser Notification API integration (permission request, display)

### 5.3 Blazor UI

- [ ] Status indicators on user avatars (MudAvatar + colored dot)
- [ ] Notification bell with unread count (MudBadge)
- [ ] Notification panel/dropdown
- [ ] User status selector (Online, Idle, DND)

### 5.4 Tests

- [ ] Integration: user connects → Online broadcast, disconnects → grace period → Offline
- [ ] Integration: notification created → delivered via SignalR → persisted

**Exit criteria**: Presence indicators work. Notifications delivered and persisted. DND suppresses notifications.

---

## Phase 6: Voice Channels

**Goal**: Drop-in/drop-out voice chat via WebRTC P2P mesh.

### 6.1 VoiceSignalingHub

- [ ] Implement `JoinVoiceChannel`, `LeaveVoiceChannel`
- [ ] Implement `SendOffer`, `SendAnswer`, `SendIceCandidate`
- [ ] Implement server-to-client broadcasts: `UserJoinedVoice`, `UserLeftVoice`, `ReceiveOffer`, `ReceiveAnswer`, `ReceiveIceCandidate`, `VoiceChannelUsers`
- [ ] Track voice channel participants in memory

### 6.2 Client-Side WebRTC

- [ ] JavaScript interop for `RTCPeerConnection`
- [ ] `VoiceControls.razor` (InteractiveWebAssembly) — join/leave, mute/unmute
- [ ] Peer connection management (create offer for each existing user on join)
- [ ] ICE candidate exchange
- [ ] Audio stream handling (getUserMedia, addTrack)

### 6.3 Blazor UI

- [ ] Voice channel indicators in sidebar (participant count, active speaker)
- [ ] Voice participant list overlay
- [ ] Mute/deafen controls

### 6.4 Tests

- [ ] Integration: signaling flow (join → offer → answer → ICE exchange)
- [ ] Unit: peer tracking logic

**Exit criteria**: Users can join voice channels and talk to each other via WebRTC.

---

## Phase 7: Admin Panel

**Goal**: Server administration UI.

### 7.1 Admin Pages

- [ ] `Admin/Dashboard.razor` — server stats (users, channels, messages)
- [ ] `Admin/Users.razor` — user list, role management, ban/unban
- [ ] `Admin/Channels.razor` — channel management
- [ ] `Admin/Invites.razor` — create/revoke invites, usage tracking
- [ ] `Admin/Settings.razor` — server name, registration mode
- [ ] `Admin/ApiKeys.razor` — create/list/revoke API keys

### 7.2 API Controllers

- [ ] `AdminController` — server settings, user management
- [ ] `InvitesController` — CRUD
- [ ] `ApiKeysController` — CRUD

### 7.3 Authorization

- [ ] `[Authorize(Roles = "Admin")]` on admin pages and endpoints
- [ ] `[Authorize(Roles = "Admin,Moderator")]` for moderation actions

### 7.4 Tests

- [ ] Integration: admin can change roles, non-admin gets 403
- [ ] Integration: invite creation → validation → use → exhaustion

**Exit criteria**: Full admin panel functional. Role-based access enforced.

---

## Phase 8: User Profiles

**Goal**: User profile viewing and editing.

### 8.1 Profile Features

- [ ] Profile fields: DisplayName, Bio, Pronouns, CustomStatus, AvatarUrl
- [ ] Profile view popover (click avatar → profile card)
- [ ] Profile edit modal (own profile only)
- [ ] User list page with search/filter

### 8.2 API

- [ ] `UsersController` — get profile, update own profile, list users

### 8.3 Caching

- [ ] Cache user profiles via HybridCache (invalidate on update)

### 8.4 Tests

- [ ] Integration: update profile → cache invalidated → new data returned

**Exit criteria**: Users can view and edit profiles.

---

## Phase 9: Polish & Hardening

**Goal**: Production-ready quality.

### 9.1 Resilience

- [ ] Polly policies on OAuth `HttpClient` registrations
- [ ] Circuit breaker for external services
- [ ] Rate limiting on auth endpoints (built-in .NET 9 rate limiter)

### 9.2 Security

- [ ] CSRF protection for Blazor Server forms
- [ ] CSP headers
- [ ] Input sanitization (XSS prevention)
- [ ] Secure cookie configuration
- [ ] HTTPS enforcement

### 9.3 Performance

- [ ] Review and tune HybridCache TTLs
- [ ] Database index verification (`EXPLAIN ANALYZE` on key queries)
- [ ] Static asset caching headers
- [ ] Blazor WASM compression (Brotli)

### 9.4 Deployment

- [ ] Finalize Dockerfile
- [ ] Finalize docker-compose.yml (prod)
- [ ] Bare-metal deployment guide (systemd + nginx)
- [ ] Backup/restore documentation
- [ ] `.env.example` with all required variables

### 9.5 Documentation

- [ ] Update README
- [ ] Configuration reference
- [ ] Deployment guides

**Exit criteria**: App is production-ready. Docker and bare-metal deployments verified. Documentation complete.

---

## Phase Summary

| Phase | Description | Key Deliverables |
|-------|------------|-----------------|
| 1 | Foundation & Infrastructure | Solution, DB, Redis, health checks, observability, Docker |
| 2 | Authentication & Authorization | JWT, OAuth, API keys, roles, registration modes |
| 3 | Text Channels & Real-Time | Channels, messages, SignalR + Redis backplane, search, Blazor UI |
| 4 | Direct Messages | 1-on-1 DMs, conversation list |
| 5 | Presence & Notifications | Online/idle/offline, push notifications, DND |
| 6 | Voice Channels | WebRTC P2P, signaling hub, voice controls |
| 7 | Admin Panel | Server settings, user/channel/invite management |
| 8 | User Profiles | Profile view/edit, caching |
| 9 | Polish & Hardening | Security, performance, deployment, docs |
