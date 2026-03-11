# HotBox Provisioner — Technical Specification

**Version**: 1.0
**Status**: Specification
**Date**: 2026-03-10

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Data Model](#data-model)
4. [API Surface](#api-surface)
5. [Docker Orchestration](#docker-orchestration)
6. [DNS & SSL](#dns--ssl)
7. [Instance Lifecycle](#instance-lifecycle)
8. [Update Mechanism](#update-mechanism)
9. [Security](#security)
10. [Resource Quotas](#resource-quotas)
11. [UI Routes](#ui-routes)
12. [Configuration](#configuration)
13. [Out of Scope](#out-of-scope)
14. [Open Questions](#open-questions)

---

## Overview

### What It Is

The HotBox Provisioner is a web application that enables self-hosted operators to provision and manage isolated HotBox chat instances via a web dashboard. An admin user can:

- Create new HotBox instances (each with a Docker app container + PostgreSQL database)
- Assign instances to invited users (or let users self-serve via invite links)
- Monitor instance health, resource usage, and activity
- Update instances to the latest HotBox version with one click
- Stop, restart, or delete instances
- Manage invite tokens and user access

### Why It Exists

HotBox is open-source, self-hosted, and fork-friendly. The provisioner reduces operational burden by automating the repetitive tasks of deploying and managing multiple instances on a single server. It targets small-to-mid-size deployments (roughly 5-50 instances per server) rather than hyperscale multi-tenant SaaS.

### Positioning

- **Open-source friendly**: Operators can fork, modify, and deploy with full transparency
- **Self-contained**: Each instance is isolated in its own Docker containers with a dedicated database
- **Invite-based**: Admin controls who can create instances; users get guided setup wizards
- **Minimal operational overhead**: One-click updates, automatic DNS/SSL via Caddy, built-in resource quotas
- **Not hyperscale**: Assumes single-server deployments; no load balancing or instance migration across servers

---

## Architecture

### Monorepo Structure

The Provisioner lives in the existing `hotbox-chat` monorepo:

```
hotbox-chat/
├── src/
│   ├── HotBox.Application/          (existing HotBox)
│   ├── HotBox.Client/               (existing HotBox)
│   ├── HotBox.Core/                 (existing HotBox)
│   ├── HotBox.Infrastructure/       (existing HotBox)
│   ├── HotBox.Provisioner.Core/     (NEW) — Domain models, interfaces, enums
│   ├── HotBox.Provisioner.Infrastructure/ (NEW) — Data access (EF Core + PostgreSQL)
│   └── HotBox.Provisioner.Application/    (NEW) — ASP.NET Core API + Blazor WASM
├── docs/
│   └── provisioner/
│       ├── spec.md                  (this file)
│       └── dns-setup.md             (existing DNS/SSL guide)
└── deploy/
    └── provisioner/                 (docker-compose, Caddyfile, scripts)
```

### Three-Layer Architecture

The Provisioner follows the same three-layer pattern as the main HotBox application:

1. **HotBox.Provisioner.Core** — Domain models, interfaces, business logic
   - Entities: `Instance`, `Invite`, `User`, `PlatformConfig`, `InstanceSnapshot`
   - Enums: `InstanceStatus`, `UserRole`, `InviteStatus`, `RegistrationMode`
   - Interfaces: `IInstanceService`, `IInviteService`, `IDockerService`, `ICaddyService`, `IDnsService`
   - No infrastructure dependencies

2. **HotBox.Provisioner.Infrastructure** — Data access and external services
   - EF Core DbContext for provisioner metadata (instances, invites, users, config snapshots)
   - Implementations of Docker, Caddy, DNS services
   - Docker SDK (`Docker.DotNet`) for container orchestration
   - Caddy API client for dynamic Caddyfile management
   - DNS provider abstraction (Namecheap, Cloudflare, DigitalOcean)

3. **HotBox.Provisioner.Application** — ASP.NET Core API and Blazor WASM UI
   - REST API controllers for instances, invites, users, platform config
   - SignalR hub for real-time status updates during provisioning
   - Blazor WASM shell, pages, and components for all user flows
   - Admin dashboard, instance setup wizard, invite signup flow

### Relationship to Main HotBox

The Provisioner is **independent**:
- It does NOT import HotBox.Core, HotBox.Infrastructure, or HotBox.Client
- It manages HotBox instances as opaque Docker services
- The provisioner's database is separate from each tenant HotBox instance
- The provisioner's API never calls into tenant instance APIs (no cross-instance knowledge)

Each tenant HotBox instance:
- Runs a stock HotBox app container (no modifications)
- Has its own PostgreSQL database
- Has its own admin account (seeded during provisioning)
- Is unaware it's being managed by the provisioner

---

## Data Model

### Entities

#### Instance

Represents a provisioned HotBox chat instance.

| Field | Type | Nullable | Notes |
|-------|------|----------|-------|
| `Id` | GUID | No | Primary key |
| `Subdomain` | string | No | e.g., "thbr" for "thbr.hotboxchat.ca" — unique |
| `FriendlyName` | string | No | User-facing name, e.g., "The Hub" |
| `OwnerId` | GUID | No | FK to User |
| `Status` | InstanceStatus enum | No | provisioning, running, stopped, error, deleting |
| `StatusMessage` | string | Yes | Error details if status = error |
| `DockerContainerId` | string | Yes | Docker container ID of the app |
| `DockerNetworkName` | string | No | Docker network for isolation |
| `PostgresContainerId` | string | Yes | Docker container ID of PostgreSQL |
| `PostgresPort` | int | No | Port PostgreSQL listens on (host side) |
| `PostgresPassword` | string | No | Database password (encrypted in DB) |
| `HotBoxVersion` | string | No | e.g., "1.2.3" — tracks what version is running |
| `InstanceAdminUsername` | string | No | Admin username inside the HotBox instance |
| `InstanceAdminEmail` | string | No | Admin email inside the HotBox instance |
| `RegistrationMode` | RegistrationMode enum | No | open, invite-only, or closed |
| `CreatedAt` | DateTime | No | UTC |
| `UpdatedAt` | DateTime | No | UTC |
| `LastHealthCheckAt` | DateTime | Yes | Last time provisioner verified it's healthy |
| `NextUpdateCheckAt` | DateTime | Yes | When to check for new HotBox version |
| `Cpu` | int | No | CPU cores quota (0 = unlimited) |
| `Memory` | int | No | Memory in MB (0 = unlimited) |
| `Disk` | int | No | Disk in GB quota (0 = unlimited) |

#### Invite

Represents an invite token that allows a user to create an instance.

| Field | Type | Nullable | Notes |
|-------|------|----------|-------|
| `Id` | GUID | No | Primary key |
| `Token` | string | No | Unique token (e.g., `uh8sX9kL2mP`) — indexed for fast lookup |
| `CreatedBy` | GUID | No | FK to User (admin who created it) |
| `Status` | InviteStatus enum | No | active, used, expired |
| `MaxUses` | int | Yes | Max number of times it can be used (null = unlimited) |
| `UsedCount` | int | No | How many times it's been used so far |
| `ExpiresAt` | DateTime | Yes | When it expires (null = never expires) |
| `CreatedAt` | DateTime | No | UTC |
| `UsedAt` | DateTime | Yes | When it was claimed (if status = used) |

#### User

Provisioner platform user (separate from HotBox instance users).

| Field | Type | Nullable | Notes |
|-------|------|----------|-------|
| `Id` | GUID | No | Primary key |
| `Username` | string | No | Unique username |
| `Email` | string | No | Unique email |
| `DisplayName` | string | No | User's full name |
| `PasswordHash` | string | No | bcrypt or Identity hash |
| `Role` | UserRole enum | No | admin, user |
| `CreatedAt` | DateTime | No | UTC |
| `UpdatedAt` | DateTime | No | UTC |
| `Instances` | ICollection<Instance> | — | Navigation: instances owned by this user |

#### PlatformConfig

Global provisioner configuration (one record, typically ID = 1).

| Field | Type | Nullable | Notes |
|-------|------|----------|-------|
| `Id` | int | No | Primary key (always 1) |
| `BaseDomain` | string | No | e.g., "hotboxchat.ca" |
| `ServerIpAddress` | string | No | Public IP of the server |
| `MaxInstances` | int | No | Max instances allowed on this server |
| `DnsProviderType` | string | No | "namecheap", "cloudflare", "digitalocean" |
| `DnsApiKey` | string | No | API key for DNS provider (encrypted) |
| `DnsApiUser` | string | Yes | For Namecheap, the API username |
| `CaddyApiUrl` | string | No | e.g., "http://localhost:2019" |
| `DockerSocketPath` | string | No | e.g., "/var/run/docker.sock" |
| `HotBoxImageName` | string | No | e.g., "hotboxchat/app:latest" |
| `PostgresImageName` | string | No | e.g., "postgres:16-alpine" |
| `PostgresPort` | int | No | Base port for PostgreSQL instances (incremented per instance) |
| `QuotasEnabledCpu` | bool | No | Are CPU quotas enforced? (default: false) |
| `QuotasEnabledMemory` | bool | No | Are memory quotas enforced? (default: false) |
| `QuotasEnabledDisk` | bool | No | Are disk quotas enforced? (default: false) |
| `DefaultCpuCores` | int | No | CPU cores per instance when quotas enabled (default: 1) |
| `DefaultMemoryMb` | int | No | Memory per instance when quotas enabled (default: 512) |
| `DefaultDiskGb` | int | No | Disk per instance when quotas enabled (default: 20) |
| `InitializedAt` | DateTime | Yes | When setup wizard completed |

#### InstanceSnapshot

Audit trail of instance events (for activity feed in admin dashboard).

| Field | Type | Nullable | Notes |
|-------|------|----------|-------|
| `Id` | GUID | No | Primary key |
| `InstanceId` | GUID | No | FK to Instance |
| `EventType` | string | No | "created", "updated", "started", "stopped", "deleted", "error", "upgraded" |
| `Message` | string | Yes | User-facing description of the event |
| `Details` | string | Yes | JSON blob with extra details (e.g., old version → new version) |
| `CreatedAt` | DateTime | No | UTC |

### Relationships

```
User (1) ──── (*) Instance
         ├─ owns instances

User (1) ──── (*) Invite
         ├─ creates invites

PlatformConfig (1) ──── (1) Everything
                  ├─ global settings for all instances
```

---

## API Surface

All endpoints require JWT authentication (Bearer token in Authorization header). The token is issued upon login to the provisioner dashboard.

### Authentication

#### POST /api/auth/login

**Request**:
```json
{
  "username": "admin",
  "password": "password"
}
```

**Response** (200 OK):
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "...",
  "expiresIn": 3600
}
```

#### POST /api/auth/refresh

Refresh an expired access token using the refresh token.

**Request**:
```json
{
  "refreshToken": "..."
}
```

**Response** (200 OK):
```json
{
  "accessToken": "...",
  "expiresIn": 3600
}
```

#### POST /api/auth/logout

Revoke the current token (optional; clients can just discard the token).

---

### Instances

#### GET /api/instances

List all instances.

**Query Parameters**:
- `page`: int (default: 1)
- `pageSize`: int (default: 20)
- `status`: InstanceStatus? (optional filter)
- `ownerId`: GUID? (optional filter by owner)

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": "...",
      "subdomain": "thbr",
      "friendlyName": "The Hub",
      "ownerId": "...",
      "status": "running",
      "statusMessage": null,
      "dockerContainerId": "abc123...",
      "hotBoxVersion": "1.2.3",
      "cpuCores": 1,
      "memoryMb": 512,
      "diskGb": 20,
      "createdAt": "2026-03-01T10:00:00Z",
      "updatedAt": "2026-03-10T15:30:00Z",
      "lastHealthCheckAt": "2026-03-10T15:25:00Z"
    }
  ],
  "totalCount": 5,
  "pageNumber": 1,
  "pageSize": 20
}
```

#### GET /api/instances/{id}

Get details of a single instance.

**Response** (200 OK):
```json
{
  "id": "...",
  "subdomain": "thbr",
  "friendlyName": "The Hub",
  "ownerId": "...",
  "status": "running",
  "statusMessage": null,
  "dockerContainerId": "abc123...",
  "dockerNetworkName": "hotbox-thbr-net",
  "postgresContainerId": "def456...",
  "postgresPort": 5433,
  "hotBoxVersion": "1.2.3",
  "instanceAdminUsername": "admin",
  "instanceAdminEmail": "admin@thbr.hotboxchat.ca",
  "registrationMode": "invite-only",
  "cpuCores": 1,
  "memoryMb": 512,
  "diskGb": 20,
  "createdAt": "2026-03-01T10:00:00Z",
  "updatedAt": "2026-03-10T15:30:00Z",
  "lastHealthCheckAt": "2026-03-10T15:25:00Z"
}
```

#### POST /api/instances

Create a new instance (admin only, or from invite wizard for invited users).

**Request**:
```json
{
  "subdomain": "my-instance",
  "friendlyName": "My Community",
  "instanceAdminUsername": "admin",
  "instanceAdminEmail": "admin@example.com",
  "registrationMode": "open",
  "cpuCores": 1,
  "memoryMb": 512,
  "diskGb": 20,
  "inviteToken": "optional_invite_token_if_from_wizard"
}
```

**Response** (201 Created):
```json
{
  "id": "...",
  "subdomain": "my-instance",
  "status": "provisioning",
  ...
}
```

Begins the provisioning sequence (see **Instance Lifecycle** section).

#### POST /api/instances/{id}/start

Start a stopped instance.

**Response** (202 Accepted):
```json
{
  "id": "...",
  "status": "provisioning",
  "statusMessage": "Starting Docker container..."
}
```

#### POST /api/instances/{id}/stop

Gracefully stop a running instance.

**Response** (202 Accepted):
```json
{
  "id": "...",
  "status": "stopping",
  "statusMessage": "Stopping Docker container..."
}
```

#### POST /api/instances/{id}/restart

Restart a running instance.

**Response** (202 Accepted):
```json
{
  "id": "...",
  "status": "provisioning",
  "statusMessage": "Restarting Docker container..."
}
```

#### POST /api/instances/{id}/force-kill

Force-kill a stuck instance (SIGKILL container).

**Response** (202 Accepted):
```json
{
  "id": "...",
  "status": "error",
  "statusMessage": "Force killed due to admin request"
}
```

#### POST /api/instances/{id}/delete

Delete an instance (stops containers, deletes Docker objects, keeps audit trail).

**Request**:
```json
{
  "deleteDatabase": true,  // If true, also delete the PostgreSQL container and data
  "reason": "No longer needed"
}
```

**Response** (202 Accepted):
```json
{
  "id": "...",
  "status": "deleting",
  "statusMessage": "Deleting Docker containers and data..."
}
```

#### POST /api/instances/{id}/update

Update instance to the latest HotBox version.

**Response** (202 Accepted):
```json
{
  "id": "...",
  "status": "provisioning",
  "statusMessage": "Pulling latest image and applying updates..."
}
```

Pulls the latest HotBox image, stops the current container, runs migrations, and restarts.

#### POST /api/instances/{id}/logs

Stream recent logs from the instance.

**Query Parameters**:
- `lines`: int (default: 100) — how many recent lines to return
- `container`: "app" | "postgres" (default: "app") — which container to fetch logs from

**Response** (200 OK):
```
HTTP/2.0 200 OK
Content-Type: text/event-stream

data: {"timestamp":"2026-03-10T15:30:00Z","level":"info","message":"Started listening on port 5000"}
data: {"timestamp":"2026-03-10T15:30:01Z","level":"info","message":"Application started successfully"}
...
```

Alternatively, return recent logs as JSON:

```json
{
  "logs": [
    {"timestamp": "2026-03-10T15:30:00Z", "level": "info", "message": "..."},
    ...
  ],
  "truncated": false
}
```

#### GET /api/instances/{id}/health

Quick health check (does NOT require auth — used by external monitoring).

**Response** (200 OK):
```json
{
  "status": "healthy",
  "uptime": 86400,
  "containerRunning": true,
  "databaseConnected": true,
  "lastCheck": "2026-03-10T15:30:00Z"
}
```

Or (503 Service Unavailable):
```json
{
  "status": "unhealthy",
  "reason": "Database connection failed"
}
```

---

### Invites

#### GET /api/invites

List all invite tokens.

**Query Parameters**:
- `page`: int (default: 1)
- `pageSize`: int (default: 20)
- `status`: InviteStatus? (optional filter)

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": "...",
      "token": "uh8sX9kL2mP",
      "createdBy": "...",
      "status": "active",
      "maxUses": 5,
      "usedCount": 2,
      "expiresAt": "2026-04-10T00:00:00Z",
      "createdAt": "2026-03-10T10:00:00Z",
      "usedAt": null
    }
  ],
  "totalCount": 12,
  "pageNumber": 1,
  "pageSize": 20
}
```

#### POST /api/invites

Create a new invite token.

**Request**:
```json
{
  "maxUses": 10,
  "expiresInDays": 30
}
```

**Response** (201 Created):
```json
{
  "id": "...",
  "token": "uh8sX9kL2mP",
  "createdBy": "...",
  "status": "active",
  "maxUses": 10,
  "usedCount": 0,
  "expiresAt": "2026-04-09T10:00:00Z",
  "createdAt": "2026-03-10T10:00:00Z"
}
```

#### GET /api/invites/{token}/validate

Validate an invite token (no auth required — public endpoint).

**Response** (200 OK):
```json
{
  "valid": true,
  "token": "uh8sX9kL2mP",
  "maxUses": 10,
  "usedCount": 2,
  "remainingUses": 8,
  "expiresAt": "2026-04-10T00:00:00Z",
  "expired": false
}
```

Or (400 Bad Request):
```json
{
  "valid": false,
  "reason": "Token has expired"
}
```

#### POST /api/invites/{id}/revoke

Revoke an invite token (admin only).

**Response** (200 OK):
```json
{
  "id": "...",
  "token": "uh8sX9kL2mP",
  "status": "expired"
}
```

---

### Users

#### GET /api/users

List all provisioner users (admin only).

**Query Parameters**:
- `page`: int (default: 1)
- `pageSize`: int (default: 20)

**Response** (200 OK):
```json
{
  "items": [
    {
      "id": "...",
      "username": "admin",
      "email": "admin@example.com",
      "displayName": "Administrator",
      "role": "admin",
      "createdAt": "2026-03-01T10:00:00Z",
      "instanceCount": 5
    }
  ],
  "totalCount": 2,
  "pageNumber": 1,
  "pageSize": 20
}
```

#### POST /api/users

Create a new provisioner user (admin only).

**Request**:
```json
{
  "username": "newuser",
  "email": "newuser@example.com",
  "displayName": "New User",
  "password": "initial_password",
  "role": "user"
}
```

**Response** (201 Created):
```json
{
  "id": "...",
  "username": "newuser",
  "email": "newuser@example.com",
  "displayName": "New User",
  "role": "user",
  "createdAt": "2026-03-10T10:00:00Z"
}
```

#### PUT /api/users/{id}

Update a user (admin can update anyone; users can only update themselves).

**Request**:
```json
{
  "displayName": "Updated Name",
  "email": "updated@example.com"
}
```

**Response** (200 OK):
```json
{
  "id": "...",
  "username": "newuser",
  "email": "updated@example.com",
  "displayName": "Updated Name",
  "role": "user"
}
```

#### DELETE /api/users/{id}

Delete a user (admin only). Instances owned by that user are unassigned (ownership set to null or transferred to admin).

**Response** (204 No Content)

---

### Platform Config

#### GET /api/platform/config

Get the global provisioner configuration (admin only).

**Response** (200 OK):
```json
{
  "id": 1,
  "baseDomain": "hotboxchat.ca",
  "serverIpAddress": "192.0.2.1",
  "maxInstances": 20,
  "dnsProviderType": "namecheap",
  "caddyApiUrl": "http://localhost:2019",
  "dockerSocketPath": "/var/run/docker.sock",
  "hotBoxImageName": "hotboxchat/app:latest",
  "postgresImageName": "postgres:16-alpine",
  "quotasEnabledCpu": false,
  "quotasEnabledMemory": false,
  "quotasEnabledDisk": false,
  "defaultCpuCores": 1,
  "defaultMemoryMb": 512,
  "defaultDiskGb": 20,
  "initializedAt": "2026-03-01T10:00:00Z"
}
```

#### PUT /api/platform/config

Update the global configuration (admin only). Typically only DNS provider credentials and quotas are updated after initial setup.

**Request**:
```json
{
  "quotasEnabledCpu": true,
  "quotasEnabledMemory": true,
  "defaultCpuCores": 2,
  "defaultMemoryMb": 1024
}
```

**Response** (200 OK):
```json
{
  "id": 1,
  "baseDomain": "hotboxchat.ca",
  ...
  "quotasEnabledCpu": true,
  "defaultCpuCores": 2
}
```

#### POST /api/platform/health

Perform a system health check (admin only).

**Response** (200 OK):
```json
{
  "overall": "healthy",
  "checks": [
    {
      "name": "Docker daemon",
      "status": "healthy",
      "message": "Connected to Docker socket"
    },
    {
      "name": "Caddy reverse proxy",
      "status": "healthy",
      "message": "API responding at http://localhost:2019"
    },
    {
      "name": "DNS provider",
      "status": "healthy",
      "message": "Namecheap API authenticated"
    },
    {
      "name": "Database",
      "status": "healthy",
      "message": "Provisioner database connected"
    },
    {
      "name": "Disk space",
      "status": "warning",
      "message": "Only 15% disk space remaining"
    }
  ]
}
```

---

### Statistics

#### GET /api/stats/dashboard

Get dashboard summary stats (admin only).

**Response** (200 OK):
```json
{
  "totalInstances": 5,
  "runningInstances": 4,
  "stoppedInstances": 1,
  "errorInstances": 0,
  "totalUsers": 2,
  "activeInvites": 3,
  "systemCpuUsage": 42.5,
  "systemMemoryUsageMb": 2048,
  "systemDiskUsageGb": 50,
  "systemDiskCapacityGb": 100,
  "totalUserCount": 87
}
```

#### GET /api/stats/activity

Get recent activity feed for dashboard (admin only).

**Query Parameters**:
- `limit`: int (default: 20)

**Response** (200 OK):
```json
{
  "events": [
    {
      "id": "...",
      "instanceId": "...",
      "instanceName": "The Hub",
      "eventType": "created",
      "message": "Instance created by admin",
      "createdAt": "2026-03-10T15:00:00Z"
    },
    {
      "id": "...",
      "instanceId": "...",
      "instanceName": "Dev Server",
      "eventType": "upgraded",
      "message": "Upgraded from 1.2.2 to 1.2.3",
      "createdAt": "2026-03-10T14:00:00Z"
    }
  ]
}
```

---

## Docker Orchestration

### Container Naming Convention

Each instance creates two Docker containers:

```
App container:       hotbox-{subdomain}-app
Database container:  hotbox-{subdomain}-db
Docker network:      hotbox-{subdomain}-net
```

Example for subdomain `thbr`:
```
hotbox-thbr-app  (HotBox app)
hotbox-thbr-db   (PostgreSQL database)
hotbox-thbr-net  (Isolated network)
```

### Docker Compose File Generation

The provisioner generates a `docker-compose.yml` for each instance. Example:

```yaml
version: '3.8'

services:
  # HotBox application
  hotbox-thbr-app:
    image: hotboxchat/app:latest
    container_name: hotbox-thbr-app
    restart: unless-stopped
    networks:
      - hotbox-thbr-net
    ports:
      - "5000"  # Internal port; Caddy reverse proxies from outside
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://0.0.0.0:5000
      - Database__Host=hotbox-thbr-db
      - Database__Port=5432
      - Database__Name=hotbox
      - Database__Username=hotbox
      - Database__Password=${DB_PASSWORD}
      - Jwt__SecretKey=${JWT_SECRET}
      - Jwt__Issuer=https://thbr.hotboxchat.ca
      - Jwt__Audience=https://thbr.hotboxchat.ca
      - WebRtc__IceServers=stun:stun.l.google.com:19302
      - ObservabilityOptions__ServiceName=hotbox-thbr
      - ObservabilityOptions__Environment=production
      - ObservabilityOptions__LogLevel=Information
    depends_on:
      - hotbox-thbr-db
    cpus: '1'
    mem_limit: 512m
    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "3"

  # PostgreSQL database
  hotbox-thbr-db:
    image: postgres:16-alpine
    container_name: hotbox-thbr-db
    restart: unless-stopped
    networks:
      - hotbox-thbr-net
    environment:
      - POSTGRES_USER=hotbox
      - POSTGRES_PASSWORD=${DB_PASSWORD}
      - POSTGRES_DB=hotbox
    volumes:
      - hotbox-thbr-db-data:/var/lib/postgresql/data
    cpus: '1'
    mem_limit: 512m
    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "3"

networks:
  hotbox-thbr-net:
    driver: bridge
    ipam:
      config:
        - subnet: 172.20.0.0/16

volumes:
  hotbox-thbr-db-data:
```

### Docker Operations

#### Provisioning Sequence

1. **Create Docker network** — `docker network create hotbox-{subdomain}-net`
2. **Pull images** — `docker pull hotboxchat/app:latest` and `docker pull postgres:16-alpine` (if not already cached)
3. **Start PostgreSQL** — `docker compose up -d {subdomain}-db`
4. **Wait for DB health** — Poll the database until it accepts connections (max 30 seconds)
5. **Run migrations** — Execute `dotnet ef database update` in a temporary init container
6. **Start HotBox app** — `docker compose up -d {subdomain}-app`
7. **Health check** — Poll the app's `/health` endpoint until it responds with 200 (max 60 seconds)
8. **Update Caddy** — Add a reverse proxy rule to the Caddyfile
9. **Reload Caddy** — Issue an HTTP request to Caddy's admin API to load the new config
10. **Mark as running** — Update instance status to "running"

#### Resource Quotas

If quotas are enabled (per instance or globally), apply Docker resource constraints:

```bash
docker update \
  --cpus {cpuCores} \
  --memory {memoryMb}m \
  hotbox-{subdomain}-app
```

PostgreSQL quota (if enabled) would apply similarly to the `-db` container.

Disk quotas are enforced at the Docker volume level (requires special device mapper setup on the host), so they are **complex** and may be documented separately or omitted from MVP.

---

## DNS & SSL

See `docs/provisioner/dns-setup.md` for detailed DNS and SSL configuration. Summary:

### Wildcard DNS

The provisioner assumes:
- A wildcard A record `*.hotboxchat.ca` pointing to the server's public IP
- A root A record `hotboxchat.ca` pointing to the same IP

### Caddy Integration

Caddy is deployed as a sidecar container and:
- Issues wildcard SSL certificates via Let's Encrypt DNS-01 challenge
- Acts as a reverse proxy, routing subdomains to instance Docker containers
- Automatically reloads when the provisioner adds a new instance

### Caddyfile Management

The provisioner maintains the Caddyfile with a root block for the wildcard domain:

```
*.hotboxchat.ca {
    tls {
        dns namecheap {
            api_key {env.NAMECHEAP_API_KEY}
            user {env.NAMECHEAP_API_USER}
        }
    }

    # Per-instance rules added dynamically by the provisioner
    # Example:
    # @thbr host thbr.hotboxchat.ca
    # handle @thbr {
    #     reverse_proxy hotbox-thbr-app:5000
    # }
}

hotboxchat.ca {
    reverse_proxy localhost:8000  # Provisioner dashboard
}
```

When an instance is created, the provisioner:
1. Calls Caddy's admin API to add the reverse proxy rule
2. Caddy automatically includes it in the TLS handshake for that subdomain
3. The wildcard cert already covers all subdomains, so no new cert is issued

### DNS Provider Abstraction

The provisioner defines an interface for DNS operations (primarily used during setup):

```csharp
public interface IDnsService
{
    Task<bool> ValidateApiCredentialsAsync(string apiKey, string apiUser);
    Task<bool> VerifyDnsRecordsAsync(string baseDomain, string serverIp);
}
```

Implementations exist for Namecheap, Cloudflare, and DigitalOcean. The actual DNS updates (for Let's Encrypt DNS-01) are handled by Caddy's DNS plugins.

---

## Instance Lifecycle

### States

```
┌─────────────────────────────────────────────────────┐
│                                                     │
│  [Provisioning] ──→ [Running] ←─ [Stopped] ←─ [Error]
│        ↓               ↑              ↑
│      [Error]           └──────────────┘
│        ↓
│    [Deleting] ──→ [Deleted]
│
```

### State Definitions

| State | Description | Actions Allowed |
|-------|-------------|-----------------|
| `provisioning` | Docker containers being created/started, migrations running | stop (transitions to stopped) |
| `running` | App and database containers healthy, ready to serve traffic | stop, restart, delete, update |
| `stopped` | Containers exist but are not running | start, delete |
| `error` | Unrecoverable error during provisioning or health check | restart, force-kill, delete, view logs |
| `deleting` | Containers and data being removed | none (read-only state) |

### State Transitions

#### Create Instance

```
(new) → provisioning → running

If any step fails:
provisioning → error
```

#### Start Instance

```
stopped → provisioning → running

If startup fails:
provisioning → error
```

#### Stop Instance

```
running → stopping → stopped
```

#### Restart Instance

```
running → provisioning → running
```

#### Force Kill Instance

```
(any) → error (manual intervention required)
```

#### Delete Instance

```
running/stopped/error → deleting → (record deleted from DB)
```

### Status Message

Each state can have a `statusMessage` field with details:

- `"Pulling Docker images..."`
- `"Running database migrations..."`
- `"Waiting for app to start (45/60 seconds)..."`
- `"Application is healthy and ready"`
- `"Database connection failed: connection timeout"`
- `"User requested force kill"`

---

## Update Mechanism

### One-Click Updates

When an admin clicks "Update" on an instance:

1. **Poll Docker registry** — Fetch the latest image manifest to determine the new version
2. **Compare versions** — If the running instance is already on the latest, return a message "Already up to date"
3. **Pull latest image** — `docker pull hotboxchat/app:latest`
4. **Update Compose file** — Regenerate the compose file (in case config structure changed)
5. **Stop the app** — `docker compose down`
6. **Start the app** — `docker compose up -d hotbox-{subdomain}-app`
7. **Run migrations** — In a temporary container, run `dotnet ef database update`
8. **Health check** — Poll the app's `/health` endpoint (max 60 seconds)
9. **Record the update** — Store a snapshot with old version and new version
10. **Mark as running** — Update instance status and `hotBoxVersion` field

### Version Tracking

The `Instance.HotBoxVersion` field stores the semantic version string (e.g., `"1.2.3"`). The provisioner periodically checks for new versions and updates the UI to notify admins.

### Rollback (Out of Scope for MVP)

Rolling back to a previous version is **not** implemented in MVP. Admins would need to manually manage this or contact support.

---

## Security

### Authentication & Authorization

#### Admin Authentication

- Single seeded admin account during platform setup
- Username + password login with JWT token issuance
- JWT stored in browser `localStorage` (or `sessionStorage` for "remember me" = false)
- All admin endpoints require `Authorization: Bearer {token}` header
- Token includes `sub` (user ID) and `role` claim

#### Role-Based Access Control

| Endpoint | Admin | User |
|----------|-------|------|
| GET /instances | ✓ | ✓ (own only) |
| POST /instances | ✓ | ✓ (if invited) |
| DELETE /instances | ✓ | ✗ (owner cannot delete; only admin) |
| GET /users | ✓ | ✗ |
| POST /users | ✓ | ✗ |
| GET /invites | ✓ | ✗ |
| POST /invites | ✓ | ✗ |
| PUT /platform/config | ✓ | ✗ |

#### Invite Tokens

- Generated as random 10-character alphanumeric strings (e.g., `uh8sX9kL2mP`)
- Tokens are salted/hashed in the database (never stored plaintext)
- Validation checks: token exists, status = "active", not expired, under max uses limit
- Tokens are one-time use or have a use count; after claiming (creating an instance), the invite transitions to `status = "used"`

### Instance Isolation

#### Container Networking

- Each instance runs in its own Docker network (`hotbox-{subdomain}-net`)
- Instances cannot directly reach other instances' containers
- All traffic flows through Caddy on the host

#### Database Isolation

- Each instance has a dedicated PostgreSQL database (separate from provisioner DB)
- PostgreSQL is isolated to the instance's Docker network; no external port exposed
- Database credentials are unique per instance and stored encrypted in the provisioner DB

#### Secrets Management

All sensitive credentials are:
- Encrypted at rest in the provisioner database (using EF Core's `[Encrypted]` attribute or similar)
- Never logged to stdout
- Never sent to client-side code
- Passed to Docker containers via environment variables only

### What the Provisioner Can/Cannot Do

#### The Provisioner CAN:

- Start, stop, restart, and delete instance containers
- Access instance container logs and status
- Execute initialization scripts in containers (migrations, seeding)
- Update the instance's image version
- Access the instance's database (to run migrations)

#### The Provisioner CANNOT:

- Access the instance's in-memory state or user data
- Modify the instance's application code or configuration (beyond env vars)
- Read user credentials or JWTs from the instance
- Bypass the instance's own authentication/authorization
- Intercept instance traffic (Caddy sees HTTPS-encrypted traffic)

### Transport Security

- All provisioner traffic: HTTPS only (TLS 1.3+)
- Caddy enforces HTTPS redirects for instances
- Caddy certificate is auto-renewed via Let's Encrypt
- Instance app communicates with its database over Docker internal network (unencrypted but isolated)

---

## Resource Quotas

### Architecture

Resource quotas are **optional** and controlled globally via `PlatformConfig`:

- `QuotasEnabledCpu` (bool, default: false)
- `QuotasEnabledMemory` (bool, default: false)
- `QuotasEnabledDisk` (bool, default: false)

When enabled, each instance is assigned limits (from `PlatformConfig` defaults or custom per-instance):

- `Instance.Cpu` (int, number of cores, 0 = unlimited)
- `Instance.Memory` (int, MB, 0 = unlimited)
- `Instance.Disk` (int, GB, 0 = unlimited)

### Docker Resource Constraints

CPU and memory limits are enforced at the Docker container level:

```bash
docker update \
  --cpus 1 \
  --memory 512m \
  hotbox-{subdomain}-app
```

These constraints are applied:
- During instance creation (if quotas enabled)
- When editing instance config (admin updates quotas)
- When enabling/disabling quotas globally (applies to all instances)

### Disk Quotas

Disk quotas are **complex** and likely deferred to post-MVP:
- Requires Linux `cgroups v2` and device mapper
- Each instance's Docker volume would need its own quota
- Monitoring disk usage requires reading the volume's filesystem

For MVP, disk limits are **advisory only** (documented but not enforced).

### Monitoring

The provisioner periodically monitors resource usage:

```csharp
// Example telemetry collection (not yet implemented)
public interface IResourceMonitor
{
    Task<ResourceSnapshot> GetInstanceResourcesAsync(string instanceId);
}

public class ResourceSnapshot
{
    public string InstanceId { get; set; }
    public float CpuUsagePercent { get; set; }  // 0-100
    public int MemoryUsageMb { get; set; }
    public int DiskUsageMb { get; set; }
    public DateTime CapturedAt { get; set; }
}
```

Dashboard stats card shows aggregate resource usage.

### Alerts (Future)

When quotas are enabled, the provisioner could alert admins if:
- An instance exceeds CPU limit repeatedly (throttled)
- An instance exceeds memory limit (killed and restarted)
- Disk usage nears the limit

These alerts are **out of scope for MVP**.

---

## UI Routes

### Public Routes (No Auth Required)

```
/                            - Provisioner landing page (redirect to /login if not authenticated)
/login                       - Login form
/invites/{token}/signup      - Invite signup wizard (6-screen flow)
/health                      - System health status (public, for monitoring)
```

### Authenticated Routes (Require JWT)

#### Admin Routes

```
/admin/dashboard             - Dashboard with stats and activity feed
/admin/instances             - Instance list and management
/admin/instances/{id}        - Instance detail and logs
/admin/invites               - Invite token management
/admin/users                 - User management
/admin/settings              - Platform configuration
/admin/settings/setup        - Platform setup wizard (only on first initialization)
```

#### User Routes

```
/user/instances              - List instances owned by this user
/user/instances/{id}         - Instance detail (read-only)
/user/profile                - Edit user profile
/user/logout                 - Logout
```

### Wizards

#### Setup Wizard (First-Run Only)

Accessible at `/admin/settings/setup` when `PlatformConfig.InitializedAt` is null.

6 steps:
1. Prerequisites check (Docker, ports, resources)
2. Domain configuration (base domain, server IP, max instances)
3. DNS provider setup (select provider, enter credentials)
4. SSL/Caddy configuration (review Caddy config)
5. Admin account seeding (username, email, password)
6. Review & initialize (summary, animated init)

#### Instance Setup Wizard (Admin Creates Instance)

Accessible at `/admin/instances/create`.

4 steps:
1. Subdomain selection (with live availability check)
2. Instance admin account (username, email, password)
3. Server settings (registration mode, quotas if enabled)
4. Review & launch (summary, animated provisioning)

#### Invite Signup Wizard (Invited User Creates Instance)

Accessible at `/invites/{token}/signup`.

6 steps:
1. Invite landing page (validate token, show welcome)
2. Account creation (display name, email, password)
3. Instance naming (friendly name + subdomain with availability check)
4. Server configuration (registration mode)
5. Review (summary of all selections)
6. Provisioning (animated sequence)

---

## Configuration

### appsettings.json

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/provisioner-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  },
  "Database": {
    "Provider": "PostgreSQL",
    "Host": "localhost",
    "Port": 5432,
    "Name": "hotbox_provisioner",
    "Username": "hotbox",
    "Password": "***"
  },
  "Jwt": {
    "SecretKey": "***",
    "Issuer": "https://hotboxchat.ca",
    "Audience": "https://hotboxchat.ca",
    "ExpirySeconds": 3600,
    "RefreshTokenExpiryDays": 7
  },
  "ProvisionerOptions": {
    "BaseDomain": "hotboxchat.ca",
    "ServerIpAddress": "192.0.2.1",
    "MaxInstances": 20,
    "DnsProviderType": "namecheap",
    "DnsApiKey": "***",
    "DnsApiUser": "admin",
    "CaddyApiUrl": "http://localhost:2019",
    "DockerSocketPath": "/var/run/docker.sock",
    "HotBoxImageName": "hotboxchat/app:latest",
    "PostgresImageName": "postgres:16-alpine",
    "PostgresPort": 5433,
    "QuotasEnabledCpu": false,
    "QuotasEnabledMemory": false,
    "QuotasEnabledDisk": false,
    "DefaultCpuCores": 1,
    "DefaultMemoryMb": 512,
    "DefaultDiskGb": 20,
    "ProvisioningTimeoutSeconds": 300,
    "HealthCheckIntervalSeconds": 60,
    "HealthCheckTimeoutSeconds": 5
  },
  "AdminSeed": {
    "Username": "admin",
    "Email": "admin@hotboxchat.ca",
    "Password": "***",
    "DisplayName": "Administrator"
  }
}
```

### Environment Variables

All `appsettings.json` settings can be overridden via environment variables:

```bash
DATABASE_HOST=db.example.com
DATABASE_USERNAME=hotbox
DATABASE_PASSWORD=***
JWT_SECRETKEY=***
PROVISIONER_BASEDOMAIN=hotboxchat.ca
PROVISIONER_SERVERIPADDRESS=192.0.2.1
PROVISIONER_DNSAPIKEY=***
PROVISIONER_DNSAPIUSER=admin
CADDY_APIURL=http://caddy:2019
```

### Docker Compose Example

```yaml
version: '3.8'

services:
  provisioner-db:
    image: postgres:16-alpine
    container_name: provisioner-db
    restart: unless-stopped
    environment:
      - POSTGRES_USER=hotbox
      - POSTGRES_PASSWORD=password123
      - POSTGRES_DB=hotbox_provisioner
    volumes:
      - provisioner-db-data:/var/lib/postgresql/data

  provisioner:
    build: .
    container_name: provisioner
    restart: unless-stopped
    ports:
      - "8000:8000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://0.0.0.0:8000
      - DATABASE_HOST=provisioner-db
      - DATABASE_USERNAME=hotbox
      - DATABASE_PASSWORD=password123
      - JWT_SECRETKEY=your-secret-key-here
      - PROVISIONER_BASEDOMAIN=hotboxchat.ca
      - PROVISIONER_SERVERIPADDRESS=192.0.2.1
      - PROVISIONER_DNSAPIKEY=your-namecheap-api-key
      - PROVISIONER_DNSAPIUSER=your-namecheap-username
      - CADDY_APIURL=http://caddy:2019
    depends_on:
      - provisioner-db
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock

  caddy:
    build:
      context: .
      dockerfile: Dockerfile.caddy
    container_name: provisioner-caddy
    restart: unless-stopped
    ports:
      - "80:80"
      - "443:443"
    environment:
      - NAMECHEAP_API_KEY=your-api-key
      - NAMECHEAP_API_USER=your-username
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy-data:/data
      - caddy-config:/config
    depends_on:
      - provisioner

volumes:
  provisioner-db-data:
  caddy-data:
  caddy-config:
```

---

## Out of Scope (MVP)

The following features are **explicitly deferred** from the initial MVP release:

1. **Backups & Disaster Recovery**
   - No automated backup of instance databases
   - No point-in-time restore
   - No backup storage (S3, etc.)
   - Admins must manually backup data if needed

2. **Multi-Admin Roles**
   - Only one seeded admin account
   - No role hierarchy (e.g., "superadmin", "moderator", "viewer")
   - No fine-grained permissions (e.g., "can create instances but not delete")

3. **Horizontal Scaling**
   - Provisioner assumes single-server deployment
   - No load balancing across multiple provisioner instances
   - No distributed state or failover

4. **Instance Migration**
   - No moving instances between servers
   - No clustering or replication across servers

5. **Monitoring & Alerting**
   - No proactive alerts for resource exhaustion
   - No email notifications
   - No integration with external monitoring (Prometheus, Datadog)
   - Dashboard shows current state only

6. **Advanced Networking**
   - No VPN or inter-instance communication
   - No private networks for multi-instance teams
   - All instances are independent

7. **Multi-Tenancy Management**
   - No ability to "archive" instances (soft-delete with data retained)
   - No data export/import
   - No snapshot/clone of instances

8. **Advanced DNS**
   - No support for custom domains per instance (e.g., "chat.mycompany.com")
   - Wildcard DNS only; subdomain pattern is fixed

9. **OAuth for Provisioner**
   - Admin login is username/password only
   - No OAuth to provisioner dashboard (Google, Microsoft, etc.)
   - Invited users must create a provisioner account

10. **Audit Logging**
    - Basic activity snapshots recorded (create, delete, update)
    - No detailed audit trail of all API calls
    - No tamper detection

11. **Disk Quotas**
    - Advisory only, not enforced
    - Volume-level quotas are complex; deferred

12. **Custom Branding for Instances**
    - Each instance is a stock HotBox installation
    - No per-instance logo or color customization
    - No white-label support

13. **Instance-Level Configuration UI**
    - Admins cannot modify instance app settings through the provisioner
    - No UI to change registration mode, invite links, etc. post-creation
    - Instance settings must be changed via the instance's own admin panel

---

## Open Questions

These items are candidates for future discussion or clarification:

1. **Invite Flow Confirmation**
   - After an invited user completes the signup wizard, should the provisioner send an email confirmation? Or just auto-login?
   - Should the invite token be marked "used" immediately after signup, or only after instance provisioning succeeds?

2. **Health Check Frequency**
   - Current proposal: check every 60 seconds per instance. Is this too frequent (unnecessary load) or too infrequent (stale data)?
   - Should health checks be disabled for stopped instances?

3. **Docker Image Tagging Strategy**
   - Proposal: use `hotboxchat/app:latest` for all instances. Should we support version pinning per instance (e.g., `hotboxchat/app:1.2.3`)?
   - Where are Docker images stored? Docker Hub, private registry, or built from source?

4. **Instance Deletion Confirmation**
   - Should the provisioner require a confirmation step (e.g., re-type the instance name) before deletion?
   - Should it keep a "trash" for 30 days before permanently deleting?

5. **Admin Password Reset**
   - How does an admin reset their password if they forget it? Provisioner runs with a single admin account.
   - Should there be an emergency recovery process (e.g., SSH into the server and run a CLI command)?

6. **Invite Expiration Logic**
   - When should invites expire? After a fixed date, or after N days of inactivity?
   - Can a used invite be re-activated by admin?

7. **Instance Failover**
   - If a container crashes, should the provisioner auto-restart it immediately or wait for admin intervention?
   - Current proposal: auto-restart via Docker's `restart: unless-stopped` policy.

8. **Caddyfile Atomicity**
   - How do we safely update the Caddyfile if the provisioner crashes mid-write?
   - Should we use a temporary file and atomic rename?

9. **DNS Validation Timing**
   - The setup wizard validates DNS during initialization. What if DNS setup is incomplete at that point?
   - Should the provisioner continue to check DNS health periodically?

10. **Quotas & Oversubscription**
    - If CPU quotas total 20 cores but the server only has 16 cores, is oversubscription allowed?
    - Should the provisioner prevent instance creation if total quotas would exceed available resources?

11. **Observability**
    - Should the provisioner export metrics to Prometheus/OpenTelemetry?
    - Should instance logs be aggregated in a central logging system (ELK, Datadog)?

12. **Database Upgrades**
    - PostgreSQL version upgrades are complex. Should instances be pinned to a specific Postgres version or always use the latest tag?
    - How do we handle breaking changes in Postgres versions?

13. **Secrets Rotation**
    - How often should instance database passwords be rotated?
    - Should the provisioner support key rotation for JWT secrets?

---

## Implementation Checklist (Reference)

This spec provides the foundation for implementation. Developers should:

- [ ] Create Core projects (`HotBox.Provisioner.Core`, `HotBox.Provisioner.Infrastructure`, `HotBox.Provisioner.Application`)
- [ ] Define entities and migrations per the Data Model section
- [ ] Implement REST API endpoints per the API Surface section
- [ ] Integrate Docker SDK for container orchestration
- [ ] Integrate Caddy API client for reverse proxy management
- [ ] Build DNS provider abstraction for setup wizard
- [ ] Implement three-page flows (Setup Wizard, Instance Wizard, Invite Signup)
- [ ] Build admin dashboard with stats and activity feed
- [ ] Add comprehensive error handling and logging
- [ ] Write unit and integration tests for critical paths
- [ ] Document deployment and operational procedures

---

## Related Documents

- **DNS Setup Guide**: `docs/provisioner/dns-setup.md` — Detailed walkthrough for configuring wildcard DNS and SSL
- **Main HotBox Spec**: `docs/technical-spec.md` — Architecture of the core HotBox application
- **Deployment Docs**: `docs/deployment/` — Deployment procedures for HotBox itself
