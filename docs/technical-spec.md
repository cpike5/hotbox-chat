# Technical Specification: HotBox

**Version**: 1.0
**Date**: 2026-02-11
**Status**: Draft

---

## 1. System Architecture Overview

HotBox is a self-hosted real-time communication platform built on ASP.NET Core (backend), Blazor WebAssembly (frontend), SignalR (real-time text/signaling), and WebRTC (voice). The architecture follows a three-layer pattern: Core (domain) -> Infrastructure (data access) -> Application (UI/API).

### 1.1 High-Level Architecture

```
+----------------------------------------------------+
|                    Docker Host                      |
|                                                     |
|  +-----------+    +-----------------------------+   |
|  |           |    |     ASP.NET Core Server      |   |
|  |  Blazor   |    |                             |   |
|  |  WASM     |<-->|  +--------+  +-----------+  |   |
|  |  Client   |    |  |SignalR  |  | REST API  |  |   |
|  |  (static) |    |  | Hubs   |  | Controllers|  |   |
|  |           |    |  +--------+  +-----------+  |   |
|  +-----------+    |       |           |         |   |
|       ^           |  +----v-----------v------+  |   |
|       |           |  |    Application Layer   |  |   |
|  WebRTC P2P       |  |    (Services, CQRS)    |  |   |
|  (voice only)     |  +----------+------------+  |   |
|       |           |             |                |   |
|       v           |  +----------v------------+  |   |
|  +----------+     |  |  Infrastructure Layer  |  |   |
|  | Browser  |     |  |  (EF Core, Identity)   |  |   |
|  | WebRTC   |     |  +----------+------------+  |   |
|  | API      |     |             |                |   |
|  +----------+     |  +----------v------------+  |   |
|                   |  |     Core Layer         |  |   |
|                   |  |  (Entities, Interfaces) |  |   |
|                   |  +-----------------------+  |   |
|                   +-----------------------------+   |
|                              |                      |
|  +---------------------------v-------------------+  |
|  |            Database (SQLite / PostgreSQL)      |  |
|  +-----------------------------------------------+  |
|                                                     |
|  +-----------------+  +--------------------------+  |
|  | Seq (dev logs)  |  | Elasticsearch (prod APM) |  |
|  +-----------------+  +--------------------------+  |
+----------------------------------------------------+
```

### 1.2 Data Flow

```
User Browser
    |
    |-- HTTPS --> Blazor WASM static files (served by ASP.NET Core)
    |
    |-- WSS (SignalR) --> ChatHub (text messages, presence, notifications)
    |-- WSS (SignalR) --> VoiceSignalingHub (WebRTC signaling: SDP, ICE)
    |
    |-- WebRTC P2P -----> Other user browsers (voice audio streams)
    |
    |-- HTTPS REST --> API Controllers (auth, channels, users, admin)
```

### 1.3 Component Relationships

| Component | Communicates With | Protocol |
|-----------|------------------|----------|
| Blazor WASM Client | ASP.NET Core Server | HTTPS, WSS (SignalR) |
| Blazor WASM Client | Other Clients | WebRTC (P2P voice) |
| ASP.NET Core Server | Database | EF Core (TCP) |
| ASP.NET Core Server | Seq (dev) | HTTP (Serilog sink) |
| ASP.NET Core Server | Elasticsearch (prod) | OTLP / HTTP |
| SignalR Hubs | Blazor Client | WebSocket |

---

## 2. Backend Architecture

### 2.1 Solution Structure

```
HotBox.sln
|
+-- src/
|   +-- HotBox.Core/                    # Domain layer (no dependencies)
|   |   +-- Entities/
|   |   |   +-- User.cs
|   |   |   +-- Channel.cs
|   |   |   +-- Message.cs
|   |   |   +-- DirectMessage.cs
|   |   |   +-- Role.cs
|   |   |   +-- VoiceChannel.cs
|   |   |   +-- Invite.cs
|   |   +-- Enums/
|   |   |   +-- ChannelType.cs
|   |   |   +-- RegistrationMode.cs
|   |   |   +-- UserStatus.cs
|   |   +-- Interfaces/
|   |   |   +-- IChannelRepository.cs
|   |   |   +-- IMessageRepository.cs
|   |   |   +-- IDirectMessageRepository.cs
|   |   |   +-- IUserRepository.cs
|   |   |   +-- IInviteRepository.cs
|   |   |   +-- IUnitOfWork.cs
|   |   |   +-- ISearchService.cs
|   |   +-- HotBox.Core.csproj
|   |
|   +-- HotBox.Infrastructure/          # Data access layer
|   |   +-- Data/
|   |   |   +-- HotBoxDbContext.cs
|   |   |   +-- Configurations/          # EF Core Fluent API configs
|   |   |   |   +-- UserConfiguration.cs
|   |   |   |   +-- ChannelConfiguration.cs
|   |   |   |   +-- MessageConfiguration.cs
|   |   |   |   +-- DirectMessageConfiguration.cs
|   |   |   +-- Migrations/
|   |   +-- Repositories/
|   |   |   +-- ChannelRepository.cs
|   |   |   +-- MessageRepository.cs
|   |   |   +-- DirectMessageRepository.cs
|   |   |   +-- UserRepository.cs
|   |   |   +-- InviteRepository.cs
|   |   +-- Search/
|   |   |   +-- PostgresSearchService.cs
|   |   |   +-- MySqlSearchService.cs
|   |   |   +-- SqliteSearchService.cs
|   |   |   +-- FallbackSearchService.cs      # SQL LIKE fallback
|   |   +-- Identity/
|   |   |   +-- AppUser.cs              # ASP.NET Identity user (extends IdentityUser)
|   |   +-- Extensions/
|   |   |   +-- InfrastructureServiceExtensions.cs
|   |   +-- HotBox.Infrastructure.csproj
|   |
|   +-- HotBox.Application/            # UI/API layer (ASP.NET Core host)
|   |   +-- Controllers/
|   |   |   +-- AuthController.cs
|   |   |   +-- ChannelsController.cs
|   |   |   +-- MessagesController.cs
|   |   |   +-- DirectMessagesController.cs
|   |   |   +-- SearchController.cs
|   |   |   +-- UsersController.cs
|   |   |   +-- AdminController.cs
|   |   +-- Hubs/
|   |   |   +-- ChatHub.cs
|   |   |   +-- VoiceSignalingHub.cs
|   |   +-- Services/
|   |   |   +-- IChannelService.cs
|   |   |   +-- ChannelService.cs
|   |   |   +-- IMessageService.cs
|   |   |   +-- MessageService.cs
|   |   |   +-- IDirectMessageService.cs
|   |   |   +-- DirectMessageService.cs
|   |   |   +-- ISearchService.cs
|   |   |   +-- SearchService.cs
|   |   |   +-- IPresenceService.cs
|   |   |   +-- PresenceService.cs
|   |   |   +-- INotificationService.cs
|   |   |   +-- NotificationService.cs
|   |   +-- Configuration/
|   |   |   +-- HotBoxOptions.cs
|   |   |   +-- AuthOptions.cs
|   |   |   +-- OAuthProviderOptions.cs
|   |   |   +-- DatabaseOptions.cs
|   |   |   +-- SearchOptions.cs
|   |   +-- Extensions/
|   |   |   +-- ApplicationServiceExtensions.cs
|   |   |   +-- AuthenticationExtensions.cs
|   |   |   +-- ObservabilityExtensions.cs
|   |   +-- Middleware/
|   |   |   +-- RequestLoggingMiddleware.cs
|   |   +-- Program.cs
|   |   +-- appsettings.json
|   |   +-- appsettings.Development.json
|   |   +-- HotBox.Application.csproj
|   |
|   +-- HotBox.Client/                 # Blazor WASM project
|   |   (see Section 3)
|
+-- tests/
|   +-- HotBox.Core.Tests/
|   +-- HotBox.Infrastructure.Tests/
|   +-- HotBox.Application.Tests/
|   +-- HotBox.Client.Tests/
|
+-- docker/
|   +-- Dockerfile
|   +-- docker-compose.yml
|   +-- docker-compose.dev.yml
|
+-- docs/
```

### 2.2 Project Dependencies

```
HotBox.Core          --> (none - zero external dependencies)
HotBox.Infrastructure --> HotBox.Core, EF Core, ASP.NET Identity
HotBox.Application    --> HotBox.Core, HotBox.Infrastructure, SignalR, Serilog, OpenTelemetry
HotBox.Client         --> (standalone Blazor WASM, communicates via HTTP/SignalR)
```

### 2.3 API Endpoint Design

All API endpoints are prefixed with `/api/v1/`.

#### Authentication

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/v1/auth/register` | Register new user (respects registration mode) |
| POST | `/api/v1/auth/login` | Email/password login, returns JWT |
| POST | `/api/v1/auth/refresh` | Refresh JWT token |
| POST | `/api/v1/auth/logout` | Invalidate refresh token |
| GET | `/api/v1/auth/providers` | List enabled OAuth providers |
| GET | `/api/v1/auth/external/{provider}` | Initiate OAuth flow |
| GET | `/api/v1/auth/external/callback` | OAuth callback handler |

#### Channels

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/channels` | List all channels (text + voice) |
| POST | `/api/v1/channels` | Create channel (admin/mod) |
| GET | `/api/v1/channels/{id}` | Get channel details |
| PUT | `/api/v1/channels/{id}` | Update channel (admin/mod) |
| DELETE | `/api/v1/channels/{id}` | Delete channel (admin) |

#### Messages

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/channels/{channelId}/messages` | Get message history (paginated) |
| POST | `/api/v1/channels/{channelId}/messages` | Post message (also via SignalR) |

#### Direct Messages

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/dm/conversations` | List DM conversations |
| GET | `/api/v1/dm/{userId}/messages` | Get DM history (paginated) |
| POST | `/api/v1/dm/{userId}/messages` | Send DM (also via SignalR) |

#### Search

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/search/messages` | Search channel messages (query params: `q`, `channelId?`, `authorId?`, `before?`, `after?`, `page`, `pageSize`) |
| GET | `/api/v1/search/dm` | Search direct messages (query params: `q`, `participantId?`, `before?`, `after?`, `page`, `pageSize`) |
| POST | `/api/v1/admin/search/reindex` | Backfill search index from database (admin only) |

#### Users

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/users` | List users |
| GET | `/api/v1/users/{id}` | Get user profile |
| PUT | `/api/v1/users/me` | Update own profile |
| GET | `/api/v1/users/me` | Get own profile |

#### Admin

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/v1/admin/settings` | Get server settings |
| PUT | `/api/v1/admin/settings` | Update server settings |
| POST | `/api/v1/admin/users` | Create user (closed registration) |
| PUT | `/api/v1/admin/users/{id}/role` | Assign role to user |
| POST | `/api/v1/admin/invites` | Generate invite code |
| DELETE | `/api/v1/admin/invites/{code}` | Revoke invite |

### 2.4 SignalR Hub Design

#### ChatHub (`/hubs/chat`)

Handles all real-time text messaging, presence, and notification delivery.

**Server Methods (client calls these):**

| Method | Parameters | Description |
|--------|-----------|-------------|
| `JoinChannel` | `channelId: Guid` | Subscribe to channel messages |
| `LeaveChannel` | `channelId: Guid` | Unsubscribe from channel |
| `SendMessage` | `channelId: Guid, content: string` | Send message to channel |
| `SendDirectMessage` | `recipientId: Guid, content: string` | Send DM |
| `StartTyping` | `channelId: Guid` | Broadcast typing indicator |
| `StopTyping` | `channelId: Guid` | Stop typing indicator |
| `UpdateStatus` | `status: UserStatus` | Update presence (online/idle/dnd) |

**Client Methods (server calls these):**

| Method | Parameters | Description |
|--------|-----------|-------------|
| `ReceiveMessage` | `message: MessageDto` | New channel message |
| `ReceiveDirectMessage` | `message: DirectMessageDto` | New DM |
| `UserJoined` | `channelId: Guid, user: UserDto` | User joined channel |
| `UserLeft` | `channelId: Guid, userId: Guid` | User left channel |
| `UserTyping` | `channelId: Guid, userId: Guid` | Typing indicator |
| `UserStoppedTyping` | `channelId: Guid, userId: Guid` | Stopped typing |
| `PresenceUpdate` | `userId: Guid, status: UserStatus` | User status changed |
| `Notification` | `notification: NotificationDto` | General notification |

**Connection Lifecycle:**
- `OnConnectedAsync`: Mark user online, notify all connected clients, join user's subscribed channel groups
- `OnDisconnectedAsync`: Mark user offline (with grace period for reconnection), notify clients, clean up groups

**Group Management:**
- Each text channel maps to a SignalR group named `channel:{channelId}`
- DM conversations use groups named `dm:{sortedUserIdPair}` (consistent ordering)

#### VoiceSignalingHub (`/hubs/voice`)

Handles WebRTC signaling only -- no audio passes through the server.

**Server Methods:**

| Method | Parameters | Description |
|--------|-----------|-------------|
| `JoinVoiceChannel` | `channelId: Guid` | Enter voice channel, get peer list |
| `LeaveVoiceChannel` | `channelId: Guid` | Leave voice channel |
| `SendOffer` | `targetUserId: Guid, sdp: string` | Send WebRTC SDP offer |
| `SendAnswer` | `targetUserId: Guid, sdp: string` | Send WebRTC SDP answer |
| `SendIceCandidate` | `targetUserId: Guid, candidate: string` | Send ICE candidate |
| `ToggleMute` | `channelId: Guid, isMuted: bool` | Broadcast mute status |
| `ToggleDeafen` | `channelId: Guid, isDeafened: bool` | Broadcast deafen status |

**Client Methods:**

| Method | Parameters | Description |
|--------|-----------|-------------|
| `UserJoinedVoice` | `channelId: Guid, user: VoiceUserDto` | New peer in voice |
| `UserLeftVoice` | `channelId: Guid, userId: Guid` | Peer left voice |
| `ReceiveOffer` | `fromUserId: Guid, sdp: string` | Receive SDP offer |
| `ReceiveAnswer` | `fromUserId: Guid, sdp: string` | Receive SDP answer |
| `ReceiveIceCandidate` | `fromUserId: Guid, candidate: string` | Receive ICE candidate |
| `UserMuteChanged` | `userId: Guid, isMuted: bool` | Peer mute state |
| `UserDeafenChanged` | `userId: Guid, isDeafened: bool` | Peer deafen state |
| `VoiceChannelState` | `users: VoiceUserDto[]` | Full voice channel state on join |

---

## 3. Frontend Architecture (Blazor WASM)

### 3.1 Project Structure

```
HotBox.Client/
+-- wwwroot/
|   +-- css/
|   |   +-- app.css                     # Main styles (from design tokens)
|   +-- js/
|   |   +-- webrtc-interop.js           # WebRTC JSInterop bridge
|   |   +-- notification-interop.js     # Browser notification API bridge
|   |   +-- audio-interop.js            # Audio device enumeration/control
|   +-- index.html
+-- Layout/
|   +-- MainLayout.razor                # App shell: sidebar + main content
|   +-- MainLayout.razor.css
+-- Components/
|   +-- Sidebar/
|   |   +-- Sidebar.razor               # Left sidebar container
|   |   +-- ServerHeader.razor
|   |   +-- ChannelList.razor           # Text + voice channel list
|   |   +-- ChannelItem.razor
|   |   +-- VoiceChannelItem.razor
|   |   +-- VoiceUserList.razor
|   |   +-- DirectMessageList.razor
|   |   +-- DirectMessageItem.razor
|   |   +-- UserPanel.razor             # Bottom-left user info + controls
|   |   +-- VoiceConnectedPanel.razor   # Voice connection controls
|   +-- Chat/
|   |   +-- ChatView.razor              # Main chat area
|   |   +-- ChannelHeader.razor
|   |   +-- MessageList.razor           # Scrollable message area
|   |   +-- MessageGroup.razor          # Grouped messages by author
|   |   +-- MessageInput.razor          # Text input + send
|   |   +-- TypingIndicator.razor
|   +-- Search/
|   |   +-- SearchOverlay.razor         # Ctrl+K modal overlay
|   |   +-- SearchOverlay.razor.css
|   |   +-- SearchInput.razor           # Debounced search input
|   |   +-- SearchResults.razor         # Scrollable result list
|   |   +-- SearchResultItem.razor      # Individual result card
|   |   +-- SearchHighlight.razor       # Query term highlighting
|   +-- Members/
|   |   +-- MembersPanel.razor          # Right-side members panel
|   |   +-- MemberItem.razor
|   +-- Auth/
|   |   +-- LoginPage.razor
|   |   +-- RegisterPage.razor
|   |   +-- OAuthButtons.razor          # Dynamic OAuth provider buttons
|   +-- Admin/
|   |   +-- AdminPanel.razor
|   |   +-- ChannelManagement.razor
|   |   +-- UserManagement.razor
|   |   +-- InviteManagement.razor
|   |   +-- ServerSettings.razor
|   +-- Shared/
|       +-- Avatar.razor                # Reusable avatar with status dot
|       +-- StatusDot.razor
|       +-- UnreadBadge.razor
|       +-- LoadingSpinner.razor
|       +-- ErrorBoundary.razor
+-- Services/
|   +-- IApiClient.cs                   # HTTP API client interface
|   +-- ApiClient.cs                    # HttpClient wrapper for REST calls
|   +-- IChatHubService.cs
|   +-- ChatHubService.cs              # SignalR ChatHub client
|   +-- IVoiceHubService.cs
|   +-- VoiceHubService.cs             # SignalR VoiceSignalingHub client
|   +-- IWebRtcService.cs
|   +-- WebRtcService.cs               # WebRTC JSInterop wrapper
|   +-- INotificationService.cs
|   +-- NotificationService.cs         # Browser notification JSInterop
|   +-- IAuthService.cs
|   +-- AuthService.cs                 # Auth state, token management
+-- State/
|   +-- AppState.cs                    # Central application state
|   +-- ChannelState.cs                # Active channel, message cache
|   +-- VoiceState.cs                  # Voice connection state
|   +-- PresenceState.cs               # User online/offline/idle status
|   +-- AuthState.cs                   # Current user, JWT tokens
|   +-- SearchState.cs                 # Search query, results, loading state
+-- Models/
|   +-- ChannelDto.cs
|   +-- MessageDto.cs
|   +-- DirectMessageDto.cs
|   +-- UserDto.cs
|   +-- VoiceUserDto.cs
|   +-- NotificationDto.cs
|   +-- AuthResponseDto.cs
|   +-- SearchResultDto.cs
|   +-- SearchResponse.cs
+-- Program.cs
+-- _Imports.razor
+-- HotBox.Client.csproj
```

### 3.2 Component Hierarchy

```
App.razor
+-- Router
    +-- MainLayout.razor
        +-- Sidebar.razor
        |   +-- ServerHeader.razor
        |   +-- ChannelList.razor
        |   |   +-- ChannelItem.razor (foreach text channel)
        |   |   +-- VoiceChannelItem.razor (foreach voice channel)
        |   |       +-- VoiceUserList.razor
        |   +-- DirectMessageList.razor
        |   |   +-- DirectMessageItem.razor (foreach DM)
        |   +-- VoiceConnectedPanel.razor (when in voice)
        |   +-- UserPanel.razor
        +-- ChatView.razor (or DMView, AdminPanel based on route)
        |   +-- ChannelHeader.razor
        |   +-- MessageList.razor
        |   |   +-- MessageGroup.razor (foreach author group)
        |   +-- TypingIndicator.razor
        |   +-- MessageInput.razor
        +-- MembersPanel.razor (toggleable)
            +-- MemberItem.razor (foreach member)
```

### 3.3 State Management

The client uses a service-based state management approach -- no external state library. State services are registered as scoped services (singleton-like in Blazor WASM) and use events to notify components of changes.

```csharp
// Pattern for state services
public class AppState
{
    public event Action? OnChange;

    private ChannelDto? _activeChannel;
    public ChannelDto? ActiveChannel
    {
        get => _activeChannel;
        set { _activeChannel = value; NotifyStateChanged(); }
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
```

Components subscribe in `OnInitialized` and unsubscribe in `Dispose`:

```csharp
@implements IDisposable

@code {
    [Inject] private AppState State { get; set; } = default!;

    protected override void OnInitialized()
    {
        State.OnChange += StateHasChanged;
    }

    public void Dispose()
    {
        State.OnChange -= StateHasChanged;
    }
}
```

### 3.4 SignalR Client Integration

Both hub connections are established after login and maintained for the session lifetime. Reconnection is handled automatically with exponential backoff.

```csharp
// ChatHubService.cs pattern
public class ChatHubService : IChatHubService, IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly NavigationManager _navigation;
    private readonly AuthState _authState;

    public event Action<MessageDto>? OnMessageReceived;
    public event Action<Guid, UserStatus>? OnPresenceUpdate;

    public async Task ConnectAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_navigation.ToAbsoluteUri("/hubs/chat"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_authState.AccessToken);
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10) })
            .Build();

        _hubConnection.On<MessageDto>("ReceiveMessage", message =>
        {
            OnMessageReceived?.Invoke(message);
        });

        await _hubConnection.StartAsync();
    }
}
```

### 3.5 Design Tokens

The UI design system is defined in the existing HTML prototype at `c:\Users\cpike\workspace\hot-box\temp\prototype.html`. The CSS custom properties (design tokens) from that prototype will be carried into the Blazor app's `wwwroot/css/app.css`. Key tokens include:

| Token | Value | Usage |
|-------|-------|-------|
| `--bg-deepest` | `#1a1a1e` | Outermost background, user panel |
| `--bg-deep` | `#1e1e23` | Sidebar, members panel |
| `--bg-base` | `#232329` | Main content area |
| `--bg-raised` | `#2a2a32` | Message input, cards |
| `--bg-surface` | `#32323c` | Avatars, elevated surfaces |
| `--bg-hover` | `#3a3a46` | Hover states |
| `--bg-active` | `#424250` | Active/selected states |
| `--text-primary` | `#e4e4e8` | Primary text |
| `--text-secondary` | `#a0a0aa` | Secondary text, message bodies |
| `--text-muted` | `#6e6e7a` | Timestamps, hints |
| `--accent` | `#7aa2f7` | Links, active indicators, accent color |
| `--status-online` | `#50c878` | Online status dot |
| `--status-idle` | `#e2b93d` | Idle status dot |
| `--status-dnd` | `#f7768e` | Do Not Disturb status dot |
| `--status-offline` | `#565664` | Offline status dot |

---

## 4. Database Design

### 4.1 Entity Models

#### AppUser (extends IdentityUser)

```
AppUser (inherits IdentityUser<Guid>)
├── DisplayName          : string (required, max 32)
├── AvatarUrl            : string? (nullable, future use)
├── Status               : UserStatus (enum: Online, Idle, DnD, Offline)
├── LastSeenUtc          : DateTime
├── CreatedAtUtc         : DateTime
├── Messages             : ICollection<Message>
├── SentDirectMessages   : ICollection<DirectMessage>
├── ReceivedDirectMessages : ICollection<DirectMessage>
```

#### Channel

```
Channel
├── Id                   : Guid (PK)
├── Name                 : string (required, max 64)
├── Topic                : string? (max 256)
├── Type                 : ChannelType (enum: Text, Voice)
├── SortOrder            : int
├── CreatedAtUtc         : DateTime
├── CreatedByUserId      : Guid (FK -> AppUser)
├── Messages             : ICollection<Message>
```

#### Message

```
Message
├── Id                   : Guid (PK)
├── Content              : string (required, max 4000)
├── ChannelId            : Guid (FK -> Channel)
├── AuthorId             : Guid (FK -> AppUser)
├── CreatedAtUtc         : DateTime
├── EditedAtUtc          : DateTime? (nullable, future use)
├── Channel              : Channel (nav)
├── Author               : AppUser (nav)
```

#### DirectMessage

```
DirectMessage
├── Id                   : Guid (PK)
├── Content              : string (required, max 4000)
├── SenderId             : Guid (FK -> AppUser)
├── RecipientId          : Guid (FK -> AppUser)
├── CreatedAtUtc         : DateTime
├── ReadAtUtc            : DateTime? (nullable)
├── Sender               : AppUser (nav)
├── Recipient            : AppUser (nav)
```

#### Role (via ASP.NET Identity)

Uses `IdentityRole<Guid>` with seeded roles:
- `Admin` -- full server control
- `Moderator` -- channel management, user moderation
- `Member` -- standard user (default)

#### Invite

```
Invite
├── Id                   : Guid (PK)
├── Code                 : string (unique, 8 chars)
├── CreatedByUserId      : Guid (FK -> AppUser)
├── CreatedAtUtc         : DateTime
├── ExpiresAtUtc         : DateTime? (nullable)
├── MaxUses              : int? (nullable)
├── UseCount             : int (default 0)
├── IsRevoked            : bool (default false)
```

### 4.2 Entity Relationships

```
AppUser (1) ---< (many) Message
AppUser (1) ---< (many) DirectMessage (as Sender)
AppUser (1) ---< (many) DirectMessage (as Recipient)
AppUser (1) ---< (many) Invite (as Creator)
Channel (1) ---< (many) Message
```

### 4.3 EF Core Multi-Provider Strategy

The database provider is selected at startup based on configuration. The `HotBoxDbContext` inherits from `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>`.

**Provider registration pattern in `InfrastructureServiceExtensions.cs`:**

```csharp
public static IServiceCollection AddHotBoxInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var dbOptions = configuration.GetSection("Database").Get<DatabaseOptions>()!;

    services.AddDbContext<HotBoxDbContext>(options =>
    {
        switch (dbOptions.Provider.ToLowerInvariant())
        {
            case "sqlite":
                options.UseSqlite(dbOptions.ConnectionString);
                break;
            case "postgresql":
                options.UseNpgsql(dbOptions.ConnectionString);
                break;
            case "mysql":
            case "mariadb":
                options.UseMySql(dbOptions.ConnectionString,
                    ServerVersion.AutoDetect(dbOptions.ConnectionString));
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported database provider: {dbOptions.Provider}");
        }
    });

    // Repository registrations
    services.AddScoped<IChannelRepository, ChannelRepository>();
    services.AddScoped<IMessageRepository, MessageRepository>();
    // ... etc

    return services;
}
```

**NuGet packages per provider:**

| Provider | Package |
|----------|---------|
| SQLite | `Microsoft.EntityFrameworkCore.Sqlite` |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| MySQL/MariaDB | `Pomelo.EntityFrameworkCore.MySql` |

### 4.4 Indexing Strategy

| Table | Index | Columns | Purpose |
|-------|-------|---------|---------|
| Messages | IX_Messages_ChannelId_CreatedAt | ChannelId, CreatedAtUtc DESC | Message history pagination |
| DirectMessages | IX_DM_Participants_CreatedAt | SenderId, RecipientId, CreatedAtUtc DESC | DM history |
| DirectMessages | IX_DM_Recipient_Read | RecipientId, ReadAtUtc | Unread DM count |
| Channels | IX_Channels_Type_Sort | Type, SortOrder | Channel list ordering |
| Invites | IX_Invites_Code | Code (unique) | Invite lookup |

### 4.5 Date/Time Handling

- All timestamps are stored in UTC (`DateTime` with `Kind = Utc`)
- Property names use the `Utc` suffix (e.g., `CreatedAtUtc`, `LastSeenUtc`)
- The Blazor client converts to local timezone for display using the browser's `Intl.DateTimeFormat` via JSInterop
- Display format: relative time for recent messages ("2 minutes ago"), absolute time for older ("Feb 11, 2026 3:22 PM")

---

## 5. Authentication and Authorization

### 5.1 ASP.NET Identity Setup

- Identity is configured with `AppUser` (extends `IdentityUser<Guid>`) and `IdentityRole<Guid>`
- Password requirements: minimum 8 characters, at least one uppercase, one lowercase, one digit
- Account lockout: 5 failed attempts, 15-minute lockout
- Email confirmation: disabled for MVP (self-hosted, trusted users)

### 5.2 JWT Authentication

The Blazor WASM client authenticates via JWT bearer tokens:

- **Access token**: Short-lived (15 minutes), stored in memory only (not localStorage)
- **Refresh token**: Longer-lived (7 days), stored in an HttpOnly secure cookie
- SignalR connections include the access token via query string parameter (standard SignalR pattern)

**Token flow:**
1. User logs in via `/api/v1/auth/login` -> receives access token + refresh cookie
2. Access token is held in `AuthState` (in-memory)
3. `ApiClient` attaches access token to all HTTP requests via `DelegatingHandler`
4. On 401, the handler automatically calls `/api/v1/auth/refresh`
5. SignalR hub connections pass the access token via `AccessTokenProvider`

### 5.3 OAuth Integration (Optional/Configurable)

OAuth providers are configured in `appsettings.json` and only enabled when credentials are provided. The login UI dynamically renders buttons for configured providers.

```json
{
  "Auth": {
    "OAuth": {
      "Google": {
        "Enabled": true,
        "ClientId": "...",
        "ClientSecret": "..."
      },
      "Microsoft": {
        "Enabled": false,
        "ClientId": "",
        "ClientSecret": ""
      }
    }
  }
}
```

**Packages:**
- `Microsoft.AspNetCore.Authentication.Google`
- `Microsoft.AspNetCore.Authentication.MicrosoftAccount`

The `/api/v1/auth/providers` endpoint returns the list of enabled providers, and the Blazor `LoginPage.razor` conditionally renders OAuth buttons.

### 5.4 Registration Modes

Configured via `appsettings.json`:

| Mode | Behavior |
|------|----------|
| `Open` | Anyone can register |
| `InviteOnly` | Registration requires a valid invite code |
| `Closed` | Only admins can create accounts |

```json
{
  "Auth": {
    "RegistrationMode": "InviteOnly"
  }
}
```

### 5.5 Role-Based Authorization

| Role | Permissions |
|------|------------|
| Admin | Everything: manage server settings, channels, users, roles, invites |
| Moderator | Manage channels (create/edit/delete), kick/mute users |
| Member | Send messages, join channels, use voice, send DMs |

Authorization is enforced at both API (via `[Authorize(Roles = "Admin")]` attributes) and SignalR hub level (via custom `IAuthorizationHandler` implementations).

### 5.6 Admin Seeding

On first run (when no admin user exists), the server creates an admin account from configuration:

```json
{
  "Auth": {
    "Seed": {
      "AdminEmail": "admin@hotbox.local",
      "AdminPassword": "ChangeMe123!"
    }
  }
}
```

This is handled in `Program.cs` during application startup, after `EnsureCreated`/migration.

---

## 6. Voice Chat: WebRTC Architecture

### 6.1 Recommendation: P2P (Peer-to-Peer) with Full Mesh

**For the HotBox MVP targeting <10 concurrent voice users per channel, P2P full mesh is the recommended approach.** Here is the rationale:

| Factor | P2P Full Mesh | SFU (Selective Forwarding Unit) |
|--------|---------------|--------------------------------|
| Server complexity | None -- signaling only | Requires a media server (Go/C++/Rust) |
| Server cost | Zero media processing | CPU + bandwidth for forwarding |
| Max practical users | 4-8 comfortably, 10 is the upper limit | 50+ |
| Latency | Lowest possible (direct peer connection) | Slightly higher (extra hop) |
| Privacy | Audio never touches the server | Audio passes through server (unencrypted at SFU) |
| Implementation effort | Low -- browser APIs + SignalR signaling | High -- deploy + integrate external SFU |
| .NET ecosystem fit | Excellent -- no non-.NET dependencies | Poor -- no production-quality .NET SFU exists |

**Why P2P for HotBox:**
1. The primary audience is groups of ~10 people. P2P full mesh handles this comfortably for audio-only (no video).
2. Audio-only streams are ~40-100 kbps per peer. At 10 users, each client uploads/downloads 9 streams = ~360-900 kbps. This is well within any residential internet connection.
3. An SFU would require deploying a separate service (LiveKit in Go, mediasoup in Node.js) alongside the .NET server, violating the "no JavaScript on the server" principle and adding massive deployment complexity.
4. SIPSorcery (the only .NET WebRTC library) does not have a production-ready SFU implementation as of early 2026.
5. The P2P approach means voice audio **never touches the server**, which aligns with the privacy goals.

**Migration path to SFU:** If HotBox eventually needs to support larger voice channels (20+ users), LiveKit can be added as a sidecar container in docker-compose. The signaling protocol is already abstracted behind `IVoiceHubService`, so the client would only need a new implementation.

### 6.2 WebRTC Implementation Architecture

```
Browser A                    Server                    Browser B
    |                          |                          |
    |-- JoinVoiceChannel ----->|                          |
    |                          |<-- JoinVoiceChannel -----|
    |                          |                          |
    |<-- VoiceChannelState ----|                          |
    |   (list of peers)        |                          |
    |                          |                          |
    |== RTCPeerConnection ===========================>|   |
    |   (create offer)         |                          |
    |-- SendOffer ------------>|-- ReceiveOffer --------->|
    |                          |                          |
    |                          |<-- SendAnswer -----------|
    |<-- ReceiveAnswer --------|                          |
    |                          |                          |
    |-- SendIceCandidate ----->|-- ReceiveIceCandidate -->|
    |<-- ReceiveIceCandidate --|<-- SendIceCandidate -----|
    |                          |                          |
    |========= P2P Audio Stream (direct) ============>|   |
    |<======== P2P Audio Stream (direct) =============|   |
```

### 6.3 JSInterop Bridge (`webrtc-interop.js`)

Since WebRTC APIs are browser-native JavaScript, a thin JSInterop layer is required. This is the **only** JavaScript in the project, and it exists solely because the browser's `RTCPeerConnection` API has no .NET equivalent in WASM.

The JS bridge exposes these methods to Blazor:

| JS Function | Purpose |
|------------|---------|
| `WebRtcInterop.createPeerConnection(peerId, iceServers)` | Create RTCPeerConnection for a peer |
| `WebRtcInterop.createOffer(peerId)` | Generate SDP offer |
| `WebRtcInterop.createAnswer(peerId, remoteSdp)` | Generate SDP answer from remote offer |
| `WebRtcInterop.setRemoteAnswer(peerId, remoteSdp)` | Apply remote SDP answer |
| `WebRtcInterop.addIceCandidate(peerId, candidate)` | Add ICE candidate |
| `WebRtcInterop.getUserMedia(audioConstraints)` | Request microphone access |
| `WebRtcInterop.toggleMute(isMuted)` | Mute/unmute local audio track |
| `WebRtcInterop.closePeerConnection(peerId)` | Tear down connection |
| `WebRtcInterop.closeAllConnections()` | Tear down all connections |

Callbacks from JS to Blazor (via `DotNetObjectReference`):

| Callback | Purpose |
|----------|---------|
| `OnIceCandidate(peerId, candidate)` | Forward ICE candidate to signaling |
| `OnTrackReceived(peerId)` | Remote audio track added |
| `OnConnectionStateChange(peerId, state)` | Monitor connection health |

### 6.4 ICE/STUN/TURN Configuration

For NAT traversal, the client needs STUN/TURN servers:

```json
{
  "Voice": {
    "IceServers": [
      {
        "Urls": ["stun:stun.l.google.com:19302"]
      },
      {
        "Urls": ["turn:turn.hotbox.local:3478"],
        "Username": "hotbox",
        "Credential": "changeme"
      }
    ]
  }
}
```

- **STUN**: Free public servers work for most NAT types. Google's STUN server is a reasonable default.
- **TURN**: Required for symmetric NAT / strict firewalls. Self-hosting `coturn` as an optional docker-compose service is recommended for reliability. TURN is optional and only needed if users are behind restrictive NATs.

---

## 7. Real-Time Communication

### 7.1 SignalR Configuration

```csharp
// Program.cs
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 64 * 1024; // 64KB max message
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});
```

### 7.2 Presence System

User presence (online/idle/offline) is tracked server-side in `PresenceService`:

- **Online**: Active SignalR connection with recent activity
- **Idle**: Active connection but no activity for 5 minutes (client sends idle status)
- **Offline**: No active SignalR connection (after grace period of 30 seconds for reconnection)

Presence is stored in-memory (`ConcurrentDictionary<Guid, UserPresence>`) since this data is ephemeral and the scale target is ~100 users. No need for Redis or distributed cache.

### 7.3 Notification Delivery

Notifications are delivered via SignalR and displayed using the browser's Notification API (via JSInterop). Notification types:

| Type | Trigger |
|------|---------|
| New message in subscribed channel | Message posted to a channel the user has joined |
| Direct message | Any incoming DM |
| Mention | Message containing `@username` (plain text matching) |

The client requests browser notification permission on first login. A JSInterop bridge (`notification-interop.js`) calls `Notification.requestPermission()` and `new Notification(title, options)`.

### 7.4 Message History and Pagination

- Channel messages are persisted to the database and loaded via REST API on channel switch
- Default page size: 50 messages
- Infinite scroll loads older messages (paginated by `CreatedAtUtc` cursor)
- New messages arrive in real-time via SignalR and are appended to the in-memory list
- Full-text search is available via native database FTS (see Section 7.5)

### 7.5 Message Search

HotBox uses native database full-text search capabilities rather than an external search engine. This keeps the infrastructure lightweight (no Elasticsearch dependency for search) while providing ranked, quality results.

#### Provider-Aware Search Strategy

| Provider | FTS Mechanism | Ranking | Stemming | Setup |
|----------|--------------|---------|----------|-------|
| **PostgreSQL** | `tsvector` / `tsquery` | `ts_rank` / `ts_rank_cd` | Yes (language-aware via dictionaries) | GIN index on `tsvector` column |
| **MySQL/MariaDB** | `FULLTEXT` index | `MATCH...AGAINST` relevance score | Basic (built-in) | `FULLTEXT` index on `Content` column |
| **SQLite** | FTS5 virtual tables | BM25 ranking | Basic (porter tokenizer) | FTS5 virtual table + triggers to sync with main table |

The `ISearchService` interface (defined in Core) abstracts the provider differences. Infrastructure provides provider-specific implementations that are selected at startup based on `DatabaseOptions.Provider`.

#### Search Service Interface

```csharp
// HotBox.Core/Interfaces/ISearchService.cs
public interface ISearchService
{
    Task<SearchResult> SearchMessagesAsync(
        Guid callingUserId,
        string query,
        Guid? channelId = null,
        Guid? authorId = null,
        DateTime? before = null,
        DateTime? after = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    Task<SearchResult> SearchDirectMessagesAsync(
        Guid callingUserId,
        string query,
        Guid? participantId = null,
        DateTime? before = null,
        DateTime? after = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    Task EnsureSearchIndexAsync(CancellationToken cancellationToken = default);
}
```

#### Search Result Shape

```csharp
public class SearchResult
{
    public IReadOnlyList<SearchHit> Items { get; init; } = [];
    public long TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool IsDegraded { get; init; } // True if using LIKE fallback
}

public class SearchHit
{
    public Guid MessageId { get; init; }
    public string Content { get; init; } = string.Empty;
    public string ContentHighlight { get; init; } = string.Empty; // Snippet with <mark> tags
    public Guid ChannelId { get; init; }    // For channel messages
    public string ChannelName { get; init; } = string.Empty;
    public Guid AuthorId { get; init; }
    public string AuthorDisplayName { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public double Score { get; init; }
}
```

#### Implementation Details

**PostgreSQL** -- Uses a stored `SearchVector` column (type `tsvector`) on `Messages` and `DirectMessages` tables, maintained via a database trigger or computed column. A GIN index provides fast lookups. Queries use `plainto_tsquery` for user input and `ts_headline` for highlighted snippets.

**MySQL/MariaDB** -- Uses `FULLTEXT` indexes on the `Content` column. Queries use `MATCH(Content) AGAINST(:query IN BOOLEAN MODE)` with relevance scoring.

**SQLite** -- Uses FTS5 virtual tables (`messages_fts`, `direct_messages_fts`) that mirror the main tables. Kept in sync via database triggers on INSERT. Queries use the FTS5 `MATCH` syntax with `bm25()` for ranking and `snippet()` for highlighted results.

**Fallback** -- If FTS setup fails or is unavailable, falls back to `EF.Functions.Like(m.Content, $"%{query}%")` with no ranking. The `IsDegraded` flag on the response signals this to the client.

#### Search Scope and Permissions

- **Channel messages**: All authenticated users can search all channels (no per-channel permissions in MVP). `channelId` parameter optionally scopes to a single channel.
- **Direct messages**: Queries are always filtered to conversations involving the calling user (`senderId == callingUserId OR recipientId == callingUserId`). This is enforced at the service level.
- **Offset pagination**: Search results are relevance-ranked, not time-ordered, so cursor-based pagination does not apply. Standard page/pageSize is used.

#### Index Initialization

On application startup, `EnsureSearchIndexAsync` is called to create FTS infrastructure if it does not exist:
- PostgreSQL: Creates `SearchVector` column, GIN index, and update trigger
- MySQL/MariaDB: Creates `FULLTEXT` index
- SQLite: Creates FTS5 virtual table and sync triggers

For existing instances with messages, the admin endpoint `POST /api/v1/admin/search/reindex` rebuilds the search index by batch-processing all existing messages.

---

## 8. Configuration System

### 8.1 appsettings.json Structure

```json
{
  "HotBox": {
    "ServerName": "The HotBox",
    "Port": 5000
  },

  "Database": {
    "Provider": "sqlite",
    "ConnectionString": "Data Source=hotbox.db"
  },

  "Auth": {
    "RegistrationMode": "Open",
    "Seed": {
      "AdminEmail": "admin@hotbox.local",
      "AdminPassword": "ChangeMe123!",
      "AdminDisplayName": "Admin"
    },
    "Jwt": {
      "Secret": "CHANGE-THIS-TO-A-RANDOM-64-CHAR-STRING",
      "Issuer": "HotBox",
      "Audience": "HotBox",
      "AccessTokenExpirationMinutes": 15,
      "RefreshTokenExpirationDays": 7
    },
    "OAuth": {
      "Google": {
        "Enabled": false,
        "ClientId": "",
        "ClientSecret": ""
      },
      "Microsoft": {
        "Enabled": false,
        "ClientId": "",
        "ClientSecret": ""
      }
    }
  },

  "Search": {
    "Enabled": true,
    "DefaultPageSize": 25,
    "MaxPageSize": 100,
    "MinQueryLength": 2
  },

  "Voice": {
    "IceServers": [
      {
        "Urls": ["stun:stun.l.google.com:19302"]
      }
    ]
  },

  "Observability": {
    "Serilog": {
      "MinimumLevel": "Information",
      "Seq": {
        "Enabled": true,
        "ServerUrl": "http://localhost:5341"
      },
      "Elasticsearch": {
        "Enabled": false,
        "NodeUrls": ["http://localhost:9200"]
      }
    },
    "OpenTelemetry": {
      "Enabled": true,
      "Endpoint": "http://localhost:4317",
      "ServiceName": "HotBox"
    }
  },

  "Channels": {
    "DefaultChannels": [
      { "Name": "general", "Topic": "General conversation", "Type": "Text" },
      { "Name": "Lounge", "Topic": null, "Type": "Voice" }
    ]
  },

  "Roles": {
    "Definitions": ["Admin", "Moderator", "Member"],
    "DefaultRole": "Member"
  }
}
```

### 8.2 Options Pattern Classes

Each configuration section maps to a strongly-typed Options class:

| Options Class | Config Section | Registration |
|---------------|---------------|-------------|
| `HotBoxOptions` | `HotBox` | `services.Configure<HotBoxOptions>(config.GetSection("HotBox"))` |
| `DatabaseOptions` | `Database` | `services.Configure<DatabaseOptions>(config.GetSection("Database"))` |
| `AuthOptions` | `Auth` | `services.Configure<AuthOptions>(config.GetSection("Auth"))` |
| `OAuthProviderOptions` | `Auth:OAuth:{Provider}` | Bound per provider |
| `JwtOptions` | `Auth:Jwt` | `services.Configure<JwtOptions>(config.GetSection("Auth:Jwt"))` |
| `SearchOptions` | `Search` | `services.Configure<SearchOptions>(config.GetSection("Search"))` |
| `VoiceOptions` | `Voice` | `services.Configure<VoiceOptions>(config.GetSection("Voice"))` |
| `ObservabilityOptions` | `Observability` | `services.Configure<ObservabilityOptions>(config.GetSection("Observability"))` |

### 8.3 Environment Variable Overrides

All settings can be overridden via environment variables using the standard ASP.NET Core `__` separator convention:

```
Database__Provider=postgresql
Database__ConnectionString=Host=db;Database=hotbox;Username=hotbox;Password=secret
Auth__RegistrationMode=InviteOnly
Auth__Jwt__Secret=my-production-secret-key-at-least-64-chars
Auth__OAuth__Google__Enabled=true
Auth__OAuth__Google__ClientId=xxx.apps.googleusercontent.com
Auth__OAuth__Google__ClientSecret=xxx
```

---

## 9. Observability

### 9.1 Serilog Pipeline

**NuGet Packages:**
- `Serilog.AspNetCore` -- ASP.NET Core integration
- `Serilog.Sinks.Console` -- Console output
- `Serilog.Sinks.Seq` -- Seq log aggregation (development)
- `Elastic.Serilog.Sinks` -- Elasticsearch (production) -- the modern replacement for the archived `Serilog.Sinks.Elasticsearch`
- `Serilog.Sinks.Async` -- Async wrapper for production sinks
- `Serilog.Enrichers.Environment` -- Machine name, environment enrichment
- `Serilog.Exceptions` -- Structured exception detail

**Configuration in `ObservabilityExtensions.cs`:**

```csharp
public static IServiceCollection AddHotBoxObservability(
    this IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    // Serilog
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName()
        .Enrich.WithExceptionDetails()
        .WriteTo.Console()
        .WriteTo.Conditional(
            _ => configuration.GetValue<bool>("Observability:Serilog:Seq:Enabled"),
            wt => wt.Seq(configuration["Observability:Serilog:Seq:ServerUrl"]!))
        .WriteTo.Conditional(
            _ => configuration.GetValue<bool>("Observability:Serilog:Elasticsearch:Enabled"),
            wt => wt.Elasticsearch(/* Elastic.Serilog.Sinks config */))
        .CreateLogger();

    // OpenTelemetry
    services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(
                configuration["Observability:OpenTelemetry:Endpoint"]!)))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => o.Endpoint = new Uri(
                configuration["Observability:OpenTelemetry:Endpoint"]!)));

    return services;
}
```

### 9.2 OpenTelemetry Setup

**NuGet Packages:**
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`
- `OpenTelemetry.Instrumentation.EntityFrameworkCore`
- `OpenTelemetry.Exporter.OpenTelemetryProtocol` (OTLP exporter)

**What gets instrumented:**
- HTTP requests (ASP.NET Core middleware)
- Outgoing HTTP calls (HttpClient)
- EF Core database queries
- Custom spans for SignalR hub methods (manual instrumentation)

### 9.3 Development vs Production

| Aspect | Development | Production |
|--------|------------|------------|
| Log sink | Console + Seq | Console + Elasticsearch |
| Log level | Debug | Information |
| Traces | OTLP -> Seq (or Jaeger) | OTLP -> Elasticsearch APM |
| Metrics | OTLP -> console/Seq | OTLP -> Elasticsearch |
| Seq URL | `http://localhost:5341` | N/A |
| Elasticsearch | N/A | `http://elasticsearch:9200` |

---

## 10. Docker

### 10.1 Dockerfile (Multi-Stage Build)

```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY *.sln .
COPY src/HotBox.Core/*.csproj src/HotBox.Core/
COPY src/HotBox.Infrastructure/*.csproj src/HotBox.Infrastructure/
COPY src/HotBox.Application/*.csproj src/HotBox.Application/
COPY src/HotBox.Client/*.csproj src/HotBox.Client/
RUN dotnet restore

COPY src/ src/
RUN dotnet publish src/HotBox.Application/HotBox.Application.csproj \
    -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "HotBox.Application.dll"]
```

### 10.2 docker-compose.yml (Production)

```yaml
services:
  hotbox:
    build: .
    ports:
      - "5000:5000"
    environment:
      - Database__Provider=postgresql
      - Database__ConnectionString=Host=db;Database=hotbox;Username=hotbox;Password=${DB_PASSWORD}
      - Auth__Jwt__Secret=${JWT_SECRET}
      - Auth__RegistrationMode=InviteOnly
      - Observability__Serilog__Elasticsearch__Enabled=true
      - Observability__Serilog__Elasticsearch__NodeUrls__0=http://elasticsearch:9200
      - Observability__OpenTelemetry__Endpoint=http://elasticsearch:4317
    depends_on:
      db:
        condition: service_healthy
    restart: unless-stopped

  db:
    image: postgres:17
    environment:
      - POSTGRES_DB=hotbox
      - POSTGRES_USER=hotbox
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U hotbox"]
      interval: 5s
      timeout: 5s
      retries: 5
    restart: unless-stopped

  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.17.0
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
      - "ES_JAVA_OPTS=-Xms512m -Xmx512m"
    volumes:
      - esdata:/usr/share/elasticsearch/data
    restart: unless-stopped

volumes:
  pgdata:
  esdata:
```

### 10.3 docker-compose.dev.yml (Development)

```yaml
services:
  seq:
    image: datalust/seq:latest
    ports:
      - "5341:80"
    environment:
      - ACCEPT_EULA=Y
    restart: unless-stopped
```

Development uses SQLite (file-based, no container needed) and Seq for log aggregation.

### 10.4 Optional: TURN Server

For users behind restrictive NATs, a `coturn` container can be added:

```yaml
  coturn:
    image: coturn/coturn:latest
    ports:
      - "3478:3478/udp"
      - "3478:3478/tcp"
    command: >
      --no-tls
      --no-dtls
      --realm=hotbox.local
      --user=hotbox:changeme
      --listening-port=3478
    restart: unless-stopped
```

---

## 11. NuGet Package Summary

### HotBox.Core
```
(no external packages -- pure domain)
```

### HotBox.Infrastructure
```
Microsoft.AspNetCore.Identity.EntityFrameworkCore
Microsoft.EntityFrameworkCore.Sqlite
Npgsql.EntityFrameworkCore.PostgreSQL
Pomelo.EntityFrameworkCore.MySql
Microsoft.EntityFrameworkCore.Tools  (dev dependency)
```

### HotBox.Application
```
Microsoft.AspNetCore.SignalR
Microsoft.AspNetCore.Authentication.JwtBearer
Microsoft.AspNetCore.Authentication.Google
Microsoft.AspNetCore.Authentication.MicrosoftAccount
Serilog.AspNetCore
Serilog.Sinks.Console
Serilog.Sinks.Seq
Serilog.Sinks.Async
Serilog.Enrichers.Environment
Serilog.Exceptions
Elastic.Serilog.Sinks
OpenTelemetry.Extensions.Hosting
OpenTelemetry.Instrumentation.AspNetCore
OpenTelemetry.Instrumentation.Http
OpenTelemetry.Instrumentation.EntityFrameworkCore
OpenTelemetry.Exporter.OpenTelemetryProtocol
```

### HotBox.Client
```
Microsoft.AspNetCore.Components.WebAssembly
Microsoft.AspNetCore.SignalR.Client
Microsoft.Extensions.Http
```

---

## 12. Open Questions Resolved

| Question | Resolution |
|----------|-----------|
| WebRTC P2P vs SFU? | **P2P full mesh** for MVP. Audio-only with <10 users per channel is well within P2P limits. SFU can be added later as a sidecar if needed. |
| Notification delivery? | **SignalR push + Browser Notification API** via JSInterop. No push notification service needed. |
| Message history/search? | **Persist all messages**, paginate with cursor-based pagination. **Full-text search via native database FTS** (PostgreSQL `tsvector`/`tsquery`, MySQL `FULLTEXT`, SQLite FTS5). No external search engine needed. |
| User presence? | **Yes, MVP.** Tracked server-side via SignalR connection lifecycle + idle timer. |
| UI layout? | **Discord-inspired three-panel layout** per the HTML prototype at `temp/prototype.html`. |

---

## References

- [WebRTC Architecture: P2P vs SFU vs MCU](https://www.red5.net/blog/webrtc-architecture-p2p-sfu-mcu-xdn/)
- [SFU vs MCU vs P2P Comparison](https://getstream.io/blog/what-is-a-selective-forwarding-unit-in-webrtc/)
- [SIPSorcery .NET WebRTC Library](https://github.com/sipsorcery-org/sipsorcery)
- [LiveKit Open Source SFU](https://docs.livekit.io/intro/)
- [Serilog + OpenTelemetry Integration](https://github.com/serilog/serilog-sinks-opentelemetry)
- [Elastic Serilog Sink (replacement for archived Serilog.Sinks.Elasticsearch)](https://github.com/serilog-contrib/serilog-sinks-elasticsearch)
- [ASP.NET Core OpenTelemetry Packages](https://last9.io/blog/serilog-and-opentelemetry/)
- [Blazor WASM WebRTC Integration Patterns](https://github.com/aykay76/blazorcam)
