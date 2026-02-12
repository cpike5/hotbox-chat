# User Profile Feature Specification

**Version:** 1.0
**Date:** 2026-02-12
**Status:** Draft

---

## Table of Contents

1. [Feature Overview](#1-feature-overview)
2. [Domain Analysis](#2-domain-analysis)
3. [Data Model](#3-data-model)
4. [API Design](#4-api-design)
5. [UI/UX Design](#5-uiux-design)
6. [Implementation Plan](#6-implementation-plan)
7. [Testing Strategy](#7-testing-strategy)

---

## 1. Feature Overview

User profiles give HotBox members a lightweight identity beyond just a display name. Users can view each other's profiles, edit their own, and see profile information inline throughout the chat experience.

### User Stories

| # | As a... | I want to... | So that... |
|---|---------|-------------|-----------|
| 1 | Member | View another user's profile by clicking their name or avatar | I can learn who they are |
| 2 | Member | Edit my own display name, avatar, bio, and pronouns | I can express my identity |
| 3 | Member | Set a custom status message (e.g., "On vacation") | Others know what I'm up to |
| 4 | Member | See when a user joined and when they were last active | I know if someone is around |
| 5 | Admin | View and edit any user's profile | I can moderate the server |

### Scope

**MVP (this spec):**
- View own profile
- Edit own profile (display name, avatar URL, bio, pronouns, custom status)
- View another user's profile via popover
- Profile popover triggered from: message author name, members panel, DM list
- Admin can view any profile

**Future (not this spec):**
- Avatar image upload (currently just a URL field)
- Profile banners/backgrounds
- Connected accounts display
- Activity/game status
- User-to-user blocking

---

## 2. Domain Analysis

### Domains Involved

Based on the ownership boundaries defined in `docs/ownership-domains.md`:

| Domain | Agent | Responsibility for this Feature |
|--------|-------|---------------------------------|
| **Platform** | `platform` | Add new fields to `AppUser` entity, update EF Core configuration, generate migration |
| **Auth & Security** | `auth-security` | Build `UsersController` with profile endpoints, enforce authorization (self-edit vs admin-edit) |
| **Client Experience** | `client-experience` | Build profile popover component, edit profile modal, integrate into existing UI touch points |

The **Messaging** and **Real-time & Media** domains are **not directly involved**. Chat messages already carry `AuthorId` and `AuthorDisplayName` which is sufficient for triggering the profile popover.

### Coordination Points

1. **Platform -> Auth & Security**: Platform adds entity fields and migration. Auth & Security builds the controller and service that reads/writes those fields. Auth & Security must wait for the migration to be ready.

2. **Auth & Security -> Client Experience**: Auth & Security defines the API response/request shapes. Client Experience builds UI that consumes them. Client Experience must wait for the API contract to be finalized (the DTOs in this spec serve as that contract).

3. **Client Experience (internal)**: The profile popover must integrate into multiple existing components (`MembersPanel.razor`, `MessageList.razor`, `DirectMessageList.razor`). These are all owned by Client Experience, so no cross-domain coordination is needed.

---

## 3. Data Model

### Existing AppUser Entity

File: `src/HotBox.Core/Entities/AppUser.cs`

```csharp
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public UserStatus Status { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    // ... navigation properties
}
```

### New Fields to Add

| Field | Type | Max Length | Nullable | Default | Notes |
|-------|------|-----------|----------|---------|-------|
| `Bio` | `string?` | 256 | Yes | `null` | Short "about me" text |
| `Pronouns` | `string?` | 50 | Yes | `null` | e.g., "he/him", "she/her", "they/them" |
| `CustomStatus` | `string?` | 128 | Yes | `null` | User-set status text, e.g., "On vacation" |

These are intentionally simple string fields. No new enums, no new tables. The existing `Status` enum (`Online`, `Idle`, `DoNotDisturb`, `Offline`) remains the system-managed presence status. `CustomStatus` is the user-set freetext overlay.

### Updated AppUser Entity

File to modify: `src/HotBox.Core/Entities/AppUser.cs`

```csharp
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public UserStatus Status { get; set; }
    public string? Bio { get; set; }
    public string? Pronouns { get; set; }
    public string? CustomStatus { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    // ... navigation properties unchanged
}
```

### EF Core Configuration Updates

File to modify: `src/HotBox.Infrastructure/Data/Configurations/AppUserConfiguration.cs`

Add to the `Configure` method:

```csharp
builder.Property(u => u.Bio)
    .HasMaxLength(256);

builder.Property(u => u.Pronouns)
    .HasMaxLength(50);

builder.Property(u => u.CustomStatus)
    .HasMaxLength(128);
```

### Migration

A new EF Core migration is needed: `AddUserProfileFields`

This adds three nullable string columns to the `AspNetUsers` table. No data loss, no index changes, fully backward-compatible.

### DTOs

#### UserProfileResponse (server -> client)

File to create: `src/HotBox.Application/Models/UserProfileResponse.cs`

```csharp
public class UserProfileResponse
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Pronouns { get; set; }
    public string? CustomStatus { get; set; }
    public string Status { get; set; } = string.Empty;  // Online/Idle/DoNotDisturb/Offline
    public string Role { get; set; } = string.Empty;     // Admin/Moderator/Member
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
```

#### UpdateProfileRequest (client -> server)

File to create: `src/HotBox.Application/Models/UpdateProfileRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

public class UpdateProfileRequest
{
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    [Url]
    public string? AvatarUrl { get; set; }

    [MaxLength(256)]
    public string? Bio { get; set; }

    [MaxLength(50)]
    public string? Pronouns { get; set; }

    [MaxLength(128)]
    public string? CustomStatus { get; set; }
}
```

#### Client-Side Models

File to create: `src/HotBox.Client/Models/UserProfileResponse.cs`

Mirror of the server-side `UserProfileResponse` (identical shape, different namespace). This follows the existing pattern where `HotBox.Client/Models/` has its own copies of response models (see `MessageResponse.cs`, `ChannelResponse.cs`, etc.).

File to create: `src/HotBox.Client/Models/UpdateProfileRequest.cs`

```csharp
public class UpdateProfileRequest
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? Pronouns { get; set; }
    public string? CustomStatus { get; set; }
}
```

---

## 4. API Design

All endpoints are under `/api/users` (following the existing routing pattern in `AuthController` which uses `api/auth` without the `v1` prefix -- the technical spec says `/api/v1/` but the actual codebase uses `/api/`).

### Endpoints

#### GET /api/users/me

Get the authenticated user's own profile.

- **Auth**: `[Authorize]` (any authenticated user)
- **Response**: `200 OK` with `UserProfileResponse`
- **Notes**: Returns the full profile for the current user including email (self-only field). The role is fetched via `UserManager.GetRolesAsync()`.

#### PUT /api/users/me

Update the authenticated user's own profile.

- **Auth**: `[Authorize]` (any authenticated user)
- **Request body**: `UpdateProfileRequest`
- **Response**: `200 OK` with updated `UserProfileResponse`
- **Validation**:
  - `DisplayName` is required, 1-100 characters
  - `AvatarUrl` must be a valid URL if provided, max 500 characters
  - `Bio` max 256 characters
  - `Pronouns` max 50 characters
  - `CustomStatus` max 128 characters
- **Notes**: Users can only edit their own profile. The `Status` field (Online/Idle/etc.) is NOT editable here -- it's managed by the presence system.

#### GET /api/users/{id}

Get another user's public profile.

- **Auth**: `[Authorize]` (any authenticated user)
- **Response**: `200 OK` with `UserProfileResponse`, or `404 Not Found`
- **Notes**: Returns the same shape as `/me` but for any user. All fields are considered public in a small private server context.

#### GET /api/users

List all users (brief info).

- **Auth**: `[Authorize]` (any authenticated user)
- **Response**: `200 OK` with `List<UserProfileResponse>`
- **Notes**: Returns all users. No pagination needed at the ~100 user scale. Useful for member lists and user search.

### Controller

File to create: `src/HotBox.Application/Controllers/UsersController.cs`

```csharp
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    // Inject: UserManager<AppUser>, IPresenceService, ILogger
}
```

The controller reads directly from `UserManager<AppUser>` (ASP.NET Identity) since there is no separate user repository. This is consistent with how `AuthController` and `AdminController` already work -- they both inject `UserManager<AppUser>` directly.

### Authorization Rules

| Action | Who Can Do It |
|--------|---------------|
| View any profile | Any authenticated user |
| Edit own profile | The user themselves |
| Edit another's profile | Admin only (via admin panel, not this controller) |
| List all users | Any authenticated user |

Admins editing other users' profiles is already handled by the existing `AdminController` at `PUT /api/admin/users/{id}/role`. A future enhancement could add `PUT /api/admin/users/{id}/profile` if needed, but it is out of scope for MVP.

---

## 5. UI/UX Design

### Design Tokens Reference

From `docs/designs/design-spec.md`, the profile UI should use:

- **Popover background**: `--bg-raised` (`#1c1c25`)
- **Popover border**: `--border-light` (`rgba(255, 255, 255, 0.07)`)
- **Popover shadow**: `--shadow-overlay` (`0 12px 40px rgba(0, 0, 0, 0.7), 0 0 0 1px var(--border-subtle)`)
- **Popover border radius**: `--radius-lg` (`12px`)
- **Section labels**: `--text-muted`, `--font-mono`, 10-11px, uppercase, `letter-spacing: 0.06em`
- **Primary text**: `--text-primary` (`#e2e2ea`)
- **Secondary text**: `--text-secondary` (`#9898a8`)
- **Avatar sizing**: 64px for the profile popover (larger than the 26-34px used in member list/messages)
- **Z-index**: 50-60 range (above members overlay at 30, below modals at 100+)

### 5.1 Profile Popover (View Profile)

The profile popover is a floating card that appears when a user clicks on another user's name or avatar. It is the primary way to view a profile.

**Trigger points** (click, not hover):
- Message author name in `MessageList.razor`
- Member name/avatar in `MembersPanel.razor`
- User name in `DirectMessageList.razor`

**Layout:**

```
+----------------------------------+
|  [64px Avatar]                   |
|  DisplayName        [Role Badge] |
|  Pronouns                        |
|  CustomStatus (if set)           |
|                                  |
|  ---- ABOUT ----                 |
|  Bio text here...                |
|                                  |
|  ---- MEMBER SINCE ----          |
|  Feb 11, 2026                    |
|                                  |
|  [Send Message]                  |
+----------------------------------+
```

**Component structure:**

```
UserProfilePopover.razor
  - Avatar (64px, with status dot)
  - Display name (--text-primary, 15px, weight 600)
  - Pronouns (--text-muted, 12px) -- only shown if set
  - Custom status (--text-secondary, 13px) -- only shown if set
  - Role badge (small pill: accent bg for Admin, muted for Member)
  - Bio section (--text-secondary, 13px)
  - Member since date (--text-muted, mono, 10px)
  - "Send Message" button (opens or navigates to DM) -- only for other users
```

**Behavior:**
- Positioned relative to the trigger element (below or above, depending on viewport)
- Closes on click-outside, Escape key, or clicking another user
- Shows a loading skeleton while the API request is in flight
- Caches profile data for the session to avoid repeated API calls

**File to create:** `src/HotBox.Client/Components/Profile/UserProfilePopover.razor`

### 5.2 Edit Profile Modal (Edit Own Profile)

Accessed from the user panel at the bottom-left of the sidebar (the existing `UserPanel` area) via a gear/edit icon, or from the user's own profile popover via an "Edit Profile" button.

**Layout:**

```
+--------------------------------------+
|  Edit Profile                    [X] |
|                                      |
|  DISPLAY NAME                        |
|  [____________________________]      |
|                                      |
|  AVATAR URL                          |
|  [____________________________]      |
|  (Preview: [avatar])                 |
|                                      |
|  PRONOUNS                            |
|  [____________________________]      |
|                                      |
|  CUSTOM STATUS                       |
|  [____________________________]      |
|                                      |
|  BIO                                 |
|  [____________________________]      |
|  [____________________________]      |
|                                      |
|          [Cancel]  [Save Changes]    |
+--------------------------------------+
```

**Component structure:**

- Modal overlay (full-screen backdrop at `--bg-deepest` with 0.5 opacity)
- Card (max-width 440px, `--bg-deep` background, `--radius-lg` border radius)
- Form inputs follow the `.form-input` pattern from the design spec
- Save button follows `.btn-primary` / `.btn-submit` pattern
- Cancel button follows `.btn-secondary` pattern

**Behavior:**
- Opens as a centered modal with backdrop
- Pre-populates with current profile data from `/api/users/me`
- Client-side validation (required display name, URL format for avatar)
- Shows inline validation errors per the `.form-group.has-error` pattern
- On save: `PUT /api/users/me`, then updates `AuthState.CurrentUser` and closes modal
- Avatar preview: if an avatar URL is entered, show a small preview next to the input
- Focus trap inside the modal while open

**Files to create:**
- `src/HotBox.Client/Components/Profile/EditProfileModal.razor`

### 5.3 Integration Points

**MembersPanel.razor** (`src/HotBox.Client/Components/MembersPanel.razor`):
- Add `@onclick` handler to each member item
- On click: open `UserProfilePopover` positioned near the clicked member

**MessageList.razor** (`src/HotBox.Client/Components/MessageList.razor`):
- Add `@onclick` handler to the message author name (the `.msg-author` span)
- On click: open `UserProfilePopover` positioned near the clicked name

**DirectMessageList.razor** (or equivalent DM list component):
- Add `@onclick` handler to the DM user entry
- On click: open `UserProfilePopover` positioned near the clicked entry

**User panel area** (bottom-left sidebar):
- Add a small edit/gear icon button next to the current user's name
- On click: open `EditProfileModal`

### 5.4 Popover Positioning

Rather than implementing a full positioning library, use a simple approach:

1. On click, capture the trigger element's bounding rect
2. Position the popover absolutely relative to the viewport
3. Prefer placing it to the right of the trigger (for sidebar items) or below (for message authors)
4. If it would overflow the viewport, flip to the opposite side
5. This logic lives in the `UserProfilePopover.razor` component's `@code` block

### 5.5 ApiClient Methods

Add to `src/HotBox.Client/Services/ApiClient.cs`:

```csharp
// ── Users ──────────────────────────────────────────────────────────

public async Task<UserProfileResponse?> GetMyProfileAsync(CancellationToken ct = default)
{
    return await GetAsync<UserProfileResponse>("api/users/me", ct);
}

public async Task<UserProfileResponse?> GetUserProfileAsync(Guid userId, CancellationToken ct = default)
{
    return await GetAsync<UserProfileResponse>($"api/users/{userId}", ct);
}

public async Task<UserProfileResponse?> UpdateMyProfileAsync(
    UpdateProfileRequest request,
    CancellationToken ct = default)
{
    return await PutReturningAsync<UserProfileResponse>("api/users/me", request, ct);
}
```

---

## 6. Implementation Plan

### Work Items

| # | Task | Domain Agent | Depends On | Size | Phase |
|---|------|-------------|------------|------|-------|
| 1 | Add `Bio`, `Pronouns`, `CustomStatus` fields to `AppUser` entity | **platform** | -- | S | MVP |
| 2 | Update `AppUserConfiguration.cs` with max-length constraints | **platform** | #1 | S | MVP |
| 3 | Generate EF Core migration `AddUserProfileFields` | **platform** | #2 | S | MVP |
| 4 | Create `UserProfileResponse` and `UpdateProfileRequest` DTOs (server) | **auth-security** | #1 | S | MVP |
| 5 | Create `UsersController` with GET/PUT endpoints | **auth-security** | #3, #4 | M | MVP |
| 6 | Create client-side `UserProfileResponse` and `UpdateProfileRequest` models | **client-experience** | #4 | S | MVP |
| 7 | Add `GetMyProfileAsync`, `GetUserProfileAsync`, `UpdateMyProfileAsync` to `ApiClient` | **client-experience** | #5, #6 | S | MVP |
| 8 | Build `UserProfilePopover.razor` component | **client-experience** | #7 | M | MVP |
| 9 | Build `EditProfileModal.razor` component | **client-experience** | #7 | M | MVP |
| 10 | Integrate popover into `MembersPanel.razor` | **client-experience** | #8 | S | MVP |
| 11 | Integrate popover into `MessageList.razor` (author name click) | **client-experience** | #8 | S | MVP |
| 12 | Add edit profile trigger to user panel area | **client-experience** | #9 | S | MVP |

### Execution Order

```
Phase 1 (parallel):
  platform:  #1 -> #2 -> #3  (entity + config + migration)

Phase 2 (after Phase 1):
  auth-security:  #4 -> #5   (DTOs + controller)

Phase 3 (after Phase 2):
  client-experience:  #6 -> #7 -> #8, #9 (parallel) -> #10, #11, #12 (parallel)
```

Platform work (#1-3) and auth-security work (#4-5) are sequential because the controller needs the entity fields to exist. Client experience work (#6-12) is sequential after the API is built but has internal parallelism (popover and modal can be built simultaneously, integration points can be done in parallel once the popover is ready).

### Detailed Task Descriptions

#### Task 1-3: Platform (Entity + Migration)

**Files to modify:**
- `src/HotBox.Core/Entities/AppUser.cs` -- add three nullable string properties
- `src/HotBox.Infrastructure/Data/Configurations/AppUserConfiguration.cs` -- add max-length constraints

**Files to create:**
- New migration via `dotnet ef migrations add AddUserProfileFields`

**Pattern to follow:** Look at the existing `AppUser.cs` (line 10, `AvatarUrl` property) for the nullable string pattern. Look at `AppUserConfiguration.cs` (lines 16-17, `AvatarUrl` config) for the max-length configuration pattern.

#### Task 4-5: Auth & Security (DTOs + Controller)

**Files to create:**
- `src/HotBox.Application/Models/UserProfileResponse.cs`
- `src/HotBox.Application/Models/UpdateProfileRequest.cs`
- `src/HotBox.Application/Controllers/UsersController.cs`

**Pattern to follow:** Look at `src/HotBox.Application/Controllers/AuthController.cs` for the controller pattern (constructor injection of `UserManager<AppUser>`, `ILogger`, route attribute style). Look at `src/HotBox.Application/Models/AdminModels.cs` for the DTO pattern (simple POCOs with `System.ComponentModel.DataAnnotations` attributes).

**Controller implementation notes:**
- Inject `UserManager<AppUser>`, `IPresenceService` (for live status), `ILogger<UsersController>`
- For `GET /api/users/me`: extract user ID from `User.FindFirstValue(ClaimTypes.NameIdentifier)`, load via `UserManager.FindByIdAsync()`, get role via `UserManager.GetRolesAsync()`
- For `PUT /api/users/me`: same user ID extraction, validate request, update fields, call `UserManager.UpdateAsync()`
- For `GET /api/users/{id}`: `UserManager.FindByIdAsync()`, return 404 if null
- For `GET /api/users`: `UserManager.Users.ToListAsync()` -- acceptable at ~100 user scale

#### Task 6-7: Client Experience (Client Models + ApiClient)

**Files to create:**
- `src/HotBox.Client/Models/UserProfileResponse.cs`
- `src/HotBox.Client/Models/UpdateProfileRequest.cs`

**File to modify:**
- `src/HotBox.Client/Services/ApiClient.cs` -- add three new methods in a `// -- Users --` section

**Pattern to follow:** Look at the existing `MessageResponse` in `src/HotBox.Client/Models/MessageResponse.cs` for the client model pattern. Look at the `// -- Admin --` section in `ApiClient.cs` (lines 175-241) for how API methods are organized and structured.

#### Task 8: UserProfilePopover Component

**File to create:** `src/HotBox.Client/Components/Profile/UserProfilePopover.razor`

**Parameters:**
- `Guid UserId` -- the user to display
- `ElementReference TriggerElement` -- for positioning (or use absolute x/y coordinates)
- `EventCallback OnClose` -- callback when the popover should close

**Behavior:**
- On initialize: call `ApiClient.GetUserProfileAsync(UserId)`
- Show loading skeleton while loading (follow the skeleton pattern in `MembersPanel.razor` lines 6-22)
- Render profile data per the layout in Section 5.1
- Register click-outside handler and Escape key handler to close
- "Send Message" button: navigate to DM with that user (follow existing DM navigation pattern)

**Pattern to follow:** Look at `src/HotBox.Client/Components/MembersPanel.razor` for the avatar color generation (`GetAvatarColor`), initials extraction (`GetInitials`), and status dot rendering patterns. Look at `src/HotBox.Client/Components/SearchOverlay.razor` for overlay/popover patterns (click-outside, escape key handling).

#### Task 9: EditProfileModal Component

**File to create:** `src/HotBox.Client/Components/Profile/EditProfileModal.razor`

**Parameters:**
- `EventCallback OnClose` -- callback when modal closes
- `EventCallback OnSaved` -- callback when profile is successfully saved

**Behavior:**
- On initialize: call `ApiClient.GetMyProfileAsync()` to pre-populate form
- Form fields: DisplayName (required), AvatarUrl, Pronouns, CustomStatus, Bio (textarea)
- Client-side validation before submit
- On save: call `ApiClient.UpdateMyProfileAsync()`, update `AuthState.CurrentUser` with new display name, invoke `OnSaved`, close modal
- Escape key closes, backdrop click closes

**Pattern to follow:** Look at the form patterns in `docs/designs/design-spec.md` Section "Form Elements" for CSS classes (`.form-input`, `.form-label`, `.form-group`, `.form-error`, `.btn-submit`, `.btn-secondary`). Look at `src/HotBox.Client/Components/SearchOverlay.razor` for the modal overlay pattern.

#### Tasks 10-12: Integration

**Task 10 -- MembersPanel integration:**
- Modify `src/HotBox.Client/Components/MembersPanel.razor`
- Add an `@onclick` handler to each `.member-item` div (line 38)
- Track `selectedUserId` in component state
- Render `<UserProfilePopover>` conditionally when a user is selected

**Task 11 -- MessageList integration:**
- Modify `src/HotBox.Client/Components/MessageList.razor`
- Add an `@onclick` handler to the message author name
- Same pattern as MembersPanel: track selected user, render popover

**Task 12 -- User panel edit trigger:**
- The user panel area at the bottom-left sidebar currently shows the logged-in user's name
- Add a small gear/edit icon button
- On click: render `<EditProfileModal>`

---

## 7. Testing Strategy

### Unit Tests

| Test | Location | What It Verifies |
|------|----------|-----------------|
| `UsersController.GetMe_ReturnsProfile` | `tests/HotBox.Application.Tests/` | Authenticated user gets their own profile |
| `UsersController.GetMe_Unauthenticated_Returns401` | `tests/HotBox.Application.Tests/` | Anonymous request is rejected |
| `UsersController.UpdateMe_ValidRequest_UpdatesProfile` | `tests/HotBox.Application.Tests/` | Profile fields are updated and persisted |
| `UsersController.UpdateMe_EmptyDisplayName_Returns400` | `tests/HotBox.Application.Tests/` | Validation rejects empty display name |
| `UsersController.UpdateMe_TooLongBio_Returns400` | `tests/HotBox.Application.Tests/` | Validation rejects bio over 256 chars |
| `UsersController.GetUser_ExistingUser_ReturnsProfile` | `tests/HotBox.Application.Tests/` | Can view another user's profile |
| `UsersController.GetUser_NonExistent_Returns404` | `tests/HotBox.Application.Tests/` | 404 for unknown user ID |
| `UsersController.GetAllUsers_ReturnsAllUsers` | `tests/HotBox.Application.Tests/` | List endpoint returns all users |

### Integration Tests

| Test | What It Verifies |
|------|-----------------|
| Profile roundtrip | Register user -> update profile -> get profile -> verify all fields match |
| Migration applies cleanly | `AddUserProfileFields` migration runs on SQLite without error |
| Existing users unaffected | After migration, existing users have null Bio/Pronouns/CustomStatus |

### Client Tests (Manual/Future)

| Scenario | Steps |
|----------|-------|
| View profile popover | Click a member name in the members panel, verify popover shows correct data |
| Edit profile | Open edit modal, change display name and bio, save, verify updates in popover |
| Popover dismissal | Open popover, click outside, verify it closes. Open popover, press Escape, verify it closes |
| Validation feedback | Open edit modal, clear display name, click save, verify error message appears |

---

## Date/Time Handling

- `CreatedAtUtc` and `LastSeenUtc` are stored in UTC (per existing convention)
- The profile popover displays `CreatedAtUtc` as "Member since Feb 11, 2026" (local timezone, long date format)
- `LastSeenUtc` is displayed as relative time for recent ("5 minutes ago") or absolute for older ("Feb 10, 2026")
- Follows the existing convention from `docs/technical-spec.md` Section 4.5

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2026-02-12 | Initial specification |
