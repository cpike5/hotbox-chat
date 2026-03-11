# Product Requirements Document: HotBox Provisioner

**Version**: 1.0
**Date**: 2026-03-10
**Status**: Draft

---

## Executive Summary

HotBox Provisioner is a self-service management platform that empowers administrators and invited users to provision and manage isolated HotBox chat instances on a single server. It operates as a companion application within the hotbox-chat monorepo, handling platform setup, instance lifecycle management, DNS configuration, SSL certificate automation, invite generation, and instance updates. The system prioritizes ease of use, security, and operational simplicity for small-to-medium deployments.

---

## Problem Statement

Deploying and managing multiple isolated HotBox instances manually is labor-intensive. System administrators need:
- A guided setup experience for initial platform configuration (domain, DNS, SSL)
- A dashboard to view and manage all running instances
- A simple invite flow for creating new instances without direct admin access
- One-click updates for HotBox versions across instances
- Built-in resource monitoring and instance lifecycle controls
- Minimal operational overhead on a single server

Invited users, in turn, need:
- A straightforward guided wizard to create their own instance
- No need to understand Docker, DNS, or reverse proxies
- Clear feedback during provisioning and setup

---

## Primary Purpose

Provide a user-friendly, web-based platform for provisioning, managing, and operating multiple isolated HotBox chat instances on a single server, balancing administrative control with self-service flexibility.

---

## Target Users

### 1. Server Operator / Platform Administrator
The person setting up and maintaining the Provisioner platform on a VPS. Responsible for:
- Initial platform configuration (domain, DNS provider credentials, SSL setup)
- Monitoring server health and instance resource usage
- Creating invites and managing users
- Performing platform maintenance and updates
- Setting quotas and policies

**User Segment**: Technical, familiar with VPS deployment, Docker, and domain management. ~1-2 people per deployment.

### 2. Instance Admin (Invited User)
A user who receives an invite and goes through the setup wizard to create their own HotBox instance. Responsible for:
- Naming their instance and setting initial admin account
- Configuring basic server settings (registration mode)
- Inviting members to their instance

**User Segment**: Non-technical to semi-technical; just needs to follow a guided workflow. Expects everything to "just work." Can be 5-50+ people.

### 3. Hosting Business Operator (Future)
A business operator who might deploy Provisioner for commercial SaaS or hosting. Not a primary focus for MVP but should not be actively blocked by the design.

---

## User Flows

### Flow 1: Platform Setup Wizard (First-Run)

**Context**: Server operator runs the Provisioner for the first time and needs to configure the platform before any instances can be created.

**Entry**: Navigate to `/setup` or Provisioner auto-redirects to setup on first load.

**6 Steps:**

1. **Prerequisites Check**
   - Verify Docker is installed and running
   - Verify Docker Compose is available
   - Verify PostgreSQL container can be reached (if external)
   - Display any missing dependencies with clear remediation steps
   - Allow proceeding only if all checks pass

2. **Domain Configuration**
   - Input root domain (e.g., `hotboxchat.ca`)
   - Display formatted instance subdomain examples (e.g., `instance-name.hotboxchat.ca`)
   - Allow proceeding; domain cannot be changed after setup

3. **DNS Provider Setup**
   - Select DNS provider (Namecheap, Cloudflare, or manual)
   - Input provider credentials (API key, username)
   - Test credentials by querying provider API
   - Display success or error clearly
   - Show which A records need to exist (root domain and wildcard)

4. **SSL / Caddy Configuration**
   - Auto-detect Caddy in PATH or Docker
   - Input Caddy admin API endpoint (or auto-use default)
   - If Caddy not found, provide clear instructions for installation
   - Confirm Caddy can reach DNS provider credentials
   - Show sample Caddyfile (read-only, for reference)

5. **Admin Account Seeding**
   - Email address for platform admin
   - Strong password
   - Confirm password
   - Clear password requirements (min 12 chars, mix of upper/lower/numbers/symbols)

6. **Review & Initialize**
   - Show summary of all configuration choices
   - Display time estimate for initialization (usually <30 seconds)
   - Button to "Confirm & Proceed"
   - Once confirmed, show animated progress (Checking DNS → Provisioning Caddy → Creating database → Seeding admin user → Complete)
   - Redirect to login on completion

**Constraints**:
- Platform setup can only be run once (unless admin explicitly resets the platform, which is dangerous)
- All configuration is persisted to environment variables and appsettings.json
- After completion, admin dashboard should be immediately usable

---

### Flow 2: Admin Dashboard

**Context**: Platform operator is logged in and managing instances, invites, and platform settings.

**Main Views**:

#### 2.1 Dashboard (Home)
- **Header**: Welcome message, current server status (CPU, memory, disk usage)
- **Stats Cards**: Total instances, active instances, total users, invites pending
- **Recent Activity Feed**: Last 10 actions (instance created, instance updated, user logged in, etc.)
- **Quick Actions**: Create invite, Create instance (admin-only)
- **Alert Banner**: Any platform health warnings (low disk, failing instances, etc.)

#### 2.2 Instances View
- **Table Columns**: Instance name, subdomain, HotBox version, status (running/stopped/error), admin email, member count, created date
- **Row Expandable Details**:
  - Instance settings (registration mode, description)
  - Resource usage bar chart (CPU %, memory %, disk %)
  - Container logs (last 50 lines, searchable)
  - Admin account for instance (email, created date)
  - Backup status (if backups are enabled; placeholder for now)
- **Row Actions** (dropdown menu):
  - Start / Stop / Restart
  - Force Kill (dangerous, with confirmation)
  - Update to Latest (checks for new HotBox version, offers upgrade)
  - View in New Tab (links to instance's actual HotBox app)
  - Delete (with multi-step confirmation)
  - Edit Settings (in-line or modal)
  - View Logs (dedicated modal)
- **Bulk Actions**: Start multiple, stop multiple, update multiple
- **Filters**: Status, version, admin email, created date range
- **Search**: Instance name or subdomain

#### 2.3 Invites View
- **Table Columns**: Invite code (masked, with copy button), status (active/revoked/used), created by, created date, expiry date (or "never"), max uses (or "unlimited"), uses remaining, actions
- **Row Actions**:
  - Copy invite link (generates full URL: `https://hotboxchat.ca/invite/{code}`)
  - Revoke (prevents future uses)
  - View Details (who used it, when, instance created)
  - Delete (archive, don't actually delete)
- **Create New Invite Button**:
  - Modal form with options:
    - Expiry time (1 hour, 24 hours, 7 days, 30 days, never)
    - Max uses (1, 5, 10, unlimited)
    - Optional note (for tracking, e.g., "For Project X Team")
  - Shows generated invite code and full URL
  - Copy-to-clipboard button
  - Display QR code (optional, nice-to-have)

#### 2.4 Users View
- **Table Columns**: Email, role (admin, user), status (active, suspended), instances owned, last login, created date
- **Row Actions**:
  - View details (all instances user owns, activity log)
  - Change role
  - Suspend / Reactivate
  - Delete (with confirmation; cascading delete of owned instances needs careful handling)
- **Filters**: Role, status, created date range
- **Search**: Email address

#### 2.5 Settings View
- **Sections**:
  - **Platform Info**: Root domain, Caddy version, database type/version (read-only)
  - **DNS Provider**: Current provider, API credentials (masked, can rotate)
  - **Instance Quotas** (all disabled by default, can enable):
    - Max instances per user (optional, e.g., 5)
    - CPU limit per instance (cores)
    - Memory limit per instance (GB)
    - Disk limit per instance (GB)
    - Max message history per instance (days, then auto-clean)
  - **Docker Configuration**: Registry URL, pull credentials, resource monitoring interval
  - **Backup Settings** (placeholder for future): Enable/disable, frequency, retention policy, storage location
  - **Email Configuration** (placeholder for future): SMTP settings for invite emails
  - **Branding**: Platform name (default "HotBox"), logo URL, primary color (optional, affects landing page)

---

### Flow 3: Instance Setup Wizard (Admin Creates)

**Context**: Platform admin clicks "Create Instance" and walks through instance setup. Creates one isolated HotBox instance with a dedicated PostgreSQL database.

**Entry**: Admin dashboard → Create Instance button

**4 Steps:**

1. **Subdomain Selection**
   - Text input for subdomain name (alphanumeric + hyphens, max 63 chars)
   - Real-time availability check: "Checking availability..." → "Available" or "Already taken"
   - Show formatted full domain (e.g., `my-chat.hotboxchat.ca`)
   - Auto-suggest slugified names based on input

2. **Instance Admin Account**
   - Email address for the instance's first admin user
   - Strong password (same requirements as platform admin)
   - Confirm password
   - This user will be the super-admin of the instance once provisioned

3. **Server Settings**
   - HotBox version: Dropdown with latest 5 versions, default to latest
   - Registration mode: Radio buttons for Open / Invite-Only / Closed
     - Show explanation of each (Open = anyone can sign up, Invite-Only = new users need invite code, Closed = admin creates accounts only)
   - Description (optional, max 200 chars, for internal notes)

4. **Review & Launch**
   - Summary of all inputs
   - Display time estimate (usually 30-60 seconds)
   - Button: "Confirm & Provision"
   - Once confirmed, show animated progress:
     - Creating Docker network
     - Pulling HotBox image
     - Provisioning PostgreSQL database
     - Starting HotBox container
     - Configuring Caddy
     - Waiting for health checks
     - Complete
   - Show instance URL and login credentials
   - Auto-redirect to instance details view or offer "Open Instance" button

**Constraints**:
- Instance name must be globally unique (across all instances on this server)
- Instance names cannot be reserved keywords (e.g., "admin", "api", "setup")
- Provisioning is atomic: if any step fails, entire instance is cleaned up and admin is prompted to retry or contact support

---

### Flow 4: Invite Signup Flow (Invited User)

**Context**: User receives an invite link and goes through a wizard to create their own instance. Only invited users can create instances (not open signup).

**Entry**: Click invite link (e.g., `https://hotboxchat.ca/invite/{code}`) or navigate to `/invite/{code}`

**6 Screens:**

1. **Invite Validation**
   - Parse invite code from URL
   - Verify invite is valid, active, not expired, and has remaining uses
   - If invalid: show error ("Invite expired", "Invite revoked", "Too many uses", etc.) with option to contact admin
   - If valid: proceed to next screen

2. **Account Creation**
   - Email address (required, must be unique across platform)
   - Password (required, strong requirements, same as admin account setup)
   - Confirm password
   - Optional: Display name (for the platform user, not the instance)
   - "Create Account" button
   - Show password strength indicator
   - On success: user is logged in, proceed to next screen

3. **Instance Naming**
   - Text input for instance name (alphanumeric + hyphens, max 63 chars)
   - Real-time availability check (same as admin flow)
   - Show formatted full domain
   - Auto-suggest slugified names
   - Optional: Description (max 200 chars, for internal notes)

4. **Server Configuration**
   - HotBox version: Dropdown with latest 3-5 versions (or auto-use latest)
   - Registration mode: Radio buttons for Open / Invite-Only / Closed
   - Show explanation of each
   - Optional: Instance name (display name for the instance, separate from subdomain)

5. **Review & Confirm**
   - Summary of:
     - Instance subdomain
     - Instance admin (user's email)
     - HotBox version
     - Registration mode
   - Display time estimate for provisioning
   - Checkbox: "I understand this creates a new isolated chat server"
   - Button: "Confirm & Provision"

6. **Provisioning Animation**
   - Animated progress identical to Flow 3, Step 4
   - Display instance URL and initial admin account (auto-logged-in)
   - Offer "Open My Instance" button
   - Optional: "Invite members to my instance" quick action

**Constraints**:
- One instance per user (MVP constraint; future: allow multiple instances per user)
- Invite must be consumed within the provisioning flow (cannot reuse after account creation if user abandons)
- Instance creation is atomic (fail-safe like Flow 3)

---

## Feature Requirements

Features are grouped by priority and lifecycle stage.

### P0: MVP (Required for v1.0 Launch)

#### Core Instance Lifecycle
- **Create Instance** (admin or invited user)
  - Provision HotBox container
  - Create PostgreSQL database (dedicated per instance)
  - Seed initial admin user
  - Register subdomain with Caddy
  - Healthy: container running, database responsive, app responding to health checks

- **Start / Stop Instance**
  - `docker-compose pause` / `docker-compose unpause` (or container start/stop)
  - Update Caddy routing (disable on stop, re-enable on start)
  - Zero data loss
  - Status immediately reflected in dashboard

- **Restart Instance**
  - Full container restart
  - Re-check health
  - Maintain all data and state

- **Force Kill Instance** (dangerous, requires confirmation)
  - Forcefully stop container if it's hung or unresponsive
  - Clear warning: "This will force stop the container. Data is safe, but users will be disconnected."
  - Requires admin-level permission

- **Delete Instance** (requires multi-step confirmation)
  - Warn: "This permanently deletes the instance, database, and all chat history. This cannot be undone."
  - Require user to type instance name to confirm
  - Stop and remove container
  - Drop PostgreSQL database
  - Deregister from Caddy
  - Archive invite history
  - Remove from dashboard

#### Instance Updates
- **Check for Updates**
  - Query HotBox GitHub releases or version endpoint
  - Display latest available version vs. current version
  - Show changelog (or link to it)

- **One-Click Update**
  - Admin clicks "Update to X.Y.Z"
  - System pulls new image: `docker pull hotbox:X.Y.Z`
  - Stop running instance
  - Start new container with new image
  - Run database migrations (if needed)
  - Re-check health
  - Auto-rollback if health checks fail (keep old image, restart with old version, show error)
  - Display "Update successful" or "Update failed, rolled back" clearly

#### Invite Management
- **Create Invite**
  - Generate cryptographically secure random code (URL-safe, 32 chars)
  - Set expiry (1 hour to never)
  - Set max uses (1 to unlimited)
  - Optional description/note
  - Store in database with created-by user
  - Display invite URL immediately (with copy button)

- **Revoke Invite**
  - Mark invite as inactive
  - Prevent further uses
  - Already-used invites cannot be revoked (historical record)

- **Track Invite Usage**
  - Record when invite is used (user created, instance created)
  - Display in invite details: "Used by {email} on {date}, created instance {name}"

#### User Management (Platform)
- **List Users**
  - All users who've created accounts on the platform
  - Show email, role, status, instances owned

- **View User Details**
  - Email, creation date, last login
  - List of instances owned by user
  - Activity log (instance created, updated, deleted)

- **Suspend / Reactivate User**
  - Prevent suspended user from logging in
  - Does NOT automatically stop their instances (they remain running for data safety)
  - Suspended user cannot create new invites or instances

- **Delete User** (dangerous, requires confirmation)
  - Warn about cascading effects
  - Cannot delete if user owns active instances (must delete instances first or allow cascade)
  - Remove user account, activity history, and owned invites

#### Platform Configuration
- **Set Root Domain** (one-time, in setup)
  - Cannot be changed after setup (would require re-configuring all instances)

- **Configure DNS Provider** (one-time, in setup; can rotate credentials later)
  - Store API credentials securely (in environment, not in database)
  - Test credentials during setup
  - Show current provider and allow re-authenticating

- **Configure SSL / Caddy** (in setup)
  - Detect Caddy installation
  - Verify certificate provisioning works
  - Display wildcard certificate status (valid, expiring, expired)

#### Resource Monitoring (Display Only, MVP)
- **Show Resource Usage Per Instance**
  - CPU usage (%)
  - Memory usage (%)
  - Disk usage (%)
  - Refreshed every 30 seconds (configurable)
  - Bar charts in instance details view
  - Alert if any metric exceeds 90%

- **Show Server-Level Stats**
  - Total CPU available / used
  - Total memory available / used
  - Total disk available / used
  - Used by Provisioner vs. instances

- **Activity Feed**
  - Log recent actions: instance created, started, stopped, updated, deleted
  - Track who performed each action and when
  - Display last 10 on dashboard

#### Auth & Authorization
- **Admin-Only Views**: Settings, Users, Invites (create), delete instance
- **Platform Admin Role**: Full access to all features
- **Instance Admin Role**: Can only view/manage their own instance from the Provisioner (limited scope)
- **Public Views**: Invite signup page, setup wizard (when not initialized)

#### Error Handling & Rollback
- **Atomic Operations**: Instance creation, deletion, updates either fully succeed or fully rollback
- **Health Checks**: Wait for containers to be healthy before marking operations as complete
- **Automatic Rollback**: Failed updates automatically rollback to previous version
- **Clear Error Messages**: User-facing messages explain what went wrong and next steps

---

### P1: Fast Follow (Soon After MVP)

#### Resource Quotas (Optional, Disabled by Default)
- **Per-Instance Quotas** (enforced via Docker limits):
  - CPU cores (e.g., 1 core max)
  - Memory (e.g., 1 GB max)
  - Disk space (e.g., 10 GB max)

- **Per-User Quotas**:
  - Max instances per user (e.g., 5)
  - Enforced during instance creation: prevent creation if quota exceeded

- **Admin Can Increase Quotas** for specific users (one-off overrides)

- **Quota Alerts**: Warn when instance is approaching limits

#### Enhanced Monitoring
- **Persistent Metrics**: Store CPU, memory, disk usage history per instance (24h, 7d, 30d)
- **Graphs**: Display usage over time in instance details
- **Alerts**: Send alerts to admin when thresholds are exceeded (via email or in-app notification)
- **Predictive**: Estimate when disk will fill at current growth rate

#### Backups (Placeholder Infrastructure)
- **Automatic Backups**: Daily or weekly backups of instance database (compressed, encrypted)
- **Backup Management**: List, download, restore, delete backups from dashboard
- **Retention Policy**: Keep last N backups, auto-delete old ones
- **Disaster Recovery**: Admin can restore instance from backup (creates new container, restores data)

#### Bulk Operations
- **Bulk Start / Stop / Restart**: Select multiple instances, apply action to all
- **Bulk Update**: Update multiple instances to same version with one click
- **Batch Create Invites**: Generate multiple invites at once (e.g., for team on-boarding)

#### Logs & Debugging
- **Persistent Logs**: Store container stdout/stderr for 7 days
- **Log Search**: Filter logs by keyword, date range
- **Download Logs**: Export logs for debugging
- **Webhook Logging**: Log all API calls (if API is added) for audit trail

#### API for Scripting (Optional)
- **REST API** (unauthenticated for now, internal only):
  - Create instance
  - List instances
  - Get instance details
  - Update instance
  - Delete instance
  - Create invite
  - List invites
- **Intended Use**: Automation, scripting, integration with other tools
- **Not for MVP**: Can add post-launch

---

### P2: Future (Post-MVP, Consider Later)

#### Multi-Admin Support
- **Admin Role Management**: Platform admin can create/delete other admins
- **Admin Activity Audit**: Log which admin performed which action
- **Admin Permissions Granularity**: Different admin levels (full, read-only, etc.)

#### Horizontal Scaling
- **Multi-Server Deployment**: Provision instances across multiple physical servers
- **Load Balancer**: Route traffic across servers
- **Shared Database**: Central database for Provisioner, per-instance DBs on target servers
- **Service Discovery**: Auto-register new servers, handle server removal

#### Billing & Metering (SaaS Use Case)
- **Usage Tracking**: Track resource consumption per instance
- **Cost Calculation**: Compute cost per instance (per CPU-hour, memory-hour, disk-month)
- **Invoice Generation**: Auto-generate invoices for customers
- **Payment Integration**: Stripe, Paddle, or similar
- **Quota Enforcement**: Stop/suspend instances if payment is overdue

#### Instance Migration
- **Migrate Instance**: Move an instance from one server to another (for maintenance, balancing)
- **Zero-Downtime Migration**: Keep instance running while migrating (complex, needs careful planning)

#### Multi-Instance Per User (MVP Currently Limits to 1)
- **Allow Users to Own Multiple Instances**: Remove the one-instance-per-user constraint
- **Instance Listing**: Show all instances owned by a user
- **Quick Switch**: Jump between owned instances from dropdown

#### CLI Tool
- **Command-Line Interface**: provisioner-cli for scripting and automation
- **Commands**: create-instance, delete-instance, start-instance, etc.
- **Authentication**: API key or OAuth token for CLI

#### Dashboard Customization
- **Custom Widgets**: Admin can add/remove dashboard widgets
- **Saved Filters**: Save frequently-used filters for instances
- **Dark / Light Theme**: User preference (MVP: dark only)

#### Email Integration
- **Send Invites via Email**: Admin can email invites instead of manually sharing URLs
- **Automated Emails**: Notify users when instances are updated, quota warnings, etc.
- **SMTP Configuration**: Store SMTP credentials securely

#### Webhooks
- **Event Webhooks**: Trigger external services on instance events (created, updated, deleted, failed health check)
- **Custom Integrations**: Sync instance data with external systems (monitoring, ticketing, etc.)

#### Instance Templates
- **Pre-configured Instances**: Create templates with preset HotBox version, registration mode, quotas
- **Clone from Template**: Quickly provision new instances with same settings

#### Analytics
- **Instance Analytics**: Track member count over time, message count, peak usage times
- **Platform Analytics**: Total instances, users, growth trends
- **Reporting**: Generate reports for planning and capacity planning

---

## Feature Interaction Matrix

| Feature | Admin Only | Invite User | Public |
|---------|-----------|-------------|--------|
| Setup Wizard | Yes | No | No (one-time) |
| Dashboard | Yes | No | No |
| Create Instance (admin) | Yes | No | No |
| Instance Setup (invite) | No | Yes | No |
| View Instances (own) | Yes | Yes | No |
| Manage Instances (own) | Yes | Limited* | No |
| Manage Invites | Yes | No | No |
| Manage Users | Yes | No | No |
| Settings | Yes | No | No |
| Login (platform) | Yes | Yes | No |

*Limited: Invited users can view their own instance but cannot delete, only have access to limited settings (registration mode, display name).

---

## UI / UX Requirements

### Design System

#### Color Palette
Use HotBox's existing design system (from `app.css`):
- **Primary Background**: Dark slate (`#1a1a2e` or similar)
- **Secondary Background**: Lighter slate (`#16213e` or similar)
- **Accent Color**: Teal (`#0f3460` or `#16a085` or similar, match existing)
- **Text Primary**: Off-white / light gray (`#e0e0e0` or `#f0f0f0`)
- **Text Secondary**: Muted gray (`#888` or `#999`)
- **Success**: Green (`#28a745` or `#27ae60`)
- **Error**: Red (`#dc3545` or `#e74c3c`)
- **Warning**: Orange (`#ffc107` or `#f39c12`)
- **Info**: Blue (`#17a2b8` or `#3498db`)

#### Typography
- **Body Font**: DM Sans (from existing HotBox)
- **Monospace**: JetBrains Mono (from existing HotBox)
- **Headlines**: DM Sans, bold
- **Sizes**: Consistent with HotBox (check `app.css` for existing scale)

#### Components
Reuse existing HotBox Blazor components where possible:
- Buttons (primary, secondary, danger, disabled states)
- Input fields (text, password, email, with validation)
- Dropdowns / Select
- Modals / Dialogs
- Tables (sortable, filterable)
- Cards
- Toasts / Alerts
- Progress bars
- Loading spinners
- Status badges

#### Responsive Design
- **Desktop-First**: Optimize for 1366+ wide screens (admin dashboard is desktop-focused)
- **Tablet Support**: Collapse sidebars, stack tables vertically if needed
- **Mobile**: Invited signup flow should work on mobile (responsive modals, single-column layout)

### Key Interaction Patterns

#### Confirmation Dialogs
- Destructive actions (delete instance, revoke invite) require explicit confirmation
- Multi-step for highly dangerous actions (delete asks for instance name as confirmation)
- Clear warning message in red or orange

#### Real-Time Feedback
- Copy-to-clipboard buttons show "Copied!" toast for 2 seconds
- Form validation shows inline errors immediately
- Availability checks debounce input, show "Checking..." while querying
- Progress indicators during long operations (provisioning, updates)

#### Status Indicators
- **Instances**: Icon + text badge (Running, Stopped, Error, Provisioning, Updating)
- **Invites**: Icon + text (Active, Revoked, Used, Expired)
- **Users**: Icon + text (Active, Suspended)
- **Health**: Color-coded (green = healthy, yellow = warning, red = error)

#### Navigation
- **Top Bar**: Provisioner logo, navigation menu, user dropdown (profile, logout, settings)
- **Sidebar**: Main nav (Dashboard, Instances, Invites, Users, Settings)
- **Breadcrumbs**: On detail pages (Dashboard > Instances > instance-name)

#### Tables & Lists
- Sortable columns (click header to sort)
- Filterable (dropdowns, date pickers, text search)
- Expandable rows (click to reveal details)
- Pagination (20 rows per page default)
- Bulk actions (checkbox, select all, apply action)

#### Forms
- Required fields marked with asterisk (*)
- Help text below field labels
- Password strength indicator on password fields
- "Copy" buttons for generated codes
- "Show/Hide" toggle for password fields
- Clear save/cancel buttons

---

## Technical Constraints

### Single Server Deployment
- All instances run on one physical host
- Shared Docker daemon and network bridge
- Shared PostgreSQL (one database per instance, same PostgreSQL server)
- Caddy reverse proxy runs on the same host

### Docker & Orchestration
- Uses Docker Compose for orchestration (no Kubernetes)
- HotBox app container + PostgreSQL container per instance
- Caddy container for reverse proxy (one, shared)
- Provisioner app container (one)

### DNS Provider Integration
- Must support DNS-01 ACME challenge (for wildcard certs)
- Supported providers: Namecheap, Cloudflare, Route 53 (others possible)
- Credentials stored securely (environment variables, not database)

### Database
- Provisioner database: PostgreSQL (or MySQL, SQLite for dev)
- Instance databases: One PostgreSQL database per instance (or could be SQLite per instance, but PostgreSQL preferred for production)
- Both can run on same PostgreSQL server (separate schemas/databases)

### Resource Limits
- Single server typically supports ~5-10 instances of moderate size (depends on server specs)
- CPU: Expect 1-2 cores per instance (configurable via Docker limits)
- Memory: Expect 512 MB - 1 GB per instance (configurable)
- Disk: Instance data grows with message history (configurable limits via quotas)

### SSL Certificate Management
- Wildcard certificate (*.hotboxchat.ca) obtained via ACME + DNS-01 challenge
- Caddy handles renewal automatically
- Certificate valid for 90 days (Let's Encrypt standard)

---

## Security & Authorization

### Authentication
- **Setup Wizard**: No auth (one-time, before any user exists)
- **Platform Admin**: Email/password login to Provisioner dashboard
- **Instance Admin**: Uses HotBox's auth (inside their instance app, separate)

### Authorization
- **Platform Admin**: Full access to all Provisioner features
- **Invited User**: Can create account and one instance via invite flow; limited self-service
- **Instance Members**: Log into their instance's HotBox app (not relevant to Provisioner)

### Secrets & Credentials
- DNS provider API keys: Stored in environment variables (not in database)
- Admin passwords: Hashed before storage
- Session tokens: Secure, httpOnly cookies
- Database passwords: In environment, not in code

### Invite Security
- Invite codes: 32-character cryptographically random strings (URL-safe base64)
- Single-use or max-use enforcement (can be revoked)
- Expiry enforcement (no use after expiry)
- Rate limiting on invite creation (prevent spam)

### HTTPS Only
- All Provisioner and instance traffic over HTTPS
- SSL certificates auto-managed by Caddy
- No HTTP fallback

---

## Success Metrics

### Deployment & Onboarding
- [ ] Platform setup completes in <5 minutes for a user with prerequisites met
- [ ] Setup wizard has zero user abandonment (all users who start complete it)
- [ ] No manual intervention needed after setup (fully automated)

### Instance Creation
- [ ] Admin can create an instance in <2 minutes (via dashboard)
- [ ] Invited user can create an instance in <3 minutes (via signup flow)
- [ ] Instance health check passes within 60 seconds of provisioning
- [ ] Instance is accessible (HTTPS, subdomain resolves, HotBox app loads) immediately after provisioning

### User Experience
- [ ] No admin needs to SSH into the server to manage instances
- [ ] No manual Docker command needed post-setup (everything via dashboard)
- [ ] Dashboard loads in <1 second
- [ ] All forms have client-side validation (no server round-trips for basic errors)
- [ ] All destructive actions have clear warnings and multi-step confirmation

### Reliability
- [ ] Update completes without downtime (instance remains accessible)
- [ ] Automatic rollback on failed update
- [ ] Instance can be stopped/started without data loss
- [ ] Provisioner crash does not affect running instances (they continue to serve)

### Scalability
- [ ] Dashboard remains responsive with 20+ instances
- [ ] Instance provisioning time stays <60s even with 5 instances already running
- [ ] Monitoring metrics are refreshed every 30 seconds without impacting performance

---

## Out of Scope (MVP)

### Instance Features
- **Backups**: Queued for P1; placeholder UI only
- **Instance templates**: Future feature
- **Multi-instance per user**: MVP limits to 1 instance per invited user
- **Restore from backup**: Queued for P1

### Platform Features
- **Multi-admin**: Only one seeded admin in MVP
- **Horizontal scaling**: Single-server only
- **Billing / SaaS metering**: Not for MVP (future feature for hosting businesses)
- **Email integration**: Invites are manual (copy-paste links)
- **Webhooks**: Not needed for MVP
- **CLI tool**: Manual dashboard only for MVP
- **API**: No public API (internal only if added post-MVP)
- **Custom branding**: Platform name is "HotBox Provisioner" (not customizable)
- **Analytics**: Not in MVP (can add detailed reporting post-launch)

### Deployment Models
- **Kubernetes**: Not supported (Docker Compose only)
- **Multi-cloud**: Design doesn't prevent it, but not tested
- **Windows Server**: Linux-only (Docker on Linux target)

### Administrative Features
- **Instance data migration**: Cannot move instance between servers
- **Database replication**: Not supported
- **Instance snapshotting**: No point-in-time snapshots
- **Partial restores**: Backups restore entire instance (if enabled)

---

## Open Questions

1. **Resource Quota Defaults**: If quotas are enabled, what should the defaults be? (e.g., 1 core, 1 GB RAM, 10 GB disk per instance)

2. **Backup Storage**: Where should instance database backups be stored? Local disk, S3, or configurable?

3. **Instance Auto-Scaling**: If an instance's resource usage exceeds quota, should it be automatically stopped/paused, or just trigger an alert?

4. **Cascading Deletes**: When a user is deleted, should their instances also be deleted (cascading), or should deletion be blocked if user owns instances?

5. **Multi-Instance Per User**: Should this be allowed in MVP or deferred to P1? Currently designed to allow 1 per user for simplicity.

6. **Email Notifications**: Should invites be sent via email, or are copy-paste URLs sufficient for MVP?

7. **Instance Downtime During Updates**: Is a brief downtime (10-30 seconds) acceptable during updates, or is zero-downtime required?

8. **Metrics Retention**: How long should historical metrics (CPU, memory, disk) be retained? (24h, 7d, 30d, forever?)

9. **API Authentication**: If an internal REST API is added, should it use API keys, OAuth, or JWT?

10. **Instance Naming Constraints**: Besides alphanumeric + hyphens, should there be additional reserved names (e.g., "admin", "api", "www")? What's the final list?

11. **Admin Account Seed**: After setup, is there a way to reset the admin password, or is it locked? (Should there be a recovery mechanism?)

12. **Caddy Configuration**: Should Caddy config be auto-generated and managed by Provisioner, or manually edited by admin?

---

## Related Documentation

- **DNS Setup Guide**: `/docs/provisioner/dns-setup.md` — Step-by-step DNS and Caddy configuration for specific providers (Namecheap, Cloudflare, Route 53)
- **Technical Specification**: `/docs/provisioner/spec.md` — Architecture, database schema, API contracts, implementation details
- **Deployment Guide**: `/docs/deployment/provisioner.md` — How to deploy the Provisioner itself on a VPS
- **HotBox Requirements**: `/docs/requirements/requirements.md` — Core HotBox features and constraints

---

## Glossary

| Term | Definition |
|------|-----------|
| **Instance** | An isolated HotBox chat application with its own database, admin account, and subdomain |
| **Provisioner** | The management platform that creates, manages, and monitors HotBox instances |
| **Platform Admin** | The person who runs the Provisioner server and manages instances/users |
| **Instance Admin** | A user who owns a specific instance (created via invite or directly by platform admin) |
| **Invite** | A one-time or limited-use URL that allows a user to create an account and instance |
| **Subdomain** | Part of the instance URL (e.g., `my-chat` in `my-chat.hotboxchat.ca`) |
| **Caddy** | The reverse proxy and SSL certificate manager |
| **Wildcard Certificate** | A single SSL cert covering `*.hotboxchat.ca` (all subdomains) |
| **Docker Compose** | Orchestration tool that manages containers (HotBox app, PostgreSQL) |
| **Quota** | Resource limit (CPU, memory, disk, instance count) |
| **Rollback** | Reverting to a previous state (e.g., previous HotBox version after failed update) |

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-03-10 | Documentation Team | Initial PRD, comprehensive feature set, four user flows, open questions |
