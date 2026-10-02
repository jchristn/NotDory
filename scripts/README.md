# Agent connection scripts

One-shot scripts to connect (or disconnect) an AI agent to the NotDory MCP server. There is an
`install-<agent>` and a `remove-<agent>` script per supported agent, in a folder per OS.

```
scripts/
  windows/   install-<agent>.bat   remove-<agent>.bat
  macos/     install-<agent>.sh    remove-<agent>.sh
  linux/     install-<agent>.sh    remove-<agent>.sh
```

Supported agents: **claude**, **codex**, **gemini**, **cursor**, **mux**.

## Usage

Windows (Command Prompt / PowerShell):

```bat
scripts\windows\install-cursor.bat
scripts\windows\remove-cursor.bat
```

macOS / Linux:

```sh
sh scripts/macos/install-cursor.sh      # or scripts/linux/... ; the two are identical
sh scripts/linux/remove-cursor.sh
```

Each `install` script is idempotent — it updates the existing `notdory` entry in place and preserves every
other MCP server in the config. Restart the agent afterward to pick up the change.

## What each script does

| Agent | How it connects | Config it edits |
| --- | --- | --- |
| **claude** | runs the `claude` CLI at user scope (`claude mcp add --scope user`), then installs the NotDory SessionStart hook (`notdory-claude-hook.ps1` / `notdory-claude-hook.sh`) | Claude Code's own store, and `~/.claude/settings.json` for the hook |
| **codex** | writes an `mcpServers.notdory` entry (`type: http`) | `~/.codex/config.json` |
| **cursor** | writes an `mcpServers.notdory` entry | `~/.cursor/mcp.json` |
| **gemini** | writes an `mcpServers.notdory` entry (`httpUrl`) | `~/.gemini/settings.json` |
| **mux** | appends a `notdory` object to the `servers` array (bearer auth, access key only) | `~/.mux/mcp-servers.json` |

The scripts connect to `http://127.0.0.1:8720/mcp`. Every agent authenticates with the credential
**access key alone** — none send the secret, which stays client-side. **Mux** carries the access key as a
bearer token (`Authorization: Bearer <accessKey>`); **claude**, **codex**, **cursor**, and **gemini** send
it in the `x-access-key` header. Because the access key alone authenticates, treat it as a **capability
token** and prefer a least-privilege credential. On Windows the JSON is edited with PowerShell; on
macOS/Linux with `python3` (required). The `claude` scripts require the `claude` CLI on `PATH`.

## Overriding the defaults

Set environment variables before running:

| Variable | Default | Applies to |
| --- | --- | --- |
| `NOTDORY_MCP_URL` | `http://127.0.0.1:8720/mcp` | claude, codex, cursor, gemini |
| `NOTDORY_MCP_BASE_URL` | `http://127.0.0.1:8720` | mux (path is `/mcp`) |
| `NOTDORY_ACCESS_KEY` | `notdorydefaultkey` | all |
| `NOTDORY_CODEX_CONFIG` / `NOTDORY_CURSOR_CONFIG` / `NOTDORY_GEMINI_CONFIG` / `NOTDORY_MUX_CONFIG` | the paths above | override a config file location |

No script sends a secret key, and `NOTDORY_SECRET_KEY` is no longer used by any script.

Every `install` script also accepts an optional `[ACCESS_KEY]` as its **first positional argument**, which
overrides `NOTDORY_ACCESS_KEY` (which in turn overrides the default). The access key is the public,
transferable material and is a **capability token** — for mux it authenticates on its own, so scope it
least-privilege:

```sh
sh scripts/linux/install-mux.sh access_ci      # access key as arg #1; mux sends it as a bearer token
```

Example (macOS/Linux), connecting with a least-privilege credential:

```sh
NOTDORY_ACCESS_KEY=access_ci sh scripts/linux/install-cursor.sh
```

Change the default credential before exposing NotDory outside a trusted local environment; the defaults are
local-development values. See `docs/CONNECTING_AGENTS.md` and `docs/INSTRUCTIONS_FOR_*.md` for the full
per-agent connection notes.

## Claude Code: every project, no further steps

`install-claude` is the only step. It registers NotDory for every project (user scope) and installs a SessionStart hook
in `~/.claude/settings.json`. At the start of each session the hook sends NotDory the project's git remote and folder name
(`GET /v1.0/api/session?remote=...&directory=...&format=text`), and Claude Code adds the returned context (how to use
NotDory, the project's scope, its categories and instructions, and the most recent memories) to the model's context
before its first turn. The server picks the scope by the remote's repository name, then the folder name, so a clone in
a differently named folder still finds its memory; a new repository gets a scope named for it; a folder that is not a
git repository and matches nothing gets none. The hook prints nothing when NotDory is unreachable, so it never blocks a
session.

- `NOTDORY_MCP_URL` (default `http://127.0.0.1:8720/mcp`), `NOTDORY_ACCESS_KEY`, and `NOTDORY_REST_URL` (default: the MCP host
  on port 8700) configure it; `NOTDORY_SESSION_HOOK=0` skips the hook.
- Re-running updates the NotDory entry and hook in place and keeps every other hook and setting (the settings file is
  backed up to `.bak`). `remove-claude` removes both.
- On macOS and Linux the hook is written with `python3` or `node`; without either the script prints the hook to add.
