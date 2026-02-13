# Implementation Plan: HotBox

**Version**: 1.0
**Date**: 2026-02-11
**Companion Document**: `docs/technical-spec.md`

> **Implementation Status (Feb 2026)**: Phases 1-4 are substantially complete. Additional features implemented outside the original plan: API key management, MCP agent accounts, user profiles (bio, pronouns, custom status). Phases 5+ remain pending.

---

## 1. Solution and Project Structure

### 1.1 Create Solution

```
HotBox.sln
|
+-- src/
|   +-- HotBox.Core/                  # .NET 8 Class Library
|   +-- HotBox.Infrastructure/        # .NET 8 Class Library
|   +-- HotBox.Application/           # ASP.NET Core 8 Web Application
|   +-- HotBox.Client/                # Blazor WebAssembly Standalone App
|
+-- tests/
|   +-- HotBox.Core.Tests/            # xUnit
|   +-- HotBox.Infrastructure.Tests/  # xUnit + EF Core InMemory/SQLite
|   +-- HotBox.Application.Tests/     # xUnit + WebApplicationFactory
|   +-- HotBox.Client.Tests/          # bUnit
|
+-- docker/
|   +-- Dockerfile
|   +-- docker-compose.yml
|   +-- docker-compose.dev.yml
|   +-- .env.example
|
+-- docs/
|   +-- requirements.md
|   +-- technical-spec.md
|   +-- implementation-plan.md
```

### 1.2 Project Creation Commands

```bash
dotnet new sln -n HotBox
dotnet new classlib -n HotBox.Core -o src/HotBox.Core
dotnet new classlib -n HotBox.Infrastructure -o src/HotBox.Infrastructure
dotnet new web -n HotBox.Application -o src/HotBox.Application
dotnet new blazorwasm -n HotBox.Client -o src/HotBox.Client --empty

dotnet sln add src/HotBox.Core
dotnet sln add src/HotBox.Infrastructure
dotnet sln add src/HotBox.Application
dotnet sln add src/HotBox.Client

dotnet add src/HotBox.Infrastructure reference src/HotBox.Core
dotnet add src/HotBox.Application reference src/HotBox.Core
dotnet add src/HotBox.Application reference src/HotBox.Infrastructure
```

### 1.3 Key Technical Decisions

| Decision | Rationale |
|----------|-----------|
| .NET 8 | Current LTS release with stable Blazor WASM + SignalR support |
| Separate Blazor WASM project | Deployed as static files, served by the ASP.NET Core host. Keeps client and server cleanly separated. |
| P2P WebRTC for voice | No external SFU dependency. Aligns with privacy goals and <10 user voice channels. See technical spec Section 6.1 for full analysis. |
| JWT (access) + HttpOnly cookie (refresh) | Secure token management for Blazor WASM. Access token in memory avoids XSS exposure; refresh in HttpOnly cookie avoids CSRF with SameSite. |
| Cursor-based pagination | More efficient than offset pagination for real-time message streams. Uses `CreatedAtUtc` as cursor. |
| In-memory presence tracking | `ConcurrentDictionary` is sufficient for ~100 users. Avoids Redis dependency. |
| `Elastic.Serilog.Sinks` over `Serilog.Sinks.Elasticsearch` | The original sink is archived. Elastic's official replacement is actively maintained. |

---

## 2. Phase Breakdown

### Phase 1: Project Scaffolding and Core Infrastructure
**Estimated effort**: 2-3 days
**Dependencies**: None (starting point)

#### What gets built:
1. **Solution structure** -- Create all projects with correct references
2. **Core layer** -- Entity classes, enums, repository interfaces
3. **Infrastructure layer** -- DbContext, entity configurations, repository implementations, multi-provider setup
4. **Configuration** -- Options classes, `appsettings.json` with all sections, environment variable overrides
5. **DI registration** -- `InfrastructureServiceExtensions.cs` and `ApplicationServiceExtensions.cs` using `IServiceCollection` extension methods
6. **Observability** -- Serilog + OpenTelemetry wired up in `ObservabilityExtensions.cs`
7. **Database migrations** -- Initial migration for all entities
8. **Docker** -- Dockerfile, docker-compose.yml, docker-compose.dev.yml (with Seq)

#### Files created:

**Core layer:**
- `src/HotBox.Core/Entities/Channel.cs`
- `src/HotBox.Core/Entities/Message.cs`
- `src/HotBox.Core/Entities/DirectMessage.cs`
- `src/HotBox.Core/Entities/Invite.cs`
- `src/HotBox.Core/Enums/ChannelType.cs`
- `src/HotBox.Core/Enums/RegistrationMode.cs`
- `src/HotBox.Core/Enums/UserStatus.cs`
- `src/HotBox.Core/Interfaces/IChannelRepository.cs`
- `src/HotBox.Core/Interfaces/IMessageRepository.cs`
- `src/HotBox.Core/Interfaces/IDirectMessageRepository.cs`
- `src/HotBox.Core/Interfaces/IInviteRepository.cs`
- `src/HotBox.Core/Options/HotBoxOptions.cs`
- `src/HotBox.Core/Options/AuthOptions.cs`
- `src/HotBox.Core/Options/JwtOptions.cs`
- `src/HotBox.Core/Options/OAuthProviderOptions.cs`
- `src/HotBox.Core/Options/DatabaseOptions.cs`
- `src/HotBox.Core/Options/VoiceOptions.cs`
- `src/HotBox.Core/Options/ObservabilityOptions.cs`

**Infrastructure layer:**
- `src/HotBox.Infrastructure/Identity/AppUser.cs`
- `src/HotBox.Infrastructure/Data/HotBoxDbContext.cs`
- `src/HotBox.Infrastructure/Data/Configurations/ChannelConfiguration.cs`
- `src/HotBox.Infrastructure/Data/Configurations/MessageConfiguration.cs`
- `src/HotBox.Infrastructure/Data/Configurations/DirectMessageConfiguration.cs`
- `src/HotBox.Infrastructure/Data/Configurations/InviteConfiguration.cs`
- `src/HotBox.Infrastructure/Repositories/ChannelRepository.cs`
- `src/HotBox.Infrastructure/Repositories/MessageRepository.cs`
- `src/HotBox.Infrastructure/Repositories/DirectMessageRepository.cs`
- `src/HotBox.Infrastructure/Repositories/InviteRepository.cs`
- `src/HotBox.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs`

**Application layer:**
- `src/HotBox.Application/DependencyInjection/ApplicationServiceExtensions.cs`
- `src/HotBox.Application/DependencyInjection/ObservabilityExtensions.cs`
- `src/HotBox.Application/Program.cs`
- `src/HotBox.Application/appsettings.json`
- `src/HotBox.Application/appsettings.Development.json`

**Docker:**
- `docker/Dockerfile`
- `docker/docker-compose.yml`
- `docker/docker-compose.dev.yml`
- `docker/.env.example`

#### Acceptance criteria:
- `dotnet build` succeeds for all projects
- `dotnet run --project src/HotBox.Application` starts the server
- SQLite database is created on first run with all tables
- Serilog writes structured logs to console
- Seq receives logs in development (via docker-compose.dev.yml)
- Configuration loads from appsettings.json and can be overridden by environment variables

---

### Phase 2: Authentication and User Management
**Estimated effort**: 3-4 days
**Dependencies**: Phase 1 complete

#### What gets built:
1. **ASP.NET Identity** -- Configure Identity with AppUser, roles, password policy
2. **JWT auth** -- Token generation, validation, refresh flow
3. **Auth controller** -- Register, login, logout, refresh, OAuth endpoints
4. **Registration modes** -- Open, invite-only, closed logic
5. **Admin seeding** -- Create admin user on first run from config
6. **Role seeding** -- Create Admin, Moderator, Member roles
7. **Default channel seeding** -- Create configured default channels on first run
8. **OAuth** -- Google and Microsoft external auth (conditional on config)
9. **User controller** -- Profile endpoints

#### Files created:

**Application layer:**
- `src/HotBox.Application/Controllers/AuthController.cs`
- `src/HotBox.Application/Controllers/UsersController.cs`
- `src/HotBox.Application/Extensions/AuthenticationExtensions.cs`
- `src/HotBox.Application/Services/ITokenService.cs`
- `src/HotBox.Application/Services/TokenService.cs`

**Infrastructure layer:**
- `src/HotBox.Infrastructure/Data/Seeding/DatabaseSeeder.cs`

#### Acceptance criteria:
- User can register (when mode allows), login, receive JWT
- JWT refresh works via HttpOnly cookie
- Admin account is auto-created from config on first run
- Roles are seeded on first run
- Default channels are created on first run
- OAuth login works when providers are configured
- Login UI endpoint returns list of enabled auth providers
- Registration is blocked when mode is Closed (except admin creation)
- InviteOnly mode requires valid invite code to register

---

### Phase 3: Text Channels and Real-Time Messaging
**Estimated effort**: 4-5 days
**Dependencies**: Phase 2 complete

#### What gets built:
1. **Channel service** -- CRUD operations for channels
2. **Message service** -- Send, retrieve (paginated) messages
3. **ChatHub** -- SignalR hub for real-time messaging, typing indicators
4. **Channel controller** -- REST endpoints for channel management
5. **Message controller** -- REST endpoints for message history
6. **Blazor client shell** -- MainLayout with sidebar, chat area, members panel
7. **Blazor SignalR client** -- ChatHubService connecting to ChatHub
8. **Blazor state management** -- AppState, ChannelState, AuthState
9. **Core chat components** -- ChannelList, MessageList, MessageInput, ChannelHeader

#### Files created:

**Core layer:**
- `src/HotBox.Core/Interfaces/IChannelService.cs`
- `src/HotBox.Core/Interfaces/IMessageService.cs`

**Infrastructure layer:**
- `src/HotBox.Infrastructure/Services/ChannelService.cs`
- `src/HotBox.Infrastructure/Services/MessageService.cs`

**Application layer:**
- `src/HotBox.Application/Controllers/ChannelsController.cs`
- `src/HotBox.Application/Controllers/MessagesController.cs`
- `src/HotBox.Application/Hubs/ChatHub.cs`

**Client:**
- `src/HotBox.Client/Layout/MainLayout.razor`
- `src/HotBox.Client/Layout/MainLayout.razor.css`
- `src/HotBox.Client/Components/Sidebar/Sidebar.razor`
- `src/HotBox.Client/Components/Sidebar/ServerHeader.razor`
- `src/HotBox.Client/Components/Sidebar/ChannelList.razor`
- `src/HotBox.Client/Components/Sidebar/ChannelItem.razor`
- `src/HotBox.Client/Components/Sidebar/UserPanel.razor`
- `src/HotBox.Client/Components/Chat/ChatView.razor`
- `src/HotBox.Client/Components/Chat/ChannelHeader.razor`
- `src/HotBox.Client/Components/Chat/MessageList.razor`
- `src/HotBox.Client/Components/Chat/MessageGroup.razor`
- `src/HotBox.Client/Components/Chat/MessageInput.razor`
- `src/HotBox.Client/Components/Chat/TypingIndicator.razor`
- `src/HotBox.Client/Components/Members/MembersPanel.razor`
- `src/HotBox.Client/Components/Members/MemberItem.razor`
- `src/HotBox.Client/Components/Shared/Avatar.razor`
- `src/HotBox.Client/Components/Shared/StatusDot.razor`
- `src/HotBox.Client/Components/Shared/UnreadBadge.razor`
- `src/HotBox.Client/Services/IApiClient.cs`
- `src/HotBox.Client/Services/ApiClient.cs`
- `src/HotBox.Client/Services/IChatHubService.cs`
- `src/HotBox.Client/Services/ChatHubService.cs`
- `src/HotBox.Client/Services/IAuthService.cs`
- `src/HotBox.Client/Services/AuthService.cs`
- `src/HotBox.Client/State/AppState.cs`
- `src/HotBox.Client/State/ChannelState.cs`
- `src/HotBox.Client/State/AuthState.cs`
- `src/HotBox.Client/wwwroot/css/app.css` (design tokens from prototype)

#### Acceptance criteria:
- Users can see channel list in sidebar
- Clicking a channel loads message history (paginated)
- New messages appear in real-time via SignalR
- Typing indicators show when other users are typing
- Message input sends messages and they appear instantly
- Messages are persisted to the database
- Members panel shows online/offline users
- Design matches the HTML prototype (`temp/prototype.html`) visual style
- Admins/mods can create and manage channels via API

---

### Phase 4: Direct Messages
**Estimated effort**: 2-3 days
**Dependencies**: Phase 3 complete

#### What gets built:
1. **DM service** -- Send, retrieve DMs with pagination
2. **ChatHub DM support** -- Add DM methods to existing ChatHub
3. **DM controller** -- REST endpoints
4. **DM sidebar section** -- DirectMessageList, DirectMessageItem components
5. **DM view** -- Reuse ChatView with DM-specific data source

#### Files created:

**Core layer:**
- `src/HotBox.Core/Interfaces/IDirectMessageService.cs`

**Infrastructure layer:**
- `src/HotBox.Infrastructure/Services/DirectMessageService.cs`

**Application layer:**
- `src/HotBox.Application/Controllers/DirectMessagesController.cs`

**Client:**
- `src/HotBox.Client/Components/Sidebar/DirectMessageList.razor`
- `src/HotBox.Client/Components/Sidebar/DirectMessageItem.razor`

#### Acceptance criteria:
- Users can see DM list in sidebar
- Users can open a DM conversation
- DM messages appear in real-time
- Unread DM indicator shows in sidebar
- DM history loads with pagination

---

### Phase 4.5: Message Search
**Estimated effort**: 4-6 days
**Dependencies**: Phase 3 complete (can run in parallel with Phases 4-7)

#### What gets built:
1. **Search service interface** -- `ISearchService` in Core with provider-agnostic search contract
2. **Provider-specific FTS implementations** -- PostgreSQL (`tsvector`/`tsquery`), MySQL/MariaDB (`FULLTEXT`/`MATCH...AGAINST`), SQLite (FTS5), and `LIKE` fallback
3. **FTS index setup** -- Startup task to create FTS infrastructure (virtual tables, GIN indexes, triggers) per provider
4. **Search controller** -- REST endpoints for channel message search and DM search
5. **Search configuration** -- `SearchOptions` class and `appsettings.json` section
6. **Blazor search UI** -- Global search overlay (Ctrl+K), search input with debounce, result list with highlighting
7. **Jump to message** -- Clicking a search result navigates to the message in its channel
8. **Admin reindex endpoint** -- Backfill FTS index from existing messages

#### Files created:

**Core layer:**
- `src/HotBox.Core/Interfaces/ISearchService.cs`

**Infrastructure layer:**
- `src/HotBox.Infrastructure/Search/PostgresSearchService.cs`
- `src/HotBox.Infrastructure/Search/MySqlSearchService.cs`
- `src/HotBox.Infrastructure/Search/SqliteSearchService.cs`
- `src/HotBox.Infrastructure/Search/FallbackSearchService.cs`

**Core layer:**
- `src/HotBox.Core/Options/SearchOptions.cs`

**Application layer:**
- `src/HotBox.Application/Controllers/SearchController.cs`

**Client:**
- `src/HotBox.Client/Components/Search/SearchOverlay.razor`
- `src/HotBox.Client/Components/Search/SearchOverlay.razor.css`
- `src/HotBox.Client/Components/Search/SearchInput.razor`
- `src/HotBox.Client/Components/Search/SearchResults.razor`
- `src/HotBox.Client/Components/Search/SearchResultItem.razor`
- `src/HotBox.Client/Components/Search/SearchHighlight.razor`
- `src/HotBox.Client/State/SearchState.cs`
- `src/HotBox.Client/Models/SearchResultDto.cs`
- `src/HotBox.Client/Models/SearchResponse.cs`

#### Files modified:
- `src/HotBox.Application/appsettings.json` -- Add `Search` config section
- `src/HotBox.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` -- Register provider-specific search service
- `src/HotBox.Application/DependencyInjection/ApplicationServiceExtensions.cs` -- Register SearchOptions
- `src/HotBox.Client/Layout/MainLayout.razor` -- Add SearchOverlay, Ctrl+K handler
- `src/HotBox.Client/Services/IApiClient.cs` -- Add `SearchMessagesAsync`, `SearchDirectMessagesAsync`
- `src/HotBox.Client/Services/ApiClient.cs` -- Implement search API calls
- `src/HotBox.Client/State/ChannelState.cs` -- Add `JumpToMessageId` for navigate-to-message
- `src/HotBox.Client/Components/Chat/MessageList.razor` -- Support scroll-to-message + highlight animation
- `src/HotBox.Client/wwwroot/css/app.css` -- Add `--search-highlight-bg`, `--search-highlight-text` tokens

#### Acceptance criteria:
- Users can press Ctrl+K (or click search icon) to open the global search overlay
- Typing a query (2+ characters) returns ranked results after 300ms debounce
- Search results show message content with highlighted matches, author, channel, and timestamp
- Clicking a search result navigates to that message in its channel with a brief highlight animation
- Channel message search works across all channels (or scoped to one via optional filter)
- DM search only returns messages involving the calling user
- PostgreSQL uses `tsvector`/`tsquery` with GIN index for ranked, stemmed search
- MySQL/MariaDB uses `FULLTEXT` index with `MATCH...AGAINST`
- SQLite uses FTS5 virtual tables with BM25 ranking
- If FTS is unavailable, search degrades to SQL `LIKE` with `IsDegraded` flag in the response
- Admin can trigger reindex via `POST /api/admin/search/reindex`
- Empty state, loading state, and error state are handled in the UI

#### Risk areas:
- **SQLite FTS5 setup**: Requires raw SQL migrations for virtual tables and triggers since EF Core does not support FTS5 natively. Mitigation: use `migrationBuilder.Sql()` for raw DDL.
- **Provider-specific SQL**: Each provider's FTS syntax differs significantly. Mitigation: `ISearchService` abstraction isolates the differences; each implementation is tested independently.
- **Stale denormalized data**: If a user changes their display name or a channel is renamed, search results may show the old name until re-indexed. Acceptable for MVP at ~100 users.

---

### Phase 5: User Presence and Notifications
**Estimated effort**: 2-3 days
**Dependencies**: Phase 3 complete (can run in parallel with Phase 4)

#### What gets built:
1. **Presence service** -- Track online/idle/offline status server-side
2. **ChatHub presence** -- Broadcast presence updates on connect/disconnect/idle
3. **Notification service** -- Determine which notifications to send
4. **Browser notification JSInterop** -- `notification-interop.js` + .NET wrapper
5. **Client presence state** -- PresenceState tracking all user statuses
6. **Status dots** -- Display real-time status on avatars

#### Files created:

**Core layer:**
- `src/HotBox.Core/Interfaces/IPresenceService.cs`
- `src/HotBox.Core/Interfaces/INotificationService.cs`

**Infrastructure layer:**
- `src/HotBox.Infrastructure/Services/PresenceService.cs`
- `src/HotBox.Infrastructure/Services/NotificationService.cs`

**Client:**
- `src/HotBox.Client/wwwroot/js/notification-interop.js`
- `src/HotBox.Client/Services/INotificationService.cs`
- `src/HotBox.Client/Services/NotificationService.cs`
- `src/HotBox.Client/State/PresenceState.cs`

#### Acceptance criteria:
- User status dots update in real-time (online/idle/offline)
- Browser notifications fire for new messages and DMs
- Users going idle after 5 minutes of inactivity triggers status update
- Users disconnecting updates their status after 30-second grace period

---

### Phase 6: Voice Channels (WebRTC)
**Estimated effort**: 5-7 days
**Dependencies**: Phase 3 complete

#### What gets built:
1. **VoiceSignalingHub** -- SignalR hub for WebRTC signaling
2. **WebRTC JSInterop bridge** -- `webrtc-interop.js` managing RTCPeerConnection
3. **Blazor WebRTC service** -- .NET wrapper around JSInterop
4. **Voice UI components** -- VoiceChannelItem, VoiceUserList, VoiceConnectedPanel
5. **Voice state** -- VoiceState tracking connections, mute/deafen status
6. **Audio device management** -- `audio-interop.js` for device enumeration
7. **ICE server configuration** -- Load STUN/TURN from appsettings

#### Files created:

**Application layer:**
- `src/HotBox.Application/Hubs/VoiceSignalingHub.cs`

**Client:**
- `src/HotBox.Client/wwwroot/js/webrtc-interop.js`
- `src/HotBox.Client/wwwroot/js/audio-interop.js`
- `src/HotBox.Client/Services/IVoiceHubService.cs`
- `src/HotBox.Client/Services/VoiceHubService.cs`
- `src/HotBox.Client/Services/IWebRtcService.cs`
- `src/HotBox.Client/Services/WebRtcService.cs`
- `src/HotBox.Client/Components/Sidebar/VoiceChannelItem.razor`
- `src/HotBox.Client/Components/Sidebar/VoiceUserList.razor`
- `src/HotBox.Client/Components/Sidebar/VoiceConnectedPanel.razor`
- `src/HotBox.Client/State/VoiceState.cs`

#### Acceptance criteria:
- Users can click a voice channel to join
- Audio streams between connected peers (P2P mesh)
- Voice user list shows who is in each voice channel
- Mute/deafen controls work and broadcast state to other users
- Disconnect button leaves the voice channel
- Connection works across NAT with STUN (and TURN if configured)
- Multiple users (3-5) can voice chat simultaneously

#### Risk areas:
- **NAT traversal**: Symmetric NATs will block P2P. Mitigation: document TURN server setup, include coturn in docker-compose as optional.
- **Browser compatibility**: WebRTC APIs vary slightly between browsers. Mitigation: test on Chrome, Firefox, Edge. Safari has known WebRTC quirks -- document limitations.
- **JSInterop complexity**: The WebRTC bridge is the most complex JS in the project. Mitigation: keep the JS layer thin (just RTCPeerConnection management), put all logic in the Blazor service.
- **Echo/feedback**: Without echo cancellation, voice can feed back. Mitigation: rely on browser's built-in echo cancellation (`echoCancellation: true` in getUserMedia constraints).

---

### Phase 7: Authentication UI (Blazor)
**Estimated effort**: 2-3 days
**Dependencies**: Phase 2 (backend) + Phase 3 (client shell) complete

#### What gets built:
1. **Login page** -- Email/password form + dynamic OAuth buttons
2. **Register page** -- Registration form (respects registration mode)
3. **Auth flow** -- Token storage, auto-redirect, refresh handling
4. **Route guards** -- Redirect unauthenticated users to login

#### Files created:

**Client:**
- `src/HotBox.Client/Components/Auth/LoginPage.razor`
- `src/HotBox.Client/Components/Auth/RegisterPage.razor`
- `src/HotBox.Client/Components/Auth/OAuthButtons.razor`

#### Acceptance criteria:
- Login page renders with email/password and any configured OAuth buttons
- Registration page shows/hides based on registration mode
- After login, user is redirected to the main chat view
- Unauthenticated users are redirected to login
- JWT refresh works transparently

---

### Phase 8: Admin Panel
**Estimated effort**: 2-3 days
**Dependencies**: Phase 7 complete

#### What gets built:
1. **Admin controller** -- Server settings, user management, invite management
2. **Admin panel UI** -- Settings, channel management, user management, invites
3. **Role assignment UI** -- Assign/remove roles from users

#### Files created:

**Application layer:**
- `src/HotBox.Application/Controllers/AdminController.cs`

**Client:**
- `src/HotBox.Client/Components/Admin/AdminPanel.razor`
- `src/HotBox.Client/Components/Admin/ChannelManagement.razor`
- `src/HotBox.Client/Components/Admin/UserManagement.razor`
- `src/HotBox.Client/Components/Admin/InviteManagement.razor`
- `src/HotBox.Client/Components/Admin/ServerSettings.razor`

#### Acceptance criteria:
- Admin can access admin panel (non-admins cannot)
- Admin can create/edit/delete channels
- Admin can create user accounts (for closed registration)
- Admin can assign roles
- Admin can generate and revoke invite codes
- Admin can change registration mode

---

### Phase 9: Polish, Testing, and Docker
**Estimated effort**: 3-4 days
**Dependencies**: All previous phases complete

#### What gets built:
1. **Error handling** -- Global error boundary, API error responses, SignalR reconnection UI
2. **Loading states** -- Skeleton screens, loading indicators
3. **Responsive refinements** -- Ensure the three-panel layout works at different sizes
4. **Test suite** -- Unit tests for services, integration tests for API, bUnit for components
5. **Docker validation** -- Full docker-compose up with PostgreSQL and Elasticsearch
6. **Documentation** -- README with setup instructions, docker-compose quickstart

#### Acceptance criteria:
- `docker-compose up` starts the full stack (app + PostgreSQL + Elasticsearch)
- Application handles network disconnections gracefully (reconnection UI)
- All critical paths have tests
- README contains quickstart instructions
- `dotnet test` passes across all test projects

---

## 3. Execution Order and Parallelism

```
Phase 1 (Scaffolding)
    |
    v
Phase 2 (Auth Backend)
    |
    v
Phase 3 (Text Channels + Blazor Client Shell) ----+
    |                                               |
    +-------+-------+-------+                      |
    |       |       |       |                      |
    v       v       v       v                      v
Phase 4  Phase 4.5 Phase 5  Phase 6          Phase 7
(DMs)   (Search) (Presence) (Voice)         (Auth UI)
    |       |       |       |                  |
    +-------+-------+-------+------------------+
    |
    v
Phase 8 (Admin Panel)
    |
    v
Phase 9 (Polish + Docker)
```

**Phases that can run in parallel:**
- Phase 4 (DMs), Phase 4.5 (Search), Phase 5 (Presence), Phase 6 (Voice), and Phase 7 (Auth UI) are all independent after Phase 3
- Phase 7 depends on Phase 2 (auth backend) AND Phase 3 (client shell), but both are complete by this point

**Critical path**: Phase 1 -> Phase 2 -> Phase 3 -> Phase 6 (voice is the longest and riskiest)

**Total estimated effort**: 29-41 days for a single developer working on all phases sequentially. With parallelism on Phases 4-7, the critical path is approximately 20-25 days.

---

## 4. Risk Areas and Mitigations

### High Risk

| Risk | Impact | Mitigation |
|------|--------|------------|
| WebRTC NAT traversal failures | Voice chat won't work for some users | Include coturn TURN server as optional docker-compose service. Document NAT troubleshooting. |
| Blazor WASM download size | Slow initial load | Enable compression, lazy-load non-critical assemblies, implement loading screen. |
| P2P mesh scaling at 8-10 users | Audio quality degrades | Audio-only at ~50kbps/stream keeps bandwidth low. Document the upper limit. Prepare SFU migration path. |

### Medium Risk

| Risk | Impact | Mitigation |
|------|--------|------------|
| EF Core multi-provider migrations | Different SQL dialects cause migration issues | Test migrations against all three providers in CI. Use provider-specific migration bundles if needed. |
| JWT token management in Blazor WASM | Token refresh race conditions | Implement token refresh lock with SemaphoreSlim. Handle concurrent 401 responses. |
| SignalR reconnection edge cases | Messages lost during reconnection | Client requests missed messages via REST API after reconnection. Track last-received message timestamp. |
| Browser notification permission denied | Users miss messages | Show in-app notification badge/toast as fallback. Don't depend solely on browser notifications. |
| Multi-provider FTS SQL differences | Search behavior varies across SQLite/PostgreSQL/MySQL | Each provider has its own `ISearchService` implementation, tested independently. `LIKE` fallback for edge cases. |

### Low Risk

| Risk | Impact | Mitigation |
|------|--------|------------|
| Serilog/OpenTelemetry configuration complexity | Observability not working in prod | Phase 1 validates the full pipeline. Development uses Seq for immediate feedback. |
| OAuth provider configuration errors | External login fails | OAuth is optional. Login page only shows configured providers. Clear error messages. |

---

## 5. Testing Strategy

| Layer | Framework | What to Test |
|-------|-----------|-------------|
| Core | xUnit | Entity validation, enum coverage |
| Infrastructure | xUnit + SQLite in-memory | Repository methods, query correctness, seeding |
| Application | xUnit + WebApplicationFactory | API endpoints, auth flow, SignalR hub methods |
| Client | bUnit | Component rendering, state changes, event handling |
| Integration | xUnit + Docker (optional) | Full stack with PostgreSQL |

### Test project packages:
```
xunit
xunit.runner.visualstudio
Microsoft.NET.Test.Sdk
Moq (or NSubstitute)
FluentAssertions
Microsoft.AspNetCore.Mvc.Testing  (Application.Tests)
bunit                              (Client.Tests)
Microsoft.EntityFrameworkCore.InMemory (Infrastructure.Tests)
```

---

## 6. Post-MVP Considerations

Once the MVP is stable, the following features from the requirements can be planned:

1. **File/image sharing** -- Add file upload API, blob storage (local disk or S3-compatible), message attachments
2. **Push-to-talk** -- Requires keyboard hook integration; may need native desktop client (Avalonia)
3. **Message formatting** -- Markdown parser, emoji picker, threaded replies
4. **Screen sharing** -- Extension of WebRTC with `getDisplayMedia()` -- fits naturally into the existing P2P architecture
5. **Native desktop client** -- Avalonia cross-platform app consuming the same API/SignalR/WebRTC stack
6. **SFU migration** -- If voice channels need to support 20+ users, add LiveKit as a docker-compose sidecar

---

## 7. Development Environment Setup

### Prerequisites
- .NET 8 SDK
- Docker Desktop
- IDE: Visual Studio 2022 / Rider / VS Code with C# Dev Kit

### Quickstart
```bash
# Clone and set up
git clone <repo>
cd hot-box

# Start dev dependencies (Seq for logging)
docker-compose -f docker/docker-compose.dev.yml up -d

# Run the application (SQLite, no external DB needed)
dotnet run --project src/HotBox.Application

# Access
# App:  http://localhost:5000
# Seq:  http://localhost:5341
```

### Recommended IDE Extensions
- **C# Dev Kit** (VS Code) or built-in support (Visual Studio/Rider)
- **REST Client** or **Postman** for API testing
- **Blazor Tooling** (built into Visual Studio / Rider)
