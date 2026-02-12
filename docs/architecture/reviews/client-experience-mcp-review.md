# Client Experience Domain Review: MCP Agent Tools

**Date:** 2026-02-12
**Reviewer:** Client Experience Domain Agent
**Requirements Document:** `docs/requirements/mcp-agent-tools.md`
**Issue:** #106

## Executive Summary

This review evaluates the MCP Agent Tools requirements from the Client Experience domain perspective. The feature introduces agent/bot users that can create accounts and interact via API keys. The client UI must support API key management in the admin panel and visually distinguish agent users from human users in member lists and chat displays.

Overall assessment: **Low-to-moderate complexity**. Existing admin panel patterns provide a clear foundation for API key management. Agent user display requires new badge/icon treatment but fits cleanly into existing member list and message display components.

---

## 1. API Key Management UI

### Requirements

From `docs/requirements/mcp-agent-tools.md` Phase 1:
- Admin endpoints for creating, revoking, and listing API keys
- Each key has: key value, name/label, created date, revoked flag
- One key can control multiple agent accounts

### UI Location

Add a new admin section: **API Keys**

```
src/HotBox.Client/Components/Admin/AdminApiKeyManagement.razor
```

### Existing Admin Panel Patterns

The admin panel already has three sections (`AdminUserManagement.razor`, `AdminChannelManagement.razor`, `AdminInviteManagement.razor`) that provide established patterns:

| Pattern | Example | Applies to API Keys |
|---------|---------|---------------------|
| **Section header with action button** | Users section has "Create User" button | "Generate API Key" button |
| **Table-based display** | All admin sections use `admin-table-wrap` + `admin-table` | List keys in table: Name, Key (masked), Created, Status, Actions |
| **Status badges** | Invites use `badge-active`, `badge-revoked`, etc. | `badge-active` / `badge-revoked` for API keys |
| **Modal-based creation** | User creation modal with form fields | Generate API key modal with "Name/Label" input |
| **Confirmation modal for destructive actions** | User deletion, invite revocation | Revoke API key confirmation |
| **Toast notifications** | Success/error toasts after actions | "API key generated", "API key revoked" |
| **Copy-to-clipboard button** | Invites have copy button for invite codes | Copy API key value (show full key only once at creation) |
| **Inline editing** | User role dropdown in table | No inline editing needed; keys are create-revoke only |

### Recommended Structure

**AdminApiKeyManagement.razor** should follow `AdminInviteManagement.razor` as the closest analog:

```razor
<div class="section-header">
    <div>
        <h2>API Keys</h2>
        <div class="section-header-subtitle">Manage API keys for agent integrations</div>
    </div>
    <button class="btn btn-primary" @onclick="OpenGenerateModal">
        <svg>...</svg> Generate API Key
    </button>
</div>

<div class="admin-table-wrap">
    <table class="admin-table">
        <thead>
            <tr>
                <th>Name</th>
                <th>Key</th>
                <th>Created</th>
                <th>Agents</th>
                <th>Status</th>
                <th>Actions</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var key in _apiKeys)
            {
                <tr>
                    <td>@key.Name</td>
                    <td><code class="api-key-masked">••••••••@key.KeySuffix</code></td>
                    <td><span class="date-cell">@FormatDate(key.CreatedAtUtc)</span></td>
                    <td><span class="text-mono">@key.AgentCount</span></td>
                    <td>
                        @if (key.IsRevoked)
                        {
                            <span class="badge badge-revoked">Revoked</span>
                        }
                        else
                        {
                            <span class="badge badge-active">Active</span>
                        }
                    </td>
                    <td>
                        <div class="table-actions">
                            @if (!key.IsRevoked)
                            {
                                <button class="btn-icon danger" @onclick="() => OpenRevokeConfirm(key)">
                                    <svg>...</svg>
                                </button>
                            }
                        </div>
                    </td>
                </tr>
            }
        </tbody>
    </table>
</div>
```

### Generate API Key Modal

**One-time key display:** After generating a new API key, show the full key value in a modal with a copy button and a warning that the key will not be shown again.

```razor
@if (_showNewKeyModal && _newGeneratedKey is not null)
{
    <div class="modal-overlay open">
        <div class="modal">
            <div class="modal-header">
                <h3>API Key Generated</h3>
            </div>
            <div class="modal-body">
                <p style="color:var(--text-muted);font-size:13px;margin-bottom:12px;">
                    Copy this key now. You won't be able to see it again.
                </p>
                <div style="display:flex;gap:8px;align-items:center;">
                    <code class="api-key-display">@_newGeneratedKey.Key</code>
                    <button class="copy-btn @(_copiedNewKey ? "copied" : "")"
                            @onclick="CopyNewKey">
                        @((_copiedNewKey ? "Copied!" : "Copy"))
                    </button>
                </div>
            </div>
            <div class="modal-footer">
                <button class="btn btn-primary" @onclick="CloseNewKeyModal">Done</button>
            </div>
        </div>
    </div>
}
```

### New CSS Tokens Needed

Add to `src/HotBox.Client/wwwroot/css/app.css`:

```css
/* API key display */
.api-key-masked {
  font-family: var(--font-mono);
  font-size: 12px;
  color: var(--text-muted);
  letter-spacing: 0.02em;
}

.api-key-display {
  font-family: var(--font-mono);
  font-size: 12px;
  color: var(--accent);
  background: var(--accent-muted);
  padding: 8px 12px;
  border-radius: var(--radius-md);
  letter-spacing: 0.02em;
  user-select: all;
  flex: 1;
  overflow-wrap: break-word;
}
```

### State Management

No new global state needed. `AdminApiKeyManagement.razor` can manage local state like other admin components:

- `List<AdminApiKeyResponse> _apiKeys` — loaded via `ApiClient.GetAdminApiKeysAsync()`
- Modal visibility booleans (`_showGenerateModal`, `_showNewKeyModal`, `_showRevokeConfirm`)
- Toast state for success/error messages

### API Client Methods

Add to `src/HotBox.Client/Services/ApiClient.cs`:

```csharp
Task<List<AdminApiKeyResponse>> GetAdminApiKeysAsync();
Task<AdminApiKeyResponse?> GenerateApiKeyAsync(string name);
Task<bool> RevokeApiKeyAsync(string keyId);
```

---

## 2. Agent User Display in Member Lists

### Requirements

- `IsAgent` bool flag on `ApplicationUser` entity
- Agent users should be visually distinct in member lists

### Current Member List Implementation

`src/HotBox.Client/Components/Chat/MembersPanel.razor` displays users grouped by status (Online, Idle, Do Not Disturb, Offline).

Each member item has:
- Avatar (colored circle with initials)
- Status dot (online/idle/dnd/offline)
- Display name
- Optional role tag (Admin, Moderator)

### Proposed Agent User Treatment

**Option 1: Bot Badge (Recommended)**

Add a "BOT" badge next to agent user names, similar to Discord's bot badge.

```razor
<div class="member-item @(user.Status == "Offline" ? "offline" : "")">
    <div class="member-avatar" style="background: @GetAvatarColor(user.DisplayName);">
        @GetInitials(user.DisplayName)
        <span class="status-dot @GetStatusCssClass(user.Status)"></span>
    </div>
    <span class="member-name">@user.DisplayName</span>
    @if (user.IsAgent)
    {
        <span class="member-bot-badge">BOT</span>
    }
    @if (!string.IsNullOrEmpty(user.Role) && user.Role != "Member")
    {
        <span class="member-role-tag">@user.Role</span>
    }
</div>
```

**CSS for bot badge:**

```css
.member-bot-badge {
  font-family: var(--font-mono);
  font-size: 8px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 2px 5px;
  border-radius: var(--radius-xs);
  background: rgba(93, 228, 199, 0.15);
  color: var(--accent);
  margin-left: auto;
  margin-right: 4px;
  white-space: nowrap;
}
```

**Option 2: Bot Icon Avatar**

Replace agent user avatars with a small bot icon instead of initials. Visually cleaner but less flexible if we want to preserve agent identity.

**Recommendation:** Use Option 1 (badge) for consistency with existing role tags. The badge clearly distinguishes agents without disrupting the established avatar pattern.

### Grouping Agents Separately (Future Enhancement)

Consider adding a separate "Bots" section in the member list, grouped independently of status. This is a **Phase 2 enhancement** — for Phase 1, agents should appear in their respective status groups (Online, Offline, etc.) with a BOT badge.

---

## 3. Agent User Display in Chat Messages

### Requirements

- Messages from agent users should be visually distinguishable from human users

### Current Message Display

`src/HotBox.Client/Components/Chat/MessageList.razor` renders message groups with:
- Avatar (colored circle with initials)
- Author name
- Timestamp
- Message body

### Proposed Agent Message Treatment

**Option 1: BOT Badge Next to Author Name (Recommended)**

Add the same BOT badge used in member lists next to the author name in the message header.

```razor
<div class="msg-header">
    <span class="msg-author">@message.AuthorDisplayName</span>
    @if (message.IsAgent)
    {
        <span class="msg-bot-badge">BOT</span>
    }
    <span class="msg-timestamp">@FormatTimestamp(message.CreatedAtUtc)</span>
</div>
```

**CSS for message bot badge:**

```css
.msg-bot-badge {
  font-family: var(--font-mono);
  font-size: 8px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  padding: 2px 5px;
  border-radius: var(--radius-xs);
  background: rgba(93, 228, 199, 0.12);
  color: var(--accent);
  white-space: nowrap;
  margin-left: 4px;
}
```

**Option 2: Bot Icon in Avatar**

Show a small bot icon overlay on agent user avatars in messages. More visually distinct but adds complexity to avatar rendering.

**Option 3: Subtle Background Color for Agent Messages**

Apply a very subtle background color to message groups from agents (e.g., `background: rgba(93, 228, 199, 0.02)`). Risk: may introduce visual noise and inconsistency with hover states.

**Recommendation:** Use Option 1 (badge next to author name). It's consistent with the member list treatment, non-intrusive, and clearly identifies agent messages without disrupting the existing message layout.

---

## 4. Avatar Treatment for Agent Users

### Current Avatar Implementation

Avatars use a hash-based color generation algorithm (`GetAvatarColor`) to create unique colors per user. Initials are derived from display name.

**Avatar logic in `MembersPanel.razor`:**

```csharp
private static string GetAvatarColor(string name)
{
    int hash = 0;
    foreach (char c in name)
    {
        hash = c + ((hash << 5) - hash);
    }
    int hue = Math.Abs(hash) % 360;
    return $"hsl({hue}, 32%, 38%)";
}
```

### Proposed Agent Avatar Treatment

**Option 1: Keep Existing Behavior (Recommended for Phase 1)**

Agent users get the same hash-based colored avatar with initials. The BOT badge provides sufficient visual distinction.

**Option 2: Fixed Bot Icon Avatar (Future Enhancement)**

Replace agent user avatars with a fixed bot icon (e.g., robot face SVG). This is a **Phase 2 enhancement** — for Phase 1, preserving the existing avatar behavior keeps implementation simple and consistent.

**Recommendation:** Keep existing avatar logic. The BOT badge is sufficient for Phase 1. If we later want bot-specific avatars, we can add a fallback in the avatar component:

```razor
@if (user.IsAgent)
{
    <div class="member-avatar agent-avatar">
        <svg>...</svg> <!-- bot icon -->
        <span class="status-dot @GetStatusCssClass(user.Status)"></span>
    </div>
}
else
{
    <div class="member-avatar" style="background: @GetAvatarColor(user.DisplayName);">
        @GetInitials(user.DisplayName)
        <span class="status-dot @GetStatusCssClass(user.Status)"></span>
    </div>
}
```

---

## 5. Design Token Additions

### New Tokens Needed

Add to `src/HotBox.Client/wwwroot/css/app.css` under the `:root` section:

```css
/* Bot/agent styling */
--bot-badge-bg: rgba(93, 228, 199, 0.12);
--bot-badge-color: var(--accent);
```

### Token Usage

| Token | Usage |
|-------|-------|
| `--bot-badge-bg` | Background for `.member-bot-badge`, `.msg-bot-badge` |
| `--bot-badge-color` | Text color for bot badges |

---

## 6. State Management Needs

### No New Global State Required

The `IsAgent` flag is part of the `UserPresenceInfo` DTO (for member lists) and `MessageDto` (for chat messages). No new state services needed.

### DTO Updates

Ensure the following DTOs include the `IsAgent` property:

- `src/HotBox.Client/Models/UserPresenceInfo.cs` (or equivalent)
- `src/HotBox.Client/Models/MessageDto.cs`
- `src/HotBox.Client/Models/AdminUserResponse.cs` (for admin user management table)

Example:

```csharp
public class UserPresenceInfo
{
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = "Offline";
    public bool IsAgent { get; set; } // NEW
}
```

---

## 7. Accessibility Considerations

### Screen Reader Support

**Member list:**

```razor
<div class="member-item" aria-label="@GetMemberAriaLabel(user)">
    ...
</div>

@code {
    private static string GetMemberAriaLabel(UserPresenceInfo user)
    {
        var label = $"{user.DisplayName}, {GetStatusLabel(user.Status)}";
        if (user.IsAgent) label += ", Bot";
        return label;
    }
}
```

**Message display:**

The existing `msg-author` and `msg-timestamp` structure should be supplemented with screen-reader-only text for agent messages:

```razor
<span class="msg-author">
    @message.AuthorDisplayName
    @if (message.IsAgent)
    {
        <span class="visually-hidden">Bot</span>
        <span class="msg-bot-badge" aria-hidden="true">BOT</span>
    }
</span>
```

Add to `app.css`:

```css
.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  margin: -1px;
  padding: 0;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
```

---

## 8. Concerns and Risks

### 8.1 UX Confusion: Bot vs Human Interaction

**Risk:** Users may not understand what "BOT" means or why agent users behave differently.

**Mitigation:**
- Add a help tooltip or info icon next to the "API Keys" admin section explaining what agents are
- Consider a "Bot" badge tooltip in member lists: "Automated user controlled via API"

### 8.2 BOT Badge Visual Clutter

**Risk:** Too many badges (BOT + role tag) may clutter the member list.

**Mitigation:**
- Bot badge uses smaller font size (8px) and compact padding
- Bot badge appears before role tag, creating a clear visual hierarchy
- Admin/Moderator role tags are already rare (only a few users per server), so overlap is minimal

### 8.3 API Key Security Display

**Risk:** Displaying full API keys in the admin table is a security risk.

**Mitigation:**
- Mask API keys in the table: `••••••••abcd1234` (last 8 chars visible)
- Show full key only once in the post-generation modal
- Include a warning in the modal: "Copy this key now. You won't be able to see it again."

### 8.4 Agent User Count Per Key

**Risk:** The admin table needs to show how many agents are associated with each API key. This requires a new backend endpoint or DTO enrichment.

**Coordination Point:** Confirm with **Auth-Security** or **Platform** that the admin API key list endpoint returns `AgentCount` per key.

### 8.5 Revoking API Keys with Active Agents

**Risk:** Revoking an API key may orphan agent accounts or require cascade logic.

**Coordination Point:** Confirm with **Auth-Security** what happens to agents when their parent API key is revoked:
- Do agents remain in the system but lose API access?
- Should the admin UI warn before revoking a key with active agents?

**Recommendation:** Show agent count in the revocation confirmation modal:

```
Are you sure you want to revoke API key "Test Bot Key"?
This key has 3 active agent accounts. They will no longer be able to authenticate.
```

---

## 9. Implementation Recommendations

### Phase 1 Implementation Order

1. **API Key Management Admin Component**
   - Create `AdminApiKeyManagement.razor` (follow `AdminInviteManagement.razor` pattern)
   - Add to admin sidebar navigation
   - API Client methods for create/list/revoke

2. **DTO Updates**
   - Add `IsAgent` to `UserPresenceInfo`, `MessageDto`, `AdminUserResponse`

3. **Bot Badge in Member Lists**
   - Update `MembersPanel.razor` to show BOT badge
   - Add `.member-bot-badge` CSS
   - Update `aria-label` for screen readers

4. **Bot Badge in Chat Messages**
   - Update `MessageList.razor` to show BOT badge next to author name
   - Add `.msg-bot-badge` CSS
   - Add screen-reader-only "Bot" label

5. **Design Tokens**
   - Add `--bot-badge-bg` and `--bot-badge-color` to `app.css`

6. **Testing**
   - Verify BOT badge appears in member list and chat for agent users
   - Test API key generation, masking, copy-to-clipboard, and revocation
   - Screen reader testing for aria labels

### Phase 2 Enhancements (Future)

- Separate "Bots" section in member list
- Bot-specific avatar icons (robot face SVG)
- Agent activity logs in admin panel
- Bulk API key management (revoke multiple, export list)

---

## 10. Coordination Points

### Platform Domain

- Confirm `AdminApiKeyResponse` DTO structure includes:
  - `Id`, `Name`, `KeyValue`, `KeySuffix` (last 8 chars), `CreatedAtUtc`, `IsRevoked`, `AgentCount`

### Auth-Security Domain

- Confirm API key revocation behavior: do agents remain in system?
- Confirm whether API key creation endpoint returns the full key value (needed for one-time display)

### Messaging Domain

- Confirm `MessageDto` includes `IsAgent` bool flag
- Confirm ChatHub broadcasts include `IsAgent` for real-time messages

---

## 11. Open Questions

1. **Should agent users appear in online member counts?**
   - Recommendation: Yes, treat them like normal users. Filtering bots from counts adds complexity.

2. **Should agents have a different status dot color?**
   - Recommendation: No. Use the same status colors (online/idle/offline). The BOT badge is sufficient.

3. **Should the admin panel show which users were created by each API key?**
   - Recommendation: **Phase 2 enhancement**. For Phase 1, showing agent count per key is sufficient.

4. **Should API keys have expiration dates?**
   - Recommendation: **Phase 2 enhancement**. For Phase 1, manual revocation is sufficient.

---

## 12. Summary and Next Steps

### Client Experience Deliverables

- [ ] Create `AdminApiKeyManagement.razor` component
- [ ] Add API key admin section to sidebar navigation
- [ ] Add `IsAgent` to `UserPresenceInfo`, `MessageDto`, `AdminUserResponse` DTOs
- [ ] Update `MembersPanel.razor` to show BOT badge
- [ ] Update `MessageList.razor` to show BOT badge next to author name
- [ ] Add `.member-bot-badge`, `.msg-bot-badge`, `.api-key-masked`, `.api-key-display` CSS
- [ ] Add `--bot-badge-bg`, `--bot-badge-color` design tokens
- [ ] Add screen-reader support for bot labels
- [ ] Test API key copy-to-clipboard, masking, and revocation flow

### Cross-Domain Dependencies

- **Platform**: `AdminApiKeyResponse` DTO structure
- **Auth-Security**: API key revocation behavior, agent account lifecycle
- **Messaging**: `MessageDto` includes `IsAgent` flag

### Overall Assessment

**Complexity:** Low-to-moderate
**Risk:** Low
**Recommendation:** Proceed with Phase 1 implementation. Existing admin patterns provide a solid foundation. BOT badge treatment is simple, non-intrusive, and consistent with established UI conventions.

