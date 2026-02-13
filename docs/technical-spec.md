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
|   |   |   +-- AppUser.cs
|   |   |   +-- Channel.cs
|   |   |   +-- Message.cs
|   |   |   +-- DirectMessage.cs
|   |   |   +-- Invite.cs
|   |   |   +-- ApiKey.cs
|   |   |   +-- RefreshToken.cs
|   |   |   +-- ServerSettings.cs
|   |   +-- Enums/
|   |   |   +-- ChannelType.cs
|   |   |   +-- RegistrationMode.cs
|   |   |   +-- UserStatus.cs
|   |   +-- Models/
|   |   |   +-- SearchQuery.cs
|   |   |   +-- SearchResult.cs
|   |   |   +-- SearchResultItem.cs
|   |   |   +-- ConversationSummary.cs
|   |   +-- Options/
|   |   |   +-- ServerOptions.cs
|   |   |   +-- DatabaseOptions.cs
|   |   |   +-- JwtOptions.cs
|   |   |   +-- OAuthOptions.cs
|   |   |   +-- IceServerOptions.cs
|   |   |   +-- ObservabilityOptions.cs
|   |   |   +-- AdminSeedOptions.cs
|   |   |   +-- SearchOptions.cs
|   |   +-- Interfaces/
|   |   |   +-- IChannelRepository.cs
|   |   |   +-- IMessageRepository.cs
|   |   |   +-- IDirectMessageRepository.cs
|   |   |   +-- IInviteRepository.cs
|   |   |   +-- IChannelService.cs
|   |   |   +-- IMessageService.cs
|   |   |   +-- IDirectMessageService.cs
|   |   |   +-- IInviteService.cs
|   |   |   +-- IPresenceService.cs
|   |   |   +-- INotificationService.cs
|   |   |   +-- IServerSettingsService.cs
|   |   |   +-- ITokenService.cs
|   |   |   +-- ISearchService.cs
|   |   +-- HotBox.Core.csproj
|   |
|   +-- HotBox.Infrastructure/          # Data access layer
|   |   +-- Data/
|   |   |   +-- HotBoxDbContext.cs
|   |   |   +-- Configurations/          # EF Core Fluent API configs
|   |   |   |   +-- AppUserConfiguration.cs
|   |   |   |   +-- ChannelConfiguration.cs
|   |   |   |   +-- MessageConfiguration.cs
|   |   |   |   +-- DirectMessageConfiguration.cs
|   |   |   |   +-- ApiKeyConfiguration.cs
|   |   |   |   +-- RefreshTokenConfiguration.cs
|   |   |   |   +-- ServerSettingsConfiguration.cs
|   |   |   +-- Migrations/
|   |   |   +-- Seeding/
|   |   |       +-- DatabaseSeeder.cs
|   |   +-- Repositories/
|   |   |   +-- ChannelRepository.cs
|   |   |   +-- MessageRepository.cs
|   |   |   +-- DirectMessageRepository.cs
|   |   |   +-- InviteRepository.cs
|   |   +-- Services/
|   |   |   +-- ChannelService.cs
|   |   |   +-- MessageService.cs
|   |   |   +-- DirectMessageService.cs
|   |   |   +-- InviteService.cs
|   |   |   +-- PresenceService.cs
|   |   |   +-- ServerSettingsService.cs
|   |   |   +-- TokenService.cs
|   |   |   +-- Search/
|   |   |       +-- PostgresSearchService.cs
|   |   |       +-- MySqlSearchService.cs
|   |   |       +-- SqliteSearchService.cs
|   |   |       +-- FallbackSearchService.cs
|   |   +-- DependencyInjection/
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
|   |   |   +-- AgentsController.cs
|   |   +-- Hubs/
|   |   |   +-- ChatHub.cs
|   |   |   +-- VoiceSignalingHub.cs
|   |   +-- Services/
|   |   |   +-- NotificationService.cs
|   |   +-- Authentication/
|   |   |   +-- ApiKeyAuthenticationHandler.cs
|   |   +-- Models/
|   |   |   +-- MessageResponse.cs
|   |   |   +-- DirectMessageResponse.cs
|   |   |   +-- UserResponse.cs
|   |   |   +-- (other DTOs)
|   |   +-- DependencyInjection/
|   |   |   +-- ApplicationServiceExtensions.cs
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
+-- Dockerfile
+-- docker-compose.yml
|
+-- docs/
```

### 2.2 Project Dependencies

```
HotBox.Core          --> Microsoft.Extensions.Identity.Stores (for IdentityUser<Guid>)
HotBox.Infrastructure --> HotBox.Core, EF Core, ASP.NET Identity
HotBox.Application    --> HotBox.Core, HotBox.Infrastructure, SignalR, Serilog, OpenTelemetry
HotBox.Client         --> (standalone Blazor WASM, communicates via HTTP/SignalR)
```

### 2.3 API Endpoint Design

All API endpoints are prefixed with `/api/`.

#### Authentication

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/auth/register` | Register new user (respects registration mode) |
| POST | `/api/auth/login` | Email/password login, returns JWT |
| POST | `/api/auth/refresh` | Refresh JWT token |
| POST | `/api/auth/logout` | Invalidate refresh token |
| GET | `/api/auth/providers` | List enabled OAuth providers |
| GET | `/api/auth/registration-mode` | Get current registration mode |
| GET | `/api/auth/external/{provider}` | Initiate OAuth flow |
| GET | `/api/auth/external/callback` | OAuth callback handler |

#### Channels

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/channels` | List all channels (text + voice) |
| POST | `/api/channels` | Create channel (admin/mod) |
| GET | `/api/channels/{id}` | Get channel details |
| PUT | `/api/channels/{id}` | Update channel (admin/mod) |
| DELETE | `/api/channels/{id}` | Delete channel (admin) |

#### Messages

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/channels/{channelId}/messages` | Get message history (paginated) |
| GET | `/api/messages/{id}` | Get message by ID |
| POST | `/api/channels/{channelId}/messages` | Post message (also via SignalR) |

#### Direct Messages

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/dm` | List DM conversations |
| GET | `/api/dm/{userId}` | Get DM history (paginated) |
| POST | `/api/dm/{userId}` | Send DM (also via SignalR) |

#### Search

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/search/messages` | Search channel messages (query params: `q`, `channelId?`, `senderId?`, `cursor?`, `limit`) |
| GET | `/api/search/status` | Get search index status |
| POST | `/api/search/reindex` | Rebuild search index from database (admin only) |

#### Users

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/users` | List all users |
| GET | `/api/users/search?q=term` | Search users by display name |
| GET | `/api/users/me` | Get own profile |
| PUT | `/api/users/me` | Update own profile |
| GET | `/api/users/{id}` | Get user profile |

#### Admin

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/admin/settings` | Get server settings |
| PUT | `/api/admin/settings` | Update server settings |
| GET | `/api/admin/users` | List all users (admin view) |
| POST | `/api/admin/users` | Create user (closed registration) |
| PUT | `/api/admin/users/{id}/role` | Assign role to user |
| DELETE | `/api/admin/users/{id}` | Delete user |
| GET | `/api/admin/invites` | List all invites |
| POST | `/api/admin/invites` | Generate invite code |
| DELETE | `/api/admin/invites/{code}` | Revoke invite |
| PUT | `/api/admin/channels/reorder` | Reorder channels |
| POST | `/api/admin/apikeys` | Create API key |
| GET | `/api/admin/apikeys` | List API keys |
| PUT | `/api/admin/apikeys/{id}/revoke` | Revoke API key |

#### Agents

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/admin/agents` | Create agent account with API key |
| GET | `/api/admin/agents` | List agents created by API key |

#### Health

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/health` | Health check endpoint (database connectivity) |

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
| `ReceiveMessage` | `message: MessageResponse` | New channel message |
| `ReceiveDirectMessage` | `message: DirectMessageResponse` | New DM |
| `UserJoinedChannel` | `channelId: Guid, userId: Guid, displayName: string` | User joined channel |
| `UserLeftChannel` | `channelId: Guid, userId: Guid` | User left channel |
| `UserTyping` | `channelId: Guid, userId: Guid` | Typing indicator |
| `UserStoppedTyping` | `channelId: Guid, userId: Guid` | Stopped typing |
| `DirectMessageTyping` | `senderId: Guid` | DM typing indicator |
| `DirectMessageStoppedTyping` | `senderId: Guid` | DM stopped typing |
| `UserStatusChanged` | `userId: Guid, displayName: string, status: UserStatus, isAgent: bool` | User status changed |
| `OnlineUsers` | `users: OnlineUserInfo[]` | Full list of online users (sent on connect) |
| `ReceiveNotification` | `notification: NotificationDto` | General notification |

**Connection Lifecycle:**
- `OnConnectedAsync`: Register connection, mark user online, broadcast `UserStatusChanged` to others, send `OnlineUsers` list to caller
- `OnDisconnectedAsync`: Remove connection, start grace period timer (30s). If no reconnect occurs within grace period, PresenceService raises `OnUserStatusChanged` event which triggers broadcast via IHubContext

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
| `GetIceServers` | (none) | Returns ICE server configuration |

**Client Methods:**

| Method | Parameters | Description |
|--------|-----------|-------------|
| `UserJoinedVoice` | `channelId: Guid, user: VoiceUserDto` | New peer in voice |
| `UserLeftVoice` | `channelId: Guid, userId: Guid` | Peer left voice |
| `ReceiveOffer` | `fromUserId: Guid, sdp: string` | Receive SDP offer |
| `ReceiveAnswer` | `fromUserId: Guid, sdp: string` | Receive SDP answer |
| `ReceiveIceCandidate` | `fromUserId: Guid, candidate: string` | Receive ICE candidate |
| `UserMuteChanged` | `channelId: Guid, userId: Guid, isMuted: bool` | Peer mute state (3 params) |
| `UserDeafenChanged` | `channelId: Guid, userId: Guid, isDeafened: bool` | Peer deafen state (3 params) |
| `VoiceChannelUsers` | `channelId: Guid, users: VoiceUserDto[]` | Full voice channel state on join |

**Connection Lifecycle:**
- `OnDisconnectedAsync`: Automatically removes user from all voice channels and notifies peers via `UserLeftVoice`

---

## 3. Frontend Architecture (Blazor WASM)

### 3.1 Project Structure

```
HotBox.Client/
+-- wwwroot/
|   +-- css/
|   |   +-- app.css                     # Main styles with design tokens
|   +-- js/
|   |   +-- webrtc-interop.js           # WebRTC JSInterop bridge
|   |   +-- notification-interop.js     # Browser notification API bridge
|   +-- index.html
+-- Layout/
|   +-- MainLayout.razor                # App shell: top bar + main content + members overlay
|   +-- MainLayout.razor.css
|   +-- AuthLayout.razor                # Layout for login/register pages
|   +-- AdminLayout.razor               # Layout for admin pages
+-- Pages/
|   +-- IndexRedirect.razor             # Redirect to /channels
|   +-- LoginPage.razor
|   +-- RegisterPage.razor
|   +-- OAuthCallbackPage.razor
|   +-- ChannelPage.razor               # Channel view
|   +-- DmsPage.razor                   # DM list view
|   +-- DirectMessagePage.razor         # DM conversation view
|   +-- AdminPage.razor                 # Admin dashboard
+-- Components/
|   +-- Chat/
|   |   +-- ChannelList.razor           # Horizontal channel tabs
|   |   +-- DirectMessageList.razor     # Horizontal DM tabs
|   |   +-- ChannelHeader.razor         # Channel info bar
|   |   +-- MessageList.razor           # Scrollable message area
|   |   +-- DirectMessageMessageList.razor  # DM message list
|   |   +-- MessageInput.razor          # Channel message input
|   |   +-- DirectMessageInput.razor    # DM message input
|   |   +-- TypingIndicator.razor
|   |   +-- MembersPanel.razor          # Right-side slide-in members panel
|   +-- SearchOverlay.razor             # Ctrl+K modal overlay
|   +-- ConnectionStatus.razor          # Connection status banner
|   +-- GlobalErrorBoundary.razor
|   +-- NewDmPicker.razor               # User picker for new DM
|   +-- Profile/
|   |   +-- UserProfilePopover.razor    # Popover for user profile
|   |   +-- EditProfileModal.razor      # Modal for editing own profile
|   +-- Admin/
|       +-- AdminServerSettings.razor
|       +-- AdminChannelManagement.razor
|       +-- AdminUserManagement.razor
|       +-- AdminInviteManagement.razor
|       +-- AdminApiKeyManagement.razor
+-- Services/
|   +-- ApiClient.cs                    # HttpClient wrapper (no interface)
|   +-- ChatHubService.cs               # SignalR ChatHub client (no interface)
|   +-- VoiceHubService.cs              # SignalR VoiceSignalingHub client (no interface)
|   +-- VoiceConnectionManager.cs       # Manages WebRTC peer connections
|   +-- WebRtcService.cs                # WebRTC JSInterop wrapper (no interface)
|   +-- NotificationService.cs          # Browser notification JSInterop (no interface)
|   +-- JwtParser.cs                    # JWT token parsing utility
+-- State/
|   +-- AppState.cs                     # Composite state (references all sub-states)
|   +-- ChannelState.cs                 # Active channel, message cache
|   +-- DirectMessageState.cs           # Active DM conversation
|   +-- VoiceState.cs                   # Voice connection state
|   +-- PresenceState.cs                # User online/offline/idle status
|   +-- AuthState.cs                    # Current user, JWT tokens
|   +-- SearchState.cs                  # Search query, results, loading state
+-- Models/
|   +-- MessageResponse.cs
|   +-- DirectMessageResponse.cs
|   +-- ChannelResponse.cs
|   +-- UserProfileResponse.cs
|   +-- UserInfo.cs
|   +-- AuthResponse.cs
|   +-- OnlineUserInfoModel.cs
|   +-- VoiceUserInfo.cs
|   +-- SearchResultModel.cs
|   +-- SearchResultItemModel.cs
|   +-- (many other *Response/*Request models)
+-- Program.cs
+-- _Imports.razor
+-- HotBox.Client.csproj
```

### 3.2 Component Hierarchy

```
App.razor
+-- Router
    +-- MainLayout.razor (top-bar layout)
        +-- ConnectionStatus.razor
        +-- TopBar
        |   +-- Brand
        |   +-- Section Switcher (Channels / DMs)
        |   +-- ChannelList.razor (horizontal tabs, rendered when "channels" active)
        |   +-- DirectMessageList.razor (horizontal tabs, rendered when "dms" active)
        |   +-- Voice Dropdown (voice channels + join button)
        |   +-- Members Toggle Button
        |   +-- Search Button (opens SearchOverlay)
        |   +-- Admin Link (if user is admin)
        |   +-- User Avatar (with profile popover)
        +-- Main Content Area (full width)
        |   +-- ChannelPage.razor (route: /channels/{id})
        |   |   +-- ChannelHeader.razor
        |   |   +-- MessageList.razor
        |   |   +-- TypingIndicator.razor
        |   |   +-- MessageInput.razor
        |   +-- DirectMessagePage.razor (route: /dm/{userId})
        |   |   +-- ChannelHeader.razor
        |   |   +-- DirectMessageMessageList.razor
        |   |   +-- TypingIndicator.razor
        |   |   +-- DirectMessageInput.razor
        +-- MembersPanel.razor (slide-in overlay from right, toggleable)
        +-- Bottom Voice Bar (when connected to voice)
        +-- SearchOverlay.razor (modal, Ctrl+K)
```

### 3.3 State Management

The client uses a service-based state management approach with no external state library. State is split into domain-specific sub-states, each registered as a scoped service (singleton-like in Blazor WASM) and using events to notify components of changes.

**State services:**
- `AuthState` — Current user, access token (in-memory), login/logout state
- `ChannelState` — Active channel, message cache, unread counts
- `DirectMessageState` — Active DM conversation, message cache, conversation summaries
- `PresenceState` — Online/offline/idle status for all users
- `VoiceState` — Current voice channel, connected peers, mute/deafen state
- `SearchState` — Search query, results, loading state
- `AppState` — Composite state that references all sub-states for convenience

```csharp
// Pattern used by all state services
public class ChannelState
{
    public event Action? OnChange;

    private ChannelResponse? _activeChannel;
    public ChannelResponse? ActiveChannel
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
    [Inject] private ChannelState ChannelState { get; set; } = default!;

    protected override void OnInitialized()
    {
        ChannelState.OnChange += StateHasChanged;
    }

    public void Dispose()
    {
        ChannelState.OnChange -= StateHasChanged;
    }
}
```

### 3.4 SignalR Client Integration

Both hub connections are established after login and maintained for the session lifetime. Reconnection is handled automatically with default exponential backoff (0s, 2s, 10s, 30s intervals).

```csharp
// ChatHubService.cs (concrete class, no interface)
public class ChatHubService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _baseUrl;
    private readonly ILogger<ChatHubService> _logger;

    public event Action<MessageResponse>? OnMessageReceived;
    public event Action<Guid, string, string, bool>? OnUserStatusChanged;
    public event Action<DirectMessageResponse>? OnDirectMessageReceived;
    // ... more events

    public async Task StartAsync(string accessToken)
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}/hubs/chat", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect()  // Uses default intervals
            .Build();

        RegisterHandlers(_hubConnection);
        RegisterLifecycleEvents(_hubConnection);

        await _hubConnection.StartAsync();
        _logger.LogInformation("ChatHub connection started");
    }

    private void RegisterHandlers(HubConnection connection)
    {
        connection.On<MessageResponse>("ReceiveMessage", message =>
        {
            OnMessageReceived?.Invoke(message);
        });

        connection.On<Guid, string, string, bool>("UserStatusChanged", (userId, displayName, status, isAgent) =>
        {
            OnUserStatusChanged?.Invoke(userId, displayName, status, isAgent);
        });

        // ... other handlers
    }
}
```

### 3.5 Design Tokens

The UI design system is defined in `wwwroot/css/app.css` using CSS custom properties. Key tokens include:

| Token | Value | Usage |
|-------|-------|-------|
| **Background layers** | | Deep cool slate palette |
| `--bg-deepest` | `#0c0c0f` | Outermost background |
| `--bg-deep` | `#111116` | Top bar background |
| `--bg-base` | `#16161d` | Main content area |
| `--bg-raised` | `#1c1c25` | Message input, cards |
| `--bg-surface` | `#23232e` | Avatars, elevated surfaces |
| `--bg-hover` | `#2a2a37` | Hover states |
| `--bg-active` | `#32323f` | Active/selected states |
| **Text** | | |
| `--text-primary` | `#e2e2ea` | Primary text |
| `--text-secondary` | `#9898a8` | Secondary text, message bodies |
| `--text-muted` | `#5c5c72` | Timestamps, hints |
| `--text-faint` | `#3e3e52` | Very subtle text |
| **Accent** | | Cool teal/mint |
| `--accent` | `#5de4c7` | Links, active indicators, primary accent |
| `--accent-hover` | `#7aecd5` | Hover state |
| `--accent-muted` | `rgba(93, 228, 199, 0.10)` | Subtle background tint |
| `--accent-strong` | `#a0f0de` | Strong emphasis |
| `--accent-glow` | `rgba(93, 228, 199, 0.06)` | Glow/aura effect |
| **Status** | | User presence indicators |
| `--status-online` | `#6bc76b` | Online status dot |
| `--status-idle` | `#c7a63e` | Idle status dot |
| `--status-dnd` | `#c76060` | Do Not Disturb status dot |
| `--status-offline` | `#4a4a5a` | Offline status dot |
| **Voice** | | |
| `--voice-active` | `#6bc76b` | Voice connected/speaking |
| `--voice-muted` | `#5c5c72` | Voice muted |
| **Bot Badge** | | |
| `--bot-badge-bg` | `rgba(93, 228, 199, 0.12)` | Bot badge background |
| `--bot-badge-color` | `var(--accent)` | Bot badge text |
| **Borders** | | |
| `--border-subtle` | `rgba(255, 255, 255, 0.04)` | Very subtle border |
| `--border-light` | `rgba(255, 255, 255, 0.07)` | Light border |
| `--border-focus` | `rgba(93, 228, 199, 0.3)` | Focus ring |
| **Radius** | | |
| `--radius-xs` | `3px` | Very small radius |
| `--radius-sm` | `4px` | Small radius |
| `--radius-md` | `8px` | Medium radius |
| `--radius-lg` | `12px` | Large radius |
| `--radius-pill` | `9999px` | Pill shape |
| **Shadows** | | |
| `--shadow-md` | `0 4px 24px rgba(0, 0, 0, 0.5)` | Medium shadow |
| `--shadow-lg` | `0 8px 48px rgba(0, 0, 0, 0.6)` | Large shadow |
| `--shadow-overlay` | `0 12px 40px rgba(0, 0, 0, 0.7), 0 0 0 1px var(--border-subtle)` | Overlay shadow |
| **Transitions** | | |
| `--transition-fast` | `100ms ease` | Fast transition |
| `--transition-base` | `180ms ease` | Base transition |
| `--transition-smooth` | `280ms cubic-bezier(0.4, 0, 0.2, 1)` | Smooth transition |
| **Typography** | | |
| `--font-body` | `'DM Sans', -apple-system, BlinkMacSystemFont, sans-serif` | Body font |
| `--font-mono` | `'JetBrains Mono', 'SF Mono', 'Consolas', monospace` | Monospace font |

---

## 4. Database Design

### 4.1 Entity Models

#### AppUser (extends IdentityUser)

```
AppUser (inherits IdentityUser<Guid>)
├── DisplayName          : string (required, max 32)
├── AvatarUrl            : string? (nullable, future use)
├── Bio                  : string? (nullable, max 500)
├── Pronouns             : string? (nullable, max 50)
├── CustomStatus         : string? (nullable, max 100)
├── Status               : UserStatus (enum: Online, Idle, DnD, Offline)
├── IsAgent              : bool (default false, marks bot accounts)
├── CreatedByApiKeyId    : Guid? (nullable FK -> ApiKey)
├── CreatedByApiKey      : ApiKey? (navigation property)
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

#### ApiKey

```
ApiKey
├── Id                   : Guid (PK)
├── KeyValue             : string (SHA-256 hash of actual key)
├── KeyPrefix            : string (first 8 chars of key, for lookup)
├── Name                 : string (user-friendly label)
├── CreatedAtUtc         : DateTime
├── RevokedAtUtc         : DateTime? (nullable)
├── RevokedReason        : string? (nullable)
├── IsRevoked            : bool (computed property)
├── IsActive             : bool (computed property)
├── CreatedAgents        : ICollection<AppUser>
```

#### RefreshToken

```
RefreshToken
├── Id                   : Guid (PK)
├── Token                : string (unique, hashed)
├── UserId               : Guid (FK -> AppUser)
├── ExpiresAtUtc         : DateTime
├── CreatedAtUtc         : DateTime
├── RevokedAtUtc         : DateTime? (nullable)
├── ReplacedByToken      : string? (nullable, for rotation tracking)
├── IsRevoked            : bool (computed property)
```

#### ServerSettings

```
ServerSettings
├── Id                   : Guid (PK, singleton table)
├── ServerName           : string (max 100)
├── RegistrationMode     : RegistrationMode (enum)
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
- Password requirements: minimum 8 characters, at least one uppercase, one lowercase, one digit, `RequireNonAlphanumeric = false`, `RequiredUniqueChars = 4`
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
  "AdminSeed": {
    "Email": "admin@hotbox.local",
    "Password": "ChangeMe123!",
    "DisplayName": "Admin"
  }
}
```

This is handled by the `DatabaseSeeder` hosted service, which runs after EF migrations during application startup.

### 5.7 API Key Authentication

API keys enable programmatic access for agent accounts (bots, integrations) without requiring password-based login.

#### Architecture

- Custom authentication handler: `ApiKeyAuthenticationHandler` (registered as `"ApiKey"` scheme)
- Header-based authentication: `X-Api-Key: hb_xxxxxxxxxxxxx`
- SHA-256 hashing with prefix-based lookup for performance
- Dual authentication policy: `JwtOrApiKey` policy accepts either JWT bearer tokens or API keys

#### API Key Format

- Generated key: `hb_{32-char-random-base62}` (e.g., `hb_A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6`)
- Prefix (`hb_`) stored separately for fast lookup
- Full key is SHA-256 hashed before storage

#### Flow

1. Admin creates API key via `POST /api/admin/apikeys` with a friendly name
2. Server generates random key, stores hash + prefix, returns full key **once** (never shown again)
3. Agent uses API key to create bot account via `POST /api/admin/agents`
4. Bot account includes `IsAgent = true` flag and reference to creating API key
5. Bot sends messages via REST API or SignalR using `X-Api-Key` header
6. API keys can be revoked via `PUT /api/admin/apikeys/{id}/revoke`

#### Security Considerations

- API keys bypass password rotation and 2FA (intentional for service accounts)
- Keys are scoped to admin-level permissions (for agent creation only)
- Revocation is immediate and enforced at authentication handler level
- LastUsedAtUtc tracking for audit purposes

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
    Task<SearchResult> SearchMessagesAsync(SearchQuery query, CancellationToken ct = default);

    Task InitializeIndexAsync(CancellationToken ct = default);

    Task ReindexAsync(CancellationToken ct = default);

    bool IsFullTextSearchAvailable { get; }

    string ProviderName { get; }
}
```

#### Search Query and Result Models

```csharp
// HotBox.Core/Models/SearchQuery.cs
public class SearchQuery
{
    public string QueryText { get; set; } = string.Empty;
    public Guid? ChannelId { get; set; }
    public Guid? SenderId { get; set; }
    public string? Cursor { get; set; }  // Opaque cursor for pagination
    public int Limit { get; set; } = 20;
}

// HotBox.Core/Models/SearchResult.cs
public class SearchResult
{
    public IReadOnlyList<SearchResultItem> Items { get; set; } = [];
    public string? Cursor { get; set; }  // Opaque cursor for next page
    public int TotalEstimate { get; set; }  // Estimated total (not exact count)
}

// HotBox.Core/Models/SearchResultItem.cs
public class SearchResultItem
{
    public Guid MessageId { get; set; }
    public string Snippet { get; set; } = string.Empty;  // Snippet with highlighting
    public Guid ChannelId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public double RelevanceScore { get; set; }
}
```

**Key changes from offset pagination:**
- Uses opaque cursor-based pagination instead of page/pageSize
- `TotalEstimate` instead of `TotalCount` (exact counts are expensive for FTS)
- `Snippet` field contains highlighted text (implementation-dependent format)
- `RelevanceScore` instead of `Score` for clarity

#### Implementation Details

**PostgreSQL** -- Uses a stored `SearchVector` column (type `tsvector`) on `Messages` and `DirectMessages` tables, maintained via a database trigger or computed column. A GIN index provides fast lookups. Queries use `plainto_tsquery` for user input and `ts_headline` for highlighted snippets.

**MySQL/MariaDB** -- Uses `FULLTEXT` indexes on the `Content` column. Queries use `MATCH(Content) AGAINST(:query IN BOOLEAN MODE)` with relevance scoring.

**SQLite** -- Uses FTS5 virtual tables (`messages_fts`, `direct_messages_fts`) that mirror the main tables. Kept in sync via database triggers on INSERT. Queries use the FTS5 `MATCH` syntax with `bm25()` for ranking and `snippet()` for highlighted results.

**Fallback** -- If FTS setup fails or is unavailable, falls back to `EF.Functions.Like(m.Content, $"%{query}%")` with no ranking. The `IsDegraded` flag on the response signals this to the client.

#### Search Scope and Permissions

- **Channel messages**: All authenticated users can search all channels (no per-channel permissions in MVP). `channelId` parameter optionally scopes to a single channel.
- **Direct messages**: Not implemented in MVP. Future implementation would filter to conversations involving the calling user.
- **Cursor-based pagination**: Search results use cursor-based pagination for efficiency. The cursor is an opaque string (implementation-specific) that encodes the pagination state.

#### Index Initialization

On application startup (in `Program.cs`), `InitializeIndexAsync` is called to create FTS infrastructure if it does not exist:
- PostgreSQL: Creates `SearchVector` column, GIN index, and update trigger
- MySQL/MariaDB: Creates `FULLTEXT` index
- SQLite: Creates FTS5 virtual table and sync triggers

For existing instances with messages, the admin endpoint `POST /api/search/reindex` rebuilds the search index by batch-processing all existing messages.

---

## 8. Configuration System

### 8.1 appsettings.json Structure

```json
{
  "Server": {
    "ServerName": "HotBox",
    "Port": 5000,
    "RegistrationMode": "InviteOnly"
  },

  "Database": {
    "Provider": "sqlite",
    "ConnectionString": "Data Source=hotbox.db"
  },

  "Jwt": {
    "Secret": "",
    "Issuer": "HotBox",
    "Audience": "HotBox",
    "AccessTokenExpiration": "00:15:00",
    "RefreshTokenExpiration": "7.00:00:00"
  },

  "OAuth": {
    "Google": {
      "ClientId": "",
      "ClientSecret": "",
      "Enabled": false
    },
    "Microsoft": {
      "ClientId": "",
      "ClientSecret": "",
      "Enabled": false
    }
  },

  "IceServers": {
    "StunUrls": [
      "stun:stun.l.google.com:19302"
    ],
    "TurnUrl": "",
    "TurnUsername": "",
    "TurnCredential": ""
  },

  "Observability": {
    "SeqUrl": "http://localhost:5341",
    "OtlpEndpoint": "",
    "LogLevel": "Information"
  },

  "AdminSeed": {
    "Email": "",
    "Password": "",
    "DisplayName": ""
  }
}
```

**Key differences from the original spec:**
- Top-level section is `Server` (not `HotBox`)
- `RegistrationMode` is in `Server` (not `Auth`)
- JWT section is top-level (not `Auth.Jwt`)
- OAuth section is top-level (not `Auth.OAuth`)
- `IceServers` is a flat object with arrays, not nested `Voice.IceServers`
- `Observability` is flat (no nested `Serilog`/`OpenTelemetry` sections)
- `AdminSeed` is top-level (not `Auth.Seed`)
- JWT expiration uses TimeSpan format (`"00:15:00"`) not integer minutes
- No `Search` section (search is always enabled)
- No `Channels` or `Roles` sections (seeded by DatabaseSeeder)

### 8.2 Options Pattern Classes

Each configuration section maps to a strongly-typed Options class (all located in `HotBox.Core/Options/`):

| Options Class | Config Section | Location |
|---------------|---------------|----------|
| `ServerOptions` | `Server` | `HotBox.Core/Options/` |
| `DatabaseOptions` | `Database` | `HotBox.Core/Options/` |
| `JwtOptions` | `Jwt` | `HotBox.Core/Options/` |
| `OAuthOptions` | `OAuth` | `HotBox.Core/Options/` |
| `IceServerOptions` | `IceServers` | `HotBox.Core/Options/` |
| `ObservabilityOptions` | `Observability` | `HotBox.Core/Options/` |
| `AdminSeedOptions` | `AdminSeed` | `HotBox.Core/Options/` |
| `SearchOptions` | (internal defaults) | `HotBox.Core/Options/` |

**Note:** All Options classes are in the Core project (not Application) to avoid circular dependencies and allow Infrastructure services to access configuration.

### 8.3 Environment Variable Overrides

All settings can be overridden via environment variables using the standard ASP.NET Core `__` separator convention:

```
Database__Provider=postgresql
Database__ConnectionString=Host=db;Database=hotbox;Username=hotbox;Password=secret
Server__RegistrationMode=InviteOnly
Jwt__Secret=my-production-secret-key-at-least-64-chars
OAuth__Google__Enabled=true
OAuth__Google__ClientId=xxx.apps.googleusercontent.com
OAuth__Google__ClientSecret=xxx
AdminSeed__Email=admin@example.com
AdminSeed__Password=SecurePassword123
```

---

## 9. Observability

### 9.1 Serilog Pipeline

**NuGet Packages:**
- `Serilog.AspNetCore` -- ASP.NET Core integration
- `Serilog.Sinks.Console` (included with AspNetCore) -- Console output
- `Serilog.Sinks.Seq` -- Seq log aggregation
- `Serilog.Sinks.Elasticsearch` -- Elasticsearch sink (archived but still functional)
- `Serilog.Enrichers.Environment` -- Machine name enrichment
- `Serilog.Enrichers.Thread` -- Thread ID enrichment

**Configuration in `ObservabilityExtensions.cs`:**

```csharp
public static IHostBuilder AddObservability(
    this IHostBuilder hostBuilder,
    IConfiguration configuration)
{
    hostBuilder.UseSerilog((context, services, loggerConfig) =>
    {
        var obsOptions = configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();

        // Parse minimum log level from config with safe fallback
        if (!Enum.TryParse<Serilog.Events.LogEventLevel>(obsOptions.LogLevel, ignoreCase: true, out var minLevel))
        {
            minLevel = Serilog.Events.LogEventLevel.Information;
        }

        loggerConfig
            .MinimumLevel.Is(minLevel)
            .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console();

        // Seq sink (if URL configured)
        if (!string.IsNullOrWhiteSpace(obsOptions.SeqUrl))
        {
            loggerConfig.WriteTo.Seq(obsOptions.SeqUrl);
        }
    });

    return hostBuilder;
}
```

**Key differences from original spec:**
- Uses `Serilog.Sinks.Elasticsearch` (archived package), NOT `Elastic.Serilog.Sinks`
- Console + Seq only (no conditional Elasticsearch in current implementation)
- Uses `Enrich.WithThreadId()` not `Enrich.WithExceptionDetails()`
- Does NOT include `Serilog.Exceptions` or `Serilog.Sinks.Async`
- Includes `Serilog.Enrichers.Thread` (not originally documented)

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
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY HotBox.sln ./
COPY src/HotBox.Core/HotBox.Core.csproj src/HotBox.Core/
COPY src/HotBox.Infrastructure/HotBox.Infrastructure.csproj src/HotBox.Infrastructure/
COPY src/HotBox.Application/HotBox.Application.csproj src/HotBox.Application/
COPY src/HotBox.Client/HotBox.Client.csproj src/HotBox.Client/

# Restore dependencies (app only, skip test projects)
RUN dotnet restore src/HotBox.Application/HotBox.Application.csproj

# Copy everything else and build
COPY src/ src/
RUN dotnet publish src/HotBox.Application/HotBox.Application.csproj \
    -c Release \
    -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN adduser --disabled-password --gecos '' hotbox \
    && mkdir -p /data \
    && chown hotbox:hotbox /data
USER hotbox

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HotBox.Application.dll"]
```

**Key differences:**
- Uses .NET 8.0 (not 9.0)
- Exposes port 8080 (not 5000)
- Installs `curl` for health checks
- Creates non-root `hotbox` user and switches to it
- Creates `/data` directory for SQLite database persistence

### 10.2 docker-compose.yml

```yaml
services:
  hotbox:
    build: .
    container_name: hotbox
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Database__Provider=postgresql
      - Database__ConnectionString=Host=db;Port=5432;Database=hotbox;Username=hotbox;Password=${DB_PASSWORD:-changeme}
      - Jwt__Secret=${JWT_SECRET:-super-secret-key-change-in-production-minimum-32-chars}
      - Observability__SeqUrl=http://seq:5341
      - AdminSeed__Email=${ADMIN_EMAIL:-admin@hotbox.local}
      - AdminSeed__Password=${ADMIN_PASSWORD:-Admin123!}
      - AdminSeed__DisplayName=${ADMIN_DISPLAY_NAME:-Admin}
    depends_on:
      db:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s

  db:
    image: postgres:16-alpine
    container_name: hotbox-db
    restart: unless-stopped
    environment:
      POSTGRES_DB: hotbox
      POSTGRES_USER: hotbox
      POSTGRES_PASSWORD: ${DB_PASSWORD:-changeme}
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U hotbox"]
      interval: 5s
      timeout: 3s
      retries: 5

  seq:
    image: datalust/seq:latest
    container_name: hotbox-seq
    restart: unless-stopped
    environment:
      ACCEPT_EULA: "Y"
    ports:
      - "5342:80"
    volumes:
      - seqdata:/data

volumes:
  pgdata:
  seqdata:
```

**Key differences:**
- Single `docker-compose.yml` at repo root (no separate dev file)
- Uses PostgreSQL 16-alpine (not 17)
- Exposes port 8080 (not 5000)
- Includes Seq service (port 5342:80) — NOT Elasticsearch
- Healthcheck uses `/health` endpoint
- Environment variables have sensible defaults with `:-` syntax
- Docker files at repo root, NOT in `docker/` directory

### 10.3 Optional: TURN Server

For users behind restrictive NATs, a `coturn` container can be added to `docker-compose.yml`:

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

Then update the `IceServers` configuration in `appsettings.json`:
```json
"IceServers": {
  "StunUrls": ["stun:stun.l.google.com:19302"],
  "TurnUrl": "turn:localhost:3478",
  "TurnUsername": "hotbox",
  "TurnCredential": "changeme"
}
```

---

## 11. NuGet Package Summary

### HotBox.Core
```
Microsoft.Extensions.Identity.Stores
```

### HotBox.Infrastructure
```
Microsoft.EntityFrameworkCore
Microsoft.AspNetCore.Identity.EntityFrameworkCore
Microsoft.EntityFrameworkCore.Sqlite
Microsoft.Extensions.Configuration.Binder
System.IdentityModel.Tokens.Jwt
Npgsql.EntityFrameworkCore.PostgreSQL
Pomelo.EntityFrameworkCore.MySql
```

### HotBox.Application
```
Microsoft.AspNetCore.Authentication.Google
Microsoft.AspNetCore.Authentication.JwtBearer
Microsoft.AspNetCore.Authentication.MicrosoftAccount
Microsoft.AspNetCore.Components.WebAssembly.Server
Microsoft.AspNetCore.OpenApi
Microsoft.EntityFrameworkCore.Design  (dev dependency)
Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
Swashbuckle.AspNetCore
Serilog.AspNetCore
Serilog.Sinks.Seq
Serilog.Enrichers.Environment
Serilog.Enrichers.Thread
Serilog.Sinks.Elasticsearch
OpenTelemetry.Extensions.Hosting
OpenTelemetry.Instrumentation.AspNetCore
OpenTelemetry.Instrumentation.EntityFrameworkCore
OpenTelemetry.Instrumentation.Http
OpenTelemetry.Exporter.OpenTelemetryProtocol
```

**Note:** The Application project uses `Serilog.Sinks.Elasticsearch` (the archived package), NOT `Elastic.Serilog.Sinks`. It does NOT include `Serilog.Exceptions` or `Serilog.Sinks.Async`.

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
