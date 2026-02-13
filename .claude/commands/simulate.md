---
description: Run a multi-agent chat simulation in a HotBox channel
arguments:
  - name: channel
    description: Channel name to simulate in (e.g. "general")
    required: true
  - name: agents
    description: "Number of agents: 1-5 (default: 3)"
    required: false
  - name: rounds
    description: "Number of conversation rounds (default: 10)"
    required: false
---

# Agent Chat Simulation

Run a multi-agent conversation in a HotBox text channel using the MCP tools.

## Arguments

- **channel**: $ARGUMENTS.channel (required)
- **agents**: $ARGUMENTS.agents (default: 3, max: 5)
- **rounds**: $ARGUMENTS.rounds (default: 10)

## Phase 1: Setup

### 1.1 Find the Target Channel

Use `mcp__hotbox__read_channels` with `nameFilter` set to the channel argument.
Extract the channel ID (GUID) from the result. If no channel matches, report the error and stop.

### 1.2 Generate a Run ID

Run via Bash: `date +%s` to get a Unix timestamp. Use this as a run ID for unique agent emails.

### 1.3 Create Agent Accounts

Create the requested number of agents using `mcp__hotbox__create_agent_account`.
**Create all agents in PARALLEL** (multiple tool calls in one message).

Use these identities:

| # | Display Name | Email Pattern |
|---|-------------|---------------|
| 1 | Alex | sim-alex-{runId}@hotbox.agent |
| 2 | Sam | sim-sam-{runId}@hotbox.agent |
| 3 | Jordan | sim-jordan-{runId}@hotbox.agent |
| 4 | Casey | sim-casey-{runId}@hotbox.agent |
| 5 | Riley | sim-riley-{runId}@hotbox.agent |

**Store each agent's bearer token** from the `accessToken` field in the creation response. You will need these tokens for every send/read call.

If an agent creation fails, skip it and continue with fewer agents. You need at least 1 agent to proceed.

### 1.4 Assign Personalities

Each agent has a distinct personality that determines their writing style:

| Agent | Personality | Writing Style |
|-------|-------------|---------------|
| Alex | Enthusiastic Extrovert | Upbeat, uses exclamation points, asks questions, shares opinions freely |
| Sam | Dry Wit | Sarcastic, deadpan humor, short messages, clever observations |
| Jordan | Curious Thinker | Asks interesting questions, connects ideas, sometimes philosophical |
| Casey | Chill Friend | Relaxed, casual language, supportive, positive vibes, uses "lol" and "haha" |
| Riley | Know-It-All | Has a fact for everything, makes corrections, informative but well-meaning |

### 1.5 Report Setup

Tell the user:
- Which channel was found (name and ID)
- How many agents were created (and their names)
- How many rounds will run

## Phase 2: Simulation Loop

Run the specified number of rounds. In each round:

### For each round (1 through N):

1. **Pick the agent**: Cycle through agents in order. Round 1 = Agent 1, Round 2 = Agent 2, etc., wrapping around.

2. **Read recent messages**: Use `mcp__hotbox__read_messages` with the channel ID, `limit: 15`, and the current agent's bearer token.

3. **Generate a message**: Write a chat message AS the current agent, in their personality. The message MUST:
   - Be 1-2 sentences (occasionally 3 for longer thoughts). Keep it SHORT.
   - Respond to or riff off recent messages when the conversation has context
   - Stay in character for the agent's personality
   - Sound like a real person in a casual group chat, NOT like an AI
   - Use natural chat conventions: lowercase is fine, abbreviations ok, no formal punctuation required
   - Sometimes start new topics, sometimes respond to existing threads
   - For the very first message in a simulation, kick off with a casual opener

   **Do NOT**:
   - Use markdown formatting, asterisks for emphasis, or bullet points
   - Write messages longer than ~150 characters
   - Sound robotic or overly formal
   - Repeat what other agents just said
   - Use the agent's own name in their message

4. **Send the message**: Use `mcp__hotbox__send_message` with the channel ID, the message content, and the agent's bearer token.

5. **Wait**: Run `sleep N` via Bash where N is a random number between 3 and 10. Vary it each round. Use: `sleep $((RANDOM % 8 + 3))`

6. **Brief progress**: Every 5 rounds, tell the user which round you're on (e.g. "Round 5/10...").

## Phase 3: Wrap Up

After all rounds complete, report:
- Total rounds completed
- Which agents participated and their message counts
- Remind the user the conversation is visible in the HotBox channel

## Error Handling

- If channel lookup fails → stop and report the error
- If agent creation fails → skip that agent, continue with remaining (need at least 1)
- If message send fails → log the error, continue to next round
- If message read fails → generate a topic-starter message instead of a reply

## Important Reminders

- Messages are sent SEQUENTIALLY (one per round, with delays between)
- Agent creation is done in PARALLEL
- Every `mcp__hotbox__send_message` and `mcp__hotbox__read_messages` call REQUIRES the agent's `bearerToken` — do not forget this parameter
- The `bearerToken` comes from the `accessToken` field returned by `mcp__hotbox__create_agent_account`
- Keep the conversation NATURAL — this is a casual group chat, not a formal discussion
