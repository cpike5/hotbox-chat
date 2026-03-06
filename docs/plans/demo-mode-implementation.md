# Demo Mode Implementation Plan

**Issue:** #210
**Date:** 2026-03-06

## 1. Overview

Add a toggleable "Demo Mode" to HotBox that allows anonymous users to register with just a username/display name (no email/password), get auto-routed into seeded demo channels, chat via SignalR, and be automatically cleaned up after 5 minutes of inactivity. Demo users cannot access DMs, channel creation, or admin features. A visible banner indicates temporary data, and rate limiting caps concurrent demo users at 50 with IP-based cooldowns.

## 2. Files to Create

| File | Layer | Purpose |
|------|-------|---------|
| `src/HotBox.Core/Options/DemoModeOptions.cs` | Core | Configuration POCO |
| `src/HotBox.Core/Interfaces/IDemoUserService.cs` | Core | Interface for demo user lifecycle |
| `src/HotBox.Infrastructure/Services/DemoUserService.cs` | Infrastructure | Demo user creation, tracking, cleanup |
| `src/HotBox.Infrastructure/Services/DemoCleanupService.cs` | Infrastructure | `BackgroundService` for periodic cleanup |
| `src/HotBox.Application/Controllers/DemoController.cs` | Application | `POST /api/demo/register`, `GET /api/demo/status` |
| `src/HotBox.Application/Models/DemoRegisterRequest.cs` | Application | Request DTO (username + display name) |
| `src/HotBox.Client/Pages/DemoLoginPage.razor` | Client | `/demo` simplified registration page |
| `src/HotBox.Client/Models/DemoRegisterRequest.cs` | Client | Client-side request model |
| `src/HotBox.Client/Components/DemoBanner.razor` | Client | Top banner: "Demo Mode - data is temporary" |

## 3. Files to Modify

| File | Change |
|------|--------|
| `src/HotBox.Core/Entities/AppUser.cs` | Add `bool IsDemo` property |
| `src/HotBox.Infrastructure/Data/Configurations/AppUserConfiguration.cs` | Configure `IsDemo` column with default `false`, filtered index |
| `src/HotBox.Infrastructure/Services/TokenService.cs` | Add `is_demo` claim to JWT |
| `src/HotBox.Infrastructure/Data/Seeding/DatabaseSeeder.cs` | Seed `#Games`, `#Music` channels when demo mode enabled |
| `src/HotBox.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` | Register `IDemoUserService`, bind `DemoModeOptions` |
| `src/HotBox.Application/Controllers/ChannelsController.cs` | Block create/update/delete for demo users |
| `src/HotBox.Application/Controllers/DirectMessagesController.cs` | Block all endpoints for demo users |
| `src/HotBox.Application/Hubs/ChatHub.cs` | Track demo user activity, block DMs, handle heartbeat |
| `src/HotBox.Application/Program.cs` | Register `DemoCleanupService`, wire purge event to SignalR |
| `src/HotBox.Application/DependencyInjection/ApplicationServiceExtensions.cs` | Bind `DemoModeOptions` |
| `src/HotBox.Client/Models/UserInfo.cs` | Add `bool IsDemo` property |
| `src/HotBox.Client/Services/JwtParser.cs` | Parse `is_demo` claim |
| `src/HotBox.Client/State/AuthState.cs` | Add `IsDemo` convenience property |
| `src/HotBox.Client/Services/ApiClient.cs` | Add `DemoRegisterAsync`, `GetDemoStatusAsync` methods |
| `src/HotBox.Client/Services/ChatHubService.cs` | Handle `DemoSessionExpired` hub event |
| `src/HotBox.Client/Layout/MainLayout.razor` | Render `DemoBanner`, hide DMs/admin for demo users |
| `src/HotBox.Client/Pages/LoginPage.razor` | Add "Try Demo" button when demo mode enabled |
| `src/HotBox.Client/Pages/RegisterPage.razor` | Add "Try Demo" link when demo mode enabled |

## 4. Migration

EF Core migration required for `IsDemo` column on `AspNetUsers`.

## 5. Phase Breakdown

### Phase 1: Core Layer + Configuration

**Prerequisite for all other phases.**

1. Create `DemoModeOptions.cs`:
   - `SectionName = "DemoMode"`
   - `bool Enabled` (default `false`)
   - `int MaxConcurrentUsers` (default `50`)
   - `TimeSpan SessionTimeout` (default 5 min)
   - `TimeSpan CleanupInterval` (default 1 min)
   - `TimeSpan IpCooldown` (default 2 min)
   - `string[] SeedChannels` (default `["General", "Games", "Music"]`)

2. Add `bool IsDemo` to `AppUser` entity.

3. Create `IDemoUserService` interface:
   - `CreateDemoUserAsync(username, displayName, ipAddress, ct)`
   - `GetActiveDemoUserCountAsync(ct)`
   - `IsIpCoolingDownAsync(ipAddress, ct)`
   - `RecordActivityAsync(userId)`
   - `GetExpiredDemoUserIdsAsync(ct)`
   - `PurgeDemoUserAsync(userId, ct)`

4. Add `DemoMode` section to `appsettings.json`:
   ```json
   "DemoMode": {
     "Enabled": false,
     "MaxConcurrentUsers": 50,
     "SessionTimeoutMinutes": 5,
     "CleanupIntervalMinutes": 1,
     "IpCooldownMinutes": 2,
     "SeedChannels": ["General", "Games", "Music"]
   }
   ```

### Phase 2: Infrastructure Layer

**Depends on Phase 1.**

1. Configure `IsDemo` in `AppUserConfiguration` with default value and filtered index.
2. Create EF Core migration.
3. Add `is_demo` claim to `TokenService` JWT generation.
4. Implement `DemoUserService`:
   - Password-less user creation (same pattern as OAuth flow)
   - IP cooldown tracking via `ConcurrentDictionary<string, DateTime>`
   - Activity tracking via existing `LastSeenUtc` field
   - Purge: delete messages, refresh tokens, then user
5. Implement `DemoCleanupService` (`BackgroundService`):
   - Periodic loop on `CleanupInterval`
   - Query expired demo users, purge each
   - Expose `event Func<Guid, Task>? OnDemoUserPurged` for SignalR notification
   - Prune expired IP cooldown entries
   - Only runs when `DemoModeOptions.Enabled`
6. Seed `#Games` and `#Music` channels in `DatabaseSeeder` when demo mode enabled.
7. Register services in `InfrastructureServiceExtensions`.

### Phase 3: Application Layer (API + Hubs)

**Depends on Phase 2.**

1. Create `DemoRegisterRequest` DTO (username + display name).
2. Create `DemoController`:
   - `POST /api/demo/register` — validate limits, create user, return JWT
   - `GET /api/demo/status` — return enabled/currentUsers/maxUsers
3. Modify `ChatHub`:
   - `RecordActivityAsync` on `SendMessage`, `Heartbeat`, `OnConnectedAsync` for demo users
   - Block `SendDirectMessage` for demo users
   - Helper: `IsDemoUser()` from claims
4. Guard `ChannelsController` create/update/delete for demo users (403).
5. Guard `DirectMessagesController` all endpoints for demo users (403).
6. Wire `DemoCleanupService.OnDemoUserPurged` to send `DemoSessionExpired` via `IHubContext<ChatHub>` in `Program.cs`.
7. Register singleton + hosted service pattern for `DemoCleanupService`.

### Phase 4: Client Layer (Blazor WASM)

**Depends on Phase 3 API contracts.**

1. Add `IsDemo` to `UserInfo`, parse `is_demo` claim in `JwtParser`.
2. Add `IsDemo` property to `AuthState`.
3. Add `DemoRegisterAsync` and `GetDemoStatusAsync` to `ApiClient`.
4. Create `DemoLoginPage.razor` at `/demo`:
   - Username + Display Name form
   - Show capacity (X/50 slots)
   - Error handling for rate limiting, capacity
5. Create `DemoBanner.razor`:
   - Fixed banner: "Demo Mode - Your data will be deleted after 5 minutes of inactivity"
   - Amber/warning styling
6. Modify `MainLayout.razor`:
   - Render `DemoBanner` for demo users
   - Hide DMs section switcher for demo users
   - Hide admin link for demo users
7. Handle `DemoSessionExpired` in `ChatHubService`:
   - Logout, navigate to `/login?message=demo_expired`
8. Add "Try Demo" links to `LoginPage.razor` and `RegisterPage.razor`.

### Phase 5: Testing

**After all implementation.**

1. `DemoControllerTests` — registration happy path, rate limiting, capacity, disabled returns 404
2. `DemoUserServiceTests` — creation, IP cooldown, expiry detection, purge
3. `DemoCleanupServiceTests` — cleanup loop finds and purges expired users

## 6. Execution Order

```
Phase 1 (Core + Config)
    |
    v
Phase 2 (Infrastructure)
    |
    v
Phase 3 (Application)
    |
    v
Phase 4 (Client)
    |
    v
Phase 5 (Testing)
```

Phases are sequential — each depends on the previous.

## 7. Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Password-less auth** | Same pattern as existing OAuth flow (`UserManager.CreateAsync` without password). Demo users get JWT immediately on registration. |
| **Reuse `LastSeenUtc`** | Already exists on `AppUser`. Avoids new columns. Updated via ChatHub heartbeat. |
| **`IsDemo` flag on entity** | Simple, queryable, works with existing Identity system. Cleanup queries `WHERE IsDemo = true AND LastSeenUtc < threshold`. |
| **Event-based SignalR notification** | `DemoCleanupService` raises event, `Program.cs` wires it to `IHubContext<ChatHub>`. Avoids circular dependency. |
| **Auto-generated username** | Form input becomes `DisplayName`. `UserName` gets random suffix (e.g., `demo_alice_x7k2`) for uniqueness. |
| **Skip refresh tokens for demo users** | Consider shorter JWT expiry (~10 min) and no refresh tokens. Simplifies cleanup. |

## 8. Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Cascade delete FK violations | `PurgeDemoUserAsync` deletes messages first, then tokens, then user. Test thoroughly. |
| Demo user purge during active typing | 5-min timeout with heartbeat resets is sufficient for MVP. |
| IP cooldown dictionary memory leak | `DemoCleanupService` prunes expired entries periodically. |
| Username collision | Auto-generated with random suffix ensures uniqueness. |
| Race condition on concurrent count | Acceptable for MVP — overshooting by 1-2 users is harmless. |
| Password-less user used at normal login | `CheckPasswordSignInAsync` fails for users with no password hash. Correct behavior. |

## 9. Acceptance Criteria

1. With `DemoMode:Enabled = true`, `/demo` page shows simplified registration form.
2. Demo registration creates user, returns JWT with `is_demo = true`, logs user in.
3. Demo users see amber "Demo Mode" banner at top of app.
4. Demo users see only channels (no DMs switcher, no channel create/edit/delete).
5. Demo users can send and receive messages in channels via SignalR.
6. Attempting DMs, channel management, or admin access returns 403.
7. After 5 min inactivity, user receives `DemoSessionExpired` via SignalR and is redirected to login.
8. Purged user's messages and account are deleted from database.
9. Registration rejected when 50 demo users are active.
10. Same IP cannot create new demo account within cooldown window.
11. With `DemoMode:Enabled = false`, `/api/demo/register` returns 404.
12. Login and Register pages show "Try Demo" links only when demo mode enabled.
13. Seeded channels `General`, `Games`, `Music` exist when demo mode enabled.
14. `dotnet build` succeeds. All new tests pass.
