# HotBox Provisioner — Architecture & Flow Diagrams

This document contains comprehensive Mermaid diagrams illustrating the architecture, deployment, workflows, and data model of the HotBox Provisioner system.

## 1. System Architecture (C4 Context)

The HotBox Provisioner is a multi-tenant management platform that provisions isolated HotBox chat instances on a single server. Users interact with a Blazor WASM frontend and ASP.NET Core API, which orchestrates Docker containers and manages DNS/SSL via Caddy.

```mermaid
graph TB
    User["👤 User<br/>(Browser)"]
    DNS["🌐 DNS Provider<br/>(Namecheap/Cloudflare)"]

    subgraph Provisioner["HotBox Provisioner"]
        UI["Blazor WASM UI"]
        API["ASP.NET Core API"]
        DB[(PostgreSQL)]
    end

    subgraph Infrastructure["Single Server"]
        Docker["🐳 Docker Engine"]
        Caddy["Caddy<br/>(Reverse Proxy<br/>+ SSL)")
    end

    subgraph Instance1["Instance 1: 'thbr'"]
        HB1["HotBox App"]
        PG1[(PostgreSQL)]
    end

    subgraph Instance2["Instance 2: 'docs'"]
        HB2["HotBox App"]
        PG2[(PostgreSQL)]
    end

    User -->|HTTPS| Browser["Browser"]
    Browser -->|API Calls| Caddy
    Caddy -->|provisioner.hotboxchat.ca| API
    Caddy -->|thbr.hotboxchat.ca| HB1
    Caddy -->|docs.hotboxchat.ca| HB2

    API -->|Read/Write| DB
    API -->|Create/Manage Containers| Docker
    API -->|Update DNS Records| DNS

    Docker -->|Hosts| HB1
    Docker -->|Hosts| HB2
    Docker -->|Hosts| Caddy

    HB1 -->|Query| PG1
    HB2 -->|Query| PG2
```

---

## 2. Infrastructure & Deployment Topology

This diagram shows the single-server layout with Caddy as the central routing layer, the provisioner app with its database, and multiple isolated instance clusters on their own Docker networks.

```mermaid
graph TB
    Internet["☁️ Internet<br/>Port 80/443"]

    subgraph Server["Single VPS/Dedicated Server"]
        Caddy["Caddy<br/>(Ports 80 & 443)<br/>Wildcard SSL & Routing"]

        subgraph ProvNet["Docker Network: provisioner-net"]
            PApp["Provisioner App Container<br/>Port 8000"]
            PDB["Provisioner PostgreSQL<br/>Port 5432"]
        end

        subgraph Inst1Net["Docker Network: instance-thbr-net"]
            App1["HotBox App Container<br/>Port 8080"]
            DB1["PostgreSQL Container<br/>Port 5432"]
        end

        subgraph Inst2Net["Docker Network: instance-docs-net"]
            App2["HotBox App Container<br/>Port 8080"]
            DB2["PostgreSQL Container<br/>Port 5432"]
        end

        Docker["Docker Host"]
    end

    Internet -->|provisioner.hotboxchat.ca| Caddy
    Internet -->|thbr.hotboxchat.ca| Caddy
    Internet -->|docs.hotboxchat.ca| Caddy

    Caddy -->|Route to Port 8000| PApp
    Caddy -->|Route to Port 8080| App1
    Caddy -->|Route to Port 8080| App2

    PApp -->|Connect| PDB
    App1 -->|Connect| DB1
    App2 -->|Connect| DB2

    PApp -->|Manage| Docker
    Docker -->|Isolates| ProvNet
    Docker -->|Isolates| Inst1Net
    Docker -->|Isolates| Inst2Net

    style Server fill:#f5f5f5
    style ProvNet fill:#e3f2fd
    style Inst1Net fill:#f3e5f5
    style Inst2Net fill:#e8f5e9
```

---

## 3. Instance Lifecycle State Diagram

Each provisioned instance transitions through various states during its lifecycle. This state machine tracks the operational status and supports transitions like create, start, stop, update, and delete.

```mermaid
stateDiagram-v2
    [*] --> Provisioning: create()

    Provisioning --> Running: ✓ Success
    Provisioning --> Error: ✗ Failed

    Running --> Stopped: stop()
    Running --> Updating: update()
    Running --> Deleting: delete()

    Updating --> Running: ✓ Complete
    Updating --> Error: ✗ Failed

    Stopped --> Running: start() or restart()
    Stopped --> Deleting: delete()

    Error --> Running: restart()
    Error --> Deleting: delete()
    Error --> Stopped: force_kill()

    Deleting --> Deleted: ✓ Complete
    Deleting --> Error: ✗ Failed

    Deleted --> [*]

    note right of Provisioning
        Creating Docker containers,
        setting up networks,
        running migrations
    end note

    note right of Running
        Healthy, serving traffic
        on assigned subdomain
    end note

    note right of Error
        Creation, update, or
        deletion failed—
        requires manual intervention
    end note
```

---

## 4. Platform Setup Flow (Initialization)

When an administrator first accesses the HotBox Provisioner, a guided setup process configures the platform infrastructure. This sequence shows the steps from prerequisites check through platform initialization.

```mermaid
sequenceDiagram
    participant Admin
    participant UI as Blazor UI
    participant API as ASP.NET API
    participant Docker as Docker Engine
    participant DNS as DNS Provider
    participant Caddy

    Admin->>UI: Navigate to provisioner.hotboxchat.ca
    UI->>API: GET /api/setup/status

    rect rgb(200, 220, 250)
        note over API: Phase 1: Prerequisites Check
        API->>Docker: Check if running
        Docker-->>API: Healthy
        API->>API: Check ports 80/443 available
        API->>API: Check disk space
        API-->>UI: Display prerequisites ✓
    end

    Admin->>UI: Submit domain config<br/>(base_domain, server_ip, max_instances)
    UI->>API: POST /api/setup/config/domain
    API->>API: Validate & store in PlatformConfig
    API-->>UI: Config saved

    rect rgb(220, 250, 220)
        note over API: Phase 2: DNS Provider Setup
        Admin->>UI: Select DNS provider & submit credentials
        UI->>API: POST /api/setup/config/dns
        API->>API: Encrypt & store credentials
        API->>DNS: Test connection & list domains
        DNS-->>API: Connection OK
        API-->>UI: DNS credentials validated ✓
    end

    rect rgb(250, 230, 200)
        note over API: Phase 3: SSL Setup
        Admin->>UI: Configure SSL<br/>(Caddy + DNS-01 challenge)
        UI->>API: POST /api/setup/config/ssl
        API->>API: Generate Caddyfile
        API->>Caddy: Reload configuration
        Caddy->>DNS: Perform DNS-01 challenge
        DNS-->>Caddy: Challenge validated
        Caddy-->>API: Certificate obtained
        API-->>UI: SSL configured ✓
    end

    rect rgb(250, 200, 220)
        note over API: Phase 4: Admin Account
        Admin->>UI: Enter admin username & password
        UI->>API: POST /api/setup/admin/create
        API->>API: Hash password, create admin user
        API-->>UI: Admin account created ✓
    end

    rect rgb(220, 220, 250)
        note over API: Phase 5: Platform Initialization
        Admin->>UI: Click "Initialize Platform"
        UI->>API: POST /api/setup/initialize
        API->>Docker: Create provisioner network
        API->>Caddy: Test Caddyfile validity
        API->>DNS: Create A record for base domain
        API->>API: Mark platform as initialized
        API-->>UI: Platform ready! Redirect to dashboard
    end

    Admin->>UI: Dashboard loaded
```

---

## 5. Instance Provisioning Flow

When a user creates a new chat instance, the provisioner orchestrates Docker, DNS, and database setup. This sequence traces the complete provisioning workflow from validation through health check.

```mermaid
sequenceDiagram
    participant User
    participant UI as Blazor UI
    participant API as ASP.NET API
    participant DB as Provisioner DB
    participant Docker as Docker Engine
    participant DNS as DNS Provider
    participant Caddy
    participant HBApp as HotBox App
    participant HBDb as Instance DB

    User->>UI: Submit instance config<br/>(name, subdomain, registration_mode, resources)

    rect rgb(200, 220, 250)
        note over API: Step 1: Validation
        UI->>API: POST /api/instances
        API->>DB: Check subdomain not in use
        DB-->>API: Available ✓
        API->>API: Validate registration_mode
        API->>API: Check resource quotas
        API-->>UI: Validation OK
    end

    rect rgb(220, 250, 220)
        note over DNS: Step 2: DNS A Record Creation
        API->>DNS: Create A record subdomain.hotboxchat.ca
        DNS-->>API: Record created
        API->>DB: Record instance with status=Provisioning
        DB-->>API: Stored
    end

    rect rgb(250, 230, 200)
        note over Docker: Step 3: Docker Setup
        API->>Docker: Create network instance-{slug}-net
        Docker-->>API: Network created
        API->>Docker: docker-compose pull hotbox image
        Docker-->>API: Image pulled
    end

    rect rgb(250, 200, 220)
        note over Docker: Step 4: PostgreSQL Container
        API->>Docker: docker-compose up (PostgreSQL)
        Docker->>HBDb: Start container
        HBDb-->>Docker: Port 5432 ready
        Docker-->>API: Container running
        API->>HBDb: Poll connection (retry 30s)
        HBDb-->>API: Connected ✓
    end

    rect rgb(220, 220, 250)
        note over Docker: Step 5: HotBox App Container
        API->>Docker: Generate docker-compose with HotBox config
        API->>Docker: docker-compose up (HotBox)
        Docker->>HBApp: Start with config env vars
        HBApp-->>Docker: Port 8080 ready
        Docker-->>API: Container running
    end

    rect rgb(230, 250, 230)
        note over HBApp: Step 6: Database Migrations
        API->>HBApp: Poll /health endpoint
        HBApp->>HBDb: Run EF Core migrations
        HBDb-->>HBApp: Migrations complete
        HBApp-->>API: Health OK
    end

    rect rgb(240, 240, 200)
        note over Caddy: Step 7: Caddy Configuration
        API->>Caddy: Add reverse_proxy rule for subdomain
        Caddy-->>API: Configuration reloaded
    end

    rect rgb(220, 240, 250)
        note over API: Step 8: Health Check
        API->>HBApp: GET /health (final check)
        HBApp-->>API: 200 OK
        API->>DB: Update instance status=Running
        DB-->>API: Updated
    end

    API-->>UI: Instance created successfully
    UI-->>User: Display instance URL & access details
```

---

## 6. Invite Signup & Provisioning Flow

When an invited user clicks their invite link, they are guided through account creation and immediately provisioned with their own instance. This flow combines invite validation, signup, and provisioning.

```mermaid
sequenceDiagram
    participant User as Invited User
    participant UI as Blazor UI
    participant API as ASP.NET API
    participant DB as Provisioner DB

    User->>Browser: Click invite URL<br/>provisioner.hotboxchat.ca/invite?token=xyz
    Browser->>UI: Navigate

    rect rgb(200, 220, 250)
        note over API: Step 1: Validate Invite Token
        UI->>API: GET /api/invites/validate?token=xyz
        API->>DB: Lookup invite by token
        DB-->>API: Invite found
        API->>API: Check not expired (ExpiresAt > now)
        API->>API: Check usage count < max_uses
        API->>API: Check not revoked
        API-->>UI: Invite valid ✓
    end

    rect rgb(220, 250, 220)
        note over UI: Step 2: Signup Form
        UI-->>User: Display signup form<br/>(email, password, instance_name, subdomain, registration_mode)
        User->>UI: Submit signup form
    end

    rect rgb(250, 230, 200)
        note over API: Step 3: Create User Account
        UI->>API: POST /api/auth/signup
        API->>API: Hash password
        API->>DB: Create user record linked to invite
        DB-->>API: User created
        API->>DB: Increment invite usage count
        DB-->>API: Updated
    end

    rect rgb(220, 240, 250)
        note over API: Step 4: Trigger Instance Provisioning
        API->>API: Create instance provisioning request
        API->>API: Call provisioning flow (see Diagram 5)
        note over API: Provisioning runs async<br/>in background
        API-->>UI: Instance creation initiated
    end

    rect rgb(240, 250, 220)
        note over UI: Step 5: Redirect & Wait
        UI-->>User: Show "Instance setting up..." message
        UI->>API: Poll /api/instances/{id}/status
        API->>DB: Check instance status
        DB-->>API: Status=Running
        API-->>UI: Ready
    end

    UI-->>User: Redirect to instance URL<br/>https://subdomain.hotboxchat.ca
    User->>Browser: Load instance
```

---

## 7. One-Click Update Flow

Administrators can update all instances to the latest HotBox version. The provisioner checks for newer images and coordinates rolling updates with health checks.

```mermaid
sequenceDiagram
    participant Admin
    participant UI as Blazor UI
    participant API as ASP.NET API
    participant DB as Provisioner DB
    participant Docker as Docker Engine
    participant Registry as Docker Registry
    participant HBApp as HotBox Instance

    Admin->>UI: Navigate to admin panel
    UI->>API: GET /api/admin/updates/check

    rect rgb(200, 220, 250)
        note over Docker: Step 1: Check for Updates
        API->>Registry: Fetch latest HotBox image manifest
        Registry-->>API: Latest digest & version
        API->>DB: Fetch all instances
        DB-->>API: Instance list with current image digests
        API->>API: Compare running vs. latest digests
        API-->>UI: Display available update<br/>(current vs. new version)
    end

    Admin->>UI: Click "Update All Instances"

    rect rgb(220, 250, 220)
        note over API: Step 2: Prepare Update
        UI->>API: POST /api/admin/updates/apply
        API->>Docker: docker pull latest hotbox image
        Docker->>Registry: Pull new image
        Registry-->>Docker: Image downloaded
        Docker-->>API: Image ready
    end

    loop For each instance (sequential)
        rect rgb(250, 230, 200)
            note over API: Step 3: Update Instance
            API->>DB: Mark instance status=Updating
            DB-->>API: Updated
            API->>Docker: docker-compose down<br/>for instance container
            Docker->>HBApp: Stop container gracefully
            HBApp-->>Docker: Stopped
            Docker-->>API: Container removed
        end

        rect rgb(250, 200, 220)
            note over Docker: Step 4: Start New Container
            API->>Docker: docker-compose up with new image
            Docker->>HBApp: Start container with new image
            HBApp-->>Docker: Port 8080 ready
            Docker-->>API: Container running
        end

        rect rgb(220, 220, 250)
            note over API: Step 5: Health Check
            API->>HBApp: Poll /health endpoint (30s retry)
            HBApp-->>API: 200 OK — healthy
            API->>DB: Update instance status=Running<br/>+ new image digest & version
            DB-->>API: Updated
            API-->>UI: Instance updated ✓
        end
    end

    API-->>UI: All instances updated successfully
    UI-->>Admin: Display completion summary
```

---

## 8. Data Model (Entity-Relationship Diagram)

The provisioner's data model captures platform configuration, instances, users, invites, and activity logs.

```mermaid
erDiagram
    PLATFORMCONFIG ||--o{ ADMIN : has
    PLATFORMCONFIG ||--o{ INSTANCE : contains

    ADMIN ||--o{ INVITE : creates
    ADMIN ||--o{ ACTIVITYLOG : performs

    INVITE ||--o{ USER : grants
    INVITE ||--o{ INSTANCE : provisions

    INSTANCE ||--o{ USER : has
    INSTANCE ||--o{ ACTIVITYLOG : subject_of

    USER ||--o{ ACTIVITYLOG : performs

    PLATFORMCONFIG {
        string Id PK "UUID"
        string BaseDomain "e.g., hotboxchat.ca"
        string ServerIp "Public IP of server"
        int MaxInstances "Resource limit"
        string DnsProvider "namecheap|cloudflare"
        string DnsCredentialsEncrypted "Encrypted API key"
        text CaddyConfig "Full Caddyfile content"
        boolean IsInitialized "Platform setup complete"
        datetime CreatedAt
        datetime UpdatedAt
    }

    ADMIN {
        string Id PK "UUID"
        string Username "Unique username"
        string Email
        string PasswordHash "bcrypt hash"
        datetime CreatedAt
    }

    INSTANCE {
        string Id PK "UUID"
        string Name "Display name"
        string Subdomain "Unique, e.g., 'thbr'"
        string Status "Provisioning|Running|Stopped|Deleting|Deleted|Error"
        string OwnerId FK "FK → User.Id"
        string AdminUsername "Default admin for this instance"
        string RegistrationMode "open|invite_only|closed"
        text DockerComposeConfig "Full docker-compose.yml"
        string ImageVersion "e.g., v1.2.3"
        string ImageDigest "Docker image SHA digest"
        string CpuQuota "e.g., 1.0"
        string MemoryQuota "e.g., 512M"
        string DiskQuota "e.g., 5G"
        datetime CreatedAt
        datetime UpdatedAt
        datetime LastHealthCheck
    }

    USER {
        string Id PK "UUID"
        string DisplayName
        string Email
        string PasswordHash "bcrypt hash"
        string InviteId FK "FK → Invite.Id (nullable)"
        string InstanceId FK "FK → Instance.Id"
        datetime CreatedAt
    }

    INVITE {
        string Id PK "UUID"
        string Token "Random secure token"
        string CreatedById FK "FK → Admin.Id"
        int MaxUses "How many times can be used"
        int UseCount "Current usage count"
        datetime ExpiresAt "Expiration timestamp"
        datetime RevokedAt "Revocation timestamp (nullable)"
        datetime CreatedAt
    }

    ACTIVITYLOG {
        string Id PK "UUID"
        string Action "create|update|delete|start|stop|restart"
        string EntityType "Instance|User|Invite|Platform"
        string EntityId "The affected resource ID"
        string ActorId FK "FK → User/Admin.Id (nullable)"
        text Details "JSON with additional context"
        datetime CreatedAt
    }
```

---

## 9. Request Routing Diagram (Caddy Reverse Proxy)

Every HTTP/HTTPS request enters through Caddy, which routes based on the hostname. This flowchart shows the routing logic for provisioner requests, instance requests, and error cases.

```mermaid
flowchart TD
    Request["HTTP/HTTPS Request<br/>Caddy Port 80/443"]

    Request --> ExtractHost{"Extract hostname<br/>from request"}

    ExtractHost --> IsProv{"Is provisioner<br/>base domain?<br/>provisioner.hotboxchat.ca"}

    IsProv -->|Yes| ProvRoute["Route to Provisioner App<br/>Container Port 8000"]
    IsProv -->|No| IsInst{"Is subdomain<br/>under base domain?<br/>*.hotboxchat.ca"}

    IsInst -->|Yes| ExtractSub["Extract subdomain<br/>e.g., 'thbr' from<br/>thbr.hotboxchat.ca"]
    IsInst -->|No| NotFound["Return 404"]

    ExtractSub --> LookupDB["Query Provisioner DB<br/>for Instance by subdomain"]

    LookupDB --> FoundInst{"Instance<br/>found &<br/>Running?"}

    FoundInst -->|Yes| GetPort["Get instance container<br/>port from config"]
    FoundInst -->|No| NotFound

    GetPort --> InstanceRoute["Route to Instance Container<br/>Port 8080<br/>on isolated Docker network"]

    ProvRoute --> Response["Return Response"]
    InstanceRoute --> Response
    NotFound --> Response

    style Request fill:#e3f2fd
    style ProvRoute fill:#c8e6c9
    style InstanceRoute fill:#f3e5f5
    style NotFound fill:#ffcdd2
    style Response fill:#fff9c4
```

---

## 10. Docker Network Topology

The provisioner uses Docker networks to isolate infrastructure and instances. Each instance gets its own network connecting its HotBox app and PostgreSQL containers, while Caddy connects to all networks for routing.

```mermaid
graph TB
    subgraph DockerHost["Docker Host"]
        Caddy["Caddy<br/>(Multi-network joined)<br/>Routes traffic"]

        subgraph ProvNet["provisioner-net<br/>(Bridge Network)"]
            ProvApp["Provisioner App<br/>hotboxchat-provisioner:8000"]
            ProvDB["Provisioner PostgreSQL<br/>hotboxchat-postgres:5432"]
            ProvApp -->|localhost:5432| ProvDB
        end

        subgraph ThbrNet["instance-thbr-net<br/>(Bridge Network)<br/>Isolated"]
            ThbrApp["HotBox App<br/>hotbox-thbr:8080"]
            ThbrDB["PostgreSQL<br/>postgres-thbr:5432"]
            ThbrApp -->|localhost:5432| ThbrDB
        end

        subgraph DocsNet["instance-docs-net<br/>(Bridge Network)<br/>Isolated"]
            DocsApp["HotBox App<br/>hotbox-docs:8080"]
            DocsDB["PostgreSQL<br/>postgres-docs:5432"]
            DocsApp -->|localhost:5432| DocsDB
        end

        Caddy -->|Joined to provisioner-net| ProvApp
        Caddy -->|Joined to instance-thbr-net| ThbrApp
        Caddy -->|Joined to instance-docs-net| DocsApp
    end

    Internet["☁️ Internet"]
    Internet -->|provisioner.hotboxchat.ca| Caddy
    Internet -->|thbr.hotboxchat.ca| Caddy
    Internet -->|docs.hotboxchat.ca| Caddy

    style DockerHost fill:#f5f5f5
    style ProvNet fill:#e3f2fd
    style ThbrNet fill:#f3e5f5
    style DocsNet fill:#e8f5e9
    style Caddy fill:#ffecb3
```

---

## Key Design Principles

1. **Isolation**: Each instance runs on its own Docker network with dedicated PostgreSQL, ensuring multi-tenancy isolation.

2. **Single Entry Point**: Caddy serves as the sole reverse proxy, handling SSL termination, wildcard certificates, and routing.

3. **DNS-01 Challenge**: SSL certificates are provisioned automatically via DNS-01 validation, requiring no manual certificate management.

4. **Stateless Provisioning**: The provisioner stores all instance configuration in docker-compose files, making deployment idempotent.

5. **Health-Driven Orchestration**: All async operations (provisioning, updates) use health checks to verify readiness before marking state changes.

6. **Encrypted Secrets**: DNS provider credentials and other sensitive data are encrypted at rest in the provisioner database.

7. **Audit Trail**: All administrative actions (create, update, delete) are logged in ActivityLog for compliance and debugging.

---

## Related Documentation

- **[DNS Setup Guide](./dns-setup.md)** — Detailed steps for configuring Namecheap or Cloudflare DNS with Caddy
- **[Deployment Guide](../deployment/)** — Instructions for deploying the provisioner and instances
- **[Configuration Reference](../architecture/configuration-reference.md)** — Environment variables and settings

---

*Last updated: 2026-03-10*
