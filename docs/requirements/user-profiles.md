# Requirements: User Profiles

## Executive Summary

User profiles give HotBox members a public identity and provide a consistent way to view information about any user. The profile system includes a read-only profile view page, a self-service profile edit page, an avatar dropdown menu in the top bar, and a set of reusable UI components (avatar, status indicator, admin actions) shared across the entire application.

## Problem Statement

Users need a way to view information about other members (who they are, what their role is, when they joined) and to customize their own public-facing identity. Currently there is no profile experience — no way to view another user's details or edit your own.

## Primary Purpose

Provide a clean, minimal profile experience that lets users view each other's information and manage their own public identity.

## Features

### Profile View Page (Read-Only)

A dedicated full page showing another user's public information. This is the universal destination when clicking any avatar or display name anywhere in the app — including your own avatar in chat messages or the member list.

**Displayed fields:**
- Generated avatar (initials + color)
- Display name
- Online status (Online, Idle, Do Not Disturb, Offline)
- Custom status message (if set)
- Role badge (admin, moderator, member)
- Join date

**Actions:**
- **Send Message** — button that initiates a 1-on-1 DM with the user
- **Admin actions** (visible only to users with appropriate permissions):
  - Change role
  - Kick
  - Ban

### Profile Edit Page (Own Profile Only)

A separate page for editing your own profile. Only accessible via the avatar dropdown in the top bar — not by clicking your own avatar elsewhere.

**Editable fields:**
- Display name
- Custom status message (free text, 128 character limit)

**Non-editable (displayed for context):**
- Generated avatar (initials + color)
- Role badge
- Join date

### Avatar Dropdown Menu

Clicking your own avatar in the **top bar** opens a small dropdown menu with:
- **Profile** — navigates to the profile edit page
- **Logout** — logs out and redirects to login

This is the only entry point to the profile edit page.

### Reusable UI Components

All profile-related UI elements must be built as reusable Blazor components, used consistently across the entire application.

#### Avatar Component
- Renders generated initials with a consistent color derived from the user's identity
- Includes an online status indicator dot
- Always links to the user's read-only profile view page (except in the top bar, where it triggers the dropdown)
- Used everywhere: chat messages, member list, profile pages, DM list, etc.

#### Status Indicator
- Small colored dot representing online status (Online, Idle, Do Not Disturb, Offline)
- Reusable independently or as part of the avatar component

#### Admin Action Controls
- Role change, kick, and ban controls
- Permission-gated — only rendered when the viewing user has the required role
- Reusable on the profile view page and potentially in admin panels

## Navigation & Interaction Model

| Action | Destination |
|--------|-------------|
| Click any avatar/name in chat messages | Profile view (read-only) |
| Click any avatar/name in member list | Profile view (read-only) |
| Click your own avatar in chat/member list | Profile view (read-only, same as anyone else) |
| Click your avatar in the top bar | Avatar dropdown menu |
| Dropdown → Profile | Profile edit page |
| Dropdown → Logout | Log out |

## Data Model Changes

The `AppUser` entity needs one new field:

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `CustomStatus` | `string?` | Max 128 characters, nullable | Free text status message (e.g., "In a meeting") |

Existing fields used by profiles (no changes needed):
- `DisplayName` (string, max 100 chars)
- `AvatarUrl` (string?, max 500 — unused for now, reserved for future image uploads)
- `Status` (UserStatus enum: Online, Idle, DoNotDisturb, Offline)
- `CreatedAtUtc` (DateTime — used as join date)

## API Endpoints

| Method | Route | Description | Auth |
|--------|-------|-------------|------|
| `GET` | `/api/users/{id}/profile` | Get a user's public profile | Authenticated |
| `GET` | `/api/users/me/profile` | Get current user's profile (for edit page) | Authenticated |
| `PUT` | `/api/users/me/profile` | Update current user's profile | Authenticated |

### Response: User Profile
```json
{
  "id": "guid",
  "displayName": "string",
  "status": "Online",
  "customStatus": "string|null",
  "role": "Member",
  "createdAtUtc": "2025-01-15T00:00:00Z"
}
```

### Request: Update Profile
```json
{
  "displayName": "string (required, max 100)",
  "customStatus": "string|null (max 128)"
}
```

## Out of Scope

- **Avatar image uploads** — deferred to content management feature; generated avatars only for now
- **Account settings** (email, password, 2FA) — separate settings page, separate effort
- **Profile popover/card** — may add later as a lightweight inline preview; full page only for now
- **Bio, pronouns, links** — not needed for HotBox's target audience and minimal design
- **Custom status emoji/icons** — plain text only
- **Profile visibility/privacy settings** — all profiles are visible to all authenticated users
- **Blocking users** — future feature, not part of profiles MVP

## Design Notes

- Follows HotBox's dark-mode-first, clean aesthetic
- Profile view should feel fast — no loading spinners, data should be lightweight
- The avatar component and its generated color scheme should match the existing prototype (`prototypes/main-ui-proposal.html`)
- Admin actions on the profile page should be visually de-emphasized (not primary buttons) to keep the focus on the user's information

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Separate view and edit pages | Simpler than toggling edit mode; clear separation of concerns |
| Generated avatars only (no uploads) | Avoids file storage complexity; revisited with content management |
| Top bar dropdown is the only edit entry point | Keeps avatar click behavior consistent everywhere else (always → view) |
| Custom status is plain public text | No visibility toggle, no emoji — clear it by deleting; simple |
| 128 char limit on custom status | Prevents abuse without being too restrictive |
| No profile popover for now | Full page is the foundation; popover can be layered on later |
| Account settings on a separate page | Profile = public face; account settings = security/plumbing |
