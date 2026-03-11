# Business Requirements Document: HotBox Provisioner

**Version**: 1.0
**Date**: 2026-03-10
**Status**: Approved
**Document Type**: Business Requirements Document (BRD)

---

## Executive Summary

The **HotBox Provisioner** is a new self-contained application that enables administrators to provision, manage, and operate multiple isolated HotBox chat instances on a single server. It solves the deployment friction for organizations and individuals who want to run HotBox without managing infrastructure manually.

By automating Docker container orchestration, DNS routing, SSL certificate management, and instance lifecycle, the Provisioner democratizes HotBox hosting — allowing anyone to offer HotBox as a service or run multiple community instances from a single VPS. This is a critical stepping stone toward a sustainable open-source ecosystem around HotBox, enabling forks to build commercial hosting businesses on top of the platform.

The Provisioner targets small operators (5-10 instances per server) running on a single server (Docker host), not hyperscale cloud platforms. It prioritizes ease of setup, operational simplicity, and self-hosting sovereignty.

---

## Business Objectives

1. **Remove Infrastructure Barriers**: Eliminate manual Docker/networking/SSL setup so anyone can host HotBox instances without deep DevOps knowledge.

2. **Enable Hosting Businesses**: Provide a fork-friendly foundation for others to build commercial or community hosting services on top of HotBox, driving adoption.

3. **Reduce Support Burden**: Shift deployment complexity from HotBox maintainers to a proven, documented tool, freeing maintainers to focus on the core chat product.

4. **Accelerate Time-to-Instance**: Cut instance creation time from hours (manual setup) to minutes (automated provisioning + guided setup wizard).

5. **Open Source First**: Keep the Provisioner MIT-licensed and fork-friendly, reinforcing HotBox's ethos of user sovereignty and open-source community.

---

## Stakeholders

### 1. HotBox Maintainers
- **Benefit**: Reduced support burden; Provisioner becomes the canonical deployment path for non-containerized users
- **Responsibility**: Validate Provisioner architecture; ensure it doesn't introduce security regressions to main HotBox app

### 2. Server Operators / Hosting Providers
- **Benefit**: Simple, documented way to operate multiple HotBox instances; CLI/API for automation; dashboard for visibility
- **Responsibility**: Provide DNS setup and VPS infrastructure; manage admin account credentials securely

### 3. Instance Administrators
- **Benefit**: Invite-based onboarding; guided setup wizard; access to their own HotBox instance without infrastructure knowledge
- **Responsibility**: Complete signup flow; configure initial instance preferences (name, role settings, etc.)

### 4. End Users
- **Benefit**: Easier access to HotBox instances; no need for self-hosting; faster instance creation
- **Responsibility**: Use instances normally once provisioned

### 5. Fork Authors / Commercial Hosting Businesses
- **Benefit**: Proven, auditable codebase to fork and commercialize; reference implementation for feature additions
- **Responsibility**: Maintain their own fork; support their customers; contribute improvements back if desired

---

## Scope Definition

### In Scope: MVP Features

#### Instance Management
- **BR-001**: Provisioner admin can create new HotBox instances via dashboard or API
- **BR-002**: Each instance runs in its own Docker container (app + dedicated PostgreSQL)
- **BR-003**: Instances are accessible via auto-generated subdomains (e.g., `mycompany.hotbox.ca`)
- **BR-004**: Admin can list, inspect, pause/resume, and delete instances
- **BR-005**: Instance creation includes automatic PostgreSQL database initialization and migration

#### User Access & Onboarding
- **BR-006**: Provisioner admin creates invite URLs (single-use or multi-use with expiry)
- **BR-007**: Invited users access a guided signup wizard (email, password, instance preferences)
- **BR-008**: Wizard automatically creates the user in the target HotBox instance and logs them in
- **BR-009**: Failed invites are traced (expired, used, etc.); admin can resend or revoke invites

#### Platform Administration
- **BR-010**: Single seeded admin account during Provisioner setup (environment variable or first-run wizard)
- **BR-011**: Admin dashboard shows instance metrics (container status, resource usage, user count)
- **BR-012**: Admin can configure platform-level settings (domain suffix, SMTP for emails, registration modes per instance)
- **BR-013**: Audit log for administrative actions (instance creation, deletion, user invites)

#### Infrastructure & Networking
- **BR-014**: Automatic wildcard DNS integration (Namecheap API or Cloudflare; extensible to others)
- **BR-015**: Automatic wildcard SSL certificate provisioning (Let's Encrypt via Caddy)
- **BR-016**: Caddy reverse proxy routes traffic to correct instance based on subdomain
- **BR-017**: Docker Compose orchestration; all instances on one Docker host
- **BR-018**: Environmental configuration via `.env` and `appsettings.json` (no hardcoded secrets)

#### Security
- **BR-019**: Admin credentials stored securely (hashed, salted); no plaintext passwords in logs
- **BR-020**: Invite URLs are cryptographically random, single-use by default, time-limited
- **BR-021**: HTTPS enforced for all communication (Caddy termination)
- **BR-022**: Database credentials per instance (not shared); each instance isolated
- **BR-023**: JWT authentication for API; can be disabled if only dashboard needed

### Out of Scope: Future / Post-MVP

- **Backups & Disaster Recovery**: Manual backups or cloud storage integration (post-MVP)
- **Multi-Admin Roles**: Only single admin in MVP; role-based admin access post-MVP
- **Horizontal Scaling**: No clustering, load balancing, or distributed storage; single-server only
- **Instance Migration**: No live migration between servers (post-MVP)
- **Billing & Payments**: No payment processing, subscription tiers, or metering (post-MVP)
- **OAuth for Provisioner Admin**: Admin login is email/password; OAuth for instance users is HotBox's responsibility
- **Custom Domains**: No support for CNAME-based custom domains (only subdomains)
- **High Availability**: No redundancy, failover, or HA features (single point of failure acceptable for MVP)
- **Advanced Monitoring**: No integration with Prometheus, Datadog, etc.; basic metrics only
- **Terraform / IaC**: No infrastructure-as-code templates (post-MVP, community-driven)
- **Multi-Database Types**: PostgreSQL only in MVP; MySQL/SQLite support post-MVP

---

## Business Requirements

### Instance Management

**BR-001: Instance Creation**
- Admin can create a new HotBox instance from the dashboard or API endpoint
- Input: Instance name, optional description, initial admin email, HotBox version (defaults to latest)
- Output: Instance created, assigned a unique subdomain, database initialized, container started
- Success criterion: Instance is accessible via HTTPS within 2 minutes of creation

**BR-002: Dedicated Container & Database**
- Each instance runs in its own Docker container (separate app + PostgreSQL)
- Containers are named predictably (e.g., `hotbox-instance-name`, `hotbox-instance-name-db`)
- Databases are isolated by name (e.g., `hotbox_instance_name`) and credentials
- Success criterion: Multiple instances can run simultaneously without data or resource leaks

**BR-003: Subdomain Assignment**
- Instance names are slugified and mapped to subdomains automatically (e.g., "My Company" → `my-company.hotbox.ca`)
- Admin can view assigned subdomain in instance details
- Subdomains are globally unique across the Provisioner instance (no duplicates allowed)
- Success criterion: User can visit `https://instance-subdomain.hotbox.ca` and access the instance

**BR-004: Instance Lifecycle**
- Admin can:
  - **View**: List all instances with status (running, stopped, error), creation date, user count
  - **Inspect**: View details (subdomain, container ID, database size, last action timestamp)
  - **Pause**: Stop the instance container (user cannot access; data persists)
  - **Resume**: Restart a paused instance
  - **Delete**: Permanently remove instance (container + database); requires confirmation
- Success criterion: All lifecycle operations complete within 30 seconds and provide clear feedback

**BR-005: Database Initialization**
- When an instance is created, Provisioner automatically:
  1. Creates a PostgreSQL database with schema for the HotBox version
  2. Runs EF Core migrations
  3. Seeds initial admin user from invite flow
  4. Marks database as ready
- Success criterion: Instance is fully operational (no "database not initialized" errors)

### User Access & Onboarding

**BR-006: Invite Generation**
- Admin can generate invite URLs from the dashboard or API
- Invite parameters:
  - **Target instance** (required): Which instance the invite grants access to
  - **Single-use**: Invite is consumed after one signup (default: ON)
  - **Multi-use with limit**: Invite can be used N times before expiring (e.g., 5 uses)
  - **Expiry**: Invite expires after X days (default: 7 days; configurable 1-90 days)
  - **Email restrictions** (optional): Invite only valid for specific email or domain
- Invite URL format: `https://hotbox.ca/invite/{TOKEN}`
- Success criterion: Admin can generate, view, and revoke invites; invites work as specified

**BR-007: Invite Flow & Signup Wizard**
- When user visits an invite URL:
  1. Provisioner validates invite (not expired, not consumed, email restriction passes)
  2. Wizard step 1: Enter email and password (password policy: ≥8 chars, 1 number, 1 upper/lower)
  3. Wizard step 2: Instance preferences (optional; e.g., username, role request if instance allows)
  4. Wizard step 3: Confirmation (summary of email, instance, preferences)
- Success criterion: User completes wizard and is logged into their instance within 2 minutes

**BR-008: User Provisioning**
- After wizard completion, Provisioner:
  1. Marks invite as consumed (if single-use)
  2. Creates user account in the target HotBox instance via API
  3. Issues JWT token for immediate login (session maintained during redirect)
  4. Redirects to instance (e.g., `https://my-company.hotbox.ca`)
  5. Instance loads with user already logged in
- Success criterion: User lands in instance chat, no additional login required

**BR-009: Invite Tracking & Troubleshooting**
- Admin can:
  - View invite status (pending, consumed, expired, revoked)
  - See who used an invite (email, timestamp, which user account created)
  - Resend invite URLs (generate new token if old expired)
  - Revoke invites (prevent future use)
- Success criterion: Admin can diagnose failed invites and take corrective action

### Platform Administration

**BR-010: Single Admin Seeding**
- On first Provisioner startup, if no admin exists:
  - Option A: Admin credentials passed via environment variables (`PROVISIONER_ADMIN_EMAIL`, `PROVISIONER_ADMIN_PASSWORD_HASH`)
  - Option B: First-run wizard (Provisioner shows one-time setup page requiring email + password)
- Admin account is the only initial user; further users are added via invite
- Success criterion: Admin can log in to Provisioner dashboard after setup; setup is idempotent

**BR-011: Admin Dashboard & Metrics**
- Dashboard displays:
  - **Overview**: Total instances, running/stopped counts, total users across instances, uptime
  - **Instance list**: Table with columns (Name, Subdomain, Status, Users, Created, Actions)
  - **Container metrics**: CPU %, memory %, disk usage per instance (read from Docker API)
  - **Recent activity**: List of last 20 actions (instance creation, deletion, user invite, etc.)
- Refresh interval: Real-time (WebSocket) or on-demand (button click)
- Success criterion: Dashboard loads in <1 second; metrics refresh within 5 seconds

**BR-012: Platform Configuration**
- Admin can configure:
  - **Domain suffix**: Base domain for subdomains (e.g., `hotbox.ca`; default from deployment)
  - **SMTP settings**: For sending invite emails (optional; defaults to no-op)
  - **Registration mode (per instance)**: Open, invite-only, or closed (seeded via instance creation)
  - **Max instances**: Hard cap on how many instances can be created (default: unlimited; configurable)
  - **Resource limits**: Per-instance CPU/memory constraints (passed to Docker; default: unlimited)
- Configuration persisted in database; editable via dashboard UI or API
- Changes take effect immediately or on next instance creation (as applicable)
- Success criterion: Admin can change settings and see them reflected

**BR-013: Audit Log**
- Every administrative action is logged:
  - **Timestamp**, **admin user**, **action type**, **details** (instance name, user email, old/new values)
  - Action types: instance create/delete/pause/resume, invite generate/consume/revoke, config change, admin login/logout
- Audit log is immutable and retained indefinitely (or per retention policy)
- Admin can:
  - View audit log filtered by date range, action type, or resource
  - Export log as CSV for external analysis
  - Search log by keyword
- Success criterion: Any administrative action is traceable to who did it and when

### Infrastructure & Networking

**BR-014: Wildcard DNS Integration**
- Provisioner integrates with DNS provider API to create/delete DNS records automatically
- Supported providers (MVP): Namecheap, Cloudflare (extensible architecture for others)
- On instance creation: Auto-creates A record for subdomain (e.g., `myinstance.hotbox.ca → 192.0.2.1`)
- On instance deletion: Auto-deletes corresponding A record
- DNS changes are idempotent (no errors if record already exists)
- Success criterion: User can reach instance subdomain without manual DNS setup

**BR-015: Automatic SSL Certificate Provisioning**
- Provisioner uses Caddy reverse proxy to provision wildcard Let's Encrypt certificates automatically
- Certificates cover `*.hotbox.ca` (all subdomains)
- Caddy automatically renews certificates before expiry (standard ACME renewal)
- Certificates are stored in persistent Docker volume (survives container restarts)
- HTTPS is enforced; all HTTP traffic redirects to HTTPS
- Success criterion: User visits instance URL in browser; certificate is valid (no warnings)

**BR-016: Traffic Routing**
- Caddy reverse proxy receives all HTTPS traffic on ports 80/443
- For each request:
  1. Extract subdomain from `Host` header
  2. Look up corresponding instance container
  3. Forward request to instance container (port 8080 or configurable)
  4. Return response to client
- Base domain (`hotbox.ca`) routes to Provisioner dashboard
- Unknown subdomains return 404 with helpful message
- Success criterion: Traffic to `instance1.hotbox.ca` reaches instance1; traffic to `instance2.hotbox.ca` reaches instance2

**BR-017: Docker Compose Orchestration**
- Provisioner uses Docker Compose to manage containers
- Services: Provisioner app, Caddy reverse proxy, optional: Seq (logging)
- On instance creation: Dynamically generate and apply new service definitions (or mount volumes)
- Provisioner communicates with Docker daemon via Docker API (socket mount)
- Containers auto-restart on reboot (restart policy: `unless-stopped`)
- Volumes persist data across container restarts
- Success criterion: All containers start/stop cleanly; data persists; instance survives host reboot

**BR-018: Configuration & Secrets Management**
- Configuration via environment variables and `appsettings.json`
- Sensitive data (DB passwords, API keys, JWT secret) stored in `.env` file (never committed to git)
- Provisioner reads `.env` and injects into Docker containers as environment variables
- No hardcoded secrets in source code or logs
- Configuration is validated on startup; Provisioner fails with clear error if required config missing
- Success criterion: Provisioner starts with correct environment; no secrets in logs

### Security

**BR-019: Credential Security**
- Provisioner admin password is hashed (bcrypt or Argon2) with salt
- Passwords are never logged, even in debug mode
- Password reset is admin-only (no self-service for MVP)
- Session tokens (JWT) are cryptographically random, ≥256 bits of entropy
- Success criterion: Password hashes cannot be reversed; brute force is computationally infeasible

**BR-020: Invite Token Security**
- Invite tokens are cryptographically random, ≥128 bits of entropy
- Tokens are hashed when stored in database (like passwords)
- Tokens are single-use by default; consumed immediately after signup
- Multi-use invites include counter (max uses) and are checked on each use
- Invites include creation timestamp and expiry timestamp; stale invites are rejected
- Expired invites are cleaned up from database (optional scheduled task)
- Success criterion: No brute-force attack can guess a valid invite; tokens are one-time use

**BR-021: HTTPS Enforcement**
- All Provisioner and instance traffic uses HTTPS (port 443)
- HTTP (port 80) redirects to HTTPS
- HSTS header is set (mandatory HTTPS for future requests)
- TLS 1.2+ only (no legacy SSL)
- Strong cipher suites (no weak algorithms)
- Success criterion: Browser shows padlock; visiting `http://instance.hotbox.ca` redirects to HTTPS

**BR-022: Data Isolation**
- Each instance has:
  - Separate PostgreSQL database (unique name, unique credentials)
  - Separate Docker container (isolated filesystem, separate environment)
  - Separate JWT secret (instance cannot forge tokens for another instance)
- Cross-instance user enumeration is impossible without database access
- One instance compromise does not leak data from other instances
- Success criterion: Database credentials for instance A cannot access instance B's data

**BR-023: API Authentication**
- Provisioner API endpoints (instance CRUD, invite management) require Bearer token authentication (JWT)
- JWT includes claims (admin user ID, issue time, expiry)
- Tokens expire after configurable duration (default: 24 hours)
- Token refresh endpoint allows renewing expired tokens (if refresh token provided)
- If API access not needed (dashboard-only deployment), JWT can be disabled via config
- Success criterion: Unauthenticated API calls return 401; authenticated calls work as expected

---

## Success Criteria

The HotBox Provisioner is considered successful when:

1. **Operational Efficiency**: An operator can create a new instance, add users, and have them logging in within 5 minutes (no manual DNS/SSL/Docker work)

2. **Security Posture**: Independent security audit finds no critical/high vulnerabilities in Provisioner auth, invite flow, or data isolation

3. **Reliability**: 99% uptime SLA for a single-instance deployment; instances survive host reboots; no data loss

4. **Usability**: Non-technical admin (no Kubernetes/DevOps experience) can set up Provisioner from zero to running in <30 minutes using documentation

5. **Adoption**: At least 3 third-party forks exist in the wild; at least 1 commercial hosting business built on top of forked Provisioner

6. **Documentation**: Complete deployment guide, API reference, and troubleshooting guide are available and reviewed by at least 2 operators outside the maintainer team

7. **Scalability (MVP Bound)**: Provisioner can orchestrate 10 instances on a single 2-vCPU / 4GB RAM VPS without performance degradation (instances run at acceptable performance)

---

## Constraints & Assumptions

### Constraints

| Constraint | Impact | Rationale |
|-----------|--------|-----------|
| **Single server only** | No horizontal scaling, no HA | MVP scope; simplifies architecture significantly |
| **Max 10 instances per server** | Soft guideline, not enforced | Resource limits on typical VPS (2-4 vCPU, 4-8 GB RAM) |
| **Single admin (MVP)** | No delegation of instance management | Simpler auth model; multi-admin post-MVP |
| **PostgreSQL only (MVP)** | No MySQL/SQLite for instances | Reduces testing matrix; PostgreSQL is production-grade |
| **Subdomains only (MVP)** | No custom domain support | Wildcard DNS easier than CNAME validation per domain |
| **Time zone: UTC** | Admin actions logged in UTC; emails sent in UTC | Avoids time zone complexity; operators can interpret locally |

### Assumptions

| Assumption | Implication | Verification |
|-----------|-----------|--------|
| **Admin is trusted** | No multi-factor auth, no session limits | Single admin model; compromised credentials are critical |
| **DNS provider API is reliable** | Occasional DNS propagation delays are acceptable | Can be mitigated with retry logic and error messages |
| **Host has internet access** | Let's Encrypt cert provisioning, DNS updates, docker pull | Required; can be mitigated with offline cert loading (post-MVP) |
| **Docker daemon is available** | Provisioner cannot start without Docker | Docker is installed and socket is mounted |
| **Invites are distributed securely** | Admin is responsible for secure delivery of invite URLs | Admin should use secure channels (not cleartext email or Slack) |
| **Operator has static server IP** | Wildcard DNS points to fixed IP | Standard for any hosting scenario |

---

## Dependencies

### Internal Dependencies (HotBox Project)
- **HotBox Core App**: Provisioner deploys HotBox instances as Docker containers; depends on stable HotBox releases
- **HotBox PostgreSQL Schema**: Provisioner runs EF Core migrations; depends on migration stability
- **HotBox Configuration Format**: Provisioner configures HotBox via environment variables; depends on documented config contract

### External Dependencies
- **Docker & Docker Compose**: Container runtime; assumed installed on host
- **Caddy**: Reverse proxy; used for SSL/DNS; requires Caddy 2.x+
- **PostgreSQL**: Database for instances; assumed running or included in Docker Compose
- **Let's Encrypt**: Free SSL certificates; requires outbound HTTPS access from host
- **DNS Provider APIs**: Namecheap / Cloudflare (or custom provider); requires API credentials
- **SMTP Server** (optional): For sending invite emails; can be external service or localhost

### Deployment Dependencies
- **VPS or dedicated server**: With Docker installed, ≥2 vCPU, ≥4 GB RAM, ≥50 GB disk
- **Domain name**: Owned/registered, nameservers pointing to DNS provider
- **Static public IP**: For DNS A records
- **SSH access**: For initial setup and troubleshooting

---

## Risks & Mitigation

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| **DNS propagation delays** | Medium | Users cannot access instance for minutes; support burden | Implement DNS pre-check in UI; show user propagation status; document TTL expectations |
| **Docker resource exhaustion** | Medium | Multiple instances crash; host becomes unresponsive | Implement per-instance resource limits (CPU, memory); add host monitoring alerts; document sizing guidelines |
| **Let's Encrypt rate limits** | Low | Certificate provisioning fails during testing | Use Let's Encrypt staging CA for dev/testing; document rate limit best practices; implement exponential backoff |
| **Database corruption on instance delete** | Low | Data loss; difficult to recover | Implement graceful shutdown sequence; pre-delete backup (optional post-MVP); test deletion process thoroughly |
| **Credential leaks in logs** | Low | Admin passwords/API keys exposed in plain text | Implement secret redaction in logging; regular log audits; use structured logging with guard rails |
| **Cross-instance JWT forgery** | Low | One user can impersonate user in another instance | Use unique JWT secret per instance; validate signature in instance; document JWT validation |

### Operational Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| **Admin account compromise** | Medium | Attacker can create/delete instances, generate invites, steal data | Enforce strong password policy; document secure credential storage; recommend SSH key auth for deployment |
| **DNS provider API outage** | Low | New instances cannot get DNS records; Caddy cert renewal fails | Implement offline cert loading (post-MVP); document fallback (manual DNS); add health checks for API |
| **Single point of failure (host)** | High | All instances down; no redundancy | Accept as MVP scope; document backup strategy; post-MVP: multi-region deployment |
| **No backup strategy** | High | Data loss on disk failure | Implement pre-delete snapshot (optional); document external backup strategy (post-MVP); provide `docker volume inspect` guidance |
| **Support burden from failed setups** | Medium | Unmaintainable documentation/issues; project strains | Write comprehensive setup guide; test guide with non-technical user; create troubleshooting checklist |

### Business Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| **Limited adoption / fork fatigue** | Medium | Low user growth; forks diverge from main project | Maintain high code quality; active maintainers; clear governance; encourage upstream contributions |
| **Forked Provisioners diverge** | Medium | Fractured ecosystem; support burden across variants | Use permissive license (MIT); provide upgrade path; keep core small; document extension points |
| **Unexpected usage patterns** | Medium | 50-instance deployments kill assumption of single-server | Document MVP scope clearly; monitor deployed instances; gather feedback; plan post-MVP scaling |
| **HotBox core breaking changes** | Low | Provisioner must be updated for every HotBox release | Semantic versioning; documented migration path; compatibility testing |

### Mitigation Strategy Summary

- **Security**: Regular audits, credential redaction, per-instance isolation, strong crypto
- **Reliability**: Health checks, graceful degradation, clear error messages, documentation
- **Operations**: Comprehensive docs, runbooks for common issues, monitoring alerts, logs
- **Community**: Open governance, clear scope/roadmap, welcoming to contributions, responsive maintainers

---

## Future Roadmap (Post-MVP)

The following features are **explicitly out of scope for MVP** but are planned for future releases:

- **Backup & Restore**: Automated daily backups, point-in-time restore, cloud storage integration
- **Multi-Admin Roles**: Role-based access control (admin, operator, observer)
- **Instance Migration**: Live migration between servers, zero-downtime upgrades
- **Horizontal Scaling**: Multi-server orchestration, load balancing, distributed storage
- **Billing & Metering**: Usage tracking, subscription tiers, payment processing
- **Advanced Monitoring**: Prometheus/Grafana integration, APM, custom alerts
- **Terraform Modules**: Infrastructure-as-code for Provisioner deployment
- **Multi-Database Support**: MySQL, MariaDB, SQLite for instances
- **OAuth for Provisioner**: Admin login via GitHub, Google, etc.
- **Custom Domain Support**: CNAME-based custom domains, certificate pinning per domain
- **Disaster Recovery**: Multi-region failover, cross-datacenter replication

---

## Approval & Sign-Off

| Role | Name | Signature | Date |
|------|------|-----------|------|
| **Product Owner** | — | — | — |
| **Engineering Lead** | — | — | — |
| **Security Review** | — | — | — |

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-10 | Documentation Specialist | Initial BRD; all sections complete |

---

## Appendix: Glossary

| Term | Definition |
|------|-----------|
| **Instance** | A single HotBox chat deployment (app container + database) |
| **Provisioner** | The management application that orchestrates instances |
| **Subdomain** | Unique domain under the base domain (e.g., `company.hotbox.ca`) |
| **Invite Token** | Cryptographic token that grants access to sign up for an instance |
| **Seeding** | Initializing a resource with default/bootstrap data |
| **ACME** | Automated Certificate Management Environment (Let's Encrypt protocol) |
| **DNS Provider** | Third-party API for managing DNS records (Namecheap, Cloudflare, etc.) |
| **Caddy** | Reverse proxy and SSL certificate manager used by Provisioner |
| **JWT** | JSON Web Token; cryptographic token for API authentication |
| **Slug** | URL-safe version of a name (spaces/caps removed; hyphens added) |
