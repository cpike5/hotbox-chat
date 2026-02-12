# Messaging Domain Review: MCP Agent Tools

**Domain**: Messaging
**Reviewer**: Messaging Agent
**Issue**: #105
**Date**: 2026-02-12
**Status**: Review Complete

---

## Executive Summary

The MCP Agent Tools requirements are compatible with the existing Messaging infrastructure. Agents can use current REST endpoints and SignalR hub methods with minimal changes. The primary recommendation is a **REST-first approach** for agent messaging — the MCP server should call existing REST APIs for both sending and reading messages, avoiding SignalR hub complexity.

Key findings:
- Existing REST endpoints (`MessagesController`, `DirectMessagesController`) fully support agent message sending and reading
- The `IsAgent` flag on `AppUser` (to be added by Auth & Security) is sufficient for attribution
- No messaging-specific infrastructure changes needed beyond standard API key authentication (owned by Platform/Auth)
- Rate limiting should be handled by middleware, not messaging service layer
- One architectural recommendation: Add a POST endpoint to `MessagesController` for cleaner RESTful design

---

## Review Areas

### 1. Agent Message Sending

**Current State:**

The MCP requirements specify two tools for sending messages:
- `send_message` — Post a message to a text channel as a specified agent
- `send_direct_message` — Send a DM to another user as a specified agent

**Existing Infrastructure:**

REST endpoints already exist:
- `POST /api/dm/{userId}/messages` — Send DM (exists at `src/HotBox.Application/Controllers/DirectMessagesController.cs:90-136`)
- SignalR hub method: `ChatHub.SendMessage(channelId, content)` and `ChatHub.SendDirectMessage(recipientId, content)` (exists at `src/HotBox.Application/Hubs/ChatHub.cs:121-155` and `174-201`)

However, there is **no REST endpoint** for posting messages to text channels. Channel messages currently only support SignalR-based sending via `ChatHub.SendMessage()`.

**Recommendation:**

Add a new REST endpoint to `MessagesController`:

```csharp
[HttpPost("channels/{channelId:guid}/messages")]
public async Task<IActionResult> SendMessage(
    Guid channelId,
    [FromBody] SendMessageRequest request,
    CancellationToken ct)
{
    var userId = GetUserId();
    if (userId is null)
    {
        return Unauthorized();
    }

    try
    {
        var message = await _messageService.SendAsync(channelId, userId.Value, request.Content, ct);

        var response = new MessageResponse
        {
            Id = message.Id,
            Content = message.Content,
            ChannelId = message.ChannelId,
            AuthorId = message.AuthorId,
            AuthorDisplayName = message.Author?.DisplayName ?? "Unknown",
            CreatedAtUtc = message.CreatedAtUtc,
        };

        return CreatedAtAction(nameof(GetById), new { id = message.Id }, response);
    }
    catch (KeyNotFoundException ex)
    {
        return NotFound(new { error = ex.Message });
    }
    catch (ArgumentException ex)
    {
        return BadRequest(new { error = ex.Message });
    }
}
```

This mirrors the pattern already used in `DirectMessagesController` and follows RESTful conventions.

**MCP Integration Approach:**

The MCP server should:
1. Authenticate using API key (via middleware added by Platform/Auth)
2. Call REST endpoints directly:
   - `POST /api/channels/{channelId}/messages` for channel messages
   - `POST /api/dm/{userId}/messages` for DMs
3. Parse the authenticated user ID from the API key's associated agent account
4. The existing service layer (`MessageService.SendAsync()`, `DirectMessageService.SendAsync()`) requires no changes

**Real-Time Broadcast:**

When agents post messages via REST, real-time delivery to connected clients should still occur. Currently, `ChatHub.SendMessage()` broadcasts to SignalR groups after persisting. We need to ensure REST-based message posting also triggers SignalR broadcast.

**Proposed solution:**
- Add an `IHubContext<ChatHub>` dependency to `MessagesController` (or `MessageService`)
- After persisting the message via `MessageService.SendAsync()`, broadcast to the channel's SignalR group:

```csharp
await _hubContext.Clients.Group(channelId.ToString())
    .SendAsync("ReceiveMessage", response);
```

This pattern ensures parity between SignalR-based and REST-based message posting.

---

### 2. Agent Message Reading

**Current State:**

The MCP requirements specify two tools for reading messages:
- `read_messages` — Retrieve recent messages from a text channel
- `read_direct_messages` — Retrieve recent DMs from a conversation

**Existing Infrastructure:**

REST endpoints already exist with full pagination support:
- `GET /api/channels/{channelId}/messages?before={timestamp}&limit={count}` — Get channel message history (exists at `src/HotBox.Application/Controllers/MessagesController.cs:24-50`)
- `GET /api/dm/{userId}?before={timestamp}&limit={count}` — Get DM conversation history (exists at `src/HotBox.Application/Controllers/DirectMessagesController.cs:50-88`)

**Pagination Details:**

Both endpoints use cursor-based pagination with `CreatedAtUtc`:
- Default page size: 50 messages
- Maximum page size: 100 messages (enforced via validation)
- Client sends `?before={timestamp}` to load older messages
- Messages returned in reverse chronological order (newest first)

**Assessment:**

The existing pagination API is **fully compatible** with the MCP pull-based reading approach. No changes needed.

**MCP Integration Approach:**

The MCP server should:
1. Call REST endpoints directly (same authentication as sending)
2. Use the `limit` parameter to control batch size (e.g., `limit=20` for recent messages)
3. Parse the response JSON into message DTOs
4. For the simulation loop (Phase 3), fetch recent messages to provide context for LLM-driven replies

---

### 3. Direct Message Support

**Current State:**

The MCP requirements specify that agents can send and read DMs just like channel messages.

**Existing Infrastructure:**

DM endpoints already exist and are production-ready:
- `POST /api/dm/{userId}/messages` — Send DM (exists at `src/HotBox.Application/Controllers/DirectMessagesController.cs:90-136`)
- `GET /api/dm/{userId}?before={timestamp}&limit={count}` — Get DM history (exists at `src/HotBox.Application/Controllers/DirectMessagesController.cs:50-88`)
- `GET /api/dm` — List all DM conversations (exists at `src/HotBox.Application/Controllers/DirectMessagesController.cs:25-48`)

**Validation:**

The `DirectMessageService` already enforces key business rules:
- Cannot send DM to yourself (`SendAsync:34-35`)
- Validates sender and recipient exist (`SendAsync:37-41`)
- Empty content validation (`SendAsync:31-32`)

**Assessment:**

DM support is **production-ready** with no changes needed. Agents can use the existing endpoints as-is.

---

### 4. ChatHub Implications

**Current State:**

The MCP requirements state that the MCP server will use a "pull-based approach" for reading messages (Phase 2), with real-time subscriptions explicitly out of scope.

**Existing ChatHub:**

The `ChatHub` (at `src/HotBox.Application/Hubs/ChatHub.cs`) provides:
- Real-time message delivery (`ReceiveMessage`, `ReceiveDirectMessage`)
- Typing indicators (`UserTyping`, `UserStoppedTyping`)
- Channel subscriptions (`JoinChannel`, `LeaveChannel`)
- Presence management (user online/offline status)

**Assessment:**

The MCP server does **not need to connect to ChatHub** for the pull-based reading approach. REST endpoints are sufficient for both sending and reading.

**However**, to maintain feature parity with human users:
- When agents post messages via REST, those messages should still be broadcast to connected SignalR clients (see "Real-Time Broadcast" section above)
- Agents do not need to join channels via SignalR or receive typing indicators
- Agents do not need presence status (they are headless bots, not interactive users)

**Recommendation:**

- MCP server uses REST only — no SignalR connection required
- Add `IHubContext<ChatHub>` to `MessagesController` (or `MessageService`) to broadcast REST-posted messages to SignalR groups
- Agents are exempt from presence tracking (handled by Auth/Presence domain, not Messaging)

---

### 5. Message Attribution

**Current State:**

The MCP requirements specify adding an `IsAgent` flag to `ApplicationUser` to distinguish agent users from human users.

**Existing Message Entities:**

Both `Message` and `DirectMessage` entities (at `src/HotBox.Core/Entities/Message.cs` and `DirectMessage.cs`) reference `AppUser` via navigation properties:
- `Message.Author` (navigation to `AppUser`)
- `DirectMessage.Sender` and `DirectMessage.Recipient` (both navigate to `AppUser`)

**Assessment:**

The `IsAgent` flag on `AppUser` is **sufficient** for attribution. No changes needed to message entities or service layer.

**UI Implications:**

The Client Experience domain will handle displaying agent messages differently (e.g., with a "Bot" badge). The Messaging domain only needs to return the `AppUser` data with the `IsAgent` flag populated.

**Response DTOs:**

Current `MessageResponse` and `DirectMessageResponse` DTOs (at `src/HotBox.Application/Models/`) do not include the `IsAgent` flag. This will need to be added by Client Experience when they render agent messages.

**Recommendation:**

- Messaging domain: No changes needed to entities, services, or repositories
- Client Experience domain: Add `IsAgent` field to response DTOs if needed for rendering
- Auth & Security domain: Ensure `IsAgent` flag is included in user data returned by `UserManager`

---

### 6. Rate Limiting

**Current State:**

The MCP requirements do not specify rate limiting behavior, but the "Open Questions" section does not list it as a concern. However, agent message flooding is a realistic risk.

**Existing Infrastructure:**

HotBox currently has **no rate limiting** implemented at the messaging service layer or API level.

**Recommendation:**

Rate limiting should be handled by **middleware**, not the Messaging domain. This keeps messaging services clean and allows consistent rate limiting across all API endpoints.

**Proposed Approach (for Platform/Auth domain):**

Add rate limiting middleware (e.g., `AspNetCoreRateLimit` package) with different policies:
- Human users: 60 messages/minute per channel
- Agent users: 10 messages/minute per channel (configurable)
- DMs: 30 messages/minute per conversation (shared limit)

**Messaging Domain Stance:**

The Messaging domain does **not own rate limiting**. If Platform/Auth adds rate limiting middleware, it will apply to existing endpoints with no Messaging-specific changes.

---

### 7. Infrastructure Changes

**Summary of Changes Needed:**

| Component | Change | Owner |
|-----------|--------|-------|
| `MessagesController.cs` | Add `POST /api/channels/{channelId}/messages` endpoint | Messaging |
| `MessagesController.cs` or `MessageService.cs` | Add `IHubContext<ChatHub>` to broadcast REST-posted messages | Messaging |
| `SendMessageRequest.cs` | Add DTO for channel message posting | Messaging |
| Rate limiting middleware | Add global rate limiting with agent-specific policies | Platform |
| API key authentication | Add middleware and `[Authorize]` policy for API keys | Auth & Security |
| `AppUser.cs` | Add `IsAgent` bool flag | Auth & Security / Platform |

**No Changes Needed:**
- `MessageService.SendAsync()` — already supports agent message creation
- `DirectMessageService.SendAsync()` — already supports agent DMs
- Message and DirectMessage repositories — no changes
- Pagination logic — already compatible with pull-based reading
- Search endpoints — agents can search messages using existing endpoints

---

### 8. Concerns and Risks

**1. Message Flooding**

**Risk:** Agents could spam channels with high-frequency messages if not rate-limited.

**Mitigation:**
- Add rate limiting middleware (Platform responsibility)
- MCP server should implement client-side throttling (e.g., random delays in Phase 3 simulation)

**2. Agent Account Cleanup**

**Risk:** Test agents created during simulation could clutter the user list.

**Mitigation:**
- Admin endpoints should support bulk user deletion (Auth domain responsibility)
- `IsAgent` flag allows filtering agents from user lists (Client Experience responsibility)

**3. Real-Time Delivery Gap**

**Risk:** If REST-posted messages do not trigger SignalR broadcast, connected clients won't receive agent messages in real-time.

**Mitigation:**
- Add `IHubContext<ChatHub>` broadcast to `MessagesController` (or `MessageService`)
- Test both REST and SignalR posting paths to ensure parity

**4. Notification Spam**

**Risk:** Agent messages could trigger @mention notifications, spamming human users.

**Mitigation:**
- The `NotificationService` already processes @mentions (called from `ChatHub.SendMessage:146-154`)
- If agents post via REST, notification processing must still occur
- Consider: Should agent messages trigger notifications? (Auth/Messaging coordination needed)

**Recommendation:** Add a check to `NotificationService` to skip notifications for messages authored by agents (`if (author.IsAgent) return;`)

---

### 9. Recommendations

**High Priority:**

1. **Add REST endpoint for channel message posting** (`POST /api/channels/{channelId}/messages`) — required for MCP integration
2. **Add `IHubContext<ChatHub>` broadcast** to `MessagesController` or `MessageService` — ensures real-time delivery parity
3. **Coordinate with Auth & Security** on `IsAgent` flag and notification handling

**Medium Priority:**

4. **Document agent message behavior** in `docs/technical-spec.md` Section 2.3 (API: Messages)
5. **Add integration tests** for REST-based message posting with SignalR broadcast verification

**Low Priority:**

6. **Consider adding `IsAgent` to `MessageResponse` DTO** if Client Experience needs it for rendering
7. **Monitor agent message performance** in Phase 3 simulation to validate pagination efficiency

---

## Conclusion

The Messaging domain is **ready for MCP agent integration** with one required change: adding a REST endpoint for channel message posting. The existing infrastructure (services, repositories, pagination, DM support) requires no modifications.

The recommended approach is **REST-first**: the MCP server should call REST APIs for both sending and reading messages, avoiding SignalR complexity. Real-time delivery to human users is maintained by broadcasting REST-posted messages via `IHubContext<ChatHub>`.

Key coordination points:
- **Platform**: Rate limiting middleware
- **Auth & Security**: API key authentication, `IsAgent` flag, agent account management
- **Client Experience**: Rendering agent messages with visual distinction

No blockers identified. Implementation can proceed to Phase 1 (API Key Infrastructure) with confidence that messaging will support agent access.
