# UI/UX Polish — Task Tracker

Tracking document for fixing the 26 issues identified in `docs/ui-ux-issues.md`.

---

## Phase 1: Dead-End Fixes & Loading States
*High impact, low effort — eliminates the most jarring UX problems*

| # | Issue | Status | Notes |
|---|-------|--------|-------|
| 1 | Dead-end root page after login | pending | IndexRedirect.razor → auto-nav to first channel |
| 7 | Section switching doesn't auto-select a channel | pending | MainLayout.razor:396-397 → pick first channel |
| 2 | Channel list skeleton loaders never shown | pending | MainLayout.razor → call SetLoadingChannels(true) before fetch |
| 4 | Members panel skeletons never shown | pending | MainLayout.razor → call PresenceState.SetLoading(true) on init |
| 9 | Empty channel-info-bar on root landing | pending | ChannelHeader.razor → hide bar or show welcome context |

## Phase 2: Initialization & Performance

| # | Issue | Status | Notes |
|---|-------|--------|-------|
| 3 | Sequential initialization (5 serial network calls) | pending | Task.WhenAll for steps 3-5 |
| 6 | Duplicate conversation loading | pending | Single source of truth for conversations |
| 5 | Flash of empty content between channel switches | pending | Set loading flag in SetActiveChannel() |

## Phase 3: Message Display Polish

| # | Issue | Status | Notes |
|---|-------|--------|-------|
| 11 | Timestamps only show time, never date | pending | Relative dates for older messages |
| 12 | No date separators between messages | pending | CSS exists, markup missing |
| 13 | No hover timestamp on continuation messages | pending | Gutter hover tooltip |

## Phase 4: Visual Identity Fixes

| # | Issue | Status | Notes |
|---|-------|--------|-------|
| 10 | Topbar avatar shows hardcoded "U" | pending | Use AuthState.CurrentUser.DisplayName initials |
| 14 | Voice bar chips overflow with full names | pending | Show initials instead of DisplayName |
| 15 | Notification avatars all same color | pending | Use GetAvatarColor hash |
| 16 | DMs page list items lack avatars | pending | Add avatar circles |
| 17 | App loading spinner has no branding | pending | Branded loading screen |

## Phase 5: Input & Interaction

| # | Issue | Status | Notes |
|---|-------|--------|-------|
| 18 | Message input is single-line input | pending | Convert to textarea with auto-resize |
| 19 | No keyboard shortcut to open search | pending | Verify Ctrl+K JS registration |
| 20 | No focus management on channel/DM nav | pending | Auto-focus input on navigation |
| 8 | Notifications back button goes to root | pending | Return to previous location |

## Phase 6: CSS & Accessibility Polish

| # | Issue | Status | Notes |
|---|-------|--------|-------|
| 23 | Missing focus-visible rings | pending | Add :focus-visible to interactive elements |
| 22 | Missing ::selection styling | pending | Accent-tinted selection colors |
| 24 | Toast exit animation | pending | Fade-out / slide-out |
| 25 | Toggle password SVG sizing | pending | Add .toggle-password svg rule |
| 26 | Missing scroll-behavior: smooth | pending | Add to .messages-area |
| 21 | Mention autocomplete visual feedback | pending | Colored pill for inserted mentions |

---

## Phase 1 — Implementation Plan

### Issue 1 + 7: Eliminate the dead-end root page

**Problem:** After login, users land on `/` which shows a static welcome placeholder. The `SwitchSection("channels")` method also navigates to `/` when no channel was previously active, even though `ChannelState.Channels` is populated.

**Fix:** Make `IndexRedirect.razor` subscribe to `ChannelState.OnChange` and auto-navigate to the first text channel once channels are loaded. Also fix `SwitchSection` to pick the first channel instead of navigating to `/`.

**Files:**
- `IndexRedirect.razor` — add OnInitialized logic to redirect when channels exist
- `MainLayout.razor:394-397` — change the `else` branch in `SwitchSection` to pick first channel

**Changes:**

`IndexRedirect.razor`:
- Inject `ChannelState` and `NavigationManager`
- In `OnInitialized`, check if channels are already loaded → navigate to first text channel
- Subscribe to `ChannelState.OnChange` to handle the case where channels load after the component mounts
- Use `NavigateTo(..., replace: true)` so the placeholder never appears in browser history
- Implement `IDisposable` to unsubscribe

`MainLayout.razor` `SwitchSection("channels")`:
- Replace `Navigation.NavigateTo("/")` with logic to pick the first text channel:
  ```csharp
  var firstChannel = ChannelState.Channels
      .Where(c => c.Type == ChannelType.Text)
      .OrderBy(c => c.SortOrder)
      .FirstOrDefault();
  if (firstChannel is not null)
      Navigation.NavigateTo($"/channels/{firstChannel.Id}");
  ```

### Issue 2: Channel list skeleton loaders

**Problem:** `ChannelList.razor:10` checks `ChannelState.IsLoadingChannels` but it's never set to `true`. The skeleton code is dead.

**Fix:** In `MainLayout.OnInitializedAsync`, call `ChannelState.SetLoadingChannels(true)` before `Api.GetChannelsAsync()`, and `SetLoadingChannels(false)` after.

**Files:**
- `MainLayout.razor:263-265` — wrap channel fetch with loading flag

**Changes:**
```csharp
// Before:
var channels = await Api.GetChannelsAsync();
ChannelState.SetChannels(channels);

// After:
ChannelState.SetLoadingChannels(true);
try
{
    var channels = await Api.GetChannelsAsync();
    ChannelState.SetChannels(channels);
}
finally
{
    ChannelState.SetLoadingChannels(false);
}
```

### Issue 4: Members panel skeletons

**Problem:** `PresenceState.IsLoading` defaults to `false`. Nobody sets it to `true` before presence data arrives. The members panel renders empty until `SeedAllUsers`/`SetOnlineUsers` fire.

**Fix:** Set `PresenceState.SetLoading(true)` early in `MainLayout.OnInitializedAsync`. The `SetOnlineUsers` method already sets `IsLoading = false` (line 86 of PresenceState.cs), so we just need the initial `true`.

**Files:**
- `MainLayout.razor` — add `PresenceState.SetLoading(true)` near the top of `OnInitializedAsync`

### Issue 9: Empty channel-info-bar on root landing

**Problem:** When no channel or DM is active, the `channel-info-bar` div renders with `ChannelHeader` inside, but the component outputs nothing (both `if` branches require an active channel/DM). Result: a 36px empty strip.

**Fix:** Hide the bar entirely when nothing is active. Add a conditional in `MainLayout.razor` around the channel-info-bar div.

**Files:**
- `MainLayout.razor:153-156` — conditional render

**Changes:**
```razor
@if (ChannelState.ActiveChannel is not null || DirectMessageState.ActiveConversationUserId.HasValue)
{
    <div class="channel-info-bar">
        <ChannelHeader />
    </div>
}
```
