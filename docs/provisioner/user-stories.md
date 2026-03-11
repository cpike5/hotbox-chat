# HotBox Provisioner User Stories

## Overview

The HotBox Provisioner is a side application that provisions and manages isolated HotBox chat instances via Docker. This document outlines user stories organized by epic, covering the complete lifecycle from platform setup through instance management and monitoring.

## Personas

| Persona | Role | Scope |
|---------|------|-------|
| **Platform Admin** | Runs the provisioner on their server, manages instances and invites | MVP |
| **Invited User** | Receives invite URL, creates account, launches their own HotBox instance | MVP |
| **Hosting Operator** | Forks provisioner to run a hosting business; not direct MVP user but informs design | Future |

## Epic Summary

| Epic | Stories | Priority | Target |
|------|---------|----------|--------|
| Platform Setup | US-001 to US-006 | P0 | MVP |
| Instance Lifecycle | US-007 to US-013 | P0 | MVP |
| Invite Management | US-014 to US-018 | P0 | MVP |
| Instance Monitoring | US-019 to US-024 | P1 | Fast Follow |
| User Management | US-025 to US-027 | P1 | Fast Follow |
| Resource Quotas | US-028 to US-031 | P2 | Future |

---

## Epic: Platform Setup

### US-001: Initial Setup Wizard — Prerequisites Check
**As a** Platform Admin, **I want to** run a first-time setup wizard that checks all prerequisites, **so that** I can ensure my system is ready to run the provisioner.

**Acceptance Criteria:**
- [ ] Setup wizard runs automatically on first startup when no configuration exists
- [ ] Checks for required software: Docker, Docker Compose, curl/wget
- [ ] Checks for required ports available: 80, 443, and provisioner app port
- [ ] Checks available disk space (warns if less than 10GB free)
- [ ] Checks Docker daemon connectivity
- [ ] Provides clear pass/fail status for each check
- [ ] Offers remediation steps for failed checks (links to installation docs)
- [ ] Can be re-run at any time via CLI command: `dotnet run -- --setup`
- [ ] Exit codes: 0 on success, non-zero on failure

**Priority:** P0 (MVP)
**Epic:** Platform Setup

---

### US-002: Domain & DNS Configuration Setup
**As a** Platform Admin, **I want to** configure my domain and DNS provider during setup, **so that** wildcard subdomains automatically resolve to my server.

**Acceptance Criteria:**
- [ ] Setup wizard prompts for: root domain (e.g., `hotboxchat.ca`), DNS provider (Namecheap, Cloudflare, other)
- [ ] For Namecheap: prompts for API key and username, validates connectivity
- [ ] For Cloudflare: prompts for API token, validates connectivity
- [ ] Validates domain ownership by attempting a test DNS record write
- [ ] Displays success message with A record configuration instructions if validation needed manually
- [ ] Stores credentials securely (encrypted in config file or environment variables)
- [ ] Provides option to skip if using manual DNS (no auto-provisioning of wildcard DNS records)
- [ ] Can be reconfigured later via admin settings UI

**Priority:** P0 (MVP)
**Epic:** Platform Setup

---

### US-003: SSL Certificate & Reverse Proxy Setup
**As a** Platform Admin, **I want to** configure Caddy for automatic wildcard SSL certificate generation, **so that** all instances are served over HTTPS with valid certificates.

**Acceptance Criteria:**
- [ ] Setup wizard generates a Caddyfile configured for wildcard domain (*.example.com)
- [ ] Caddyfile includes DNS validation with the chosen provider (Namecheap or Cloudflare)
- [ ] Provisioner confirms Caddy container is running and healthy
- [ ] Caddy obtains wildcard certificate within 5 minutes (with timeout handling)
- [ ] Test certificate issuance by querying Caddy logs: `docker compose logs caddy`
- [ ] Provides fallback instructions if certificate fails (manual Caddy debugging)
- [ ] Platform remains accessible via base domain (`hotboxchat.ca`) pointing to provisioner UI
- [ ] Subdomains are proxied to their respective HotBox instance containers

**Priority:** P0 (MVP)
**Epic:** Platform Setup

---

### US-004: Admin Account Seeding
**As a** Platform Admin, **I want to** create a primary admin account during setup, **so that** I can access the provisioner dashboard and manage the platform.

**Acceptance Criteria:**
- [ ] Setup wizard prompts for admin email and password (password must be at least 12 chars, include uppercase/number/special char)
- [ ] Admin account is seeded into provisioner database with role `PlatformAdmin`
- [ ] Admin account is stored with bcrypt hashing (no plaintext)
- [ ] Cannot be skipped; wizard requires valid credentials to proceed
- [ ] Email uniqueness enforced (no duplicate admin accounts)
- [ ] After setup, admin can log in via provisioner dashboard at `https://example.com`
- [ ] Password can be reset via CLI: `dotnet run -- --reset-admin-password`

**Priority:** P0 (MVP)
**Epic:** Platform Setup

---

### US-005: Configuration Validation & Persistence
**As a** Platform Admin, **I want to** validate all setup choices and persist them to a configuration file, **so that** the provisioner starts cleanly on next boot without re-running setup.

**Acceptance Criteria:**
- [ ] All setup inputs validated before persistence (domain format, DNS connectivity, port availability)
- [ ] Configuration stored in `appsettings.production.json` or environment variables (encrypted secrets)
- [ ] Setup wizard creates a `.env.production` file with all secrets (API keys, passwords)
- [ ] File ownership and permissions set to restrict access (600 on Linux/macOS)
- [ ] On next startup, provisioner reads config and skips setup if valid
- [ ] If config is corrupted or missing, setup wizard runs again
- [ ] Configuration can be viewed/edited in admin settings UI (password reset only, not API keys)

**Priority:** P0 (MVP)
**Epic:** Platform Setup

---

### US-006: Setup Completion & First-Run Message
**As a** Platform Admin, **I want to** see a clear completion message after setup, **so that** I know the provisioner is ready and understand next steps.

**Acceptance Criteria:**
- [ ] Setup wizard displays completion checklist: ✓ Prerequisites, ✓ DNS, ✓ SSL, ✓ Admin Account, ✓ Config Saved
- [ ] Shows admin login URL: `https://example.com/admin/login`
- [ ] Shows how to create first invite: "Visit Settings → Invites → Generate Invite"
- [ ] Displays provisioner logs location: `docker compose logs -f`
- [ ] Provides link to documentation (DNS troubleshooting, instance creation, etc.)
- [ ] Auto-opens admin login page in default browser (optional, can be skipped)
- [ ] Provisioner is immediately accessible and operational

**Priority:** P0 (MVP)
**Epic:** Platform Setup

---

## Epic: Instance Lifecycle

### US-007: Create Instance — Wizard Intro & Name Selection
**As a** Invited User or Platform Admin, **I want to** start an instance creation wizard that guides me through naming and configuring my instance, **so that** I can launch a new HotBox chat server.

**Acceptance Criteria:**
- [ ] Wizard opens with: "Create a New HotBox Instance"
- [ ] Step 1: User enters instance friendly name (e.g., "My Friend Group Chat") — text field, 3-50 chars, required
- [ ] Friendly name can contain letters, numbers, spaces, hyphens, underscores (no special chars)
- [ ] Friendly name is used for display in instance list and admin dashboard
- [ ] Cannot proceed to next step without entering a name
- [ ] Wizard has "Back" and "Next" buttons, and persistent step indicator (1 of 4, etc.)
- [ ] Can cancel at any time (returns to dashboard, no instance created)

**Priority:** P0 (MVP)
**Epic:** Instance Lifecycle

---

### US-008: Create Instance — Subdomain Selection & Live Availability Check
**As a** Invited User or Platform Admin, **I want to** choose a subdomain for my instance with real-time availability feedback, **so that** I know if my desired subdomain is already taken.

**Acceptance Criteria:**
- [ ] Step 2: User enters desired subdomain (e.g., "my-chat")
- [ ] Subdomain format validated: alphanumeric and hyphens only, 3-50 chars, no leading/trailing hyphens
- [ ] As user types, show live availability status (auto-debounced, checks every 1s):
  - "Available" (green checkmark)
  - "Not available" (red X)
  - "Invalid format" (yellow warning)
  - "Checking..." (spinner while query in flight)
- [ ] Final subdomain will be: `{subdomain}.{root-domain}` (e.g., `my-chat.hotboxchat.ca`)
- [ ] Show preview of final URL: "Your instance will be at: my-chat.hotboxchat.ca"
- [ ] Cannot proceed if subdomain is unavailable or invalid
- [ ] Subdomain uniqueness enforced at database level (unique constraint)

**Priority:** P0 (MVP)
**Epic:** Instance Lifecycle

---

### US-009: Create Instance — Admin Account & Registration Mode
**As a** Invited User or Platform Admin, **I want to** configure the instance admin account and set the registration mode, **so that** I control who can join my instance.

**Acceptance Criteria:**
- [ ] Step 3: User creates instance admin account (email, password, display name)
- [ ] Email format validated (standard email regex)
- [ ] Password validated: min 12 chars, uppercase, number, special char
- [ ] Display name: 1-50 chars, alphanumeric + spaces/hyphens
- [ ] User selects registration mode for this instance:
  - **Open**: Anyone with instance URL can sign up (no admin approval needed)
  - **Invite-Only**: New users need invite URLs created by instance admin
  - **Closed**: No new registrations (admin must create user accounts manually)
- [ ] Show tooltip: "You can change this later in your instance settings"
- [ ] Confirm all details before proceeding (show summary)

**Priority:** P0 (MVP)
**Epic:** Instance Lifecycle

---

### US-010: Create Instance — Review & Launch
**As a** Invited User or Platform Admin, **I want to** review all instance settings before launching, **so that** I can catch any mistakes.

**Acceptance Criteria:**
- [ ] Step 4 (Review): Display all configured settings:
  - Instance name
  - Subdomain URL
  - Admin account email
  - Registration mode
- [ ] User can edit previous steps by clicking "Edit" on each field (goes back to that step)
- [ ] "Create Instance" button triggers provisioning (disabled while in progress)
- [ ] Show confirmation: "This will create a new Docker container and database. Continue?"
- [ ] On confirm, show spinner: "Provisioning instance... this may take 2-3 minutes"
- [ ] Monitor progress with status updates: "Pulling image...", "Creating database...", "Starting container...", "Warming up..."
- [ ] On success: Show "Instance Ready! Your chat server is live at: {url}" with big green button "Visit Instance"
- [ ] On failure: Show error message, logs snippet, and option to retry or contact support
- [ ] Wizard closes and redirects to instance detail page on success

**Priority:** P0 (MVP)
**Epic:** Instance Lifecycle

---

### US-011: Instance Lifecycle Controls — Start, Stop, Restart
**As a** Platform Admin or Instance Owner, **I want to** control instance state (start, stop, restart), **so that** I can manage resource usage and perform maintenance.

**Acceptance Criteria:**
- [ ] Instance detail page shows current state: Running, Stopped, or Restarting
- [ ] Three action buttons: "Restart" (visible when Running), "Stop" (visible when Running), "Start" (visible when Stopped)
- [ ] **Restart**: Gracefully stops container, waits up to 30s, then starts. Shows spinner during action.
- [ ] **Stop**: Gracefully stops container, waits up to 30s. Warn: "Users will be disconnected." Cannot stop from invite signup flow.
- [ ] **Start**: Starts stopped container. Cannot start if instance is being deleted.
- [ ] Actions take 5-30 seconds; show "Starting instance..." spinner with timeout fallback
- [ ] If action times out, show error: "Action timed out. Check logs for details."
- [ ] After successful action, update state display immediately
- [ ] State persists across provisioner restarts

**Priority:** P0 (MVP)
**Epic:** Instance Lifecycle

---

### US-012: Instance Logs Viewer
**As a** Platform Admin or Instance Owner, **I want to** view real-time and historical logs from my instance, **so that** I can debug issues and monitor activity.

**Acceptance Criteria:**
- [ ] Instance detail page has "Logs" tab showing last 500 lines of container stdout/stderr
- [ ] Log lines show timestamp, log level (INFO, WARN, ERROR), and message
- [ ] "Tail Logs" toggle: when ON, auto-scrolls to bottom and streams new lines in real-time
- [ ] "Copy All" button copies visible logs to clipboard
- [ ] "Download Logs" button exports last 5000 lines as `.txt` file
- [ ] Filter by log level: All, INFO, WARN, ERROR (default: All)
- [ ] Search within logs: Ctrl+F (browser find, not server-side)
- [ ] Max 500 lines displayed at once (prevents UI slowdown); paginate if needed
- [ ] Refresh rate: 2 seconds when tailing; can be manually refreshed via "Refresh" button
- [ ] Clear logs button available (warns: "This clears only the UI cache, not persistent logs")

**Priority:** P0 (MVP)
**Epic:** Instance Lifecycle

---

### US-013: Delete Instance — Confirmation & Cleanup
**As a** Platform Admin or Instance Owner, **I want to** permanently delete an instance with proper confirmation, **so that** I can reclaim resources and remove instances I no longer need.

**Acceptance Criteria:**
- [ ] Instance detail page has "Delete Instance" button in danger zone (red button)
- [ ] Clicking opens modal: "This will permanently delete {instance-name} and all its data"
- [ ] Modal shows:
  - Subdomain will be freed up for reuse
  - All messages, users, and settings will be deleted (cannot be recovered)
  - Containers, volumes, and database will be removed
- [ ] Requires confirmation: User must type instance subdomain (e.g., "my-chat") to unlock delete button
- [ ] "Delete Instance" button in modal is red and only enabled after typing correct subdomain
- [ ] On confirmation, show spinner: "Deleting instance... this may take up to 2 minutes"
- [ ] On success: Show "Instance deleted" toast, redirect to dashboard
- [ ] On failure: Show error toast with logs, offer retry option
- [ ] Deleted subdomain becomes available for new instances after 1 minute (cooldown to prevent DNS conflicts)
- [ ] Delete is **not** allowed during any provisioning action (create, update, restart)

**Priority:** P0 (MVP)
**Epic:** Instance Lifecycle

---

## Epic: Invite Management

### US-014: Generate Invite URLs
**As a** Platform Admin or Instance Owner, **I want to** create invite URLs with optional constraints, **so that** I can invite specific people to join my instance.

**Acceptance Criteria:**
- [ ] Instance detail page has "Invites" tab
- [ ] "Generate Invite" button opens form:
  - Optional max uses: Number field (1-100, default 1)
  - Optional expiration: Dropdown (1 hour, 24 hours, 7 days, 30 days, never, default: 7 days)
  - Auto-fill enabled (shows placeholder: "1 use, expires in 7 days")
- [ ] On submit, generate short unique code (base64, 8-12 chars, case-insensitive for user convenience)
- [ ] Full invite URL: `https://example.com/join?code={code}` or similar
- [ ] Show "Copy to Clipboard" button with toast: "Invite copied!" on click
- [ ] Display created invite in list with:
  - Invite code (partial masked if needed)
  - Max uses / uses remaining (e.g., "2 / 3 uses")
  - Expiration date/time
  - Status: Active, Expired, Used Up
  - Actions: Copy link, Revoke (if active)
- [ ] Can generate unlimited invites per instance

**Priority:** P0 (MVP)
**Epic:** Invite Management

---

### US-015: View Invite Status & History
**As a** Platform Admin or Instance Owner, **I want to** see all invites for my instance with their current status, **so that** I can track which invites are active and how many have been used.

**Acceptance Criteria:**
- [ ] Invites tab shows table with columns:
  - Invite Code (clickable to copy)
  - Created (date/time)
  - Expires (date/time, or "Never")
  - Uses (e.g., "1 / 3" or "Used")
  - Status badge: Active (green), Expired (gray), Used Up (gray)
  - Actions: Copy link, Revoke, Delete
- [ ] Sort by: Created (newest first), Expiration, Uses (default: newest first)
- [ ] Filter by status: All, Active, Expired, Used
- [ ] Pagination: Show 25 per page, with prev/next buttons
- [ ] Search by invite code or partial code
- [ ] Disabled invites (expired or used) are shown but grayed out
- [ ] Hover on status badge shows tooltip explaining meaning
- [ ] "Revoke" action available for active invites (changes status to "Revoked")

**Priority:** P0 (MVP)
**Epic:** Invite Management

---

### US-016: Revoke Invite URLs
**As a** Platform Admin or Instance Owner, **I want to** revoke active invite URLs, **so that** I can prevent new signups if I change my mind.

**Acceptance Criteria:**
- [ ] Invites table has "Revoke" button for active invites
- [ ] Clicking opens confirmation: "Revoke this invite? Users with the link will no longer be able to sign up."
- [ ] On confirm, status changes to "Revoked" and button disappears
- [ ] Revoked invites remain visible in history (can be deleted later)
- [ ] Revoking an invite does not affect users already signed up with it (historical usage is respected)
- [ ] Toast message: "Invite revoked"

**Priority:** P0 (MVP)
**Epic:** Invite Management

---

### US-017: Delete Invite (History Cleanup)
**As a** Platform Admin or Instance Owner, **I want to** delete old or expired invites from the list, **so that** I can keep the invite history clean.

**Acceptance Criteria:**
- [ ] Invites table has "Delete" button for expired or used invites
- [ ] "Delete" button is NOT available for active invites (must revoke first)
- [ ] Clicking "Delete" opens confirmation: "Delete this invite? This cannot be undone."
- [ ] On confirm, invite is removed from list
- [ ] Historical usage is not affected (users signed up with deleted invites remain valid)
- [ ] Toast message: "Invite deleted"

**Priority:** P0 (MVP)
**Epic:** Invite Management

---

### US-018: Invite Signup Flow — Landing Page
**As a** Invited User, **I want to** land on a page that shows my invite's validity, **so that** I know if I can proceed to sign up.

**Acceptance Criteria:**
- [ ] User visits `https://example.com/join?code={code}` (or `/invite/{code}`)
- [ ] Page displays immediately with one of four states:
  1. **Valid**: "Your invite is valid and ready to use. Click below to create your account."
  2. **Expired**: "This invite has expired. Please contact the instance owner for a new one."
  3. **Used**: "This invite has reached its usage limit. Please contact the instance owner."
  4. **Revoked**: "This invite has been revoked. Please contact the instance owner."
- [ ] Show instance name/branding if available (friendly name from provisioner)
- [ ] Valid state shows: "Create Account" button → proceeds to step 2
- [ ] Invalid states show: "Request New Invite" button → opens email form to contact owner (future feature, or just shows owner email)
- [ ] Auto-detect invalid state on page load (without user action)

**Priority:** P0 (MVP)
**Epic:** Invite Management

---

## Epic: Instance Monitoring

### US-019: Dashboard — Overview Statistics
**As a** Platform Admin, **I want to** see real-time platform statistics on a dashboard, **so that** I have a quick overview of system health.

**Acceptance Criteria:**
- [ ] Dashboard shows stat cards in grid layout:
  - **Total Instances**: Number of created instances (all states)
  - **Active Instances**: Number of running instances
  - **Total Users**: Sum of registered users across all instances
  - **Total Messages**: Sum of messages across all instances (optional for MVP)
  - **System CPU**: % used by all provisioner and instance containers
  - **System Memory**: % used by all provisioner and instance containers
  - **Disk Usage**: % used on host machine
- [ ] Each stat card shows current value, up-arrow/down-arrow indicator (vs. previous hour), and color coding (green/yellow/red based on threshold)
- [ ] Refresh every 30 seconds (background XHR, no page reload)
- [ ] Hovering stat card shows "Last updated: {time}" tooltip
- [ ] Click stat card to drill down into detailed view (e.g., CPU → list of instances by CPU usage)

**Priority:** P1 (Fast Follow)
**Epic:** Instance Monitoring

---

### US-020: Dashboard — Instance List with Health Status
**As a** Platform Admin, **I want to** see a list of all instances with their current status and resource usage, **so that** I can identify problematic instances at a glance.

**Acceptance Criteria:**
- [ ] Dashboard shows all instances in a table with columns:
  - Instance Name (linked to detail page)
  - Subdomain URL
  - State (Running/Stopped badge with icon)
  - Owner Email
  - Created Date
  - CPU Usage (% with bar chart)
  - Memory Usage (% with bar chart)
  - Disk Usage (% with bar chart)
  - Last Updated (timestamp)
- [ ] Rows are color-coded by health:
  - Green: All metrics < 70%
  - Yellow: Any metric 70-85%
  - Red: Any metric > 85%
- [ ] Sort by: Name, State, CPU, Memory, Disk, Created (default: name)
- [ ] Filter by state: All, Running, Stopped
- [ ] Pagination: 10 per page with prev/next
- [ ] Search by instance name or subdomain
- [ ] Refresh every 60 seconds (auto-background update)
- [ ] Click row to open instance detail page

**Priority:** P1 (Fast Follow)
**Epic:** Instance Monitoring

---

### US-021: Recent Activity Feed
**As a** Platform Admin, **I want to** see a feed of recent platform events, **so that** I can track what's happening without digging through logs.

**Acceptance Criteria:**
- [ ] Dashboard shows "Recent Activity" panel (right sidebar or below stat cards)
- [ ] Activity feed shows events:
  - Instance created: "MyChat instance created by user@example.com"
  - Instance started: "MyChat instance started"
  - Instance stopped: "MyChat instance stopped (manual)"
  - Instance deleted: "MyChat instance deleted by admin"
  - New user signup: "john@example.com signed up to MyChat"
  - Error: "MyChat failed to start (see logs)"
- [ ] Each activity shows timestamp (relative: "2 mins ago", "1 hour ago")
- [ ] Colors: Green for creates, blue for starts, yellow for stops, red for errors, gray for deletes
- [ ] Click event to navigate to related instance or logs
- [ ] Show last 20 events (paginate if needed)
- [ ] Auto-refresh every 30 seconds (newest events appear at top)

**Priority:** P1 (Fast Follow)
**Epic:** Instance Monitoring

---

### US-022: Per-Instance Resource Monitoring Graph
**As a** Platform Admin or Instance Owner, **I want to** view historical resource usage graphs for my instance, **so that** I can identify trends and optimize instance size.

**Acceptance Criteria:**
- [ ] Instance detail page has "Monitoring" tab
- [ ] Three line charts showing last 7 days of data (default):
  - CPU usage over time (Y: %, X: time)
  - Memory usage over time (Y: %, X: time)
  - Disk usage over time (Y: GB or %, X: time)
- [ ] Charts update every 5 minutes (background fetch)
- [ ] Time range selector: Last 24 hours, Last 7 days, Last 30 days
- [ ] Hover on data point shows exact value and timestamp
- [ ] Current value displayed in tooltip or as text above chart
- [ ] Alert thresholds shown as dashed lines (CPU > 80%, Memory > 80%, Disk > 90%)
- [ ] Click chart point to jump to instance logs at that timestamp (if available)
- [ ] Export data as CSV button

**Priority:** P1 (Fast Follow)
**Epic:** Instance Monitoring

---

### US-023: Alerts for Resource Threshold Breaches
**As a** Platform Admin, **I want to** be notified when an instance exceeds resource limits, **so that** I can take action before the instance becomes unresponsive.

**Acceptance Criteria:**
- [ ] Admin settings allow setting alert thresholds:
  - CPU threshold (default: 85%)
  - Memory threshold (default: 85%)
  - Disk threshold (default: 90%)
- [ ] Toggle alerts on/off globally and per-instance
- [ ] When threshold is breached:
  - Toast notification appears in provisioner UI
  - Alert logged in Recent Activity feed
  - Email sent to admin (if email configured)
  - Alert can be dismissed (doesn't repeat for 1 hour unless threshold worsens)
- [ ] Alert shows: "Instance {name} CPU usage is {value}% (threshold: {threshold}%)"
- [ ] Clicking alert navigates to instance monitoring page
- [ ] Clear alert indicator when metric drops below threshold
- [ ] "Mute Alerts" button on instance detail page (silences for 24 hours)

**Priority:** P1 (Fast Follow)
**Epic:** Instance Monitoring

---

### US-024: Backup Status & Manual Backup Trigger
**As a** Platform Admin or Instance Owner, **I want to** see backup status and manually trigger backups, **so that** I have peace of mind my data is protected.

**Acceptance Criteria:**
- [ ] Instance detail page shows "Backups" tab
- [ ] Display last backup info: timestamp, size (GB), status (Successful, Failed, In Progress)
- [ ] Show backup schedule (if auto-backups enabled): "Daily at 2:00 AM UTC"
- [ ] "Backup Now" button manually triggers backup immediately
- [ ] During backup: Show spinner "Creating backup... this may take 5-10 minutes"
- [ ] On success: "Backup created: {size} GB, saved to {location}"
- [ ] On failure: "Backup failed. Check logs for details."
- [ ] List of last 10 backups with size and date (paginated if needed)
- [ ] Download backup button (generates download link with expiry)
- [ ] Delete old backup button (confirm before deleting)
- [ ] Note: Backup feature is not required for MVP but skeleton/placeholder is good

**Priority:** P1 (Fast Follow)
**Epic:** Instance Monitoring

---

## Epic: User Management

### US-025: View All Platform Users
**As a** Platform Admin, **I want to** see all users registered on the provisioner, **so that** I can manage accounts and troubleshoot user issues.

**Acceptance Criteria:**
- [ ] Admin settings has "Users" section showing table:
  - Email
  - Display Name
  - Instance Owned (instance name, or "-" if none)
  - Created Date
  - Last Login (if available)
  - Status (Active, Invited, Inactive)
  - Actions: Edit, Delete
- [ ] Sort by: Email, Created, Last Login (default: created)
- [ ] Filter by status: All, Active, Invited, Inactive
- [ ] Search by email or display name
- [ ] Pagination: 25 per page
- [ ] Hover on "Instance Owned" to see full instance name and link

**Priority:** P1 (Fast Follow)
**Epic:** User Management

---

### US-026: Delete Provisioner User Account
**As a** Platform Admin, **I want to** delete user accounts from the provisioner, **so that** I can remove inactive or problematic users.

**Acceptance Criteria:**
- [ ] Users table has "Delete" action button
- [ ] Clicking opens confirmation: "Delete {email}? This user will no longer be able to access the provisioner."
- [ ] **Important**: If user owns an instance, show warning: "This user owns instance '{instance}'. Deleting the user will NOT delete the instance. You must delete the instance separately if desired."
- [ ] Checkbox to acknowledge: "I understand this user owns an instance"
- [ ] On confirm, user is deleted and can no longer log in
- [ ] Toast: "User deleted"
- [ ] Cannot delete the last remaining PlatformAdmin user (show error)
- [ ] Deleted user's owned instances remain active (no cascade delete)

**Priority:** P1 (Fast Follow)
**Epic:** User Management

---

### US-027: User Inactivity Tracking
**As a** Platform Admin, **I want to** see which users are inactive, **so that** I can clean up unused accounts.

**Acceptance Criteria:**
- [ ] Users table includes "Last Login" column showing timestamp (or "Never" if never logged in)
- [ ] Filter by: Active (login in last 30 days), Inactive (no login for 30+ days), Never Logged In
- [ ] Admin settings has "Inactivity Auto-Delete" option:
  - Disabled (default)
  - Delete users inactive for 90 days (send email warning 7 days before)
  - Delete users inactive for 180 days
- [ ] If enabled, background job runs daily to identify and email users at risk
- [ ] Users marked for deletion get email: "Your account will be deleted in 7 days. Log in to prevent this."
- [ ] Can whitelist users to exclude from auto-delete

**Priority:** P1 (Fast Follow)
**Epic:** User Management

---

## Epic: Resource Quotas

### US-028: Enable/Disable Quotas Globally
**As a** Platform Admin, **I want to** enable or disable resource quotas for all instances, **so that** I can control how much compute each instance consumes.

**Acceptance Criteria:**
- [ ] Admin settings has "Resource Quotas" section with toggle:
  - "Enable Resource Quotas" (default: disabled)
- [ ] When disabled: No limits applied, all instances run unbounded
- [ ] When enabled: Default quotas apply to all new instances
- [ ] Show warning: "Enabling quotas on running instances will require restart to apply"
- [ ] Switching state shows confirmation dialog
- [ ] Once enabled, "Default Quotas" section appears

**Priority:** P2 (Future)
**Epic:** Resource Quotas

---

### US-029: Set Default Resource Quotas
**As a** Platform Admin, **I want to** configure default resource limits for new instances, **so that** I can prevent runaway consumption.

**Acceptance Criteria:**
- [ ] Admin settings → Resource Quotas shows form (only visible if quotas enabled):
  - **CPU Limit**: Number field, 0.5-4 cores (default: 1 core)
  - **Memory Limit**: Number field, 256-4096 MB (default: 512 MB)
  - **Disk Limit**: Number field, 1-100 GB (default: 5 GB)
- [ ] Tooltips explain each field
- [ ] "Save" button applies defaults to all future instances
- [ ] Show warning: "These limits apply to new instances only. Existing instances keep their current quotas."
- [ ] Save shows toast: "Default quotas updated"

**Priority:** P2 (Future)
**Epic:** Resource Quotas

---

### US-030: Per-Instance Quota Override
**As a** Platform Admin, **I want to** set custom quotas for individual instances, **so that** I can give premium users more resources.

**Acceptance Criteria:**
- [ ] Instance detail page has "Quotas" tab (only visible if quotas enabled)
- [ ] Show current quotas (inherited from defaults or custom):
  - CPU: X cores (editable, min 0.5, max 4)
  - Memory: Y MB (editable, min 256, max 4096)
  - Disk: Z GB (editable, min 1, max 100)
- [ ] Checkbox: "Override default quotas" (disabled by default)
- [ ] When checked, fields become editable
- [ ] "Save Quotas" button (disabled until changes made)
- [ ] Warning: "Changes require instance restart to take effect"
- [ ] Show "Restart to apply" button if quota changed while instance running
- [ ] Toast: "Quotas updated"

**Priority:** P2 (Future)
**Epic:** Resource Quotas

---

### US-031: Quota Enforcement & OOM/OOD Handling
**As a** Platform Admin, **I want to** have quotas enforced with graceful handling of overages, **so that** one instance doesn't crash the system.

**Acceptance Criteria:**
- [ ] Docker containers are spawned with `--cpus`, `--memory`, `--memory-swap` flags
- [ ] When memory limit hit (OOM):
  - Container receives OOM kill
  - Provisioner logs error: "Instance {name} killed due to out-of-memory"
  - Instance status becomes "Stopped (OOM)"
  - Toast alert sent to admin: "Instance {name} ran out of memory"
  - User attempting to join gets: "Instance unavailable. Admin has been notified."
- [ ] When disk limit hit (OOD):
  - Database inserts start failing with "disk quota exceeded" errors
  - HotBox instance logs errors and may crash
  - Provisioner detects stopped instance and marks as "Stopped (Disk Full)"
  - Admin alert: "Instance {name} is out of disk"
- [ ] Instance can be restarted after issue is resolved
- [ ] Quota monitoring shows usage trending toward limit with warnings

**Priority:** P2 (Future)
**Epic:** Resource Quotas

---

## Implementation Notes

### Data Persistence
- Provisioner database should store: instances, users, invites, audit logs, settings
- Recommended: PostgreSQL or SQLite (SQLite sufficient for single-server MVP)
- Backup strategy: Daily snapshots of provisioner DB (separate from instance databases)

### Docker Orchestration
- Use Docker Compose for provisioner itself
- Dynamically create/manage instance containers via Docker API (`Docker.DotNet` NuGet package)
- Volumes for instance persistence: named volumes or bind mounts for databases
- Network: Shared Compose network for instance-to-provisioner communication

### Security Considerations
- All secrets (API keys, passwords) stored encrypted at rest
- Admin session tokens should expire after 24 hours of inactivity
- Instance admin accounts are separate from provisioner accounts (no privilege escalation)
- Audit log: All admin actions (create, delete, restart) logged with timestamp and admin email
- Rate limiting on signup endpoints to prevent abuse

### Error Handling & Observability
- All provisioner errors logged with context (instance ID, user email, action)
- Container failures (OOM, timeout) tracked and reported
- Health checks for instance containers (liveness probe every 30s)
- Provisioner health endpoint: `/health` returning JSON status

### Extensibility for Hosting Operators
- All configuration externalizable via environment variables
- Instance base image should be configurable (e.g., `HOTBOX_IMAGE=ghcr.io/hotboxchat/hotboxchat:latest`)
- DNS provider abstraction (interfaces for Namecheap, Cloudflare, Route53, etc.)
- Webhook hooks for custom integrations (instance created, user signed up, etc.)

---

## Related Documentation

- **DNS Setup**: `/docs/provisioner/dns-setup.md`
- **Deployment Guide**: `/docs/deployment/` (to be created)
- **API Documentation**: `/docs/provisioner-api.md` (to be created)
- **Configuration Reference**: `/docs/provisioner-config.md` (to be created)
