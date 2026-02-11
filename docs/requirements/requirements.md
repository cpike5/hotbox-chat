# Requirements: HotBox

## Executive Summary

HotBox is an open-source, self-hosted alternative to Discord. Built for small friend groups (~10 people) who are fed up with Discord's invasive verification policies, bloated Electron client, and clunky UX. While the primary audience is small private groups, the architecture should support small communities of up to ~100 concurrent users. The project prioritizes simplicity, performance, privacy, and self-hosting ease.

## Problem Statement

Discord is increasingly hostile to user privacy (facial ID / age verification), ships a bloated Electron-based client, and delivers a clunky, buggy UI experience with disruptive update flows. Users want a lightweight, self-hosted alternative they fully control.

## Primary Purpose

A self-hosted real-time communication platform with text channels, voice channels, and direct messaging — simple, fast, and private.

## Target Users

- **Primary**: Small friend groups (<10 people) who want a private, self-hosted chat platform
- **Secondary**: Small open-source communities (up to ~100 concurrent users) looking for a Discord alternative
- **Server Admin**: The person self-hosting and configuring the instance

## Core Features (MVP)

### Text Channels
- Organized text channels within a server (e.g., shitposting, gaming, general)
- Plain text messaging (no markdown, reactions, or threads for MVP)
- Flat channel list — no categories/nesting for MVP

### Voice Channels
- Persistent "always-on" voice rooms — drop in / drop out model
- Separate from text channels — no per-voice-channel text chat
- WebRTC as starting point (needs research for implementation details)

### Direct Messages
- Private 1-on-1 conversations outside of channels

### Authentication
- Email/password registration and login
- OAuth support (Google, Microsoft) — **optional and configurable**
- Login UI dynamically shows only configured auth providers
- Configurable registration mode: open, invite-only, or closed (admin creates accounts)

### Roles & Permissions
- Basic role system (admin, moderator, member at minimum)
- Configurable through application settings

### Message Search
- Full-text search across channel messages and direct messages
- Uses native database full-text search (no external search engine)
  - PostgreSQL: `tsvector`/`tsquery` with ranked results and language-aware stemming
  - MySQL/MariaDB: `FULLTEXT` index with `MATCH...AGAINST`
  - SQLite: FTS5 virtual tables with BM25 ranking
- Global search (Ctrl+K / Cmd+K) with optional per-channel filtering
- Search results include highlighted matching text, author, channel, and timestamp
- Clicking a result navigates to the message in context

### Notifications
- Desktop/browser notifications for mentions and messages

### Configuration
- Highly configurable via `appsettings.json` and environment variables
- Includes but not limited to:
  - Port configuration
  - Admin user/password seeding on first run
  - Role definitions
  - Registration mode (open / invite-only / closed)
  - OAuth provider settings (client ID/secret per provider)
  - Database provider selection
- Configuration list will grow as features are specified in detail

### Observability
- Built in from day one, not bolted on later
- **Logging**: Serilog with structured logging
- **Tracing & Metrics**: OpenTelemetry
- **Production**: Elasticsearch for APM traces, metrics, and logs
- **Development**: Seq for local log aggregation

## Future Features (Post-MVP)

- File/image/GIF/video sharing
- Push-to-talk (system-level keystroke hooks)
- Message formatting (markdown, emoji reactions, threads)
- Screen sharing
- Bot/plugin extensibility system
- Native desktop client (Avalonia for cross-platform)
- E2E encryption (way out of scope for MVP)

## Explicitly Out of Scope

- Per-voice-channel text chat (Discord overcomplicates this)
- JavaScript on the server (ever)
- Electron-based desktop client (ever)
- Scaling beyond ~100 concurrent users (community can fork/contribute)

## Tech Stack

| Layer | Technology | Notes |
|-------|-----------|-------|
| **Backend** | ASP.NET Core | API + real-time via SignalR |
| **Real-time** | SignalR (text), WebRTC (voice) | WebRTC needs research |
| **Web Client** | Blazor WASM | No JS frameworks |
| **Database (Dev)** | SQLite | Zero-config for development |
| **Database (Prod)** | PostgreSQL / MySQL / MariaDB | Configurable via EF Core providers |
| **ORM** | Entity Framework Core | Multi-provider support |
| **Auth** | ASP.NET Identity + OAuth | OAuth providers optional |
| **Logging** | Serilog | Structured logging |
| **Observability** | OpenTelemetry | Tracing and metrics |
| **APM (Prod)** | Elasticsearch | Traces, metrics, production logs |
| **Logging (Dev)** | Seq | Local log aggregation |
| **Containerization** | Docker / Docker Compose | First-class from the start |

## Design Preferences

- **Theme**: Dark mode by default
- **Feel**: Fast, responsive, not clunky — no loading screens blocking usage
- **Layout**: TBD — Discord-ish channel sidebar is functional but exact design to be explored through prototyping
- **Updates**: Seamless — no disruptive "downloading update" splash screens
- **Aesthetic**: Cleaner and less noisy than Discord, exact direction to be prototyped

## Architecture Principles

- Three-layer architecture: Core (domain) → Infrastructure (data) → Application (UI/API)
- `IServiceCollection` extension methods for DI registration
- Options pattern (`IOptions<T>`) for configuration
- Docker-first deployment
- Configuration via environment variables and `appsettings.json`
- Observable from day one (logging, tracing, metrics)

## Constraints

- **Scale**: ~100 concurrent users max design target
- **Performance**: Should feel snappy — no Electron-like sluggishness
- **Security**: Standard auth best practices; E2E encryption is a future goal
- **Self-hosting**: Must be easy to deploy via Docker Compose with minimal configuration

## Open Questions

- ~~WebRTC implementation details~~ — **Resolved**: P2P full mesh for MVP
- ~~Exact UI layout and design direction~~ — **Resolved**: Discord-inspired three-panel layout (prototype at `prototypes/main-ui-proposal.html`)
- ~~Notification delivery mechanism~~ — **Resolved**: SignalR push + Browser Notification API
- ~~Message history / search~~ — **Resolved**: Persist all messages, provider-aware native database FTS for search
- ~~User presence~~ — **Resolved**: MVP feature, tracked via SignalR connection lifecycle

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| ASP.NET Core backend | Aligns with developer experience, SignalR for real-time |
| Blazor WASM client | No JS frameworks, stays in .NET ecosystem, no Electron |
| Web-only for MVP | Fastest path to usable; native desktop client later |
| No per-voice-channel text | Unnecessary complexity, Discord overengineers this |
| SQLite for dev, SQL for prod | Easy dev setup, production-grade databases when deployed |
| Docker from day one | Avoid retrofitting later, self-hosting friendly |
| OAuth is optional | Login buttons appear dynamically based on configuration |
| Observability built-in | Serilog + OpenTelemetry from the ground up |
| No JS on the server | Non-negotiable |
