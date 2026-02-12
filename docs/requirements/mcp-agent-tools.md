# Requirements: MCP Agent Tools

## Executive Summary

Add MCP (Model Context Protocol) server capabilities to HotBox, enabling AI agents to create accounts and interact with the platform via text channels and direct messages. The immediate purpose is testing and simulation — spinning up multiple agent users to stress test, break things, and validate real-time behavior. The foundation supports future bot/agent integrations.

## Problem Statement

HotBox needs a way to simulate realistic multi-user activity for testing. Manually creating accounts and typing messages doesn't scale. An MCP-based approach lets AI agents drive the platform programmatically, and the same infrastructure becomes the bot/integration story later.

## Primary Purpose

Enable MCP-compatible clients to create and control agent users on HotBox for testing, simulation, and future bot integrations.

## Target Users

- **Developers** — using MCP tools via Claude Code or Claude Desktop to test and simulate load
- **AI agents/bots** — future consumers of the same API key infrastructure

## Phased Delivery

### Phase 0: Domain Review & Design

Run the requirements past the relevant domain agents before any implementation begins.

| Agent | Concern |
|-------|---------|
| **platform** | API key entity, migration, new project in solution, middleware |
| **auth-security** | API key auth scheme, `IsAgent` flag on ApplicationUser, auth flow for agents |
| **messaging** | Agents sending/reading messages via channels and DMs |
| **client-experience** | Admin UI for API key management, displaying agent users |

**Deliverables:**
- Domain agent feedback collected and conflicts resolved
- Architecture/design decisions documented in `docs/architecture/`

### Phase 1: API Key Infrastructure

**Features:**
- `ApiKey` entity — key value, name/label, created date, revoked flag
- Each API key is independent (not tied to a specific user account)
- One key can control multiple agent accounts
- `IsAgent` bool flag on `ApplicationUser` entity
- API key authentication middleware (header-based)
- Agent accounts created via API key are linked back to the key that created them
- Admin endpoints for creating, revoking, and listing API keys
- No scopes or permissions on keys for now — all keys have full agent access

**Deliverables:**
- Entity, migration, middleware implemented
- Admin endpoints functional
- Documentation: API key management guide in `docs/`

### Phase 2: MCP Server & Tools

**Setup:**
- Standalone project in the solution (e.g., `HotBox.Mcp`)
- Communicates with HotBox over HTTP/SignalR (exercises the real stack)
- Authenticates using an API key

**MCP Tools:**

| Tool | Description |
|------|-------------|
| `create_agent_account` | Register a new user with `IsAgent = true`, linked to the calling API key |
| `list_agent_accounts` | List all agent accounts created under the current API key |
| `send_message` | Post a message to a text channel as a specified agent |
| `send_direct_message` | Send a DM to another user as a specified agent |
| `read_messages` | Retrieve recent messages from a text channel |
| `read_direct_messages` | Retrieve recent DMs from a conversation |

**Deliverables:**
- MCP server project functional with all 6 tools
- Connectable from Claude Desktop and Claude Code
- Documentation: MCP server setup guide in `docs/`

### Phase 3: Agent Simulation & Orchestration

**Features:**
- Run 1-5 agents concurrently in a channel
- Each agent has a distinct personality
- Simulation loop: wait (random delay) → read recent messages → post a reply
- LLM-driven message generation for realistic conversations
- Can be driven via Claude Code or a standalone CLI harness (approach TBD at implementation time)

**Deliverables:**
- Working simulation with configurable agent count and personalities
- Documentation: Agent simulation guide in `docs/`

## Technical Decisions

| Decision | Rationale |
|----------|-----------|
| Per-bot API keys (no scopes) | Simple revocation per key, scopes can be layered on later |
| `IsAgent` flag on ApplicationUser | Lightweight way to distinguish agents from real users without a separate entity |
| One key controls many agents | Testing use case: one key, spawn N users |
| Separate MCP project | Clean separation; talks to HotBox over its public API to test the real stack |
| Domain review before implementation | Changes cut across platform, auth, messaging, and client domains |
| Documentation per phase | Keeps docs current alongside implementation |

## Out of Scope

- API key scopes/permissions system
- Audio/voice channel interaction via MCP
- Channel management (create/delete) via MCP
- API key management via MCP (stays in HotBox admin)
- Real-time message subscriptions (pull-based reading only for now)

## Open Questions

- Exact API key header format (e.g., `X-Api-Key` vs `Authorization: ApiKey ...`)
- Whether Phase 3 orchestrator is a standalone CLI app or a script-based approach
- Random delay strategy for simulation loop (fixed intervals, random range, message-rate-based)
- Admin UI specifics for API key management (dedicated page vs settings section)
