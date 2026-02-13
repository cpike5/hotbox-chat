# Notification System Specification

## Summary of Investigation

I examined the full codebase to understand the current architecture, existing notification infrastructure, patterns for entities/configurations/services/DI/client state/pages, and all the gathered requirements. Here are the key findings that shaped this spec:

**Existing infrastructure to refactor:**
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Interfaces/INotificationService.cs` -- currently @mention-only with a single method `ProcessMessageNotificationsAsync`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Services/NotificationService.cs` -- tightly coupled to mentions, sends raw `NotificationPayload` via SignalR with no persistence
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Models/NotificationPayload.cs` -- channel-mention-specific DTO (no notification type, no source type, no read state)
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/NotificationService.cs` -- browser-only, no in-app toast capability
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Models/NotificationPayloadModel.cs` -- mirror of server-side `NotificationPayload`, also mention-specific

**Existing infrastructure to build on:**
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/ToastService.cs` -- fully functional toast system with auto-dismiss, already rendered in MainLayout via `<ToastContainer />`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/UnreadStateService.cs` -- good pattern for hub-event-driven client state
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Services/ReadStateService.cs` -- pattern for DB-backed read state with upsert
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Interfaces/IPresenceService.cs` -- `GetStatus(Guid userId)` method for DND checking
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Hubs/ChatHub.cs` -- existing `ReceiveNotification` SignalR event, DM sending at line 182-213

**Key patterns to follow:**
- Entity pattern: `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Entities/Message.cs` (Guid PK, nav properties, DateTime UTC convention)
- EF Configuration pattern: `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/Configurations/MessageConfiguration.cs` (IEntityTypeConfiguration, fluent API)
- DI registration: `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` (scoped services in Infrastructure) and `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/DependencyInjection/ApplicationServiceExtensions.cs` (hub-dependent services in Application)
- Controller pattern: `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Controllers/MessagesController.cs` (route `api/`, `[Authorize]`, ClaimTypes.NameIdentifier for user ID)
- Client service registration: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/DependencyInjection/ClientServiceExtensions.cs` (all scoped)
- Page routes: `/channels/{Id:guid}`, `/dm/{UserId:guid}`, `/admin`, `/dms`

The full spec document has been saved below.

---

**Spec document saved to:** `/home/cpike/workspace/hotbox-chat/temp/notification-system-spec.md`

Here is the content that was produced:

---

```markdown
# Notification System - Implementation Specification

**Version**: 1.0
**Date**: 2026-02-13
**Status**: Draft
**Companion Documents**: `temp/notification-system-requirements.md`, `docs/technical-spec.md`

---

## 1. Architecture Overview

### 1.1 How It Fits in the Three-Layer Architecture

| Layer | New Components | Responsibility |
|-------|---------------|----------------|
| **Core** | `Notification` entity, `UserNotificationPreference` entity, `NotificationType` enum, `NotificationSourceType` enum, redesigned `INotificationService` interface | Domain models and contracts only. No SignalR, no EF dependencies. |
| **Infrastructure** | `NotificationConfiguration`, `UserNotificationPreferenceConfiguration`, `NotificationRepository` | EF Core persistence, DB queries, configuration. Implements `INotificationRepository`. |
| **Application** | Refactored `NotificationService`, new `NotificationsController`, ChatHub changes | Orchestrates notification creation, preference checking, SignalR delivery. Depends on `IHubContext<ChatHub>` so must live in Application. |
| **Client** | `NotificationState`, refactored notification handling in `MainLayout`, notification toast integration, `NotificationsPage`, preferences UI | In-app toast display, notification history page, preferences management. |

### 1.2 Component Flow Diagram

```
Event Source (ChatHub.SendMessage / ChatHub.SendDirectMessage)
    |
    v
NotificationService.CreateAsync(type, senderId, recipientId, payload, sourceId, sourceType)
    |
    +-- Check: Is recipient the sender? --> Skip
    +-- Check: IPresenceService.GetStatus(recipientId) == DoNotDisturb? --> Persist but don't deliver
    +-- Check: UserNotificationPreference muted for this source? --> Persist but don't deliver
    |
    +-- Persist to DB via INotificationRepository
    |
    +-- If delivery allowed:
        +-- IHubContext<ChatHub>.Clients.User(recipientId).SendAsync("ReceiveNotification", dto)
            |
            v
        Client: ChatHubService.OnNotificationReceived event
            |
            v
        NotificationState.AddNotification(dto)
            |
            +-- ToastService.Show(...) with click-to-navigate callback
            +-- Update unread notification count badge
```

### 1.3 Extension Point for New Notification Types

Adding a new notification type requires:
1. Add a value to `NotificationType` enum in Core (e.g., `SystemAlert`)
2. Call `INotificationService.CreateAsync(...)` from wherever the event originates
3. Add a display string mapping in the client `NotificationState` for the toast message format

No changes needed to the delivery pipeline, persistence, or preference system. The framework is type-agnostic.

---

## 2. Data Model

### 2.1 NotificationType Enum

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Enums/NotificationType.cs` (NEW)

```csharp
using System.Text.Json.Serialization;

namespace HotBox.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    Mention,
    DirectMessage
}
```

Note: `[JsonConverter(typeof(JsonStringEnumConverter))]` is required per CLAUDE.md enum serialization rules.

### 2.2 NotificationSourceType Enum

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Enums/NotificationSourceType.cs` (NEW)

```csharp
using System.Text.Json.Serialization;

namespace HotBox.Core.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationSourceType
{
    Channel,
    DirectMessage
}
```

### 2.3 Notification Entity

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Entities/Notification.cs` (NEW)

Follow the pattern from `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Entities/Message.cs`.

```csharp
namespace HotBox.Core.Entities;

public class Notification
{
    public Guid Id { get; init; }

    public NotificationType Type { get; set; }

    public Guid RecipientId { get; set; }

    public Guid SenderId { get; set; }

    /// <summary>
    /// JSON-serialized payload containing type-specific data
    /// (sender display name, message preview, channel name, etc.)
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the source entity (channel ID or other user ID for DMs).
    /// Used for navigation and mute preference matching.
    /// </summary>
    public Guid SourceId { get; set; }

    public NotificationSourceType SourceType { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }

    // Navigation properties
    public AppUser Recipient { get; set; } = null!;
    public AppUser Sender { get; set; } = null!;
}
```

Note: `PayloadJson` is stored as a plain string column. We avoid EF Core JSON column mapping to maintain SQLite/MySQL/PostgreSQL compatibility without provider-specific configuration. The service layer handles serialization/deserialization.

### 2.4 UserNotificationPreference Entity

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Entities/UserNotificationPreference.cs` (NEW)

```csharp
namespace HotBox.Core.Entities;

public class UserNotificationPreference
{
    public Guid Id { get; init; }

    public Guid UserId { get; set; }

    /// <summary>
    /// The type of source being muted (Channel or DirectMessage).
    /// </summary>
    public NotificationSourceType SourceType { get; set; }

    /// <summary>
    /// The ID of the source being muted (channel ID or other user's ID for DMs).
    /// </summary>
    public Guid SourceId { get; set; }

    public bool IsMuted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    // Navigation
    public AppUser User { get; set; } = null!;
}
```

### 2.5 EF Core Configuration - Notification

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/Configurations/NotificationConfiguration.cs` (NEW)

Follow the pattern from `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/Configurations/MessageConfiguration.cs`.

```csharp
using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .ValueGeneratedOnAdd();

        builder.Property(n => n.Type)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(n => n.RecipientId)
            .IsRequired();

        builder.Property(n => n.SenderId)
            .IsRequired();

        builder.Property(n => n.PayloadJson)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(n => n.SourceId)
            .IsRequired();

        builder.Property(n => n.SourceType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(n => n.CreatedAtUtc)
            .IsRequired();

        // Relationships
        builder.HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Sender)
            .WithMany()
            .HasForeignKey(n => n.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        // Primary query: get user's notifications ordered by newest
        builder.HasIndex(n => new { n.RecipientId, n.CreatedAtUtc })
            .IsDescending(false, true);

        // Unread notification count query
        builder.HasIndex(n => new { n.RecipientId, n.ReadAtUtc });
    }
}
```

### 2.6 EF Core Configuration - UserNotificationPreference

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/Configurations/UserNotificationPreferenceConfiguration.cs` (NEW)

```csharp
using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class UserNotificationPreferenceConfiguration : IEntityTypeConfiguration<UserNotificationPreference>
{
    public void Configure(EntityTypeBuilder<UserNotificationPreference> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd();

        builder.Property(p => p.UserId)
            .IsRequired();

        builder.Property(p => p.SourceType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(p => p.SourceId)
            .IsRequired();

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired();

        builder.Property(p => p.UpdatedAtUtc)
            .IsRequired();

        // Unique constraint: one preference per user per source
        builder.HasIndex(p => new { p.UserId, p.SourceType, p.SourceId })
            .IsUnique();

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### 2.7 DbContext Updates

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/HotBoxDbContext.cs` (MODIFY)

Add two DbSet properties:

```csharp
public DbSet<Notification> Notifications => Set<Notification>();
public DbSet<UserNotificationPreference> UserNotificationPreferences => Set<UserNotificationPreference>();
```

No changes to `OnModelCreating` -- it already uses `ApplyConfigurationsFromAssembly` which auto-discovers new `IEntityTypeConfiguration<T>` implementations.

### 2.8 Migration

Generate after adding entities and configurations:

```bash
cd /home/cpike/workspace/hotbox-chat
dotnet ef migrations add AddNotificationSystem \
  --project src/HotBox.Infrastructure \
  --startup-project src/HotBox.Application
```

---

## 3. Backend Design

### 3.1 INotificationRepository Interface

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Interfaces/INotificationRepository.cs` (NEW)

```csharp
using HotBox.Core.Entities;
using HotBox.Core.Enums;

namespace HotBox.Core.Interfaces;

public interface INotificationRepository
{
    Task<Notification> CreateAsync(Notification notification, CancellationToken ct = default);

    Task<List<Notification>> GetByRecipientAsync(
        Guid recipientId,
        DateTime? before = null,
        int limit = 50,
        CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken ct = default);

    Task MarkAllAsReadAsync(Guid recipientId, CancellationToken ct = default);

    Task<bool> IsSourceMutedAsync(
        Guid userId,
        NotificationSourceType sourceType,
        Guid sourceId,
        CancellationToken ct = default);

    Task<List<UserNotificationPreference>> GetPreferencesAsync(
        Guid userId,
        CancellationToken ct = default);

    Task SetMutePreferenceAsync(
        Guid userId,
        NotificationSourceType sourceType,
        Guid sourceId,
        bool isMuted,
        CancellationToken ct = default);
}
```

### 3.2 NotificationRepository Implementation

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Repositories/NotificationRepository.cs` (NEW)

Implementation uses `HotBoxDbContext` directly, following the pattern from existing repositories (e.g., `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Repositories/MessageRepository.cs`). Key implementation details:

- `GetByRecipientAsync` uses cursor-based pagination with `before` parameter on `CreatedAtUtc`, ordered descending
- `MarkAllAsReadAsync` does a bulk update: `SET ReadAtUtc = @now WHERE RecipientId = @id AND ReadAtUtc IS NULL`
- `IsSourceMutedAsync` is a simple existence check with `IsMuted == true`
- `SetMutePreferenceAsync` upserts using the unique index on `(UserId, SourceType, SourceId)`

### 3.3 Redesigned INotificationService Interface

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Interfaces/INotificationService.cs` (MODIFY -- full replacement)

```csharp
using HotBox.Core.Enums;

namespace HotBox.Core.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Creates a notification, persists it, checks preferences/DND,
    /// and delivers via SignalR if appropriate.
    /// </summary>
    /// <param name="type">The notification type (Mention, DirectMessage, etc.)</param>
    /// <param name="senderId">The user who triggered the notification</param>
    /// <param name="recipientId">The user receiving the notification</param>
    /// <param name="senderDisplayName">Display name of the sender</param>
    /// <param name="messagePreview">Truncated message content for preview</param>
    /// <param name="sourceId">ID of the source (channel ID or other user ID for DMs)</param>
    /// <param name="sourceType">Type of source (Channel or DirectMessage)</param>
    /// <param name="sourceName">Display name of the source (channel name or user name)</param>
    /// <param name="ct">Cancellation token</param>
    Task CreateAsync(
        NotificationType type,
        Guid senderId,
        Guid recipientId,
        string senderDisplayName,
        string messagePreview,
        Guid sourceId,
        NotificationSourceType sourceType,
        string sourceName,
        CancellationToken ct = default);

    /// <summary>
    /// Processes @mentions in a channel message, creating notifications
    /// for each mentioned user. Replaces the old ProcessMessageNotificationsAsync.
    /// </summary>
    Task ProcessMentionNotificationsAsync(
        Guid senderId,
        string senderDisplayName,
        Guid channelId,
        string channelName,
        string messageContent,
        CancellationToken ct = default);
}
```

### 3.4 NotificationService Implementation

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Services/NotificationService.cs` (MODIFY -- full rewrite)

Dependencies:
- `INotificationRepository` (persistence + preference queries)
- `IPresenceService` (DND check via `GetStatus(userId)`)
- `IHubContext<ChatHub>` (SignalR delivery)
- `UserManager<AppUser>` (mention username lookup -- existing dependency)
- `ILogger<NotificationService>`

Key logic in `CreateAsync`:
1. Skip if `senderId == recipientId`
2. Persist notification to DB via `INotificationRepository.CreateAsync`
3. Check DND: `_presenceService.GetStatus(recipientId) == UserStatus.DoNotDisturb` -- if true, return (persisted but not delivered)
4. Check mute: `_notificationRepository.IsSourceMutedAsync(recipientId, sourceType, sourceId)` -- if true, return
5. Build `NotificationResponse` DTO and send via `_hubContext.Clients.User(recipientId.ToString()).SendAsync("ReceiveNotification", dto)`

`ProcessMentionNotificationsAsync` preserves the existing regex extraction logic but calls `CreateAsync` for each mentioned user instead of directly sending SignalR messages.

### 3.5 NotificationResponse DTO

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Models/NotificationResponse.cs` (NEW -- replaces `NotificationPayload.cs`)

```csharp
using HotBox.Core.Enums;

namespace HotBox.Application.Models;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public Guid SenderId { get; set; }
    public string SenderDisplayName { get; set; } = string.Empty;
    public string MessagePreview { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public NotificationSourceType SourceType { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
```

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Models/NotificationPayload.cs` (DELETE after migration)

### 3.6 NotificationsController

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Controllers/NotificationsController.cs` (NEW)

Follow the pattern from `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Controllers/MessagesController.cs`.

```csharp
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
```

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/notifications` | Get notification history for current user (paginated: `?before={utc}&limit=50`) |
| GET | `/api/notifications/unread-count` | Get unread notification count for current user |
| POST | `/api/notifications/mark-all-read` | Mark all notifications as read for current user |
| GET | `/api/notifications/preferences` | Get mute preferences for current user |
| PUT | `/api/notifications/preferences` | Set a mute preference (body: `{ sourceType, sourceId, isMuted }`) |

User ID extracted from `ClaimTypes.NameIdentifier` (same pattern as `MessagesController`).

### 3.7 ChatHub Integration Changes

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Hubs/ChatHub.cs` (MODIFY)

Changes to `SendMessage` method (around line 124-163):
- Replace `_notificationService.ProcessMessageNotificationsAsync(...)` with `_notificationService.ProcessMentionNotificationsAsync(...)` (same signature, renamed method)

Changes to `SendDirectMessage` method (around line 182-213):
- After the existing DM sending logic, add a call to create a DM notification:

```csharp
// After sending the DM to both users (existing lines 204-213):
await _notificationService.CreateAsync(
    NotificationType.DirectMessage,
    senderId,
    recipientId,
    senderDisplayName,
    content.Length > 100 ? content[..100] : content,
    senderId,          // sourceId = sender's user ID (for navigation to /dm/{senderId})
    NotificationSourceType.DirectMessage,
    senderDisplayName, // sourceName
    default);
```

Note: For DM notifications, `sourceId` is the sender's user ID because the client navigates to `/dm/{senderId}` to open that conversation.

### 3.8 DI Registration Changes

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` (MODIFY)

Add after existing repository registrations (around line 91):
```csharp
services.AddScoped<INotificationRepository, NotificationRepository>();
```

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/DependencyInjection/ApplicationServiceExtensions.cs` (MODIFY)

The existing registration at line 157 already registers `INotificationService` as `NotificationService`:
```csharp
services.AddScoped<INotificationService, NotificationService>();
```
No change needed here -- the refactored `NotificationService` replaces the old one in place.

---

## 4. Client Design

### 4.1 NotificationResponseModel (Client DTO)

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Models/NotificationResponseModel.cs` (NEW -- replaces `NotificationPayloadModel.cs`)

```csharp
using HotBox.Core.Enums;

namespace HotBox.Client.Models;

public class NotificationResponseModel
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public Guid SenderId { get; set; }
    public string SenderDisplayName { get; set; } = string.Empty;
    public string MessagePreview { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public NotificationSourceType SourceType { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}
```

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Models/NotificationPayloadModel.cs` (DELETE after migration)

### 4.2 NotificationState

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/State/NotificationState.cs` (NEW)

Follow the pattern from `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/UnreadStateService.cs`.

```csharp
namespace HotBox.Client.State;

public class NotificationState : IDisposable
{
    private readonly ApiClient _api;
    private readonly ChatHubService _chatHub;
    private readonly ToastService _toastService;
    private readonly NavigationManager _navigation;
    private readonly PresenceState _presenceState;
    private readonly ILogger<NotificationState> _logger;

    private readonly List<NotificationResponseModel> _notifications = new();
    private int _unreadCount;
    private bool _initialized;

    public event Action? OnChange;

    public IReadOnlyList<NotificationResponseModel> Notifications => _notifications;
    public int UnreadCount => _unreadCount;

    // Constructor subscribes to ChatHubService.OnNotificationReceived
    // On new notification:
    //   1. Add to _notifications list
    //   2. Increment _unreadCount
    //   3. Show toast via ToastService with context-aware message:
    //      - Mention: "{sender} mentioned you in #{channelName}"
    //      - DirectMessage: "{sender} sent you a message"
    //   4. NotifyStateChanged()

    // InitializeAsync(): Fetch initial unread count from GET /api/notifications/unread-count
    // LoadHistoryAsync(before?): Fetch from GET /api/notifications?before=&limit=50
    // MarkAllAsReadAsync(): POST /api/notifications/mark-all-read, reset _unreadCount
}
```

Key detail: The toast message is built client-side based on `NotificationType`:
- `NotificationType.Mention` -- `"{SenderDisplayName} mentioned you in #{SourceName}"`
- `NotificationType.DirectMessage` -- `"{SenderDisplayName} sent you a message"`

Toast click navigation (handled via a callback on toast display or extending ToastService):
- `NotificationSourceType.Channel` -- navigate to `/channels/{SourceId}`
- `NotificationSourceType.DirectMessage` -- navigate to `/dm/{SourceId}`

### 4.3 ToastService Enhancement

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/ToastService.cs` (MODIFY)

The existing `ToastService` only supports plain text messages with no click action. Add an overload that accepts a navigation URL:

```csharp
public record ToastItem(Guid Id, string Message, ToastType Type, DateTime CreatedAt, string? NavigateUrl = null);

// New method:
public void ShowInfo(string message, string? navigateUrl, int? durationMs = null)
    => Show(message, ToastType.Info, durationMs, navigateUrl);
```

### 4.4 ToastContainer Enhancement

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Components/Shared/ToastContainer.razor` (MODIFY)

Add click handler to toast items that have a `NavigateUrl`:

```razor
<div class="toast-item @typeCss @(toast.NavigateUrl != null ? "clickable" : "")"
     @onclick="() => HandleToastClick(toast)">
```

```csharp
private void HandleToastClick(ToastItem toast)
{
    if (toast.NavigateUrl is not null)
    {
        Navigation.NavigateTo(toast.NavigateUrl);
        ToastService.RemoveToast(toast.Id);
    }
}
```

### 4.5 ChatHubService Changes

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/ChatHubService.cs` (MODIFY)

Change the event type at line 66:
```csharp
// OLD:
public event Action<NotificationPayloadModel>? OnNotificationReceived;
// NEW:
public event Action<NotificationResponseModel>? OnNotificationReceived;
```

Change the handler registration at line 282:
```csharp
// OLD:
connection.On<NotificationPayloadModel>("ReceiveNotification", payload => ...);
// NEW:
connection.On<NotificationResponseModel>("ReceiveNotification", notification => ...);
```

### 4.6 MainLayout Changes

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Layout/MainLayout.razor` (MODIFY)

1. Replace injection of `NotificationService` with `NotificationState`:
```razor
@inject NotificationState NotificationState
```

2. Remove the old `HandleNotificationReceived` method (line 279-284) -- notification handling is now in `NotificationState`

3. Remove `ChatHubService.OnNotificationReceived` subscription/unsubscription (lines 246, 446)

4. Add a notification bell button in the topbar-right section (after the search button, around line 110):
```razor
<!-- Notification bell -->
<button class="topbar-icon-btn" aria-label="Notifications" @onclick="NavigateToNotifications"
        style="position: relative;">
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
         stroke-linecap="round" stroke-linejoin="round">
        <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>
        <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>
    </svg>
    @if (NotificationState.UnreadCount > 0)
    {
        <span class="notification-badge">@(NotificationState.UnreadCount > 99 ? "99+" : NotificationState.UnreadCount.ToString())</span>
    }
</button>
```

5. Initialize `NotificationState` in `OnInitializedAsync` (after UnreadState init, around line 262):
```csharp
await NotificationState.InitializeAsync();
```

6. Subscribe to `NotificationState.OnChange` for re-rendering the badge.

### 4.7 NotificationsPage

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Pages/NotificationsPage.razor` (NEW)

Route: `@page "/notifications"`

Layout:
- Header with "Notifications" title and "Mark all as read" button
- Scrollable list of notification items, newest first
- Each item shows:
  - Sender avatar placeholder (first letter of display name, matching existing style)
  - Context line based on type (e.g., "mentioned you in #general" or "sent you a message")
  - Message preview (truncated)
  - Relative timestamp
  - Read/unread visual distinction (unread items have `--bg-raised` background, read items have `--bg-base`)
- Click on item navigates to source (`/channels/{sourceId}` or `/dm/{sourceId}`)
- Scroll to bottom triggers `LoadHistoryAsync(before: lastItem.CreatedAtUtc)` for pagination
- Empty state using existing `<EmptyState />` component from `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Components/Shared/EmptyState.razor`

### 4.8 Notification Preferences UI

Two entry points for muting, integrated into existing UI rather than a standalone page:

**Channel muting**: Add a mute toggle to `ChannelHeader.razor` (`/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Components/Chat/ChannelHeader.razor`). When viewing a text channel, show a bell icon that toggles mute for that channel.

**DM muting**: Add a mute toggle to the DM conversation header area (within `DirectMessagePage.razor` at `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Pages/DirectMessagePage.razor`).

Both call `PUT /api/notifications/preferences` with the appropriate `sourceType` and `sourceId`.

### 4.9 Client NotificationService Refactoring

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/NotificationService.cs` (KEEP but rename to `BrowserNotificationService`)

The existing browser notification JSInterop service is still useful for future browser desktop notification support. Rename to `BrowserNotificationService` to disambiguate from the notification system. It is NOT used in this phase but should not be deleted.

### 4.10 ApiClient Additions

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/ApiClient.cs` (MODIFY)

Add methods:
```csharp
// ── Notifications ────────────────────────────────────────────────

public async Task<List<NotificationResponseModel>?> GetNotificationsAsync(
    DateTime? before = null, int limit = 50, CancellationToken ct = default)

public async Task<int> GetNotificationUnreadCountAsync(CancellationToken ct = default)

public async Task MarkAllNotificationsReadAsync(CancellationToken ct = default)

public async Task<List<NotificationPreferenceModel>?> GetNotificationPreferencesAsync(
    CancellationToken ct = default)

public async Task SetNotificationPreferenceAsync(
    NotificationSourceType sourceType, Guid sourceId, bool isMuted, CancellationToken ct = default)
```

### 4.11 Client DI Registration Changes

**File**: `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/DependencyInjection/ClientServiceExtensions.cs` (MODIFY)

Add:
```csharp
services.AddScoped<NotificationState>();
```

Rename existing registration:
```csharp
// OLD: services.AddScoped<NotificationService>();
// NEW: services.AddScoped<BrowserNotificationService>();
```

---

## 5. Implementation Plan

### Phase 1: Core Data Model and Backend Framework (Medium)

**Dependencies**: None
**Files to CREATE**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Enums/NotificationType.cs`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Enums/NotificationSourceType.cs`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Entities/Notification.cs`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Entities/UserNotificationPreference.cs`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Interfaces/INotificationRepository.cs`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/Configurations/NotificationConfiguration.cs`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/Configurations/UserNotificationPreferenceConfiguration.cs`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Repositories/NotificationRepository.cs`

**Files to MODIFY**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/HotBoxDbContext.cs` (add 2 DbSets)
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` (register repository)

**Then**: Generate EF migration

**Subagents**: `platform` (entities, enums, repository, EF config, migration, DI)

### Phase 2: Backend Notification Service and API (Medium)

**Dependencies**: Phase 1 complete
**Files to CREATE**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Models/NotificationResponse.cs`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Controllers/NotificationsController.cs`

**Files to MODIFY**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Interfaces/INotificationService.cs` (full replacement)
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Services/NotificationService.cs` (full rewrite)
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Hubs/ChatHub.cs` (DM notification + rename mention method)

**Files to DELETE**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Models/NotificationPayload.cs` (replaced by `NotificationResponse.cs`)

**Subagents**: `messaging` (notification service, ChatHub integration, controller)

### Phase 3: Client State and Toast Integration (Medium)

**Dependencies**: Phase 2 complete
**Files to CREATE**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Models/NotificationResponseModel.cs`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/State/NotificationState.cs`

**Files to MODIFY**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/ChatHubService.cs` (change event type)
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/ToastService.cs` (add NavigateUrl to ToastItem)
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Components/Shared/ToastContainer.razor` (add click-to-navigate)
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/ApiClient.cs` (add notification API methods)
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/DependencyInjection/ClientServiceExtensions.cs` (register NotificationState, rename NotificationService)
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Layout/MainLayout.razor` (replace old handler, add bell icon, init NotificationState)

**Files to RENAME**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Services/NotificationService.cs` -> `BrowserNotificationService.cs`

**Files to DELETE**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Models/NotificationPayloadModel.cs` (replaced by `NotificationResponseModel.cs`)

**Subagents**: `client-experience` (client state, toast changes, MainLayout, hub service)

### Phase 4: Notification History Page (Small)

**Dependencies**: Phase 3 complete
**Files to CREATE**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Pages/NotificationsPage.razor`
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Pages/NotificationsPage.razor.css`

**Subagents**: `client-experience` (page implementation)

### Phase 5: Mute Preferences UI (Small)

**Dependencies**: Phase 3 complete (can run in parallel with Phase 4)
**Files to MODIFY**:
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Components/Chat/ChannelHeader.razor` (add mute toggle)
- `/home/cpike/workspace/hotbox-chat/src/HotBox.Client/Pages/DirectMessagePage.razor` (add mute toggle)

**Subagents**: `client-experience` (mute UI in existing components)

### Phase 6: Tests (Small)

**Dependencies**: Phase 2 complete (can start immediately after Phase 2, parallel with Phases 3-5)
**Files to CREATE**:
- `/home/cpike/workspace/hotbox-chat/tests/HotBox.Application.Tests/Services/NotificationServiceTests.cs`
- `/home/cpike/workspace/hotbox-chat/tests/HotBox.Infrastructure.Tests/Repositories/NotificationRepositoryTests.cs`
- `/home/cpike/workspace/hotbox-chat/tests/HotBox.Application.Tests/Controllers/NotificationsControllerTests.cs`

**Subagents**: `test-writer`

### Execution Order

```
Phase 1 (Data Model)
    |
    v
Phase 2 (Backend Service + API)
    |
    +------+------+
    |      |      |
    v      v      v
Phase 3  Phase 6
(Client)  (Tests)
    |
    +------+
    |      |
    v      v
Phase 4  Phase 5
(History) (Mute UI)
```

Phases 4 and 5 can run in parallel. Phase 6 can run in parallel with Phases 3-5.

---

## 6. Migration Path

### 6.1 Backwards Compatibility Strategy

The refactoring must maintain unbroken @mention notification functionality throughout. Here is the migration path:

1. **Phase 1 is additive only** -- new entities, new tables, new repository. Nothing existing changes. The old `NotificationService` continues to work.

2. **Phase 2 replaces the interface and implementation atomically** -- the `INotificationService` interface gets new methods while the old `ProcessMessageNotificationsAsync` is renamed to `ProcessMentionNotificationsAsync` (same parameters). The ChatHub call site is updated from `ProcessMessageNotificationsAsync` to `ProcessMentionNotificationsAsync` in the same commit.

3. **The `ReceiveNotification` SignalR event stays the same name** but the payload changes from `NotificationPayload` to `NotificationResponse`. This is a breaking change in the DTO shape. Since the client and server are deployed together (Blazor WASM is served by the ASP.NET Core host), this is safe -- both sides update simultaneously. There is no separate client deployment.

4. **Client-side rename**: `NotificationPayloadModel` -> `NotificationResponseModel`, `NotificationService` -> `BrowserNotificationService`. The old `HandleNotificationReceived` in `MainLayout` is removed; `NotificationState` takes over. This happens in Phase 3 as a single commit.

### 6.2 Existing Mention Notifications

The existing mention flow (`ChatHub.SendMessage` -> `NotificationService.ProcessMessageNotificationsAsync`) is preserved 1:1 in terms of behavior. The only difference is:
- Notifications are now persisted to the database (new)
- DND users won't receive real-time delivery (new)
- Muted channels won't receive real-time delivery (new)
- The DTO shape includes more fields (type, sourceId, etc.)

### 6.3 Data Migration

No data migration is needed. The notification tables start empty. There is no existing notification history to preserve (the old system was fire-and-forget via SignalR with no persistence).

---

## 7. Open Questions (Resolved with Recommendations)

| Question | Recommendation |
|----------|---------------|
| Retention policy for notification history? | Add a 90-day cleanup. Implement as a background `IHostedService` that runs daily and deletes `WHERE CreatedAtUtc < DATEADD(day, -90, GETUTCDATE())`. Not in MVP -- add as a follow-up issue. |
| Maximum notifications per user? | No hard cap. At ~100 users with moderate activity, the volume is manageable. The 90-day retention policy handles growth. |
| Notification count badge in sidebar? | Yes -- added as a bell icon in the topbar-right section (see Section 4.6). Shows unread count. |
