# Requirements: Notification System

## Executive Summary

A generic, event-driven notification framework for HotBox that persists notifications and delivers them via in-app toasts and a notification history page. DMs and @mentions are the initial event types, but the system is designed for easy extension to future notification sources.

## Problem Statement

Users have no way to know they've been mentioned or received a DM unless they're actively looking at the relevant channel or conversation. The existing @mention notification only fires a browser desktop notification — there's no in-app feedback and no history. A generic notification framework is needed as foundational infrastructure for current and future features.

## Primary Purpose

Deliver real-time in-app notifications to users and persist them for later review, with user-configurable muting preferences.

## Target Users

- **Member**: Receives notifications for DMs and @mentions, can mute channels/conversations, can view notification history
- **Admin**: Same as member (no admin-specific notification features in this phase)

## Core Features (MVP)

### 1. Notification Framework (Backend)

**Generic event-driven architecture:**
- Notification entity with: type (enum), recipient, sender, payload (JSON), source reference (channel/conversation ID), timestamp, read status
- `INotificationService` processes notification events and persists them
- Extensible enum-based notification types — adding a new type should not require changes to core delivery logic
- Integrate with existing SignalR infrastructure (`ChatHub`) for real-time delivery to connected clients

**Initial notification types:**
- **DirectMessage** — triggered when a user receives a DM
- **Mention** — triggered when a user is @mentioned in a channel message (replaces current implementation)

### 2. In-App Toast Notifications (Client)

- Real-time toast popup when a notification is received
- Shows sender name, notification type context (e.g., "mentioned you in #general" or "sent you a message"), and a message preview
- Auto-dismiss after a configurable duration
- Clicking a toast navigates to the source (channel or DM conversation)
- Respects Do Not Disturb — no toasts when DND is active
- Respects mute preferences — no toasts for muted sources

### 3. Notification History Page (Client)

- Scrollable list of past notifications, newest first
- Each entry shows: sender avatar/name, notification context, message preview, timestamp
- Clicking an entry navigates to the source
- "Mark all as read" action
- Visual distinction between read and unread notifications
- Accessible from the main navigation/sidebar

### 4. User Notification Preferences

- **Mute specific channels** — suppress notifications from a given text channel
- **Mute specific DM conversations** — suppress notifications from a given user's DMs
- **Do Not Disturb toggle** — suppress all notifications globally (wire up existing `UserStatus.DoNotDisturb` enum)
- Preferences persisted to database per user
- Preferences checked server-side before sending notification (don't send what won't be shown)

## Future Features

- Browser desktop notifications (partially implemented, re-integrate later)
- Sound/vibration alerts
- Push notifications (service workers, Web Push API)
- Per-notification-type granular settings (e.g., mentions on, DMs off)
- Notification grouping/batching
- Email digest notifications
- Webhook/integration notifications
- Keyword-based notification triggers
- Mark individual notifications as read
- Notification badges on app icon

## Out of Scope

- Browser desktop notifications (existing implementation stays but is not enhanced)
- Email notifications
- Webhook notifications
- Sound/vibration
- Notification grouping or batching
- Rich notification actions/buttons
- Per-notification-type toggle (on/off per type)
- Admin-specific notification features

## Technical Context

### Existing Infrastructure to Build On

| Component | Location | Relevance |
|-----------|----------|-----------|
| `INotificationService` / `NotificationService` | `Application/Services/` | Refactor — currently @mention-only, needs to become generic |
| `NotificationPayload` | `Application/Models/` | Extend or replace with generic payload model |
| `ChatHub` | `Application/Hubs/` | Delivery channel — already calls notification service |
| `ChatHubService` (client) | `Client/Services/` | Already has `OnNotificationReceived` event |
| `NotificationService` (client) | `Client/Services/` | Currently browser-only — refactor for in-app toasts |
| `UnreadStateService` | `Client/Services/` | Pattern reference for client-side state tracking |
| `ReadStateService` | `Application/Services/` | Pattern reference for read/unread persistence |
| `UserStatus.DoNotDisturb` | `Core/Enums/` | Exists but not wired to notification suppression |

### Tech Stack Alignment

- **Backend**: ASP.NET Core, EF Core, SignalR
- **Client**: Blazor WASM, no JS frameworks
- **Database**: SQLite (dev), PostgreSQL (prod) — EF Core multi-provider
- **Patterns**: `IServiceCollection` DI extensions, Options pattern, three-layer architecture

### New Database Entities Needed

- **Notification** — persisted notification record (type, recipientId, senderId, payload JSON, sourceId, sourceType, createdAtUtc, readAtUtc)
- **UserNotificationPreference** — mute settings (userId, sourceType enum, sourceId, isMuted)

### New/Modified Components

**Core:**
- `NotificationType` enum (DirectMessage, Mention — extensible)
- `Notification` entity
- `UserNotificationPreference` entity
- `INotificationService` interface (redesigned for generic use)

**Infrastructure:**
- EF Core configuration for new entities
- Migration for Notification and UserNotificationPreference tables
- Indexes: (RecipientId, ReadAtUtc), (RecipientId, CreatedAtUtc DESC)

**Application:**
- `NotificationService` — refactored to handle any notification type
- API endpoints: GET notifications (paginated), POST mark-all-read, GET/PUT notification preferences
- ChatHub integration updated for generic notification delivery

**Client:**
- Toast notification component (overlay, auto-dismiss, click-to-navigate)
- Notification history page/component
- Notification preferences UI (mute toggles per channel/DM)
- Updated `ChatHubService` for generic notification events
- Client-side notification state service

## Constraints

- **Scale**: ~100 concurrent users max — no need for complex queuing or batching
- **Performance**: Notification delivery should feel instant (<200ms from event to toast)
- **Security**: Users should only see their own notifications; preferences are per-user
- **Architecture**: Must follow three-layer pattern (Core → Infrastructure → Application)
- **No JS on server**: All real-time delivery via SignalR, toast rendering in Blazor

## Design Preferences

- Toast style should match existing dark theme and design tokens from `prototypes/main-ui-proposal.html`
- Toast position: top-right corner (standard convention)
- Notification page should feel lightweight — not a heavy inbox UI

## Open Questions

- Should notification history have a retention policy (e.g., auto-delete after 30 days)?
- Maximum number of notifications to retain per user?
- Should there be a notification count badge in the sidebar/nav (separate from unread message badges)?

## Decisions Made

- **Generic framework first** — build extensible infrastructure, not a point solution
- **Persistence required** — notifications stored in DB for history page
- **In-app delivery only** — no browser/push notifications in this phase
- **Server-side preference filtering** — don't send notifications that would be suppressed
- **Mark all as read** — no per-notification read toggling in MVP
- **DMs and @mentions first** — initial notification types, more added later
