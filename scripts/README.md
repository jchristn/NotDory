# Agent connection scripts

One-shot scripts that connect an AI agent to the NotDory MCP server, or disconnect it. Every OS folder has the
same scripts, with the same names, steps, messages and exit codes:

```
scripts/
  windows/   install-<agent>.bat   remove-<agent>.bat   reinstall-<agent>.bat   notdory-claude-hook.bat
  macos/     install-<agent>.sh    remove-<agent>.sh    reinstall-<agent>.sh   notdory-claude-hook.sh
  linux/     install-<agent>.sh    remove-<agent>.sh    reinstall-<agent>.sh   notdory-claude-hook.sh
  wsl/       install-<agent>.sh    remove-<agent>.sh    reinstall-<agent>.sh   notdory-claude-hook.sh   (WSL 1)
  wsl2/      install-<agent>.sh    remove-<agent>.sh    reinstall-<agent>.sh   notdory-claude-hook.sh   (WSL 2)
  tests/     test-scripts.py
```

Agents: **claude** (Claude Code), **codex**, **cursor**, **gemini** (Gemini CLI), **mux**.

NotDory serves MCP over Streamable HTTP at `http://127.0.0.1:8720/mcp` and its REST API on port 8700. See
`docs/CONNECTING_AGENTS.md` and `docs/INSTRUCTIONS_FOR_*.md` for the full per-agent connection notes.

## Usage

Windows (Command Prompt or PowerShell):

```bat
scripts\windows\install-claude.bat [ACCESS_KEY]
scripts\windows\reinstall-claude.bat [ACCESS_KEY]
scripts\windows\remove-claude.bat
```

macOS, Linux and WSL (run the WSL scripts inside the distro: `wsl2/` on WSL 2, `wsl/` on WSL 1):

```sh
scripts/macos/install-claude.sh [ACCESS_KEY]
scripts/linux/reinstall-codex.sh [ACCESS_KEY]
bash scripts/wsl2/remove-cursor.sh
```

Every script prints its usage with `-h` or `--help` (`/?` too on Windows) and rejects unknown options and extra
arguments with the same messages on every OS. Each script
checks that it runs on its own OS and names the right folder otherwise (the Linux scripts refuse to run under
WSL; the WSL scripts refuse to run outside WSL or on the other WSL version). `sh script.sh` also works: the
scripts re-run themselves with bash.

## Semantics

- **install** is idempotent: it adds the `notdory` entry, or replaces an existing one, and keeps every other server and
  setting in the config.
- **remove** deletes the `notdory` entry and succeeds with "nothing to remove" when there is none.
- **reinstall** removes the entry (continuing when there is none), then adds it again. It does the same as install;
  it exists so install, uninstall and reinstall are available for every agent on every OS.
- The server URL must be `http://` or `https://` with a host and no spaces; anything else is refused before any
  change is made.
- A config file that is not a JSON object, or a `config.toml` form the scripts cannot edit safely (an inline
  `mcp_servers` table, or `mcp_servers.notdory` written with dotted keys or as an inline table), is reported and left
  unchanged; nothing is overwritten with a guess. With Python 3.11+ the new `config.toml` is also parsed before it is
  written. Names are matched case-sensitively, like the clients do, and a key that differs from ours only in case
  is refused. Writes are atomic (a uniquely named temporary file in the same folder), keep the file's permissions
  (a new file is readable only by you), follow a symlinked config and keep CRLF line endings.
- Restart the agent afterwards to pick up the change.

## What each script does

| Agent | How it connects | Config it edits (macOS, Linux, WSL / Windows) |
| --- | --- | --- |

| **claude** | runs `claude mcp remove --scope user` and `claude mcp add --scope user --transport http` with the `x-access-key` header, then installs the NotDory SessionStart hook | Claude Code's user-scope config (`~/.claude.json`), plus `~/.claude/settings.json` for the hook |

| **codex** | writes an `[mcp_servers.notdory]` table (`url`, `http_headers`) | `${CODEX_HOME:-~/.codex}/config.toml` / `%CODEX_HOME%\config.toml` (default `%USERPROFILE%\.codex\config.toml`) |

| **cursor** | writes an `mcpServers.notdory` entry (`url`, `headers`) | `~/.cursor/mcp.json` / `%USERPROFILE%\.cursor\mcp.json` |

| **gemini** | writes an `mcpServers.notdory` entry (`httpUrl`, `headers`) | `~/.gemini/settings.json` / `%USERPROFILE%\.gemini\settings.json` |

| **mux** | writes a `notdory` object in the `servers` array (`transport: http`, origin `url`, `mcpPath`, `auth` type `bearer`) | `${MUX_CONFIG_DIR:-~/.mux}/mcp-servers.json` / `%MUX_CONFIG_DIR%\mcp-servers.json` (default `%USERPROFILE%\.mux\mcp-servers.json`) |

The formats follow each client's documentation: `claude mcp add --help`, Codex's `config.toml` (`codex mcp get notdory`
shows the result), Cursor's `mcp.json` (`url`, `headers`), the Gemini CLI's `settings.json` (`httpUrl` for Streamable
HTTP, `headers`) and Mux's `mcp-servers.json` (read from `MUX_CONFIG_DIR` when set, otherwise `~/.mux`).

Every agent authenticates with the credential **access key alone**; none sends the secret key, which stays
client-side. Claude Code, Codex, Cursor and the Gemini CLI send it in the `x-access-key` header; Mux sends it as a
bearer token (`Authorization: Bearer <accessKey>`), as `docs/INSTRUCTIONS_FOR_MUX.md` describes. Because the access key alone authenticates, treat it as a
**capability token** and use a least-privilege credential. Change the default credential before exposing NotDory
outside a trusted local environment; the defaults are local-development values.

On macOS, Linux and WSL the config is edited with `python3` (3.6 or later). On Windows the `.bat` file carries its
PowerShell code after a `## POWERSHELL` line and runs it with `powershell -Command` (not subject to the execution
policy); nothing else is needed. The
`claude` scripts need the `claude` CLI on `PATH`. The NotDory hook needs `curl` (and uses `git` when present).

## Overriding the defaults

| Variable | Default | Applies to |
| --- | --- | --- |

| `NOTDORY_MCP_URL` | `http://127.0.0.1:8720/mcp` | every agent (Mux stores its origin and path) |

| `NOTDORY_MCP_BASE_URL` | derived from `NOTDORY_MCP_URL` | mux only: the server origin, path `/mcp` |

| `NOTDORY_ACCESS_KEY` | `notdorydefaultkey` | install and reinstall; the first argument overrides it |

| `NOTDORY_REST_URL` | the `NOTDORY_MCP_URL` host on port 8700 | the Claude Code SessionStart hook |

| `NOTDORY_SESSION_HOOK` | `1` | set to `0` to skip installing the hook |

| `NOTDORY_CODEX_CONFIG`, `NOTDORY_CURSOR_CONFIG`, `NOTDORY_GEMINI_CONFIG`, `NOTDORY_MUX_CONFIG` | the paths above | the config file to edit |

| `CODEX_HOME`, `MUX_CONFIG_DIR`, `CLAUDE_CONFIG_DIR` | the clients' own defaults | honored the same way the clients honor them |

Example, connecting to a remote server:

```sh
NOTDORY_MCP_URL=https://server.example/mcp scripts/linux/install-claude.sh "$NOTDORY_ACCESS_KEY"
NOTDORY_MCP_URL=https://server.example/mcp scripts/linux/install-mux.sh "$NOTDORY_ACCESS_KEY"
```

```bat
set NOTDORY_MCP_URL=https://server.example/mcp
scripts\windows\install-claude.bat %NOTDORY_ACCESS_KEY%
```

## WSL

The `wsl/` and `wsl2/` scripts configure the agent that runs *inside* the distro and write its config under the
distro's home directory. For an agent running on Windows, run the `scripts\windows` script from Windows. They
source `_wsl-common.sh`, which:

- drops Windows (`/mnt/<drive>/...`) entries from `PATH`, so a Windows `claude`, `codex` or `gemini` shim is never
  used; a tool that exists only on the Windows side is reported as such (`WSL_KEEP_WINDOWS_PATH=1` keeps them);
- fails clearly on scripts checked out with CRLF line endings and warns about a checkout on `/mnt/<drive>`;
- refuses to run on the other WSL version.

On **WSL 2** with NAT networking, `127.0.0.1` inside the distro is not Windows. When `NOTDORY_MCP_URL` (and, for mux,
`NOTDORY_MCP_BASE_URL`) is not set and nothing answers at the default address inside WSL but the Windows host does,
the scripts use the Windows host address instead. Set the URL yourself to choose, `WSL_NO_HOST_REWRITE=1` to keep
`127.0.0.1`, or enable `networkingMode=mirrored` in `.wslconfig`. The host address can change after a Windows
restart, so re-run the install script if the agent stops connecting.

On **WSL 1** the distro shares the network with Windows, so `127.0.0.1` already reaches a server running on Windows
and the URL is never rewritten.

## Claude Code: every project, no further steps

`install-claude` is the only step. It registers NotDory for every project (user scope) and installs a SessionStart
hook in the Claude Code user settings (`${CLAUDE_CONFIG_DIR:-~/.claude}/settings.json`). At the start of each session the
hook sends NotDory the project's git remote and folder name
(`GET /v1.0/api/session?remote=...&directory=...&format=text`), and Claude Code adds the returned context (how to use
NotDory, the project's scope, its categories and instructions, and the most recent memories) to the model's context
before its first turn. The server picks the scope by the remote's repository name, then the folder name, so a clone in
a differently named folder still finds its memory; a new repository gets a scope named for it. The hook prints
nothing when NotDory is unreachable, so it never blocks a session.

- The hook command holds no credential. The access key and REST URL are written to
  `~/.notdory/session-hook.curlrc` (`%USERPROFILE%\.notdory\session-hook.curlrc` on Windows), a curl config file
  readable only by you, which the hook passes to `curl -K`.
- Re-running updates the NotDory hook in place and keeps every other hook and setting; the settings file is backed up
  to `settings.json.bak` (with the same permissions) once per run. `remove-claude` removes the hook and its key file.
- `NOTDORY_SESSION_HOOK=0` makes install and reinstall remove an existing hook instead of installing one.
- `notdory-claude-hook.sh install [REST_URL] [ACCESS_KEY]` / `remove` (`notdory-claude-hook.bat` on Windows, same
  arguments) manages the hook on its own.
- The hook is a bash command line. On Windows, Claude Code runs it with Git Bash.

## Tests

`python3 scripts/tests/test-scripts.py` runs every Unix script against a temporary home directory with fake
`claude`, `uname`, `curl` and `ip` tools and checks the files each one writes, the install/remove/reinstall rules,
argument handling and the OS guards, again under `/usr/bin/python3` when that is another version (macOS: 3.9).
When `pwsh` is installed it runs the same scenarios through the PowerShell part of every Windows script and checks
that Windows writes the same entries and prints the same messages as the Unix scripts.
