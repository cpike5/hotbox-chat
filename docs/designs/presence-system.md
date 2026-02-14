# Presence System - Design Documentation

**Version**: 1.0
**Date**: 2026-02-14
**Status**: Implemented
**Companion Documents**: `CLAUDE.md`, `docs/ownership-domains.md`

---

## 1. Overview

The presence system tracks and broadcasts which users are online, idle, do-not-disturb, or offline in real-time. It provides the foundation for the members panel, online indicators, and presence-aware features like notification suppression during DND mode.

### 1.1 Key Features

- **Real-time status tracking** — User statuses update instantly via SignalR events
- **Multi-connection support** — Users can have multiple tabs open; status persists until the last connection drops
- **Idle detection** — Automatic transition to idle after configurable inactivity period
- **Do Not Disturb** — Manual status setting that prevents idle timeout and suppresses notifications
- **Grace period** — 30-second buffer after disconnect to prevent status flicker during reconnects
- **Agent/bot presence** — Special handling for automated users via API activity tracking
- **In-memory state** — No database persistence; presence is ephemeral by design

### 1.2 Architecture Layer Boundaries

| Layer | Components | Responsibility |
|-------|-----------|----------------|
| **Core** | `UserStatus` enum, `IPresenceService` interface, `PresenceOptions` | Domain contracts and configuration |
| **Infrastructure** | `PresenceService` implementation | In-memory state management, timer orchestration, event publishing |
| **Application** | `ChatHub` integration, `AgentPresenceMiddleware`, `OnlineUserInfo` DTO | SignalR delivery, API activity tracking, HTTP middleware |
| **Client** | `PresenceState`, `MembersPanel.razor`, heartbeat logic in `MainLayout.razor` | Client-side state synchronization, UI rendering, heartbeat transmission |

---

## 2. User Status Types

The system defines four distinct statuses in the `UserStatus` enum (`/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Enums/UserStatus.cs`):

| Status | Meaning | Trigger | Behavior |
|--------|---------|---------|----------|
| **Online** | User has an active connection and recent activity | Initial connection, heartbeat while idle, explicit status change | Receives all notifications and messages in real-time |
| **Idle** | User connected but no activity for 5+ minutes (default) | Idle timeout fires, no heartbeat received | Human users only; still connected but inactive. Next heartbeat transitions back to Online. |
| **DoNotDisturb** | Manually set by user to suppress interruptions | User explicitly sets DND via `ChatHub.SetStatus()` | Notifications are persisted but not delivered in real-time. Prevents idle timeout. |
| **Offline** | No active connection (or grace period expired) | Last connection disconnects and grace period expires, agent inactivity timeout | User does not appear in "online users" list on initial connect |

**Important**: Agents (bots) do not transition to Idle. When an agent's idle timeout fires, they go directly to Offline.

---

## 3. Configuration

### 3.1 PresenceOptions

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Options/PresenceOptions.cs`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `GracePeriod` | `TimeSpan` | 30 seconds | Delay before marking a user offline after their last connection drops. Prevents flicker during page refreshes. |
| `IdleTimeout` | `TimeSpan` | 5 minutes | Time without a heartbeat before transitioning to Idle (humans) or Offline (agents). |
| `AgentInactivityTimeout` | `TimeSpan` | 5 minutes | Time without API activity before marking an agent offline. |

### 3.2 appsettings.json Example

```json
{
  "Presence": {
    "GracePeriod": "00:00:30",
    "IdleTimeout": "00:05:00",
    "AgentInactivityTimeout": "00:05:00"
  }
}
```

Configuration section name is `"Presence"` (see `PresenceOptions.SectionName` constant).

---

## 4. Backend Architecture

### 4.1 PresenceService (Singleton)

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Services/PresenceService.cs`

The `PresenceService` is registered as a **singleton** in DI and holds all presence state in memory. It is **not thread-safe by default** — connection set modifications are guarded by `_connectionLock`, and status/display name dictionaries use `ConcurrentDictionary` for atomic operations.

#### In-Memory State

| Collection | Type | Purpose | Thread Safety |
|------------|------|---------|---------------|
| `_userConnections` | `Dictionary<Guid, HashSet<string>>` | Maps userId → set of SignalR connectionIds | Guarded by `_connectionLock` (HashSet is not thread-safe) |
| `_userStatuses` | `ConcurrentDictionary<Guid, UserStatus>` | Current status per user | Thread-safe |
| `_userDisplayNames` | `ConcurrentDictionary<Guid, string>` | Display names for online users | Thread-safe |
| `_userIsAgent` | `ConcurrentDictionary<Guid, bool>` | Bot flag per user | Thread-safe |
| `_lastHeartbeat` | `ConcurrentDictionary<Guid, DateTime>` | Last heartbeat timestamp per user | Thread-safe |

#### Timer Collections

| Collection | Type | Purpose |
|------------|------|---------|
| `_graceTimers` | `ConcurrentDictionary<Guid, Timer>` | One timer per disconnected user, fires after `GracePeriod` |
| `_idleTimers` | `ConcurrentDictionary<Guid, Timer>` | One timer per online user, fires after `IdleTimeout` |
| `_agentInactivityTimers` | `ConcurrentDictionary<Guid, Timer>` | One timer per agent using API activity presence, fires after `AgentInactivityTimeout` |

#### Event

```csharp
public event Action<Guid userId, string displayName, UserStatus status, bool isAgent>? OnUserStatusChanged;
```

This event is subscribed to at application startup (`Program.cs`) and broadcasts status changes via `IHubContext<ChatHub>` to all connected clients.

### 4.2 Core Methods

#### SetOnlineAsync(userId, connectionId, displayName, isAgent)

**When called**:
- `ChatHub.OnConnectedAsync()` — User connects via SignalR
- Manual status change from Idle/DND to Online

**Behavior**:
1. Cancel grace timer and agent inactivity timer (if any)
2. Add `connectionId` to the user's connection set
3. Store display name and agent flag
4. Record heartbeat timestamp
5. Set status to `Online`
6. Reset idle timer
7. If status changed from non-Online → raise `OnUserStatusChanged` event

---

#### SetIdleAsync(userId)

**When called**:
- Idle timeout fires for a human user
- Manual status change (rare)

**Behavior**:
1. Skip if user is Offline or DND
2. Set status to `Idle`
3. Raise `OnUserStatusChanged` event

**Note**: Does NOT create a timer. The idle timer continues running; the next heartbeat will transition back to Online.

---

#### SetOfflineAsync(userId)

**When called**:
- Grace period expires after last connection drops
- Agent inactivity timeout fires
- Idle timeout fires for an agent
- Manual disconnect

**Behavior**:
1. Cancel all timers (grace, idle, agent inactivity)
2. Remove all connections from `_userConnections`
3. Remove all state (status, display name, agent flag, heartbeat)
4. Raise `OnUserStatusChanged` event with status = Offline

---

#### SetDoNotDisturbAsync(userId)

**When called**:
- User explicitly sets DND via `ChatHub.SetStatus(UserStatus.DoNotDisturb)`

**Behavior**:
1. Skip if user not in `_userStatuses` (not connected)
2. Set status to `DoNotDisturb`
3. Cancel idle timer (DND users don't go idle)
4. Raise `OnUserStatusChanged` event

---

#### RemoveConnection(userId, connectionId)

**When called**:
- `ChatHub.OnDisconnectedAsync()` — SignalR connection closes

**Behavior**:
1. Remove `connectionId` from user's connection set (under `_connectionLock`)
2. If this was the last connection:
   - Start grace timer
   - Return `true`
3. Otherwise, return `false`

**Returns**: `true` if user has no remaining connections (grace period started)

---

#### RecordHeartbeat(userId)

**When called**:
- `ChatHub.Heartbeat()` — Client sends heartbeat (every 60 seconds)

**Behavior**:
1. Update `_lastHeartbeat[userId]` to current UTC time
2. If user was Idle:
   - Transition to Online
   - Raise `OnUserStatusChanged` event
3. Reset idle timer

---

#### TouchAgentActivityAsync(userId, displayName)

**When called**:
- `AgentPresenceMiddleware.InvokeAsync()` — Agent makes an API request

**Behavior**:
1. Cancel grace timer (if any)
2. Store display name, set `isAgent = true`, record heartbeat
3. Set status to Online (if not already)
4. Cancel idle timer (agents using API activity don't use idle detection)
5. Reset agent inactivity timer
6. If status changed → raise `OnUserStatusChanged` event

**Note**: When an agent connects via SignalR, the agent inactivity timer is cancelled (line 86) and the SignalR idle timer takes over. This prevents the agent from being marked offline due to API inactivity while still connected to SignalR.

---

### 4.3 Connection Lifecycle Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ User connects via SignalR                                       │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 v
     ChatHub.OnConnectedAsync()
                 │
                 v
     PresenceService.SetOnlineAsync(userId, connectionId, displayName, isAgent)
                 │
                 ├─> Cancels grace timer
                 ├─> Adds connectionId to _userConnections[userId]
                 ├─> Sets status = Online
                 ├─> Resets idle timer (starts 5min countdown)
                 └─> Raises OnUserStatusChanged event (if status changed)

                 ↓ (Application layer subscriber)

     IHubContext<ChatHub>.Clients.All.SendAsync("UserStatusChanged", ...)
                 │
                 v
     ┌───────────────────────────────────────────────────┐
     │ All other clients receive status update           │
     └───────────────────────────────────────────────────┘

     ┌───────────────────────────────────────────────────┐
     │ Server sends full online user list to new client  │
     └───────────────────────────────────────────────────┘

     Server: ChatHub.OnConnectedAsync() line ~67
         ↓
     Clients.Caller.SendAsync("OnlineUsers", onlineUsers)
         ↓
     Client receives and calls PresenceState.SetOnlineUsers()
```

---

### 4.4 Disconnection and Grace Period Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ User closes tab / loses network connection                      │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 v
     ChatHub.OnDisconnectedAsync()
                 │
                 v
     PresenceService.RemoveConnection(userId, connectionId)
                 │
                 ├─> Removes connectionId from _userConnections[userId]
                 │
                 └─> If last connection (count == 0):
                         │
                         v
                     Starts grace timer (30 seconds default)
                         │
                         │ ┌─── User reconnects within 30s ───┐
                         │ │                                   │
                         │ v                                   v
                         │ SetOnlineAsync() called          Grace timer fires
                         │ │                                   │
                         │ v                                   v
                         │ Grace timer cancelled          OnGracePeriodExpiredAsync()
                         │ │                                   │
                         │ v                                   ├─> Double-checks no connections exist
                         │ User stays Online                   │   (under _connectionLock)
                         │                                     │
                         │                                     └─> SetOfflineAsync()
                         │                                         │
                         │                                         v
                         │                                     Raises OnUserStatusChanged(Offline)
                         │                                         │
                         │                                         v
                         │                                     Broadcast to all clients
```

**Race Condition Prevention**: The grace period callback verifies no connections exist **within the same lock** that guards connection additions (`_connectionLock`), preventing a race where a user reconnects between the check and `SetOfflineAsync()`.

---

### 4.5 Idle Detection Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ User is Online, idle timer running                              │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 │ Every ~2 minutes:
                 v
     Client: MainLayout.razor sends heartbeat
                 │
                 v
     ChatHub.Heartbeat() (line ~234)
                 │
                 v
     PresenceService.RecordHeartbeat(userId)
                 │
                 ├─> Updates _lastHeartbeat[userId] = DateTime.UtcNow
                 ├─> If status == Idle: transition to Online, raise event
                 └─> Resets idle timer (cancels old, starts new 5min timer)


┌─────────────────────────────────────────────────────────────────┐
│ User stops sending heartbeats (tab in background, browser       │
│ throttling, or user stepped away)                               │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 │ After 5 minutes:
                 v
     Idle timer fires → OnIdleTimeoutExpiredAsync(userId)
                 │
                 ├─> Check: elapsed time since last heartbeat >= IdleTimeout?
                 │
                 ├─── YES ──┬─> IsAgent? ──> SetOfflineAsync()
                 │          │
                 │          └─> Not agent ──> SetIdleAsync()
                 │                              │
                 │                              v
                 │                          Raises OnUserStatusChanged(Idle)
                 │                              │
                 │                              v
                 │                          Broadcast to all clients
                 │
                 └─── NO ──> Heartbeat arrived after timer set
                             │
                             v
                         Reschedule timer (ResetIdleTimer)
```

**Important**:
- DND users do **not** get idle timers (line 342-343 in `ResetIdleTimer`)
- Agents that go idle are marked **Offline**, not Idle (line 365-367)

---

### 4.6 Agent Presence via API Activity

Agents (bots) can be present in two ways:
1. **Via SignalR** (same as human users) — connects to ChatHub, sends heartbeats
2. **Via API activity only** — no SignalR connection, presence tracked by HTTP middleware

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Middleware/AgentPresenceMiddleware.cs`

```
┌─────────────────────────────────────────────────────────────────┐
│ Agent makes an authenticated API request (any /api/* endpoint)   │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 v
     AgentPresenceMiddleware.InvokeAsync()
                 │
                 ├─> Extracts userId from ClaimTypes.NameIdentifier
                 ├─> Checks "is_agent" claim or queries database
                 │
                 └─> If user is an agent:
                         │
                         v
                     PresenceService.TouchAgentActivityAsync(userId, displayName)
                         │
                         ├─> Sets status = Online
                         ├─> Cancels idle timer (agents using API don't idle via heartbeat)
                         ├─> Resets agent inactivity timer (5min countdown)
                         └─> Raises OnUserStatusChanged(Online) if status changed


┌─────────────────────────────────────────────────────────────────┐
│ Agent makes no API requests for 5+ minutes                      │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 v
     Agent inactivity timer fires → OnAgentInactivityTimeoutExpiredAsync(userId)
                 │
                 ├─> Verifies elapsed time >= AgentInactivityTimeout
                 │
                 └─> SetOfflineAsync()
                         │
                         v
                     Raises OnUserStatusChanged(Offline)
```

**Switching between presence modes**: If an agent connects via SignalR while using API activity presence, `SetOnlineAsync()` cancels the agent inactivity timer (line 86). The agent's presence is now tracked via SignalR heartbeats instead of API activity.

---

### 4.7 Manual Status Changes

Users can manually set their status via `ChatHub.SetStatus(UserStatus status)`:

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Hubs/ChatHub.cs` (line ~258)

```csharp
public async Task SetStatus(UserStatus status)
{
    var userId = GetUserId();
    var user = await _userManager.FindByIdAsync(userId.ToString());
    var displayName = user?.DisplayName ?? "Unknown";
    var isAgent = user?.IsAgent ?? false;

    switch (status)
    {
        case UserStatus.Online:
            await _presenceService.SetOnlineAsync(userId, Context.ConnectionId, displayName, isAgent);
            await Clients.Others.SendAsync("UserStatusChanged", userId, displayName, UserStatus.Online, isAgent);
            break;
        case UserStatus.DoNotDisturb:
            await _presenceService.SetDoNotDisturbAsync(userId);
            await Clients.Others.SendAsync("UserStatusChanged", userId, displayName, UserStatus.DoNotDisturb, isAgent);
            break;
        case UserStatus.Idle:
            await _presenceService.SetIdleAsync(userId);
            await Clients.Others.SendAsync("UserStatusChanged", userId, displayName, UserStatus.Idle, isAgent);
            break;
        default:
            throw new HubException("Cannot manually set status to Offline. Disconnect instead.");
    }
}
```

**Effect on timers**:
- Setting **Online** → Resets idle timer
- Setting **Idle** → No timer change (still connected, idle timer continues)
- Setting **DND** → Cancels idle timer (DND users cannot go idle)
- Setting **Offline** throws `HubException` — user must disconnect instead

---

## 5. SignalR Integration

### 5.1 Events (Server → Client)

| Event | Payload | When |
|-------|---------|------|
| `OnlineUsers` | `List<OnlineUserInfo>` | Sent to a client immediately upon connecting (line ~72 in `ChatHub.OnConnectedAsync`) |
| `UserStatusChanged` | `Guid userId, string displayName, UserStatus status, bool isAgent` | Broadcast to all clients whenever any user's status changes |

**OnlineUserInfo DTO**:
```csharp
// File: /home/cpike/workspace/hotbox-chat/src/HotBox.Application/Models/OnlineUserInfo.cs
public class OnlineUserInfo
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public UserStatus Status { get; set; }
    public bool IsAgent { get; set; }
}
```

### 5.2 Methods (Client → Server)

| Method | Parameters | Description |
|--------|-----------|-------------|
| `Heartbeat` | (none) | Client sends every ~2 minutes to signal activity. Called from `MainLayout.razor` via a timer. |
| `SetStatus` | `UserStatus status` | Manually set status to Online, Idle, or DoNotDisturb. Cannot set Offline (must disconnect instead). |

### 5.3 Event Wiring at Startup

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Program.cs` (line ~77)

```csharp
// Wire up presence service to broadcast status changes via SignalR
var presenceService = app.Services.GetRequiredService<IPresenceService>();
var hubContext = app.Services.GetRequiredService<IHubContext<ChatHub>>();

presenceService.OnUserStatusChanged += async (userId, displayName, status, isAgent) =>
{
    await hubContext.Clients.All.SendAsync(
        "UserStatusChanged",
        userId,
        displayName,
        status,
        isAgent);
};
```

This ensures every status change (from any source: connection, disconnection, heartbeat, timer expiration, manual change) is immediately broadcast to all connected clients.

---

## 6. Client Architecture

### 6.1 PresenceState

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/State/PresenceState.cs`

Central client-side store for all user presence information. Unlike the server (which only tracks online users), the client tracks **all registered users** to ensure the members panel shows everyone.

#### Key Data Structures

```csharp
public record UserPresenceInfo(Guid UserId, string DisplayName, string Status, bool IsAgent);

private readonly Dictionary<Guid, UserPresenceInfo> _users = new();
```

#### Key Methods

| Method | Purpose |
|--------|---------|
| `SeedAllUsers(List<UserProfileResponse>)` | Seeds **all** registered users from the database as "Offline". Called before `SetOnlineUsers` to ensure users who have never connected still appear in the members panel. Does **not** overwrite existing entries. |
| `SetOnlineUsers(List<OnlineUserInfoModel>)` | Marks all existing users as Offline first, then overlays actual online statuses from the server. Called when `"OnlineUsers"` event arrives. |
| `UpdateUserStatus(userId, displayName, status, isAgent)` | Updates a single user's status. Called on each `"UserStatusChanged"` event from the server. |
| `Clear()` | Wipes all presence data. Called on disconnect. |
| `GetStatus(userId)` | Returns the status for a specific user (default: "Offline"). |
| `GetAllUsers()` | Returns all tracked users regardless of status. |
| `GetOnlineUsers()` | Returns users with status != "Offline". |

#### OnChange Event

```csharp
public event Action? OnChange;
```

Raised whenever presence state changes. The `MembersPanel` component subscribes to this for re-rendering.

---

### 6.2 Initialization Flow in MainLayout.razor

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Layout/MainLayout.razor`

```
┌─────────────────────────────────────────────────────────────────┐
│ User loads the app, MainLayout.razor.OnInitializedAsync() runs  │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 v
     ChatHubService.StartAsync() (existing connection logic)
                 │
                 v
     SignalR connects → HandleConnectionChanged(true) fires
                 │
                 ├─> Fetch ALL users from GET /api/users via ApiClient.GetAllUsersAsync()
                 │   │
                 │   v
                 │   PresenceState.SeedAllUsers(allUsers)
                 │   │
                 │   └─> All users marked as "Offline" initially
                 │
                 └─> Server sends "OnlineUsers" event → HandleOnlineUsers() fires
                     │
                     v
                     PresenceState.SetOnlineUsers(onlineUsers)
                     │
                     ├─> Marks all existing users as Offline first
                     └─> Overlays actual online statuses


┌─────────────────────────────────────────────────────────────────┐
│ Start heartbeat timer                                           │
└────────────────┬────────────────────────────────────────────────┘
                 │
                 v
     Timer fires every 60 seconds
                 │
                 v
     ChatHubService.Heartbeat() → sends "Heartbeat" to server
```

**On Disconnect**:
1. Heartbeat timer disposed
2. `PresenceState.Clear()` wipes all presence data
3. Members panel shows loading skeleton until reconnect

---

### 6.3 Event Handlers

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Layout/MainLayout.razor` (line ~312, ~328)

```csharp
private void HandleUserStatusChanged(Guid userId, string displayName, string status, bool isAgent)
{
    PresenceState.UpdateUserStatus(userId, displayName, status, isAgent);
}

private void HandleOnlineUsers(List<HotBox.Client.Models.OnlineUserInfoModel> users)
{
    PresenceState.SetOnlineUsers(users);
}
```

These handlers are wired up to `ChatHubService` events in `OnInitializedAsync()` and disposed in `Dispose()`.

---

### 6.4 MembersPanel.razor

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Components/Chat/MembersPanel.razor`

Displays all users grouped by status.

#### Rendering Logic

```razor
@if (PresenceState.IsLoading)
{
    <!-- Skeleton loading state -->
}
else
{
    @* Group users by status *@
    @foreach (var group in GroupedUsers)
    {
        <div class="status-group">
            <div class="status-group-header">
                @group.Key - @group.Count()
            </div>
            @foreach (var user in group.OrderBy(u => u.DisplayName))
            {
                <div class="member-item @(user.Status == "Offline" ? "offline" : "")">
                    <!-- User avatar, display name, BOT badge if isAgent -->
                </div>
            }
        </div>
    }
}
```

#### Grouping Order

Users are grouped and displayed in this order:
1. **Online** — Active users
2. **Idle** — Inactive but still connected
3. **Do Not Disturb** — Manually set DND status
4. **Offline** — Disconnected or never connected

**Styling**: Offline users are rendered with `opacity: 0.55` for visual de-emphasis.

#### Subscription

```csharp
protected override void OnInitialized()
{
    PresenceState.OnChange += OnPresenceChanged;
    BuildGroupedUsers();
}

private void OnPresenceChanged()
{
    BuildGroupedUsers();
    InvokeAsync(StateHasChanged);
}

public void Dispose()
{
    PresenceState.OnChange -= OnPresenceChanged;
}
```

The panel re-renders automatically whenever `PresenceState.OnChange` fires. The `OnPresenceChanged` method rebuilds the grouped user list before triggering a re-render.

---

## 7. Design Decisions

### 7.1 In-Memory Only (No Database Persistence)

**Decision**: Presence state is not persisted to the database. On server restart, all users start as unknown until they connect or the client seeds them from the users API.

**Rationale**:
- Presence is inherently ephemeral — a user's "online" status from 5 minutes ago is meaningless
- Database writes for every status change would be high-frequency and wasteful
- Client-side merge (see 7.2) ensures all users appear in the UI regardless of server restart

**Trade-off**: After server restart, users who haven't reconnected yet appear as Offline (even if they were online before restart). This is acceptable for the target scale (~100 concurrent users).

---

### 7.2 Client-Side Merge of Database Users and Live Presence

**Decision**: The client fetches all registered users from `GET /api/users` and seeds them as Offline, then overlays live presence from SignalR.

**Rationale**:
- Ensures all users appear in the members panel, even if they've never connected since the last server restart
- Avoids requiring the server to persist "last known status" in the database
- Separates concerns: REST API provides user list, SignalR provides real-time presence

**Implementation**: `PresenceState.SeedAllUsers()` is called **before** `PresenceState.SetOnlineUsers()` so that the merge doesn't overwrite existing entries.

---

### 7.3 Grace Period (30 Seconds)

**Decision**: After a user's last connection drops, wait 30 seconds before marking them offline.

**Rationale**:
- Prevents status flicker during page refreshes (user disconnects → reconnects within ~1 second)
- Handles brief network interruptions without spurious offline transitions
- 30 seconds is long enough for reconnect attempts but short enough that users don't appear "stuck online"

**Implementation**: `RemoveConnection()` starts a grace timer when the last connection is removed. If `SetOnlineAsync()` is called before the timer fires, the timer is cancelled.

---

### 7.4 Agents Go Straight to Offline (Not Idle)

**Decision**: When an agent's idle timeout fires, it goes directly to Offline (not Idle).

**Rationale**:
- Agents are automated; there's no human who might return to the keyboard
- "Idle" implies the user might become active again soon; agents won't
- Reduces confusion in the members panel (no "Idle bots")

**Implementation**: `OnIdleTimeoutExpiredAsync()` checks `GetIsAgent(userId)` and calls `SetOfflineAsync()` instead of `SetIdleAsync()` (line 365-367).

---

### 7.5 DND Users Don't Go Idle

**Decision**: Users with DoNotDisturb status do not have idle timers.

**Rationale**:
- DND is a deliberate signal of "I'm busy, don't interrupt me"
- Transitioning DND → Idle would be confusing and break user expectations
- DND users remain in that state until they explicitly change it (or disconnect)

**Implementation**: `ResetIdleTimer()` skips creating a timer if the user's status is DoNotDisturb (line 342-343).

---

### 7.6 Singleton Service

**Decision**: `PresenceService` is registered as a singleton in DI.

**Rationale**:
- Holds in-memory state that must be shared across all SignalR hub instances
- Scoped services would create separate instances per hub invocation, losing state
- Singleton ensures all hubs see the same connection map and status dictionary

**Trade-off**: Must be thread-safe. Connection set modifications are guarded by `_connectionLock`, and status dictionaries use `ConcurrentDictionary`.

---

### 7.7 Multi-Connection Support

**Decision**: A user can have multiple SignalR connections (multiple tabs/devices). They only go through the grace period when the **last** connection drops.

**Rationale**:
- Users often have the app open in multiple tabs
- Closing one tab shouldn't mark the user offline if another tab is still connected
- More accurate representation of actual user presence

**Implementation**: `_userConnections` maps userId to a `HashSet<string>` of connectionIds. `RemoveConnection()` only starts the grace timer when the set becomes empty.

---

## 8. File Reference

### Core Layer

| File | Purpose |
|------|---------|
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Enums/UserStatus.cs` | Status enum (Online, Idle, DoNotDisturb, Offline) with `[JsonConverter]` attribute |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Interfaces/IPresenceService.cs` | Service contract with all presence methods |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Options/PresenceOptions.cs` | Configuration options (GracePeriod, IdleTimeout, AgentInactivityTimeout) |

### Infrastructure Layer

| File | Purpose |
|------|---------|
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Services/PresenceService.cs` | Full implementation with in-memory state, timers, and event publishing |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` | DI registration (singleton) |

### Application Layer

| File | Purpose |
|------|---------|
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Hubs/ChatHub.cs` | SignalR hub methods for presence (OnConnectedAsync, OnDisconnectedAsync, Heartbeat, SetStatus) |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Middleware/AgentPresenceMiddleware.cs` | API activity tracking for agents |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Models/OnlineUserInfo.cs` | DTO for online user list sent to clients |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Program.cs` | Event wiring at startup (connects `PresenceService.OnUserStatusChanged` to SignalR broadcast) |

### Client Layer

| File | Purpose |
|------|---------|
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/State/PresenceState.cs` | Client-side presence store with seed/update/clear methods |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Components/Chat/MembersPanel.razor` | UI rendering with status grouping |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/ChatHubService.cs` | SignalR event wiring for OnlineUsers and UserStatusChanged events |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Layout/MainLayout.razor` | Initialization, heartbeat timer, event handler registration |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Models/OnlineUserInfoModel.cs` | Client-side DTO (mirror of server's `OnlineUserInfo`) |

---

## 9. Debugging and Troubleshooting

### 9.1 Common Issues

| Symptom | Likely Cause | Solution |
|---------|--------------|----------|
| Users stuck "Online" after disconnect | Grace timer not firing or reconnect during grace period | Check logs for grace timer cancellation. Verify 30s have elapsed. |
| Users flicker Online/Offline rapidly | Grace period too short or network instability | Increase `GracePeriod` in configuration. |
| User shows as Offline despite being connected | Heartbeat not being sent or recorded | Check browser console for heartbeat errors. Verify timer interval in MainLayout. |
| Agent stuck Online after API inactivity | Agent inactivity timer not firing | Check logs for timer cancellation. Verify `AgentInactivityTimeout` setting. |
| All users show as Offline after server restart | Client hasn't seeded users or received OnlineUsers event | Check network tab for `/api/users` request and SignalR connection. |

### 9.2 Logging

**Server-side**:
- `SetOnlineAsync` logs "User {UserId} ({DisplayName}) is now online"
- `SetIdleAsync` logs "User {UserId} ({DisplayName}) is now idle"
- `SetOfflineAsync` logs "User {UserId} ({DisplayName}) is now offline"
- Grace timer start logs "Started grace period ({GracePeriodSeconds}s) for user {UserId}"
- All logged at `Information` level

**Client-side**:
- `HandleOnlineUsers` logs "Received online users list: {Count} users"
- `HandleUserStatusChanged` logs "User {UserId} status changed to {Status}" (debug level)

### 9.3 Inspecting State

Since `PresenceService` is a singleton, you can inject it into a controller or minimal API endpoint for debugging:

```csharp
app.MapGet("/debug/presence", (IPresenceService presence) =>
{
    var onlineUsers = presence.GetAllOnlineUsers();
    return Results.Ok(new
    {
        OnlineCount = onlineUsers.Count,
        Users = onlineUsers.Select(u => new
        {
            u.UserId,
            u.DisplayName,
            u.Status,
            u.IsAgent
        })
    });
}).RequireAuthorization();
```

**Client-side**: Presence state is exposed via `PresenceState` service. Inject it into a component or inspect via browser dev tools:

```csharp
@inject PresenceState PresenceState

<button @onclick="DumpPresence">Dump Presence State</button>

@code {
    private void DumpPresence()
    {
        var all = PresenceState.GetAllUsers();
        Console.WriteLine($"Total users: {all.Count}");
        foreach (var u in all)
        {
            Console.WriteLine($"{u.DisplayName} ({u.UserId}): {u.Status}");
        }
    }
}
```

---

## 10. Future Enhancements

### 10.1 Custom Status Messages

Allow users to set a custom status text (e.g., "In a meeting", "On vacation").

**Changes needed**:
- Add `CustomStatus` string field to `UserStatus` or create a separate status message system
- Update `PresenceService` to track custom status strings
- Update DTOs and SignalR events to include custom status
- Add UI for setting custom status in client

### 10.2 Rich Presence (Activity-Based Status)

Show what the user is currently doing (e.g., "Typing in #general", "In voice channel: Lounge").

**Changes needed**:
- Add `Activity` field to presence state
- Client sends activity updates via SignalR
- Server broadcasts activity changes
- UI renders activity alongside status

### 10.3 Status History / Presence Analytics

Track when users were online for analytics (e.g., "Most active hours").

**Changes needed**:
- Add `PresenceHistory` table to database
- Background service writes presence snapshots periodically
- Analytics API to query historical data
- Admin dashboard to visualize presence patterns

### 10.4 Mobile Push Notifications on Status Change

Notify mobile users when friends come online.

**Changes needed**:
- Add push notification tokens to user profile
- Subscribe to `OnUserStatusChanged` event
- Filter for "friend" relationships (requires friend system)
- Send push notification via Firebase/APNS

### 10.5 "Last Seen" Timestamp

Show when offline users were last online.

**Changes needed**:
- Add `LastSeenAtUtc` column to `AppUser` table
- Update column in `SetOfflineAsync()`
- Include in user profile DTOs
- Render in members panel (e.g., "Last seen 2 hours ago")

---

## 11. Summary

The presence system is a fully in-memory, real-time status tracking solution built on SignalR and ASP.NET Core. It supports multi-connection users, idle detection, do-not-disturb mode, grace periods for reconnections, and special handling for agent/bot users. The client-side state merges database user lists with live presence updates to ensure all users appear in the UI. The system is designed for ~100 concurrent users and gracefully handles server restarts by re-seeding presence on reconnect.

**Key architectural principles**:
- **Singleton service** with thread-safe in-memory state
- **Event-driven broadcasting** via `OnUserStatusChanged`
- **Timer-based transitions** for idle, grace period, and agent inactivity
- **Client-side merge** of REST API user list and SignalR presence events
- **No database persistence** for presence (ephemeral by design)

For modifications or debugging, consult the file reference in Section 8 and the troubleshooting guide in Section 9.
