# HotBox v2 — Technical Specification

**Version**: 2.0
**Date**: 2026-03-22
**Status**: Draft

---

## 1. System Architecture Overview

HotBox v2 is a self-hosted real-time communication platform built on ASP.NET Core 9, Blazor Web (hybrid render modes), SignalR with Redis backplane, and WebRTC for voice. The architecture follows a three-layer pattern: Core (domain) -> Infrastructure (data access, caching) -> Application (UI/API).

### 1.1 High-Level Architecture

```
+------------------------------------------------------------------+
|                         Docker Host                               |
|                                                                   |
|  +------------------------------------------------------------+  |
|  |                  ASP.NET Core 9 Server                      |  |
|  |                                                              |  |
|  |  +------------------+  +------------------+  +------------+  |  |
|  |  | Blazor Server    |  | SignalR Hubs     |  | REST API   |  |  |
|  |  | (Interactive SSR)|  | (ChatHub,        |  | Controllers|  |  |
|  |  |                  |  |  VoiceSignaling) |  | + Scalar   |  |  |
|  |  +--------+---------+  +--------+---------+  +-----+------+  |  |
|  |           |                     |                   |         |  |
|  |  +--------v---------------------v-------------------v------+  |  |
|  |  |              Application Layer                          |  |  |
|  |  |  (Services, FluentValidation, Polly, HybridCache)      |  |  |
|  |  +----------------------------+----------------------------+  |  |
|  |                               |                               |  |
|  |  +----------------------------v----------------------------+  |  |
|  |  |              Infrastructure Layer                       |  |  |
|  |  |  (EF Core 9, Identity, Redis, Repositories)            |  |  |
|  |  +----------------------------+----------------------------+  |  |
|  |                               |                               |  |
|  |  +----------------------------v----------------------------+  |  |
|  |  |              Core Layer                                 |  |  |
|  |  |  (Entities, Interfaces, Enums, Options, Value Objects)  |  |  |
|  |  +---------------------------------------------------------+  |  |
|  +------------------------------------------------------------+  |
|       |              |               |              |             |
|  +----v----+  +------v------+  +-----v-----+  +----v---------+  |
|  |PostgreSQL|  |    Redis    |  |    Seq    |  | Elasticsearch|  |
|  |  16+     |  |  7 Alpine   |  | (dev logs)|  |  (prod APM)  |  |
|  +---------+  +-------------+  +-----------+  +--------------+  |
+------------------------------------------------------------------+
```

### 1.2 Key Architectural Changes from v1

| Aspect | v1 | v2 | Rationale |
|--------|-----|-----|-----------|
| **Blazor mode** | Pure WASM | Hybrid (Server + WASM) | Faster load, direct service access, reduced API surface |
| **Database** | Multi-provider (PG, MySQL, SQLite) | PostgreSQL only | Eliminates FTS complexity, testing matrix, conditional migrations |
| **Caching** | None | HybridCache (memory + Redis) | Performance for channel lists, user profiles, search results |
| **SignalR** | Single-server only | Redis backplane | Multi-instance ready from day one |
| **Validation** | None | FluentValidation pipeline | Consistent input validation across all endpoints |
| **Resilience** | None | Polly v8 | Retry/circuit-breaker for OAuth, webhooks, external calls |
| **API docs** | Swashbuckle | Scalar | Swashbuckle unmaintained; Scalar has modern UI, OpenAPI 3.1 |
| **Component library** | Hand-rolled CSS | MudBlazor + Tailwind utilities | Eliminates 4,500 lines of custom CSS, adds theming + accessibility |
| **Runtime** | .NET 8 | .NET 9 | HybridCache, improved Blazor, better perf |
| **Testing** | None | xUnit + Testcontainers + Respawn | Tests from day one |

### 1.3 Data Flow

```
User Browser
    |
    |-- HTTPS ---------> Blazor Server (Interactive SSR — most pages)
    |-- HTTPS ---------> Blazor WASM (downloaded for voice controls, composer)
    |
    |-- WSS (SignalR) --> ChatHub (text messages, presence, notifications)
    |-- WSS (SignalR) --> VoiceSignalingHub (WebRTC signaling: SDP, ICE)
    |
    |-- WebRTC P2P -----> Other user browsers (voice audio streams)
    |
    |-- HTTPS REST -----> API Controllers (external consumers, MCP agents)
```

### 1.4 Component Relationships

| Component | Communicates With | Protocol |
|-----------|------------------|----------|
| Blazor Server components | Application services | Direct method call (same process) |
| Blazor WASM components | ASP.NET Core Server | HTTPS, WSS (SignalR) |
| Blazor WASM components | Other clients | WebRTC (P2P voice) |
| ASP.NET Core Server | PostgreSQL | EF Core (TCP) |
| ASP.NET Core Server | Redis | StackExchange.Redis (TCP) |
| ASP.NET Core Server | Seq (dev) | HTTP (Serilog sink) |
| ASP.NET Core Server | Elasticsearch (prod) | OTLP / HTTP |
| SignalR Hubs | All connected clients | WebSocket (via Redis backplane) |

---

## 2. Backend Architecture

### 2.1 Solution Structure

```
HotBox.sln
│
├── src/
│   ├── HotBox.Core/                        # Domain layer (zero dependencies)
│   │   ├── Entities/
│   │   │   ├── AppUser.cs
│   │   │   ├── Channel.cs
│   │   │   ├── Message.cs
│   │   │   ├── DirectMessage.cs
│   │   │   ├── Invite.cs
│   │   │   ├── ApiKey.cs
│   │   │   ├── RefreshToken.cs
│   │   │   ├── ServerSettings.cs
│   │   │   ├── Notification.cs
│   │   │   ├── UserChannelRead.cs
│   │   │   └── UserNotificationPreference.cs
│   │   ├── Enums/
│   │   │   ├── ChannelType.cs              # [JsonStringEnumConverter]
│   │   │   ├── UserRole.cs
│   │   │   ├── RegistrationMode.cs
│   │   │   ├── UserStatus.cs
│   │   │   └── NotificationType.cs
│   │   ├── Interfaces/
│   │   │   ├── IChannelRepository.cs
│   │   │   ├── IChannelService.cs
│   │   │   ├── IMessageRepository.cs
│   │   │   ├── IMessageService.cs
│   │   │   ├── IDirectMessageRepository.cs
│   │   │   ├── IDirectMessageService.cs
│   │   │   ├── IInviteRepository.cs
│   │   │   ├── IInviteService.cs
│   │   │   ├── INotificationRepository.cs
│   │   │   ├── INotificationService.cs
│   │   │   ├── IPresenceService.cs
│   │   │   ├── IReadStateService.cs
│   │   │   ├── ISearchService.cs
│   │   │   ├── IServerSettingsService.cs
│   │   │   └── ITokenService.cs
│   │   ├── Models/                         # DTOs, search models, API contracts
│   │   └── Options/                        # IOptions<T> configuration POCOs
│   │       ├── ServerOptions.cs
│   │       ├── JwtOptions.cs
│   │       ├── OAuthOptions.cs
│   │       ├── DatabaseOptions.cs
│   │       ├── IceServerOptions.cs
│   │       └── ObservabilityOptions.cs
│   │
│   ├── HotBox.Infrastructure/              # Data access & external services
│   │   ├── Data/
│   │   │   ├── HotBoxDbContext.cs
│   │   │   ├── Migrations/
│   │   │   ├── Seeding/
│   │   │   │   └── DatabaseSeeder.cs
│   │   │   └── Configurations/             # EF Core entity configs (IEntityTypeConfiguration<T>)
│   │   ├── Repositories/
│   │   │   ├── ChannelRepository.cs
│   │   │   ├── MessageRepository.cs
│   │   │   ├── DirectMessageRepository.cs
│   │   │   ├── InviteRepository.cs
│   │   │   └── NotificationRepository.cs
│   │   ├── Services/
│   │   │   ├── ChannelService.cs
│   │   │   ├── MessageService.cs
│   │   │   ├── DirectMessageService.cs
│   │   │   ├── InviteService.cs
│   │   │   ├── NotificationService.cs
│   │   │   ├── PresenceService.cs
│   │   │   ├── ReadStateService.cs
│   │   │   ├── SearchService.cs            # PostgreSQL tsvector only
│   │   │   ├── ServerSettingsService.cs
│   │   │   └── TokenService.cs
│   │   ├── Validation/                     # FluentValidation validators
│   │   │   ├── CreateChannelValidator.cs
│   │   │   ├── SendMessageValidator.cs
│   │   │   ├── RegisterUserValidator.cs
│   │   │   └── ...
│   │   └── DependencyInjection/
│   │       └── ServiceCollectionExtensions.cs
│   │
│   ├── HotBox.Application/                 # ASP.NET Core host + Blazor Server
│   │   ├── Program.cs
│   │   ├── Components/                     # Blazor Server components (Interactive SSR)
│   │   │   ├── App.razor                   # Root component
│   │   │   ├── Routes.razor
│   │   │   ├── Layout/
│   │   │   │   ├── MainLayout.razor
│   │   │   │   ├── TopBar.razor
│   │   │   │   ├── ChannelSidebar.razor
│   │   │   │   └── MembersList.razor
│   │   │   └── Pages/
│   │   │       ├── Chat.razor
│   │   │       ├── DirectMessages.razor
│   │   │       ├── Admin/
│   │   │       ├── Auth/
│   │   │       └── Settings/
│   │   ├── Controllers/                    # REST API for external consumers
│   │   │   ├── AuthController.cs
│   │   │   ├── ChannelsController.cs
│   │   │   ├── MessagesController.cs
│   │   │   ├── DirectMessagesController.cs
│   │   │   ├── UsersController.cs
│   │   │   ├── InvitesController.cs
│   │   │   ├── AdminController.cs
│   │   │   └── SearchController.cs
│   │   ├── Hubs/
│   │   │   ├── ChatHub.cs
│   │   │   └── VoiceSignalingHub.cs
│   │   ├── Middleware/
│   │   │   └── AgentPresenceMiddleware.cs
│   │   ├── DependencyInjection/
│   │   │   ├── ApplicationServiceExtensions.cs
│   │   │   └── ObservabilityExtensions.cs
│   │   └── wwwroot/                        # Static assets
│   │
│   └── HotBox.Client/                      # Blazor WASM (opt-in components)
│       ├── Components/                     # WASM-only interactive components
│       │   ├── VoiceControls.razor
│       │   ├── MessageComposer.razor
│       │   └── ...
│       ├── Services/                       # Client-side services (SignalR, HTTP)
│       └── wwwroot/
│
├── tests/
│   ├── HotBox.Core.Tests/
│   ├── HotBox.Infrastructure.Tests/
│   ├── HotBox.Application.Tests/
│   └── HotBox.Client.Tests/
│
└── HotBox.Mcp/                             # MCP server for agent tooling
    ├── Program.cs
    ├── Tools/
    └── Clients/
```

### 2.2 Project Dependencies

```
HotBox.Core          → (none)
HotBox.Infrastructure → HotBox.Core
HotBox.Client        → HotBox.Core
HotBox.Application   → HotBox.Core, HotBox.Infrastructure, HotBox.Client
HotBox.Mcp           → HotBox.Core (via HTTP client, not project ref to Application)
```

### 2.3 Dependency Injection Registration

Each layer registers its own services via `IServiceCollection` extension methods:

```csharp
// Program.cs
builder.Services.AddInfrastructure(builder.Configuration);    // EF Core, repos, services, Redis, validators
builder.Services.AddApplicationServices(builder.Configuration); // Auth, SignalR, Polly, health checks
builder.Services.AddHybridCache(options => { ... });          // HybridCache with Redis L2
```

### 2.4 API Endpoints

REST controllers serve external consumers (MCP agents, bots, third-party integrations). Blazor Server components call services directly — they do not go through REST APIs.

#### Authentication

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| POST | `/api/auth/register` | Register new user | Anonymous |
| POST | `/api/auth/login` | Login, returns JWT + refresh cookie | Anonymous |
| POST | `/api/auth/refresh` | Refresh access token | Cookie |
| POST | `/api/auth/logout` | Revoke refresh token | Authenticated |
| GET | `/api/auth/me` | Current user profile | Authenticated |
| POST | `/api/auth/external/{provider}` | Initiate OAuth flow | Anonymous |

#### Channels

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| GET | `/api/channels` | List all channels | Authenticated |
| POST | `/api/channels` | Create channel | Admin |
| GET | `/api/channels/{id}` | Get channel details | Authenticated |
| PUT | `/api/channels/{id}` | Update channel | Admin |
| DELETE | `/api/channels/{id}` | Delete channel | Admin |

#### Messages

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| GET | `/api/channels/{id}/messages` | Get messages (cursor paginated) | Authenticated |
| POST | `/api/channels/{id}/messages` | Send message | Authenticated |
| PUT | `/api/messages/{id}` | Edit message | Owner |
| DELETE | `/api/messages/{id}` | Delete message | Owner/Moderator |

#### Direct Messages

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| GET | `/api/dm/conversations` | List DM conversations | Authenticated |
| GET | `/api/dm/{userId}/messages` | Get DMs with user (cursor paginated) | Authenticated |
| POST | `/api/dm/{userId}/messages` | Send DM | Authenticated |

#### Search

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| GET | `/api/search?q={query}` | Full-text search messages | Authenticated |
| POST | `/api/admin/search/reindex` | Rebuild search indexes | Admin |

#### Users

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| GET | `/api/users` | List users | Authenticated |
| GET | `/api/users/{id}` | Get user profile | Authenticated |
| PUT | `/api/users/me` | Update own profile | Authenticated |

#### Admin

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| GET | `/api/admin/settings` | Get server settings | Admin |
| PUT | `/api/admin/settings` | Update server settings | Admin |
| POST | `/api/admin/users/{id}/role` | Change user role | Admin |
| POST | `/api/admin/users/{id}/ban` | Ban user | Admin/Moderator |

#### Invites

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| GET | `/api/invites` | List active invites | Admin |
| POST | `/api/invites` | Create invite code | Admin |
| DELETE | `/api/invites/{id}` | Revoke invite | Admin |
| GET | `/api/invites/{code}/validate` | Validate invite code | Anonymous |

#### API Keys

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| POST | `/api/apikeys` | Create API key | Admin |
| GET | `/api/apikeys` | List API keys (masked) | Admin |
| DELETE | `/api/apikeys/{id}` | Revoke API key | Admin |

#### Health

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| GET | `/health` | Health check (DB, Redis, SignalR) | Anonymous |

### 2.5 SignalR Hubs

#### ChatHub (`/hubs/chat`)

**Client → Server:**
| Method | Parameters | Description |
|--------|-----------|-------------|
| `SendMessage` | channelId, content | Send message to channel |
| `EditMessage` | messageId, newContent | Edit own message |
| `DeleteMessage` | messageId | Delete message |
| `JoinChannel` | channelId | Subscribe to channel updates |
| `LeaveChannel` | channelId | Unsubscribe from channel |
| `StartTyping` | channelId | Broadcast typing indicator |
| `StopTyping` | channelId | Clear typing indicator |
| `UpdateStatus` | status | Set presence status |
| `SendDirectMessage` | recipientId, content | Send DM |
| `MarkChannelRead` | channelId, messageId | Update read state |

**Server → Client:**
| Method | Parameters | Description |
|--------|-----------|-------------|
| `ReceiveMessage` | message | New message in subscribed channel |
| `MessageEdited` | messageId, newContent, editedAt | Message was edited |
| `MessageDeleted` | messageId | Message was deleted |
| `UserTyping` | channelId, userId, displayName | User is typing |
| `UserStoppedTyping` | channelId, userId | User stopped typing |
| `UserStatusChanged` | userId, displayName, status, isAgent | Presence update |
| `ReceiveDirectMessage` | message | Incoming DM |
| `ReceiveNotification` | notification | Push notification |
| `UnreadCountUpdated` | channelId, count | Unread badge update |

**SignalR Configuration:**
```csharp
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("HotBox");
    });
```

| Setting | Value |
|---------|-------|
| Max message size | 64 KB |
| Keep-alive interval | 15 seconds |
| Client timeout | 30 seconds |
| Backplane | Redis (StackExchange.Redis) |

#### VoiceSignalingHub (`/hubs/voice`)

**Client → Server:**
| Method | Parameters | Description |
|--------|-----------|-------------|
| `JoinVoiceChannel` | channelId | Enter voice channel |
| `LeaveVoiceChannel` | channelId | Exit voice channel |
| `SendOffer` | targetUserId, sdp | WebRTC SDP offer |
| `SendAnswer` | targetUserId, sdp | WebRTC SDP answer |
| `SendIceCandidate` | targetUserId, candidate | ICE candidate exchange |

**Server → Client:**
| Method | Parameters | Description |
|--------|-----------|-------------|
| `UserJoinedVoice` | channelId, userId, displayName | User entered voice |
| `UserLeftVoice` | channelId, userId | User left voice |
| `ReceiveOffer` | fromUserId, sdp | Incoming SDP offer |
| `ReceiveAnswer` | fromUserId, sdp | Incoming SDP answer |
| `ReceiveIceCandidate` | fromUserId, candidate | Incoming ICE candidate |
| `VoiceChannelUsers` | channelId, users[] | Current voice participants |

### 2.6 Validation Pipeline

FluentValidation integrates with both the API pipeline and Blazor Server service calls:

```csharp
// API: automatic via MVC filter
services.AddFluentValidationAutoValidation();

// Service layer: explicit validation for Blazor Server calls
public class MessageService : IMessageService
{
    private readonly IValidator<SendMessageRequest> _validator;

    public async Task<Result<Message>> SendAsync(SendMessageRequest request)
    {
        var validation = await _validator.ValidateAsync(request);
        if (!validation.IsValid)
            return Result.Failure<Message>(validation.Errors);
        // ...
    }
}
```

### 2.7 Resilience (Polly)

```csharp
// Outbound HTTP clients (OAuth providers, webhooks)
builder.Services.AddHttpClient("OAuth")
    .AddPolicyHandler(Policy.WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
    .AddPolicyHandler(Policy.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
```

---

## 3. Frontend Architecture

### 3.1 Render Mode Strategy

```
App.razor (Static SSR — shell, <head>, routes)
│
├── MainLayout.razor (InteractiveServer)
│   ├── TopBar.razor
│   ├── ChannelSidebar.razor
│   ├── Chat.razor
│   │   ├── MessageList.razor (InteractiveServer — SignalR subscription)
│   │   └── MessageComposer.razor (InteractiveWebAssembly — low-latency input)
│   ├── MembersList.razor
│   └── VoiceControls.razor (InteractiveWebAssembly — WebRTC APIs)
│
├── Auth/Login.razor (Static SSR)
├── Auth/Register.razor (Static SSR)
└── Admin/*.razor (InteractiveServer)
```

### 3.2 Component Library: MudBlazor

MudBlazor provides the foundational UI components. Custom HotBox components build on top of MudBlazor primitives.

| MudBlazor Component | HotBox Usage |
|---------------------|-------------|
| `MudLayout` / `MudDrawer` | App shell, sidebar |
| `MudAppBar` | Top bar |
| `MudList` / `MudListItem` | Channel list, member list |
| `MudTextField` | Message input, search, forms |
| `MudButton` / `MudIconButton` | Actions, voice controls |
| `MudDialog` | Modals (create channel, invite, settings) |
| `MudSnackbar` | Toast notifications |
| `MudAvatar` | User avatars with status indicators |
| `MudChip` | Role badges, status tags |
| `MudMenu` | Context menus, user dropdown |
| `MudAutocomplete` | User mentions, search suggestions |
| `MudBadge` | Unread message counts |

### 3.3 Theming

```csharp
// MudBlazor custom theme (dark mode first)
var hotboxTheme = new MudTheme
{
    PaletteLight = new PaletteLight { ... },
    PaletteDark = new PaletteDark
    {
        Black = "#0a0a0f",
        Background = "#0d0d14",
        Surface = "#14141e",
        DrawerBackground = "#101018",
        AppbarBackground = "#14141e",
        Primary = "#6366f1",        // Indigo accent
        Secondary = "#8b5cf6",
        Tertiary = "#06b6d4",
        Info = "#3b82f6",
        Success = "#22c55e",
        Warning = "#f59e0b",
        Error = "#ef4444",
        TextPrimary = "#e2e8f0",
        TextSecondary = "#94a3b8",
    },
    Typography = new Typography
    {
        Default = new Default { FontFamily = new[] { "DM Sans", "system-ui", "sans-serif" } },
        Code = new Body1 { FontFamily = new[] { "JetBrains Mono", "monospace" } },
    },
    LayoutProperties = new LayoutProperties
    {
        DefaultBorderRadius = "8px",
    }
};
```

### 3.4 Tailwind Integration

Tailwind is used only for utility classes that complement MudBlazor — spacing adjustments, custom text styles, layout tweaks. It is **not** used for component styling (that's MudBlazor's job).

```html
<!-- Good: Tailwind for layout utilities -->
<div class="flex gap-2 items-center px-3">
    <MudAvatar Size="Size.Small">@user.Initials</MudAvatar>
    <span class="text-sm truncate">@user.DisplayName</span>
</div>

<!-- Bad: Don't rebuild MudBlazor components with Tailwind -->
<!-- <button class="bg-indigo-600 px-4 py-2 rounded-lg ..."> -->
```

### 3.5 State Management

Blazor Server components have direct access to scoped services. State flows through:

1. **Scoped services** — Per-circuit state (current user, active channel, unread counts)
2. **Cascading parameters** — Theme, auth state, layout context
3. **SignalR events** — Real-time updates trigger `StateHasChanged()` on subscribed components

No external state management library (Fluxor, etc.) — the service + event pattern from v1 is retained.

---

## 4. Database Design

### 4.1 Entity Model

| Entity | Primary Key | Description |
|--------|-------------|-------------|
| `AppUser` | `Guid Id` | Extends `IdentityUser<Guid>` with DisplayName, AvatarUrl, Bio, Pronouns, CustomStatus, IsAgent, CreatedAt |
| `Channel` | `Guid Id` | Name, Description, ChannelType (Text/Voice), SortOrder, CreatedAt |
| `Message` | `Guid Id` | Content, ChannelId (FK), UserId (FK), CreatedAt, EditedAt, SearchVector (tsvector) |
| `DirectMessage` | `Guid Id` | Content, SenderId (FK), RecipientId (FK), CreatedAt, EditedAt |
| `Invite` | `Guid Id` | Code (unique), CreatedById (FK), UsedById (FK), ExpiresAt, MaxUses, UseCount |
| `ApiKey` | `Guid Id` | KeyHash (SHA-256), KeyPrefix (`hb_`), Name, UserId (FK), CreatedAt, ExpiresAt, RevokedAt |
| `RefreshToken` | `Guid Id` | TokenHash, UserId (FK), ExpiresAt, CreatedAt, RevokedAt |
| `ServerSettings` | `Guid Id` | ServerName, RegistrationMode, singleton row |
| `Notification` | `Guid Id` | UserId (FK), Type, Title, Body, IsRead, RelatedEntityId, CreatedAt |
| `UserChannelRead` | composite | UserId + ChannelId, LastReadMessageId, UpdatedAt |
| `UserNotificationPreference` | `Guid Id` | UserId, ChannelId (nullable), MuteUntil, DndEnabled |

### 4.2 Entity Relationships

```
AppUser (1) ──── (*) Message           (user sends messages)
AppUser (1) ──── (*) DirectMessage     (as sender)
AppUser (1) ──── (*) DirectMessage     (as recipient)
AppUser (1) ──── (*) Invite            (created by)
AppUser (1) ──── (*) ApiKey            (owns keys)
AppUser (1) ──── (*) RefreshToken      (active sessions)
AppUser (1) ──── (*) Notification      (receives notifications)
AppUser (1) ──── (*) UserChannelRead   (read states)
AppUser (1) ──── (*) UserNotificationPreference
Channel (1) ──── (*) Message           (channel contains messages)
Channel (1) ──── (*) UserChannelRead   (per-channel read state)
```

### 4.3 PostgreSQL-Only Simplifications

Without multi-provider support, the data layer is simplified:

```csharp
// v1: provider-conditional FTS
if (provider == "postgresql")
    builder.HasGeneratedTsVectorColumn(...);
else if (provider == "mysql")
    builder.HasIndex(...).HasAnnotation("MySQL:FullTextIndex", true);
else // sqlite
    // separate FTS5 virtual table...

// v2: PostgreSQL only
builder.Entity<Message>(entity =>
{
    entity.HasGeneratedTsVectorColumn(
        m => m.SearchVector,
        "english",
        m => m.Content);

    entity.HasIndex(m => m.SearchVector)
        .HasMethod("GIN");
});
```

### 4.4 Indexing Strategy

| Table | Column(s) | Index Type | Purpose |
|-------|----------|------------|---------|
| `Messages` | `ChannelId, CreatedAt` | B-tree (composite) | Cursor pagination by channel |
| `Messages` | `SearchVector` | GIN | Full-text search |
| `Messages` | `UserId` | B-tree | User message lookup |
| `DirectMessages` | `SenderId, RecipientId, CreatedAt` | B-tree (composite) | DM conversation pagination |
| `Invites` | `Code` | B-tree (unique) | Invite validation |
| `ApiKeys` | `KeyPrefix` | B-tree | Fast prefix-based key lookup |
| `RefreshTokens` | `TokenHash` | B-tree (unique) | Token validation |
| `UserChannelRead` | `UserId, ChannelId` | B-tree (composite, unique) | Read state lookup |

### 4.5 Date/Time Convention

All timestamps stored as UTC (`DateTime` with `Kind = Utc`). Column names use `At` suffix: `CreatedAt`, `EditedAt`, `ExpiresAt`, `RevokedAt`. Client-side conversion to local time via JavaScript interop or browser APIs.

---

## 5. Authentication & Authorization

### 5.1 Identity Configuration

```csharp
builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<HotBoxDbContext>()
.AddDefaultTokenProviders();
```

### 5.2 Token Strategy

| Token | Storage | Lifetime | Purpose |
|-------|---------|----------|---------|
| Access (JWT) | In-memory (JS) | 15 minutes | API/Hub authentication |
| Refresh | HttpOnly Secure cookie | 7 days | Silent token renewal |
| API Key | `X-Api-Key` header | Configurable / no expiry | Agent/bot authentication |

### 5.3 OAuth Providers

| Provider | Scopes | Notes |
|----------|--------|-------|
| Google | `openid`, `email`, `profile` | Configurable enable/disable |
| Microsoft | `openid`, `email`, `profile` | Configurable enable/disable |
| Discord | `identify`, `email` | Configurable enable/disable |

### 5.4 Registration Modes

| Mode | Behavior |
|------|----------|
| `Open` | Anyone can register |
| `InviteOnly` | Registration requires valid invite code |
| `Closed` | Registration disabled (admin creates accounts) |

### 5.5 Role-Based Authorization

| Role | Permissions |
|------|------------|
| `Admin` | All operations, server settings, user management, channel CRUD |
| `Moderator` | Delete any message, ban users |
| `Member` | Send/edit/delete own messages, join channels, search |

### 5.6 API Key Authentication

API keys use the `hb_` prefix convention. The full key is shown once at creation; only the SHA-256 hash is stored. Lookup is via the prefix for efficiency, then hash comparison for verification.

---

## 6. Voice Chat: WebRTC Architecture

### 6.1 P2P Full Mesh

Retained from v1. For the target scale (<10 users per voice channel), P2P mesh is the right choice.

| Aspect | P2P Mesh | SFU |
|--------|----------|-----|
| Max practical users | ~8-10 | 50+ |
| Server cost | Zero (signaling only) | Significant (media relay) |
| Latency | Lowest | Low |
| Complexity | Low | High |
| Privacy | End-to-end | Server sees media |

### 6.2 Signaling Flow

```
User A                    Server (VoiceSignalingHub)              User B
  │                              │                                  │
  ├── JoinVoiceChannel ────────>│                                  │
  │                              ├── UserJoinedVoice ─────────────>│
  │                              │                                  │
  │                              │<──────────── SendOffer ─────────┤
  │<──── ReceiveOffer ──────────│                                  │
  │                              │                                  │
  ├── SendAnswer ──────────────>│                                  │
  │                              ├── ReceiveAnswer ───────────────>│
  │                              │                                  │
  ├── SendIceCandidate ────────>│                                  │
  │                              ├── ReceiveIceCandidate ─────────>│
  │                              │                                  │
  │<═══════════════ WebRTC P2P Audio Stream ═══════════════════════>│
```

### 6.3 ICE/STUN/TURN Configuration

```json
{
  "IceServers": [
    { "Urls": ["stun:stun.l.google.com:19302"] },
    {
      "Urls": ["turn:turn.example.com:3478"],
      "Username": "hotbox",
      "Credential": "secret"
    }
  ]
}
```

Optional coturn TURN server in Docker Compose for restrictive NAT environments.

---

## 7. Real-Time Communication

### 7.1 Presence System

| Status | Trigger |
|--------|---------|
| `Online` | SignalR connected + recent activity |
| `Idle` | No activity for 5 minutes |
| `Offline` | SignalR disconnected (after 30s grace period) |
| `DoNotDisturb` | User-set (suppresses notifications) |

Presence is tracked in-memory (`ConcurrentDictionary`) and broadcast via SignalR. Multi-connection aware: a user with multiple tabs shows as Online until all connections close. Grace period prevents flicker during page refreshes.

With the Redis backplane, presence state is broadcast across instances. For true distributed presence, a future enhancement could use Redis pub/sub or a shared Redis hash.

### 7.2 Notification Delivery

```
Event occurs (new message, mention, DM)
    │
    ├── Check user preferences (muted? DND?)
    │
    ├── Persist to Notifications table
    │
    ├── Push via SignalR: ReceiveNotification
    │
    └── Client shows MudSnackbar toast + Browser Notification API (if permitted)
```

### 7.3 Message Pagination

Cursor-based pagination using `CreatedAt` + `Id` as cursor. Default page size: 50.

```
GET /api/channels/{id}/messages?before={cursor}&limit=50

Response:
{
    "items": [...],
    "nextCursor": "2026-03-22T10:00:00Z_abc123",
    "hasMore": true
}
```

### 7.4 Full-Text Search

PostgreSQL `tsvector`/`tsquery` with GIN index. Single provider simplifies the implementation:

```csharp
public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct)
{
    var tsQuery = EF.Functions.ToTsQuery("english", query.Terms);

    var messages = await _context.Messages
        .Where(m => m.SearchVector.Matches(tsQuery))
        .OrderByDescending(m => m.SearchVector.RankCoverDensity(tsQuery))
        .Select(m => new SearchResultItem
        {
            MessageId = m.Id,
            ChannelId = m.ChannelId,
            ChannelName = m.Channel.Name,
            AuthorName = m.User.DisplayName,
            Content = m.Content,
            Highlight = EF.Functions.ToTsQuery("english", query.Terms)
                .GetResultHeadline(m.Content),
            CreatedAt = m.CreatedAt,
        })
        .Take(query.Limit)
        .ToListAsync(ct);

    return new SearchResult { Items = messages };
}
```

---

## 8. Caching Strategy

### 8.1 HybridCache Integration

```csharp
// Registration
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };
});

// Configure Redis as L2
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "HotBox:";
});
```

### 8.2 Cache Usage Patterns

```csharp
// Channel list (cached, invalidated on channel CRUD)
public async Task<List<ChannelDto>> GetChannelsAsync(CancellationToken ct)
{
    return await _cache.GetOrCreateAsync(
        "channels:all",
        async token => await _repository.GetAllAsync(token),
        new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(1) },
        cancellationToken: ct);
}

// User profile (cached per user)
public async Task<UserProfileDto> GetProfileAsync(Guid userId, CancellationToken ct)
{
    return await _cache.GetOrCreateAsync(
        $"user:profile:{userId}",
        async token => await _repository.GetByIdAsync(userId, token),
        new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5) },
        cancellationToken: ct);
}

// Invalidation on mutation
public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct)
{
    await _repository.UpdateAsync(userId, request, ct);
    await _cache.RemoveAsync($"user:profile:{userId}", ct);
}
```

---

## 9. Configuration System

### 9.1 appsettings.json Structure

```jsonc
{
  "Server": {
    "ServerName": "HotBox",
    "RegistrationMode": "Open"        // Open | InviteOnly | Closed
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Database=hotbox;Username=hotbox;Password=...",
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Secret": "...",                   // min 32 chars
    "Issuer": "HotBox",
    "Audience": "HotBox",
    "AccessTokenExpiration": 15,       // minutes
    "RefreshTokenExpiration": 10080    // minutes (7 days)
  },
  "OAuth": {
    "Google": { "ClientId": "", "ClientSecret": "", "Enabled": false },
    "Microsoft": { "ClientId": "", "ClientSecret": "", "Enabled": false },
    "Discord": { "ClientId": "", "ClientSecret": "", "Enabled": false }
  },
  "IceServers": [
    { "Urls": ["stun:stun.l.google.com:19302"] }
  ],
  "Observability": {
    "SeqUrl": "http://localhost:5341",
    "OtlpEndpoint": "",
    "LogLevel": "Information"
  },
  "AdminSeed": {
    "Email": "admin@example.com",
    "Password": "...",
    "DisplayName": "Admin"
  },
  "Search": {
    "MaxResults": 100,
    "DefaultLimit": 25,
    "MinQueryLength": 2,
    "SnippetLength": 200
  }
}
```

### 9.2 Environment Variable Overrides

Double-underscore (`__`) as section separator. Environment variables take highest precedence.

```bash
ConnectionStrings__Postgres="Host=postgres;Port=5432;..."
ConnectionStrings__Redis="redis:6379"
Jwt__Secret="your-secret-here"
Server__RegistrationMode="InviteOnly"
OAuth__Google__ClientId="..."
OAuth__Google__ClientSecret="..."
OAuth__Google__Enabled="true"
```

### 9.3 Options Pattern Classes

All in `HotBox.Core/Options/`:
- `ServerOptions` — ServerName, RegistrationMode
- `JwtOptions` — Secret, Issuer, Audience, expiration times
- `OAuthOptions` — Per-provider ClientId/Secret/Enabled
- `IceServerOptions` — STUN/TURN URLs and credentials
- `ObservabilityOptions` — SeqUrl, OtlpEndpoint, LogLevel
- `AdminSeedOptions` — Email, Password, DisplayName
- `SearchOptions` — MaxResults, DefaultLimit, MinQueryLength, SnippetLength

---

## 10. Observability

### 10.1 Serilog Pipeline

```csharp
builder.Host.UseSerilog((context, services, loggerConfig) =>
{
    loggerConfig
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console()
        .WriteTo.Seq(context.Configuration["Observability:SeqUrl"]!);

    var esUrl = context.Configuration["Observability:ElasticsearchUrl"];
    if (!string.IsNullOrEmpty(esUrl))
    {
        loggerConfig.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(esUrl))
        {
            IndexFormat = "hotbox-logs-{0:yyyy.MM.dd}",
            AutoRegisterTemplate = true,
        });
    }
});
```

### 10.2 OpenTelemetry

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource("HotBox.SignalR")     // Custom SignalR spans
            .AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("HotBox.Presence")     // Custom presence metrics
            .AddMeter("HotBox.Messaging")    // Custom messaging metrics
            .AddOtlpExporter();
    });
```

### 10.3 Dev vs Prod

| Aspect | Development | Production |
|--------|------------|------------|
| Log sink | Console + Seq | Console + Elasticsearch |
| Log level | Debug | Information |
| OTLP endpoint | Local collector (optional) | Elastic APM |
| Request logging | Verbose | Standard |

---

## 11. Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<HotBoxDbContext>("database")
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")!,
        name: "redis")
    .AddSignalRHub<ChatHub>("signalr-chat")
    .AddCheck("self", () => HealthCheckResult.Healthy());
```

Endpoint: `GET /health` — returns `Healthy`, `Degraded`, or `Unhealthy` with per-check details.

---

## 12. Docker Deployment

### 12.1 Multi-Stage Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/HotBox.Application/HotBox.Application.csproj \
    -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
RUN groupadd -r hotbox && useradd -r -g hotbox hotbox
WORKDIR /app
COPY --from=build /app/publish .
USER hotbox
EXPOSE 8080
ENTRYPOINT ["dotnet", "HotBox.Application.dll"]
```

### 12.2 Docker Compose

```yaml
name: hotbox

services:
  api:
    build: .
    container_name: hotbox-api
    restart: unless-stopped
    ports:
      - "7200:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Postgres: "Host=postgres;Port=5432;Database=hotbox;Username=hotbox;Password=${DB_PASSWORD}"
      ConnectionStrings__Redis: "redis:6379"
      Jwt__Secret: "${JWT_SECRET}"
      AdminSeed__Email: "${ADMIN_EMAIL}"
      AdminSeed__Password: "${ADMIN_PASSWORD}"
      AdminSeed__DisplayName: "${ADMIN_DISPLAY_NAME}"
      Observability__SeqUrl: "http://seq:5341"
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    networks:
      - internal

  postgres:
    image: postgres:16-alpine
    container_name: hotbox-db
    restart: unless-stopped
    environment:
      POSTGRES_DB: hotbox
      POSTGRES_USER: hotbox
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U hotbox -d hotbox"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - internal

  redis:
    image: redis:7-alpine
    container_name: hotbox-redis
    restart: unless-stopped
    command: redis-server --save 60 1 --loglevel warning
    volumes:
      - redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - internal

  seq:
    image: datalust/seq:latest
    container_name: hotbox-seq
    restart: unless-stopped
    ports:
      - "7201:80"
    environment:
      ACCEPT_EULA: Y
      SEQ_FIRSTRUN_ADMINPASSWORDHASH: ${SEQ_PASSWORD}
    volumes:
      - seq-data:/data
    networks:
      - internal

volumes:
  postgres-data:
  redis-data:
  seq-data:

networks:
  internal:
    driver: bridge
```

---

## 13. NuGet Package Summary

See [stack.md](./stack.md) for the complete package manifest per project.

---

## 14. Enum Serialization Convention

All enums in `HotBox.Core/Enums/` **must** have `[JsonConverter(typeof(JsonStringEnumConverter))]`. The server serializes enums as strings; WASM components deserialize with default `System.Text.Json`. The attribute must live on the enum type in Core so both sides agree.

---

## 15. Open Questions

| Question | Status | Decision |
|----------|--------|----------|
| Blazor render mode | Resolved | Hybrid: InteractiveServer default, WASM opt-in |
| Database provider | Resolved | PostgreSQL only |
| Caching | Resolved | HybridCache (L1 + L2 Redis) |
| SignalR scaling | Resolved | Redis backplane from day one |
| Component library | Resolved | MudBlazor |
| Distributed presence | Open | In-memory for now; Redis hash if multi-instance presence diverges |
| MassTransit adoption | Deferred | When async event decoupling is needed |
| Kafka adoption | Deferred | When MassTransit transport needs upgrade |
