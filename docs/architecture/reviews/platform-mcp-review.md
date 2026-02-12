# Platform Domain Review: MCP Agent Tools

**Date:** 2026-02-12
**Reviewer:** Platform Agent
**Feature:** MCP Agent Tools (Phase 0 Domain Review)
**Requirements:** `/home/cpike/workspace/hotbox-chat/docs/requirements/mcp-agent-tools.md`

## Executive Summary

From the Platform perspective, the MCP Agent Tools feature requires:
1. A new `ApiKey` entity in `HotBox.Core`
2. An `IsAgent` flag on the existing `AppUser` entity
3. A new `HotBox.Mcp` project in the solution
4. API key authentication middleware in `HotBox.Application`
5. New repository interfaces and Options classes
6. Multi-provider migration considerations

This review covers entity design, migration strategy, solution structure, middleware integration, and infrastructure patterns based on existing conventions in the codebase.

---

## 1. ApiKey Entity Design

### Recommended Entity Structure

Based on existing entity patterns in `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Entities/`, the `ApiKey` entity should follow these conventions:

```csharp
namespace HotBox.Core.Entities;

public class ApiKey
{
    public Guid Id { get; init; }

    public string KeyValue { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string? RevokedReason { get; set; }

    public bool IsRevoked => RevokedAtUtc.HasValue;

    public bool IsActive => !IsRevoked;

    // Navigation property — agents created by this key
    public ICollection<AppUser> CreatedAgents { get; set; } = [];
}
```

### Design Rationale

| Decision | Rationale | Pattern Reference |
|----------|-----------|------------------|
| `Guid Id { get; init; }` | Matches `RefreshToken.cs`, `Message.cs`, `Channel.cs` | `src/HotBox.Core/Entities/RefreshToken.cs:5` |
| `CreatedAtUtc` suffix | All timestamps use `Utc` suffix convention | `.claude/agents/platform.md:59` |
| `RevokedAtUtc` nullable DateTime | Same revocation pattern as `RefreshToken` | `src/HotBox.Core/Entities/RefreshToken.cs:17` |
| Computed properties (`IsRevoked`, `IsActive`) | Same pattern as `RefreshToken` | `src/HotBox.Core/Entities/RefreshToken.cs:21-25` |
| `ICollection<AppUser>` navigation | Standard EF Core one-to-many pattern | `src/HotBox.Core/Entities/AppUser.cs:18-22` |

### AppUser Entity Changes

Add to `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Entities/AppUser.cs`:

```csharp
public bool IsAgent { get; set; }

public Guid? CreatedByApiKeyId { get; set; }

public ApiKey? CreatedByApiKey { get; set; }
```

This establishes the relationship: one `ApiKey` can create many agent `AppUser`s.

---

## 2. EF Core Configuration

### ApiKeyConfiguration

Create `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/Configurations/ApiKeyConfiguration.cs`:

```csharp
using HotBox.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HotBox.Infrastructure.Data.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedOnAdd();

        builder.Property(a => a.KeyValue)
            .IsRequired()
            .HasMaxLength(256); // SHA-256 hash in base64 = ~44 chars, allow room for future formats

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired();

        builder.Property(a => a.RevokedReason)
            .HasMaxLength(500);

        // Computed properties are not persisted
        builder.Ignore(a => a.IsRevoked);
        builder.Ignore(a => a.IsActive);

        // Unique constraint on key value (same pattern as RefreshToken.Token)
        builder.HasIndex(a => a.KeyValue).IsUnique();

        // Navigation: one API key can create many agents
        builder.HasMany(a => a.CreatedAgents)
            .WithOne(u => u.CreatedByApiKey)
            .HasForeignKey(u => u.CreatedByApiKeyId)
            .OnDelete(DeleteBehavior.SetNull); // Don't cascade delete agents when key is deleted
    }
}
```

**Pattern Reference**: `src/HotBox.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs`

### AppUserConfiguration Update

Add to `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/Configurations/AppUserConfiguration.cs`:

```csharp
builder.Property(u => u.IsAgent)
    .IsRequired()
    .HasDefaultValue(false);

builder.Property(u => u.CreatedByApiKeyId)
    .IsRequired(false);
```

### DbContext Update

Add to `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/HotBoxDbContext.cs`:

```csharp
public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
```

The existing `ApplyConfigurationsFromAssembly()` call will automatically pick up `ApiKeyConfiguration`.

---

## 3. Migration Considerations

### Multi-Provider Strategy

HotBox supports SQLite (dev), PostgreSQL, MySQL, and MariaDB (prod). The migration must work across all providers.

**Safe Migration Pattern:**

```csharp
public partial class AddApiKeyEntity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ApiKeys",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                KeyValue = table.Column<string>(maxLength: 256, nullable: false),
                Name = table.Column<string>(maxLength: 100, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(nullable: false),
                RevokedAtUtc = table.Column<DateTime>(nullable: true),
                RevokedReason = table.Column<string>(maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApiKeys", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_KeyValue",
            table: "ApiKeys",
            column: "KeyValue",
            unique: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsAgent",
            table: "AspNetUsers",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedByApiKeyId",
            table: "AspNetUsers",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_CreatedByApiKeyId",
            table: "AspNetUsers",
            column: "CreatedByApiKeyId");

        migrationBuilder.AddForeignKey(
            name: "FK_AspNetUsers_ApiKeys_CreatedByApiKeyId",
            table: "AspNetUsers",
            column: "CreatedByApiKeyId",
            principalTable: "ApiKeys",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_AspNetUsers_ApiKeys_CreatedByApiKeyId",
            table: "AspNetUsers");

        migrationBuilder.DropIndex(
            name: "IX_AspNetUsers_CreatedByApiKeyId",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "IsAgent",
            table: "AspNetUsers");

        migrationBuilder.DropColumn(
            name: "CreatedByApiKeyId",
            table: "AspNetUsers");

        migrationBuilder.DropTable(
            name: "ApiKeys");
    }
}
```

**Testing Requirements** (per `.claude/agents/platform.md:93`):
- Test migration against SQLite (dev default)
- Test migration against PostgreSQL (prod default)
- Verify `SetNull` behavior on API key deletion across all providers

---

## 4. New HotBox.Mcp Project

### Solution Structure

The MCP server should be a standalone project in the solution. Based on `/home/cpike/workspace/hotbox-chat/HotBox.sln`, add:

```xml
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "HotBox.Mcp", "src\HotBox.Mcp\HotBox.Mcp.csproj", "{NEW-GUID}"
```

Nested under the `src` solution folder (lines 68-72 in `HotBox.sln`).

### Project References

**HotBox.Mcp.csproj should reference:**
- **NO internal project references** (Core, Infrastructure, Application)
- Only NuGet packages for MCP protocol and HTTP client libraries

**Rationale:**
The requirements state the MCP server "communicates with HotBox over HTTP/SignalR (exercises the real stack)." This means:
- It talks to the public API (controllers and SignalR hubs)
- It uses an API key for authentication (header-based)
- It does NOT need direct access to Core entities, DbContext, or repositories

This approach validates that the public API surface is sufficient for external integrations.

### Recommended Structure

```
src/HotBox.Mcp/
  ├── HotBox.Mcp.csproj
  ├── Program.cs                    # MCP server host
  ├── Tools/                        # MCP tool implementations
  │   ├── CreateAgentAccountTool.cs
  │   ├── ListAgentAccountsTool.cs
  │   ├── SendMessageTool.cs
  │   ├── SendDirectMessageTool.cs
  │   ├── ReadMessagesTool.cs
  │   └── ReadDirectMessagesTool.cs
  ├── Clients/                      # HTTP client for HotBox API
  │   └── HotBoxApiClient.cs
  └── appsettings.json              # MCP server config (API key, HotBox URL)
```

### Docker Considerations

The MCP server is **NOT part of the docker-compose stack**. It's a developer tool that runs locally and connects to HotBox (either local dev server or Docker container).

No changes to `/home/cpike/workspace/hotbox-chat/docker-compose.yml` required.

---

## 5. API Key Authentication Middleware

### Middleware Location

Based on existing middleware patterns, create:

`/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Middleware/ApiKeyAuthenticationMiddleware.cs`

**Note**: The glob pattern `src/HotBox.Application/Middleware/*.cs` returned no results, indicating this will be the first middleware. However, `Program.cs:52` references `UseSerilogRequestLogging()`, which is middleware, so the pattern exists in the pipeline.

### Middleware Design

```csharp
using HotBox.Core.Entities;
using HotBox.Core.Options;
using HotBox.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace HotBox.Application.Middleware;

public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(RequestDelegate next, ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, HotBoxDbContext dbContext, IOptions<ApiKeyOptions> options)
    {
        var apiKeyOptions = options.Value;

        // Check for API key in header
        if (context.Request.Headers.TryGetValue(apiKeyOptions.HeaderName, out var extractedApiKey))
        {
            var apiKey = await dbContext.ApiKeys
                .FirstOrDefaultAsync(k => k.KeyValue == extractedApiKey && k.IsActive);

            if (apiKey != null)
            {
                // Create a ClaimsPrincipal for the API key
                var claims = new[]
                {
                    new Claim("ApiKeyId", apiKey.Id.ToString()),
                    new Claim("ApiKeyName", apiKey.Name),
                    new Claim(ClaimTypes.AuthenticationMethod, "ApiKey")
                };

                var identity = new ClaimsIdentity(claims, "ApiKey");
                context.User = new ClaimsPrincipal(identity);

                _logger.LogInformation("API key authenticated: {ApiKeyName} ({ApiKeyId})", apiKey.Name, apiKey.Id);
            }
            else
            {
                _logger.LogWarning("Invalid or revoked API key attempted: {ApiKey}", extractedApiKey);
            }
        }

        await _next(context);
    }
}
```

### Middleware Registration

Add to `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Program.cs` **before** `app.UseAuthentication()` (line 64):

```csharp
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
```

**Order matters**: API key middleware must run before JWT authentication to allow controllers/hubs to differentiate between user tokens and API keys.

### Open Question: Header Format

Requirements list this as an open question (line 111):
- `X-Api-Key: <key>` (custom header, common in REST APIs)
- `Authorization: ApiKey <key>` (leverages existing Authorization header)

**Recommendation**: Use `X-Api-Key` for simplicity. The existing JWT middleware uses `Authorization: Bearer <token>`, and having both schemes in the same header could cause parsing conflicts.

---

## 6. Infrastructure Changes

### New Repository Interface

Create `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Interfaces/IApiKeyRepository.cs`:

```csharp
using HotBox.Core.Entities;

namespace HotBox.Core.Interfaces;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ApiKey?> GetByKeyValueAsync(string keyValue, CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> GetAllAsync(bool includeRevoked = false, CancellationToken ct = default);

    Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken ct = default);

    Task<ApiKey> UpdateAsync(ApiKey apiKey, CancellationToken ct = default);

    Task<IReadOnlyList<AppUser>> GetAgentAccountsAsync(Guid apiKeyId, CancellationToken ct = default);
}
```

**Pattern Reference**: `src/HotBox.Core/Interfaces/IMessageRepository.cs`

### Repository Implementation

Create `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Repositories/ApiKeyRepository.cs`:

```csharp
using HotBox.Core.Entities;
using HotBox.Core.Interfaces;
using HotBox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotBox.Infrastructure.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly HotBoxDbContext _context;

    public ApiKeyRepository(HotBoxDbContext context)
    {
        _context = context;
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ApiKeys
            .Include(k => k.CreatedAgents)
            .FirstOrDefaultAsync(k => k.Id == id, ct);
    }

    public async Task<ApiKey?> GetByKeyValueAsync(string keyValue, CancellationToken ct = default)
    {
        return await _context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyValue == keyValue, ct);
    }

    public async Task<IReadOnlyList<ApiKey>> GetAllAsync(bool includeRevoked = false, CancellationToken ct = default)
    {
        var query = _context.ApiKeys.AsQueryable();

        if (!includeRevoked)
        {
            query = query.Where(k => k.RevokedAtUtc == null);
        }

        return await query.ToListAsync(ct);
    }

    public async Task<ApiKey> CreateAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        _context.ApiKeys.Add(apiKey);
        await _context.SaveChangesAsync(ct);
        return apiKey;
    }

    public async Task<ApiKey> UpdateAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        _context.ApiKeys.Update(apiKey);
        await _context.SaveChangesAsync(ct);
        return apiKey;
    }

    public async Task<IReadOnlyList<AppUser>> GetAgentAccountsAsync(Guid apiKeyId, CancellationToken ct = default)
    {
        return await _context.Users
            .Where(u => u.CreatedByApiKeyId == apiKeyId && u.IsAgent)
            .ToListAsync(ct);
    }
}
```

### New Options Class

Create `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Options/ApiKeyOptions.cs`:

```csharp
namespace HotBox.Core.Options;

public class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    public string HeaderName { get; set; } = "X-Api-Key";
}
```

**Pattern Reference**: `src/HotBox.Core/Options/DatabaseOptions.cs`

### appsettings.json Update

Add to `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/appsettings.json`:

```json
"ApiKey": {
  "HeaderName": "X-Api-Key"
}
```

### DI Registration

Add to `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` (after existing repositories, around line 89):

```csharp
services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
```

Add to `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/DependencyInjection/ApplicationServiceExtensions.cs` (after existing options, around line 24):

```csharp
services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));
```

---

## 7. Concerns and Risks

### Security Concerns

| Risk | Mitigation |
|------|-----------|
| API keys stored in plaintext in database | Consider hashing key values (same as passwords). Store hash in `KeyValue`, return plaintext key only at creation time. |
| No rate limiting on API key requests | Add rate limiting middleware specifically for API key auth (out of scope for Phase 1, document for future). |
| Key leakage in logs | Ensure `_logger` calls in middleware don't log full key values (current design logs only boolean success/failure). |
| No expiration on API keys | Requirements explicitly exclude scopes/permissions for Phase 1. Document as future enhancement. |

**Recommendation**: Implement key hashing in Phase 1. Pattern:
- Generate cryptographically secure random key (e.g., 32 bytes base64 = 44 chars)
- Hash with SHA-256 or bcrypt before storing in `ApiKey.KeyValue`
- Return plaintext key to admin only at creation (never retrievable again)
- Middleware hashes incoming header value and compares hash

### Migration Risks

| Risk | Mitigation |
|------|-----------|
| `SetNull` behavior on foreign key deletion not supported in SQLite < 3.6.19 | SQLite in .NET 8+ uses modern version. Verify in local testing. |
| Identity table (`AspNetUsers`) schema modifications | Test carefully — Identity tables have complex constraints. |
| Concurrent migrations if other branches add entities | Standard risk, resolved via merge conflict handling in migration files. |

### Middleware Order Risks

Incorrect middleware order could break authentication. Correct order (from `Program.cs`):

1. `UseSerilogRequestLogging()` (line 52)
2. `UseHttpsRedirection()` (line 60)
3. `UseBlazorFrameworkFiles()` (line 61)
4. `UseStaticFiles()` (line 62)
5. `UseCors()` (line 63)
6. **`UseMiddleware<ApiKeyAuthenticationMiddleware>()`** ← NEW
7. `UseAuthentication()` (line 64) ← JWT bearer
8. `UseAuthorization()` (line 65)

If API key middleware runs **after** `UseAuthentication()`, JWT validation will fail for API key requests.

---

## 8. Recommendations

### Phase 1 Implementation Checklist

**Entities & Configuration:**
- [ ] Create `ApiKey` entity in `src/HotBox.Core/Entities/ApiKey.cs`
- [ ] Update `AppUser` entity with `IsAgent`, `CreatedByApiKeyId`, `CreatedByApiKey`
- [ ] Create `ApiKeyConfiguration` in `src/HotBox.Infrastructure/Data/Configurations/`
- [ ] Update `AppUserConfiguration` with new properties
- [ ] Update `HotBoxDbContext` with `DbSet<ApiKey>`
- [ ] Generate migration: `dotnet ef migrations add AddApiKeyEntity -p src/HotBox.Infrastructure -s src/HotBox.Application`
- [ ] Test migration on SQLite and PostgreSQL

**Repository & Options:**
- [ ] Create `IApiKeyRepository` interface in `src/HotBox.Core/Interfaces/`
- [ ] Create `ApiKeyRepository` implementation in `src/HotBox.Infrastructure/Repositories/`
- [ ] Create `ApiKeyOptions` class in `src/HotBox.Core/Options/`
- [ ] Register repository in `InfrastructureServiceExtensions.cs`
- [ ] Register options in `ApplicationServiceExtensions.cs`
- [ ] Add `ApiKey` section to `appsettings.json`

**Middleware:**
- [ ] Create `ApiKeyAuthenticationMiddleware` in `src/HotBox.Application/Middleware/`
- [ ] Register middleware in `Program.cs` before `UseAuthentication()`
- [ ] Add structured logging for API key auth events

**HotBox.Mcp Project:**
- [ ] Add project to solution under `src/` folder
- [ ] Create `HotBox.Mcp.csproj` with NO internal project references
- [ ] Implement `HotBoxApiClient` for HTTP communication
- [ ] Implement 6 MCP tools per requirements (Phase 2)
- [ ] Add MCP server setup documentation in `docs/`

**Admin Endpoints (Auth & Security domain — coordinate):**
- [ ] Create `ApiKeyController` with Create/List/Revoke endpoints
- [ ] Require admin role for all API key management endpoints
- [ ] Document API key workflow in `docs/`

### API Key Hashing Approach

Generate keys using `RandomNumberGenerator`:

```csharp
public static string GenerateApiKey()
{
    var keyBytes = new byte[32]; // 256 bits
    RandomNumberGenerator.Fill(keyBytes);
    return Convert.ToBase64String(keyBytes); // Returns 44-char string
}
```

Hash before storing (if using bcrypt):

```csharp
var hashedKey = BCrypt.Net.BCrypt.HashPassword(plaintextKey);
```

Middleware verification:

```csharp
var apiKey = await dbContext.ApiKeys
    .FirstOrDefaultAsync(k => BCrypt.Net.BCrypt.Verify(extractedApiKey, k.KeyValue));
```

**Trade-off**: Bcrypt is slow (intentional for passwords), may impact API performance. Consider SHA-256 + salt for faster hashing if performance is critical.

### Solution Structure Validation

After adding `HotBox.Mcp` project, verify build order:

1. `HotBox.Core` (no dependencies)
2. `HotBox.Infrastructure` (depends on Core)
3. `HotBox.Application` (depends on Core + Infrastructure)
4. `HotBox.Client` (depends on Core for DTOs)
5. `HotBox.Mcp` (no internal dependencies, only NuGet packages)

Run `dotnet build` from solution root to confirm no circular references.

### Testing Strategy

**Unit Tests:**
- `ApiKeyRepository` CRUD operations (mock DbContext)
- `ApiKeyAuthenticationMiddleware` authentication logic (mock HttpContext)

**Integration Tests:**
- API key creation flow (admin endpoint → database → middleware)
- Agent account creation with API key linkage
- API key revocation (verify `IsActive` query)

**Multi-Provider Tests:**
- Run migration on SQLite, PostgreSQL, MySQL
- Verify `SetNull` FK behavior on all providers
- Test unique constraint on `KeyValue` across providers

---

## Appendix: File Paths Referenced

| File | Purpose |
|------|---------|
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Entities/RefreshToken.cs` | Pattern for revocation tracking |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Core/Entities/AppUser.cs` | Existing user entity to extend |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs` | EF configuration pattern |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/Data/HotBoxDbContext.cs` | DbContext to update |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/Program.cs` | Middleware pipeline |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Infrastructure/DependencyInjection/InfrastructureServiceExtensions.cs` | DI registration for repositories |
| `/home/cpike/workspace/hotbox-chat/src/HotBox.Application/DependencyInjection/ApplicationServiceExtensions.cs` | DI registration for options |
| `/home/cpike/workspace/hotbox-chat/HotBox.sln` | Solution structure |
| `/home/cpike/workspace/hotbox-chat/docker-compose.yml` | Docker config (no changes needed) |
| `/home/cpike/workspace/hotbox-chat/.claude/agents/platform.md` | Platform domain ownership |

---

## Next Steps

1. **Coordinate with Auth & Security domain** on admin endpoints for API key CRUD (they own controllers and auth logic)
2. **Coordinate with Messaging domain** on agent-specific message handling (if needed)
3. **Coordinate with Client Experience domain** on admin UI for API key management
4. Finalize API key hashing strategy (plaintext vs bcrypt vs SHA-256)
5. Finalize header format (`X-Api-Key` vs `Authorization: ApiKey`)
6. Begin Phase 1 implementation after domain reviews are complete
