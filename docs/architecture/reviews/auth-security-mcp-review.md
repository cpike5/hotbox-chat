# Auth & Security Domain Review: MCP Agent Tools

**Reviewer**: Auth & Security Agent
**Date**: 2026-02-12
**Requirements Document**: `docs/requirements/mcp-agent-tools.md`
**Status**: Phase 0 — Domain Review

## Executive Summary

This document reviews the MCP Agent Tools requirements from the Auth & Security domain perspective. The feature introduces API key-based authentication and agent user accounts that need to coexist with the existing JWT-based auth system.

**Overall Assessment**: The requirements are feasible but need careful design around authentication scheme coexistence, API key security, and authorization boundaries. Several critical security decisions must be made before implementation begins.

## Scope of Auth & Security Involvement

From the requirements, the Auth & Security domain is responsible for:

1. **API Key Authentication Scheme**: Design and implement header-based API key authentication
2. **`IsAgent` Flag on ApplicationUser**: Add and integrate a boolean flag to distinguish agent users from regular users
3. **Agent Account Creation Flow**: Define how agent accounts are provisioned (no password, auto-generated credentials, role assignment)
4. **Security Implications**: Ensure API key storage, rate limiting, and authorization boundaries are secure
5. **Authorization Rules**: Determine what permissions agent users have and how `[Authorize]` attributes apply

## Review by Area

### 1. API Key Authentication Scheme

#### Current State

HotBox currently uses a single authentication scheme:
- **JWT Bearer** (default): Access token (15 min) + refresh token (7 days, HttpOnly cookie)
- Configured in `src/HotBox.Application/DependencyInjection/ApplicationServiceExtensions.cs` lines 38-56
- All `[Authorize]` attributes on controllers and SignalR hubs use this scheme

#### Requirements Analysis

The MCP feature requires:
- Header-based API key authentication (e.g., `X-Api-Key: <key>` or `Authorization: ApiKey <key>`)
- API keys authenticate MCP server requests to create and manage agent accounts
- API keys should **not** replace JWT for regular user authentication

#### Design Approach: Multiple Authentication Schemes

ASP.NET Core supports multiple authentication schemes simultaneously. We need to:

1. **Add a second authentication scheme** (`ApiKeyAuthenticationHandler`)
2. **Configure default policy** to accept **either** JWT Bearer **or** API Key
3. **Keep `[Authorize]` unchanged** on existing controllers/hubs (they'll work with both)

**Implementation Strategy:**

```csharp
// In ApplicationServiceExtensions.cs
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "JwtOrApiKey"; // Custom policy name
    options.DefaultChallengeScheme = "JwtOrApiKey";
})
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options => { /* existing config */ })
.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", options => { })
.AddPolicyScheme("JwtOrApiKey", "JWT or API Key", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // Check if X-Api-Key or Authorization: ApiKey header is present
        if (context.Request.Headers.ContainsKey("X-Api-Key") ||
            context.Request.Headers["Authorization"].FirstOrDefault()?.StartsWith("ApiKey ") == true)
        {
            return "ApiKey";
        }
        return JwtBearerDefaults.AuthenticationScheme;
    };
});
```

**ApiKeyAuthenticationHandler** responsibilities:
- Extract key from `X-Api-Key` header or `Authorization: ApiKey <key>` header
- Look up `ApiKey` entity in database
- Validate: not revoked, optionally check expiration
- Create `ClaimsPrincipal` with a special claim indicating API key authentication
- Set `context.Principal` and mark authentication as successful

**Claims Structure for API Key Auth:**
```csharp
new Claim("auth_method", "api_key"),
new Claim("api_key_id", apiKey.Id.ToString()),
// No user ID claim — API keys are not tied to a specific user
```

**Key Decision**: API keys authenticate the **MCP server**, not a specific user. When the MCP server creates an agent account, that account is **linked** to the API key but the API key itself does not impersonate a user.

#### Concerns

1. **Authorization on existing endpoints**: Endpoints that check `User.Identity.IsAuthenticated` will pass for API key auth, but endpoints that rely on `ClaimTypes.NameIdentifier` (user ID) will fail unless we explicitly handle API key auth separately.
2. **SignalR Hub Authorization**: Hubs like `ChatHub` (line 14: `[Authorize]`) call `GetUserId()` which extracts `ClaimTypes.NameIdentifier`. API key-authenticated requests cannot call hub methods directly — hubs are for user connections only. **This is correct behavior** since agents will connect to SignalR as **user accounts** (with JWT), not as API key-authenticated clients.

#### Recommendation

- Implement API key authentication as a **second authentication scheme** using `AddPolicyScheme` to support both JWT and API Key
- API key auth is for **admin/agent management endpoints only**, not for regular user endpoints or SignalR hubs
- Create a new controller `ApiKeyController` or extend `AdminController` with endpoints for agent account creation and management
- Explicitly restrict API key usage to specific endpoints using `[Authorize(AuthenticationSchemes = "ApiKey")]` if needed, or check `User.FindFirst("auth_method")?.Value == "api_key"` in controller logic

---

### 2. `IsAgent` Flag on ApplicationUser Entity

#### Current State

The `AppUser` entity is defined in `src/HotBox.Core/Entities/AppUser.cs`:
```csharp
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public UserStatus Status { get; set; }
    public DateTime LastSeenUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ICollection<Message> Messages { get; set; } = [];
    public ICollection<DirectMessage> SentDirectMessages { get; set; } = [];
    public ICollection<DirectMessage> ReceivedDirectMessages { get; set; } = [];
}
```

Configuration is in `src/HotBox.Infrastructure/Data/Configurations/AppUserConfiguration.cs`.

#### Requirements Analysis

Add a boolean `IsAgent` property to distinguish agent users from regular users. This affects:
- User queries (e.g., admin user list should show agent users differently)
- UI display (agent users might have a badge or icon)
- Authorization (future: restrict certain features to non-agent users)

#### Design Approach

**Entity Change:**
```csharp
public bool IsAgent { get; set; } = false; // Default to false for regular users
```

**EF Core Configuration:**
```csharp
builder.Property(u => u.IsAgent)
    .IsRequired()
    .HasDefaultValue(false);
```

**Migration**: Platform agent will handle the EF Core migration. No concerns from Auth perspective.

#### Implications

1. **Existing User Queries**: Most queries in `AdminController` (line 91: `_userManager.Users.OrderBy(...)`) will automatically include `IsAgent`. Admin UI should filter or display agent users differently.
2. **Role Assignment**: Agent users should still have a role (default: `Member`). The `IsAgent` flag is **orthogonal** to roles — an agent can be a Member, Moderator, or Admin (though Admin agents are likely a bad idea).
3. **Authentication Flow**: When an agent user logs in via JWT (after account creation), the `IsAgent` flag does not affect authentication. It's a **data attribute**, not an auth attribute.

#### Concerns

1. **Agent User Deletion**: Should agent users be deletable? Or only revokable by revoking the API key? **Recommendation**: Admins can delete agent users via the existing `DELETE /api/admin/users/{id}` endpoint. Revoking an API key does **not** delete agent accounts created by that key — it only prevents the key from creating **new** agents.
2. **Agent User Login**: Should agent users be able to log in with email/password? **Recommendation**: No. Agent accounts should **not have passwords**. ASP.NET Identity allows password-less accounts (do not call `CreateAsync(user, password)`). If an agent needs to authenticate, the MCP server uses the API key to generate a JWT for that agent user via a dedicated endpoint.

#### Recommendation

- Add `IsAgent` bool to `AppUser`, default `false`
- Agent accounts are created **without passwords** (skip password in `UserManager.CreateAsync`)
- Admins can view, filter, and delete agent users via existing admin endpoints
- Update `AdminUserResponse` to include `IsAgent` so the UI can display agent users differently

---

### 3. Agent Account Creation Flow

#### Current State

User accounts are created via:
1. **Registration** (`POST /api/auth/register`) — requires password, checks registration mode
2. **OAuth** (`GET /api/auth/external/callback`) — password-less, auto-created on first login
3. **Admin Creation** (`POST /api/admin/users`) — requires password, admin-only

All three flows:
- Assign default role (`Member`)
- Set `Status = UserStatus.Offline`
- Set `CreatedAtUtc` and `LastSeenUtc`

#### Requirements Analysis

Agent accounts are created via:
- MCP server calls a new endpoint: `POST /api/agents/create` (or `POST /api/admin/agents`)
- Authenticated via API key (not JWT)
- No password required
- Agent is linked to the API key that created it

#### Design Approach

**New Endpoint:**
```csharp
// In AdminController or new ApiKeyController
[HttpPost("agents")]
[Authorize] // Accepts both JWT (admin) and API Key
public async Task<IActionResult> CreateAgentAccount([FromBody] CreateAgentRequest request)
{
    // If authenticated via API key, extract API key ID from claims
    var authMethod = User.FindFirst("auth_method")?.Value;
    Guid? apiKeyId = null;

    if (authMethod == "api_key")
    {
        apiKeyId = Guid.Parse(User.FindFirst("api_key_id")!.Value);
    }
    else
    {
        // JWT auth — must be Admin role
        if (!User.IsInRole("Admin"))
        {
            return Forbid();
        }
    }

    // Check if email/username already exists
    var existingUser = await _userManager.FindByEmailAsync(request.Email);
    if (existingUser != null)
    {
        return BadRequest(new { error = "An account with this email already exists." });
    }

    var agentUser = new AppUser
    {
        UserName = request.Email,
        Email = request.Email,
        DisplayName = request.DisplayName,
        IsAgent = true, // Mark as agent
        Status = UserStatus.Offline,
        CreatedAtUtc = DateTime.UtcNow,
        LastSeenUtc = DateTime.UtcNow,
    };

    // Create WITHOUT password
    var result = await _userManager.CreateAsync(agentUser);
    if (!result.Succeeded)
    {
        return BadRequest(new { error = string.Join("; ", result.Errors.Select(e => e.Description)) });
    }

    // Assign default role
    await _userManager.AddToRoleAsync(agentUser, "Member");

    // If created via API key, store the relationship in AgentAccount table (Platform responsibility)
    if (apiKeyId.HasValue)
    {
        // Store ApiKeyId -> AgentUserId mapping
        // This is Platform's concern (new table: AgentAccounts with ApiKeyId, UserId, CreatedAtUtc)
    }

    // Return JWT token so MCP server can authenticate as this agent
    var accessToken = await _tokenService.GenerateAccessTokenAsync(agentUser);

    return Ok(new
    {
        userId = agentUser.Id,
        displayName = agentUser.DisplayName,
        email = agentUser.Email,
        accessToken, // Short-lived JWT for immediate use
    });
}
```

**Request Model:**
```csharp
public class CreateAgentRequest
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
```

#### Agent Authentication After Creation

Once an agent account exists, the MCP server needs to authenticate as that agent to send messages via SignalR. Two approaches:

**Option A: Return JWT on Creation**
- Agent creation endpoint returns a JWT access token immediately
- MCP server uses this token to connect to SignalR
- Token expires after 15 minutes — MCP server must re-authenticate

**Option B: Dedicated Agent Login Endpoint**
```csharp
[HttpPost("agents/login")]
[Authorize(AuthenticationSchemes = "ApiKey")]
public async Task<IActionResult> AgentLogin([FromBody] AgentLoginRequest request)
{
    // request.UserId or request.Email identifies the agent
    var user = await _userManager.FindByIdAsync(request.UserId.ToString());
    if (user == null || !user.IsAgent)
    {
        return NotFound(new { error = "Agent account not found." });
    }

    // Verify this API key created this agent (Platform responsibility to check mapping)

    var accessToken = await _tokenService.GenerateAccessTokenAsync(user);
    return Ok(new { accessToken });
}
```

#### Recommendation

- **Option A** (return JWT on creation) is simpler for the MCP use case
- **Option B** (dedicated login endpoint) is more flexible but adds an extra API call
- **Recommended**: Use Option A for Phase 2, add Option B in Phase 3 if needed for long-running agents

---

### 4. Security Implications

#### API Key Storage

**Question**: Should API keys be stored hashed or in plaintext?

**Analysis**:
- **Hashed** (like passwords): More secure. If the database is compromised, keys cannot be recovered. However, the key must be displayed to the admin **once** when created (same as GitHub personal access tokens).
- **Plaintext**: Simpler. Admin can retrieve keys later if lost. Higher risk if database is compromised.

**Recommendation**: **Hash API keys** using the same approach as refresh tokens:
- Generate a secure random 64-byte key (Base64-encoded, like `RefreshToken`)
- Store SHA256 hash in the database
- Display the plaintext key to the admin **once** on creation
- On subsequent API key authentication, hash the incoming key and compare to stored hash

**Entity Design** (Platform responsibility):
```csharp
public class ApiKey
{
    public Guid Id { get; init; }
    public string KeyHash { get; set; } = string.Empty; // SHA256 hash
    public string Label { get; set; } = string.Empty; // e.g., "MCP Testing"
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; } // Optional expiration
    public bool IsRevoked => RevokedAtUtc.HasValue;
    public bool IsExpired => ExpiresAtUtc.HasValue && DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsActive => !IsRevoked && !IsExpired;
}
```

#### API Key Rotation

**Requirement**: Admins should be able to revoke API keys.

**Current Flow**:
- Refresh tokens are revoked via `RevokeRefreshTokenAsync` (`src/HotBox.Infrastructure/Services/TokenService.cs` line 165)
- Similar approach for API keys: set `RevokedAtUtc` to current time

**New Requirement**: Revoking an API key should **not** revoke JWTs issued to agent accounts created by that key. Agent JWTs have their own expiration (15 min). Once an agent is created, it exists independently of the API key.

**Recommendation**: API key revocation only prevents **new** agent account creation. Existing agents remain functional.

#### Rate Limiting

**Requirement**: API key-authenticated endpoints should be rate-limited to prevent abuse.

**Current State**: HotBox does not have rate limiting implemented yet (not mentioned in any existing code).

**Recommendation**:
- Add rate limiting middleware in Phase 1 (Platform responsibility)
- Apply stricter limits to API key-authenticated endpoints (e.g., 10 agent creations per hour per key)
- Use `AspNetCoreRateLimit` package or built-in .NET 8 rate limiting middleware

#### Authorization Boundaries

**Question**: What can API keys do?

**Answer** (from requirements):
- Create agent accounts
- List agent accounts created by that key
- **Cannot**: Modify server settings, manage regular users, delete channels, assign roles to existing users

**Recommendation**:
- API key auth grants access to a **subset** of admin endpoints (agent management only)
- Regular admin endpoints require JWT auth with `Admin` role
- Use a custom authorization policy:
```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("ApiKeyOrAdmin", policy =>
        policy.RequireAssertion(context =>
            context.User.FindFirst("auth_method")?.Value == "api_key" ||
            context.User.IsInRole("Admin")));
});

// On agent endpoints
[Authorize(Policy = "ApiKeyOrAdmin")]
public async Task<IActionResult> CreateAgentAccount(...) { }
```

---

### 5. Authorization Rules for Agent Users

#### Question

Should agent users (once authenticated via JWT) have the same permissions as regular `Member` users?

#### Analysis

Agent users need to:
- Send messages to text channels (via `ChatHub.SendMessage`)
- Send direct messages (via `ChatHub.SendDirectMessage`)
- Read messages (via `GET /api/channels/{id}/messages`)
- Join/leave channels (via `ChatHub.JoinChannel`)

Agent users should **not**:
- Create channels (moderator-only)
- Kick users (moderator-only)
- Change server settings (admin-only)
- Generate invite codes (admin-only)

**Current Authorization** (from `ChatHub.cs` and `AdminController.cs`):
- `ChatHub` requires `[Authorize]` but no role check — **all authenticated users** can send messages
- Admin endpoints require `[Authorize(Roles = "Admin")]`
- No moderator-specific endpoints yet

**Implication**: If an agent user is assigned the `Member` role, they have the same permissions as any other member. **This is correct behavior** for testing/simulation purposes.

#### Recommendation

- Assign agent users the `Member` role by default (same as regular users)
- No special authorization restrictions for agent users in Phase 1
- If future requirements restrict agents (e.g., agents cannot create channels even if promoted to Moderator), add a check:
```csharp
if (user.IsAgent && RequiresNonAgentPermission(action))
{
    return Forbid();
}
```

---

## Open Questions

1. **API Key Header Format**: Should we use `X-Api-Key: <key>` or `Authorization: ApiKey <key>`?
   - **Recommendation**: Support **both** for flexibility. Many tools expect `X-Api-Key`, but `Authorization` is more standard.

2. **API Key Expiration**: Should API keys expire? Or are they permanent until revoked?
   - **Recommendation**: **Optional expiration** (nullable `ExpiresAtUtc`). Default to no expiration in Phase 1, add expiration support in Phase 2 if needed.

3. **API Key Scopes**: Should API keys have scopes (e.g., `agent:create`, `agent:list`)?
   - **Recommendation**: **No scopes in Phase 1** (per requirements). All API keys have full agent management access. Scopes are explicitly out of scope.

4. **Agent User Limits**: Should there be a limit on how many agents one API key can create?
   - **Recommendation**: **No hard limit in Phase 1**. Add a configurable limit (e.g., 100 agents per key) in Phase 2 if abuse becomes a concern.

5. **API Key Ownership**: Should API keys be tied to a specific admin user?
   - **Recommendation**: **No** for Phase 1 (requirements state "not tied to a specific user account"). In Phase 2, optionally add `CreatedByUserId` to track which admin created the key.

---

## Risks and Concerns

### High Priority

1. **API Key Leakage**: If an API key is leaked, an attacker can create unlimited agent accounts. **Mitigation**: Hash keys, implement rate limiting, allow immediate revocation.
2. **Agent Account Abuse**: Malicious agents could spam messages, flood channels. **Mitigation**: Apply rate limiting to message send endpoints, allow admins to delete agent users.

### Medium Priority

3. **JWT Token Expiration Handling**: MCP server must handle JWT expiration and re-authenticate every 15 minutes. **Mitigation**: Document this clearly in Phase 2 MCP setup guide.
4. **Database Performance**: If thousands of agents are created, `IsAgent` flag queries should be indexed. **Mitigation**: Platform should add an index on `IsAgent` column if agent usage grows.

### Low Priority

5. **Agent User Impersonation**: If an admin wants to test an agent's perspective, they cannot log in as the agent (no password). **Mitigation**: Not a concern for Phase 1 — agents are controlled via MCP, not manual login.

---

## Phase 1 Deliverables (Auth Domain)

From the requirements, Phase 1 includes:
- API key entity (Platform)
- API key authentication middleware (**Auth domain**)
- `IsAgent` flag on `ApplicationUser` (**Auth domain**)
- Admin endpoints for API key management (create, list, revoke) (**Auth domain**)

### Specific Files to Create/Modify

**New Files:**
1. `src/HotBox.Application/Authentication/ApiKeyAuthenticationHandler.cs` — Custom auth handler
2. `src/HotBox.Application/Authentication/ApiKeyAuthenticationOptions.cs` — Options for the handler
3. `src/HotBox.Application/Models/CreateAgentRequest.cs` — DTO for agent creation
4. `src/HotBox.Application/Models/AgentResponse.cs` — DTO for agent response

**Modified Files:**
1. `src/HotBox.Core/Entities/AppUser.cs` — Add `IsAgent` property
2. `src/HotBox.Infrastructure/Data/Configurations/AppUserConfiguration.cs` — Add `IsAgent` configuration
3. `src/HotBox.Application/DependencyInjection/ApplicationServiceExtensions.cs` — Register API key authentication scheme
4. `src/HotBox.Application/Controllers/AdminController.cs` — Add agent management endpoints (`POST /api/admin/agents`, `GET /api/admin/agents`)
5. `src/HotBox.Application/Models/AdminUserResponse.cs` — Add `IsAgent` property

---

## Recommendations Summary

1. **Use `AddPolicyScheme`** to support both JWT and API key authentication without breaking existing `[Authorize]` attributes
2. **Hash API keys** using SHA256, display plaintext key only once on creation
3. **Create agent accounts without passwords** using `UserManager.CreateAsync(user)` (no password parameter)
4. **Return JWT on agent creation** so MCP server can immediately authenticate as that agent
5. **Assign `Member` role** to agent users by default
6. **Support both `X-Api-Key` and `Authorization: ApiKey` headers** for API key authentication
7. **Implement rate limiting** on agent creation endpoints to prevent abuse
8. **Document JWT expiration handling** clearly for MCP server developers

---

## Approval

This review represents the Auth & Security domain's assessment of the MCP Agent Tools requirements. Once all domain reviews are complete and conflicts are resolved, implementation can proceed.

**Next Steps:**
1. Platform agent reviews entity design and migration
2. Messaging agent reviews agent message sending
3. Client Experience agent reviews admin UI for API key management
4. Architecture/design document created consolidating all domain feedback
