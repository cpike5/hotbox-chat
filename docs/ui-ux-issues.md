# UI/UX Issues Audit

Comprehensive list of identified issues across the Blazor WASM client, organized by category.

---

## State Flow & Initialization

### 1. Dead-end root page after login
**Severity: High** | **Fix: Low**

After login, the user lands on `/` which renders `IndexRedirect.razor` — a static placeholder saying "Select a channel or direct message to get started." The app has already loaded channels into `ChannelState.Channels` but doesn't auto-navigate to the first one. Every login ends at this dead-end.

**Files:** `IndexRedirect.razor`, `MainLayout.razor:397`

### 2. Channel list never shows skeleton loaders
**Severity: Medium** | **Fix: Low**

`ChannelList.razor:10` checks `ChannelState.IsLoadingChannels` to render skeletons, but `MainLayout.OnInitializedAsync` never calls `SetLoadingChannels(true)` before `GetChannelsAsync()`. The channel tabs area is empty during the network fetch, then channels pop in abruptly. The skeleton code is effectively dead.

**Files:** `MainLayout.razor:264-265`, `ChannelState.cs:19`, `ChannelList.razor:10-19`

### 3. Sequential initialization — 5 network calls run serially
**Severity: Medium** | **Fix: Low**

`MainLayout.OnInitializedAsync` runs these in sequence:
1. `Api.GetChannelsAsync()`
2. `ChatHubService.StartAsync()`
3. `VoiceConnectionManager.InitializeAsync()`
4. `UnreadState.InitializeAsync()`
5. `NotificationState.InitializeAsync()`

Steps 3-5 are independent and could run with `Task.WhenAll`.

**Files:** `MainLayout.razor:264-279`

### 4. Members panel skeletons never shown
**Severity: Medium** | **Fix: Low**

`PresenceState.IsLoading` defaults to `false`. Nobody calls `SetLoading(true)` before presence data arrives. The members panel skips skeletons and renders nothing, then users pop in abruptly when `SeedAllUsers`/`SetOnlineUsers` fire.

**Files:** `PresenceState.cs:11`, `MembersPanel.razor:5-23`, `MainLayout.razor:300-309`

### 5. Flash of empty content between channel switches
**Severity: Low** | **Fix: Medium**

`ChannelState.SetActiveChannel()` clears `Messages` synchronously, but `SetLoadingMessages(true)` happens in the next async tick from `ChannelPage.OnParametersSetAsync`. For one render frame, the message area is blank (not loading, not showing messages).

**Files:** `ChannelState.cs:33-39`, `ChannelPage.razor:80-81`

### 6. Duplicate conversation loading
**Severity: Low** | **Fix: Low**

Both `DirectMessageList.razor:56-61` (topbar component) and `DmsPage.razor:62-74` independently call `Api.GetConversationsAsync()` and set `DirectMessageState.Conversations`. Whichever renders first wins; the other is a redundant network call.

**Files:** `DirectMessageList.razor:51-61`, `DmsPage.razor:57-74`

### 7. Section switching doesn't auto-select a channel
**Severity: Medium** | **Fix: Low**

When switching from DMs back to Channels, if no channel was previously active, `SwitchSection("channels")` navigates to `/` (the dead-end placeholder) even though channels are loaded in state. Should auto-select the first channel.

**Files:** `MainLayout.razor:393-398`

### 8. Notifications back button goes to root, not previous location
**Severity: Low** | **Fix: Low**

`NotificationsPage.razor:99-102` hardcodes `NavigateTo("/")`. If the user was in `#general`, clicked the bell, then clicked back, they land on the welcome placeholder instead of returning to `#general`.

**Files:** `NotificationsPage.razor:99-102`

### 9. Empty channel-info-bar on root landing
**Severity: Low** | **Fix: Low**

When no channel or DM is active (root page), the 36px `channel-info-bar` renders completely empty — a blank strip with a background and border. Looks like a visual glitch.

**Files:** `MainLayout.razor:154-156`, `ChannelHeader.razor:10-41`

---

## Visual & Layout Issues

### 10. User avatar in topbar shows hardcoded "U"
**Severity: Medium** | **Fix: Low**

`MainLayout.razor:147` renders a static "U" regardless of the logged-in user. Should show the user's initials from `AuthState.CurrentUser.DisplayName`. The status dot is also hardcoded to `online` instead of reflecting actual presence state.

**Files:** `MainLayout.razor:146-149`

### 11. Message timestamps only show time, never date
**Severity: Medium** | **Fix: Low**

`MessageList.razor:179-183` `FormatTime` always returns `h:mm tt`. Messages from yesterday or last week are indistinguishable from today's. Should show relative dates ("Yesterday 3:42 PM", "Mon 3:42 PM", "Jan 15") for older messages.

**Files:** `MessageList.razor:179-183`

### 12. No date separators between messages
**Severity: Medium** | **Fix: Medium**

CSS for `.date-separator` exists (`app.css:937-961`) but `MessageList.razor` never renders date separators between message groups. Days of conversation blend together with no visual break.

**Files:** `MessageList.razor:55-97`, `app.css:937-961`

### 13. No hover timestamp on continuation messages
**Severity: Low** | **Fix: Medium**

Continuation messages (same author within 5 min) have no visible timestamp. A 34px spacer div fills the avatar column but shows nothing on hover. Discord-style hover timestamps in the gutter would help users orient in long conversations.

**Files:** `MessageList.razor:86-93`

### 14. Voice bar user chips overflow with full display names
**Severity: Low** | **Fix: Low**

`.voice-bar-user-chip` CSS defines a 22x22 circle styled for initials, but `MainLayout.razor:182` renders `@peer.DisplayName` (the full name), which overflows the tiny circle. Should show initials.

**Files:** `MainLayout.razor:179-183`, `app.css:1101-1113`

### 15. Notification avatars all use same color
**Severity: Low** | **Fix: Low**

`NotificationsPage.razor:42-44` renders all notification avatars with the same teal `--accent` background. Should use the same `GetAvatarColor(name)` hash as messages and members for visual consistency.

**Files:** `NotificationsPage.razor:42-44`

### 16. DMs page list items lack avatars
**Severity: Low** | **Fix: Low**

The `/dms` conversation list shows name + preview + time but no avatar circle. Adding an avatar with the user's color hash would make it visually richer and consistent with the rest of the app.

**Files:** `DmsPage.razor:33-48`

### 17. App loading spinner has no branding
**Severity: Low** | **Fix: Low**

`App.razor:6-8` shows a bare spinner during WASM initialization. Since WASM takes a few seconds, this is a missed opportunity for a branded loading screen with the HotBox mark.

**Files:** `App.razor:4-8`

---

## Input & Interaction Issues

### 18. Message input is single-line `<input>`, not `<textarea>`
**Severity: Medium** | **Fix: Medium**

`MessageInput.razor:32-43` uses `<input type="text">`, limiting users to single-line messages. A `<textarea>` with auto-resize would support multi-line composition.

**Files:** `MessageInput.razor:32-43`

### 19. No keyboard shortcut to open search
**Severity: Medium** | **Fix: Low**

The search overlay has a `Ctrl+K` placeholder hint in the input but `SearchOverlay.razor` already has JS interop for `Ctrl+K` via `searchInterop.register`. Need to verify the JS side actually registers the global `keydown` handler. The search button in the topbar is the only reliable way to open it.

**Files:** `SearchOverlay.razor:21,138-143`, `MainLayout.razor:119-124`

### 20. No focus management on channel/DM navigation
**Severity: Low** | **Fix: Low**

When switching channels or DM conversations, the message input doesn't receive focus. Users must click into the input every time they switch.

**Files:** `ChannelPage.razor`, `DirectMessagePage.razor`, `MessageInput.razor`

### 21. Mention autocomplete gives no visual feedback for inserted mentions
**Severity: Low** | **Fix: Medium**

After selecting a user from the `@` autocomplete, the `@DisplayName` text in the input looks identical to regular text. A colored pill or highlight would make it clear a mention was inserted.

**Files:** `MessageInput.razor:161-177`

---

## CSS & Accessibility

### 22. Missing `::selection` styling
**Severity: Low** | **Fix: Low**

Text selection defaults to browser blue, which clashes with the dark theme. Should use accent-tinted selection colors.

**Files:** `app.css` (missing rule)

### 23. Missing focus-visible rings on interactive elements
**Severity: Medium** | **Fix: Low**

`.channel-tab`, `.dm-tab`, `.sidebar-item`, and other interactive elements lack `:focus-visible` outline styles. Keyboard users have no visual indicator of focus.

**Files:** `app.css`

### 24. Toast notifications have no exit animation
**Severity: Low** | **Fix: Low**

Toasts slide in from the right (`toast-slide-in`) but vanish instantly when dismissed/expired. A fade-out or slide-out would be smoother.

**Files:** `app.css:3603-3606`

### 25. Toggle password SVG has no explicit size
**Severity: Low** | **Fix: Low**

`.toggle-password` button is 28x28 but no `.toggle-password svg` rule exists. The inner SVG relies on default sizing.

**Files:** `app.css:1684-1700`, `LoginPage.razor:49-58`

### 26. Missing `scroll-behavior: smooth` on messages area
**Severity: Low** | **Fix: Low**

`.messages-area` uses JS-based scrolling but CSS `scroll-behavior: smooth` would improve the feel of manual scrolling and auto-scroll.

**Files:** `app.css:833-839`
