# Agent Simulation Guide

Simulate multi-agent conversations in HotBox channels using the `/simulate` command in Claude Code. This tool creates temporary agent accounts, generates realistic group chat activity, and helps test messaging infrastructure under load.

## Prerequisites

- Claude Code CLI with MCP server configured (see `docs/mcp-server-setup.md`)
- A running HotBox instance with at least one text channel
- API key with admin privileges (see `docs/api-key-management.md`)

## Quick Start

Basic simulation with default settings (3 agents, 10 rounds):

```
/simulate general
```

Custom configuration:

```
/simulate general 5 20
```

This runs 5 agents for 20 conversation rounds in the "general" channel.

## Command Syntax

```
/simulate <channel> [agents] [rounds]
```

### Parameters

| Parameter | Required | Default | Range | Description |
|-----------|----------|---------|-------|-------------|
| `channel` | Yes | - | - | Channel name to simulate in (e.g., "general", "random") |
| `agents` | No | 3 | 1-5 | Number of simultaneous agent participants |
| `rounds` | No | 10 | 1+ | Number of conversation turns (one message per round) |

### Examples

**Single agent, short test:**

```
/simulate dev-testing 1 5
```

**Full group, extended conversation:**

```
/simulate water-cooler 5 50
```

**Default settings:**

```
/simulate announcements
```

## How It Works

The simulation runs in three phases: setup, conversation loop, and wrap-up.

### Phase 1: Setup

1. **Channel lookup** — Finds the target channel by name using `mcp__hotbox__read_channels`
2. **Agent creation** — Creates 1-5 agent accounts in parallel with unique emails based on Unix timestamp
3. **Authentication** — Each agent receives a JWT bearer token for subsequent operations

Agent accounts are created with emails like `sim-alex-1739483200@hotbox.agent` (the number is the run ID, derived from the current Unix timestamp). This ensures uniqueness across multiple simulation runs.

### Phase 2: Conversation Loop

For each round (1 through N):

1. **Pick agent** — Cycles through agents in order (Agent 1 → 2 → 3 → 1... etc.)
2. **Read context** — Fetches the last 15 messages from the channel
3. **Generate message** — Creates a 1-2 sentence reply in the agent's personality
4. **Send message** — Posts the message to the channel
5. **Wait** — Pauses 3-10 seconds (random delay) before the next round
6. **Report progress** — Prints status update every 5 rounds

Messages are sent **sequentially** with delays, not in parallel. This mimics natural human conversation pacing.

### Phase 3: Wrap-Up

Prints a summary:

- Total rounds completed
- Agent participation counts (e.g., "Alex sent 7 messages")
- Reminder that the conversation is visible in the HotBox channel

## Agent Personalities

Each agent has a distinct personality and writing style to create realistic group chat dynamics.

| Agent | Personality | Writing Style | Example |
|-------|-------------|---------------|---------|
| Alex | Enthusiastic Extrovert | Upbeat, uses exclamation points, asks questions | "oh nice what are you building?" |
| Sam | Dry Wit | Sarcastic, deadpan humor, short messages | "classic monday energy" |
| Jordan | Curious Thinker | Asks interesting questions, philosophical | "do you think that scales for larger teams" |
| Casey | Chill Friend | Relaxed, casual, supportive, uses "lol" and "haha" | "lol that reminds me of last week" |
| Riley | Know-It-All | Has a fact for everything, informative | "fun fact that pattern is called observer" |

Agents are selected in order based on the number of agents configured:

- 1 agent: Alex
- 2 agents: Alex, Sam
- 3 agents: Alex, Sam, Jordan
- 4 agents: Alex, Sam, Jordan, Casey
- 5 agents: Alex, Sam, Jordan, Casey, Riley

Messages are:

- **Short** — ~150 characters max, typically 1-2 sentences
- **Casual** — Lowercase, minimal punctuation, natural group chat tone
- **Contextual** — Reply to recent messages when possible, or start new topics
- **No markdown** — Plain text only, no formatting or emoji

## Error Handling

The simulation gracefully handles common failure scenarios:

| Error | Behavior |
|-------|----------|
| Channel not found | Stops immediately, reports error |
| Agent creation fails | Skips failed agent, continues with remaining (needs at least 1) |
| Message read fails | Generates a topic-starter instead of a contextual reply |
| Message send fails | Logs error, continues to next round |

Check Claude Code output for error messages during the simulation. Server-side errors (e.g., authentication failures) will appear in the MCP tool call results.

## Stress Testing Tips

Use simulations to identify performance bottlenecks and stability issues.

### High-Volume Load

Test message throughput and database performance:

```
/simulate stress-test 5 100
```

This generates 100 messages from 5 agents (~10-15 minutes with random delays). Monitor:

- Database query times (check HotBox logs)
- SignalR message delivery latency
- Memory usage on the server
- Message ordering (are messages arriving in the correct sequence?)

### Concurrent Simulations

Run multiple simulations in parallel to test concurrent user activity:

```bash
# Terminal 1
claude /simulate general 3 50

# Terminal 2
claude /simulate random 3 50

# Terminal 3
claude /simulate dev-chat 3 50
```

Monitor:

- Database connection pool exhaustion
- Thread starvation or deadlocks
- API response times under load
- SignalR hub scaling behavior

### Long-Running Tests

Test system stability over extended periods:

```
/simulate endurance 5 500
```

This runs for ~2.5-4 hours (500 messages × 3-10 second delays). Monitor:

- Memory leaks (check server memory usage over time)
- JWT token expiration handling (default tokens expire after 24 hours)
- Database connection leaks
- Log file growth and disk space

### Observability Checks

While simulations run, use HotBox's observability stack:

- **Serilog → Seq (dev)**: Check structured logs for errors, warnings, or performance anomalies
- **OpenTelemetry traces**: Track message latency from send to delivery
- **Database query logs**: Identify slow queries or N+1 problems
- **SignalR metrics**: Monitor hub connections, messages/sec, and disconnections

### Cleanup

Agent accounts are not automatically deleted after simulations. To clean up:

1. List agents: `GET /api/admin/users` (filter `isAgent: true`)
2. Delete agents: `DELETE /api/admin/users/{id}` (API not yet implemented — manual DB cleanup required for now)

Alternatively, restart the HotBox instance with a fresh database (dev environments only).

## Troubleshooting

### "Channel not found"

**Cause:** Channel name doesn't match an existing channel (case-sensitive).

**Solution:**

- List channels: `GET /api/channels` or check the HotBox UI
- Verify spelling and capitalization (use exact name)

### "Failed to create agent account"

**Cause:** API key is invalid, revoked, or MCP server is misconfigured.

**Solution:**

- Verify MCP server is running and configured (see `docs/mcp-server-setup.md`)
- Check API key is active: `GET /api/admin/apikeys`
- Test API key manually: `curl -H "X-Api-Key: YOUR_KEY" http://localhost:5000/api/channels`

### Simulation hangs or stops

**Cause:** Network timeout, server crash, or SignalR hub error.

**Solution:**

- Check HotBox server is running: `curl http://localhost:5000/health`
- Review HotBox logs for exceptions or errors
- Check MCP server stderr output for tool call failures
- Restart the simulation with fewer agents/rounds to isolate the issue

### Messages not appearing in HotBox UI

**Cause:** SignalR connection issue or client not subscribed to the channel.

**Solution:**

- Refresh the HotBox web client
- Check browser console for SignalR connection errors
- Verify the channel has messages: `GET /api/channels/{id}/messages`

### "Unauthorized" errors mid-simulation

**Cause:** JWT token expired during a long-running simulation.

**Solution:**

- Reduce round count to complete before token expiry (default: 24 hours)
- Increase JWT expiration in HotBox config: `JwtSettings:ExpirationMinutes`
- Restart the simulation to get fresh tokens

## Best Practices

1. **Start small** — Test with 1-2 agents and 5-10 rounds before scaling up
2. **Monitor logs** — Watch HotBox server logs in Seq during simulations
3. **Isolate tests** — Use dedicated test channels (e.g., "sim-test") to avoid polluting production channels
4. **Clean up agents** — Delete temporary agent accounts after testing (see Cleanup above)
5. **Document issues** — If you find bugs or performance problems, capture logs and simulation params for debugging
6. **Use realistic configs** — 3-5 agents with 10-50 rounds mimics typical small group chat activity

## Related Documentation

- **MCP Server Setup** — `docs/mcp-server-setup.md`
- **API Key Management** — `docs/api-key-management.md`
- **MCP Tool Reference** — See "Available Tools" section in `docs/mcp-server-setup.md`

## Future Enhancements

Planned improvements to the simulation command:

- **Custom personalities** — Define agent names and personalities via config file
- **Message rate control** — Specify exact delays instead of random 3-10 seconds
- **Direct message simulation** — Test DM functionality in addition to channels
- **Voice channel simulation** — Simulate WebRTC signaling and audio streams (future)
- **Parallel message sending** — Option to send messages simultaneously from multiple agents
- **Auto-cleanup** — Delete agent accounts automatically when simulation completes
