# HotBox v2 — Technology Stack

**Version**: 2.0
**Date**: 2026-03-22
**Status**: Draft

---

## Overview

HotBox v2 is a ground-up rebuild of the self-hosted real-time communication platform. This document defines the technology stack, the rationale behind each choice, and when to introduce deferred technologies.

---

## Runtime

| Component | Technology | Version | Notes |
|-----------|-----------|---------|-------|
| **Runtime** | .NET 9 | 9.0 (STS) | Upgrade path to .NET 10 LTS (Nov 2026) |
| **Language** | C# 13 | — | |
| **SDK** | .NET SDK 9.0 | Latest patch | |

**.NET 9 over .NET 8 rationale**: HybridCache, improved Blazor render modes, built-in `MapStaticAssets`, `System.Text.Json` improvements, better AOT support. .NET 10 LTS arrives Nov 2026 — plan the upgrade then.

---

## Frontend

| Component | Technology | Notes |
|-----------|-----------|-------|
| **UI Framework** | Blazor Web (Interactive Server + WASM) | Hybrid render mode — server-side by default, WASM opt-in |
| **Component Library** | MudBlazor 8.x | Material Design, MIT, ~70 components, theming, accessibility |
| **Utility CSS** | Tailwind CSS 4 | Utility classes only — not a replacement for MudBlazor theming |
| **Icons** | MudBlazor Icons + Lucide (SVG) | MudBlazor built-in for Material icons; Lucide for supplemental |

### Blazor Render Mode Strategy

```
InteractiveServer (default)
├── Faster initial load (no WASM download)
├── Direct server-side service access (no API round-trip)
├── SignalR circuit per user (natural for a chat app)
└── Ideal for: layouts, navigation, channel lists, admin pages

InteractiveWebAssembly (opt-in)
├── Used for latency-sensitive client-side interactions
└── Ideal for: voice controls, message composer, drag-and-drop

Static SSR
├── No interactivity overhead
└── Ideal for: login page, error pages, public landing page
```

**Why not pure WASM?** The v1 architecture required every operation to round-trip through REST APIs. Blazor Server lets components call services directly, eliminating a large surface area of API controllers, client-side HTTP services, and serialization overhead. For a chat app that already maintains persistent WebSocket connections, the SignalR circuit cost is negligible.

---

## Backend

| Component | Technology | Notes |
|-----------|-----------|-------|
| **Web Framework** | ASP.NET Core 9 | |
| **API Style** | Controllers (REST) | Retained for external/MCP/agent consumers |
| **API Docs** | Scalar | Replaces Swashbuckle (unmaintained). Modern UI, OpenAPI 3.1 |
| **Real-time** | SignalR with Redis backplane | Multi-instance ready from day one |
| **Validation** | FluentValidation | Pipeline validators for all input DTOs |
| **Resilience** | Polly v8 | Retry, circuit breaker, timeout for outbound HTTP (OAuth, webhooks) |
| **Health Checks** | AspNetCore.HealthChecks.* | Postgres, Redis, SignalR, custom checks |

---

## Data

| Component | Technology | Notes |
|-----------|-----------|-------|
| **ORM** | EF Core 9 | |
| **Database** | PostgreSQL 16+ only | Dropping MySQL/SQLite multi-provider. Simplifies FTS, migrations, deployment |
| **Migrations** | EF Core Migrations | DbUp considered but EF Core migrations sufficient for single-provider |
| **Search** | PostgreSQL `tsvector`/`tsquery` | Native FTS, GIN indexes. Single provider = simpler implementation |

### Why PostgreSQL only?

v1 supported PostgreSQL, MySQL, and SQLite via provider abstraction. This added:
- Three separate FTS implementations (tsvector, FULLTEXT, FTS5)
- Provider-conditional migration logic
- Testing matrix across three databases
- Bugs that only manifested on one provider

For a self-hosted app targeting Docker deployment, PostgreSQL is universally available and eliminates this complexity. SQLite is insufficient for concurrent writes in a real-time chat app. MySQL offers no advantage over PostgreSQL for this workload.

---

## Caching

| Component | Technology | Notes |
|-----------|-----------|-------|
| **L1 + L2 Cache** | HybridCache (.NET 9) | In-memory L1 + Redis L2 with a single API |
| **Distributed Store** | Redis 7 | Cache, SignalR backplane, rate limiting state |

### HybridCache Strategy

```
Hot path (per-request):     L1 in-memory (IMemoryCache under the hood)
Shared/multi-instance:      L2 Redis (IDistributedCache under the hood)
Default:                    HybridCache handles tiering automatically

Use cases:
├── Channel list for user          → HybridCache (60s TTL)
├── User profile/display name      → HybridCache (5min TTL)
├── Server settings                → HybridCache (5min TTL)
├── Search results                 → HybridCache (30s TTL)
├── Rate limit counters            → Redis directly
└── SignalR backplane              → Redis directly
```

---

## Authentication & Security

| Component | Technology | Notes |
|-----------|-----------|-------|
| **Identity** | ASP.NET Core Identity | User management, password hashing, lockout |
| **Tokens** | JWT (access) + HttpOnly cookie (refresh) | 15-min access / 7-day refresh |
| **OAuth** | Google, Microsoft, Discord | Configurable per provider |
| **Password Audit** | HaveIBeenPwned validator | Check passwords against breach databases |
| **API Keys** | SHA-256 hashed, `hb_` prefix | For agent/bot accounts |

No changes from v1 auth design — it was already solid.

---

## Observability

| Component | Technology | Notes |
|-----------|-----------|-------|
| **Structured Logging** | Serilog | Console + Seq + Elasticsearch sinks |
| **Log Viewer (dev)** | Seq | Local structured log exploration |
| **Tracing** | OpenTelemetry | ASP.NET Core, HTTP, EF Core, SignalR instrumentation |
| **APM (prod)** | Elastic APM / Elasticsearch + Kibana | Via OpenTelemetry OTLP exporter |
| **Metrics** | OpenTelemetry Metrics | .NET 9 built-in meters + custom meters |

No changes from v1 observability — already well-designed.

---

## Testing

| Component | Technology | Notes |
|-----------|-----------|-------|
| **Framework** | xUnit | |
| **Assertions** | FluentAssertions | Readable assertion syntax |
| **Mocking** | NSubstitute | Interface mocking for unit tests |
| **Integration DB** | Testcontainers | Real PostgreSQL in Docker for integration tests |
| **DB Reset** | Respawn | Fast database cleanup between tests |
| **Blazor** | bUnit | Component testing |

### Test Project Structure

```
tests/
├── HotBox.Core.Tests/              # Pure unit tests (entities, value objects)
├── HotBox.Infrastructure.Tests/    # Repository tests with Testcontainers
├── HotBox.Application.Tests/       # Integration tests (WebApplicationFactory)
└── HotBox.Client.Tests/            # Blazor component tests (bUnit)
```

---

## Deployment

| Component | Technology | Notes |
|-----------|-----------|-------|
| **Containerization** | Docker (multi-stage build) | Non-root runtime user |
| **Orchestration** | Docker Compose | App + PostgreSQL + Redis + Seq |
| **Reverse Proxy** | Nginx or Caddy | TLS termination, WebSocket proxying |
| **Process Manager** | systemd (bare-metal) | Hardened service unit |

---

## Deferred Technologies

These are **not** in the v2 day-one stack but are architecturally compatible and can be added incrementally.

| Technology | When to Adopt | Prerequisites |
|-----------|--------------|---------------|
| **MassTransit** | When you need async command/event decoupling (notifications, search indexing, audit trails) | Define event contracts in Core |
| **Kafka** | When MassTransit in-memory/RabbitMQ transport hits throughput limits, or provisioner needs cross-instance event streaming | MassTransit abstraction in place |
| **Quartz.NET** | When you need scheduled/recurring jobs (invite expiry, token cleanup, report generation) | None — drop-in addition |
| **Audit.NET** | When admin audit trail becomes a requirement | EF Core interceptor registration |
| **Feature Management** | When provisioner needs per-instance feature toggles | `Microsoft.FeatureManagement` package |
| **FluentEmail** | When email notifications are required (password reset, invite, digest) | SMTP configuration |
| **QuestPDF** | When admin reports or data export is needed | None |
| **Avalonia** | When a native desktop client is desired | Separate project, shared Core |
| **Spectre.Console** | When CLI admin tooling is needed | Separate project |
| **ApexCharts** | When admin dashboard needs data visualization | Blazor wrapper package |

### Adoption Path: MassTransit → Kafka

```
Phase 1 (v2 launch):
    Services call each other directly (synchronous)
    PresenceService.OnUserStatusChanged → SignalR broadcast

Phase 2 (when needed):
    Add MassTransit with InMemory transport
    Publish domain events: MessageSent, UserJoined, StatusChanged
    Consumers: NotificationConsumer, SearchIndexConsumer, AuditConsumer

Phase 3 (at scale):
    Swap MassTransit transport to RabbitMQ (multi-instance)

Phase 4 (provisioner / high-throughput):
    Swap MassTransit transport to Kafka
    Event sourcing for message history across instances
```

---

## Package Manifest (Day One)

### HotBox.Core
```
(no external packages — pure domain)
```

### HotBox.Infrastructure
```
Microsoft.EntityFrameworkCore 9.x
Npgsql.EntityFrameworkCore.PostgreSQL 9.x
Microsoft.AspNetCore.Identity.EntityFrameworkCore 9.x
StackExchange.Redis 2.x
FluentValidation 11.x
FluentValidation.DependencyInjectionExtensions 11.x
```

### HotBox.Application
```
Microsoft.AspNetCore.Authentication.JwtBearer 9.x
Microsoft.AspNetCore.Authentication.Google 9.x
Microsoft.AspNetCore.Authentication.MicrosoftAccount 9.x
Microsoft.AspNetCore.SignalR.StackExchangeRedis 9.x
Microsoft.Extensions.Caching.Hybrid 9.x
Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 9.x
AspNetCore.HealthChecks.Redis 8.x
Scalar.AspNetCore 2.x
Polly 8.x
Polly.Extensions.Http 3.x
Serilog.AspNetCore 9.x
Serilog.Sinks.Seq 9.x
Serilog.Sinks.Elasticsearch 10.x
Serilog.Enrichers.Environment 3.x
Serilog.Enrichers.Thread 4.x
OpenTelemetry.Extensions.Hosting 1.x
OpenTelemetry.Instrumentation.AspNetCore 1.x
OpenTelemetry.Instrumentation.EntityFrameworkCore 1.x
OpenTelemetry.Instrumentation.Http 1.x
OpenTelemetry.Exporter.OpenTelemetryProtocol 1.x
```

### HotBox.Client
```
Microsoft.AspNetCore.Components.WebAssembly 9.x
MudBlazor 8.x
```

### Test Projects
```
xunit 2.x
FluentAssertions 7.x
NSubstitute 5.x
Testcontainers 4.x
Testcontainers.PostgreSql 4.x
Respawn 6.x
bunit 2.x
Microsoft.AspNetCore.Mvc.Testing 9.x
```
