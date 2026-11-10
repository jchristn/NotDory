#!/usr/bin/env bash
# Disconnect Codex from the NotDory MCP server: removes the 'notdory' entry.
# Config: ${CODEX_HOME:-~/.codex}/config.toml
# Succeeds with "nothing to remove" when there is no 'notdory' entry; every other server is kept.
# WSL 2 version: configures Codex inside this distro (not on Windows). For Codex on Windows, run
# scripts\windows\remove-codex.bat from Windows.
# Usage: scripts/wsl2/remove-codex.sh
#   NOTDORY_CODEX_CONFIG  config file (default: as above)
# Requires python3.
if [ -z "${BASH_VERSION:-}" ]; then exec bash "$0" "$@"; fi
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl2
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

usage() { awk 'NR > 1 && /^#/ { sub(/^# ?/, ""); print; next } NR > 1 { exit }' "$0"; }
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
    -?*) die "Unknown option: $1 (see --help)" ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments; see --help)"
wsl_require_cmd python3 "Install Python 3 inside WSL (for example: sudo apt install python3)."
CONFIG="${NOTDORY_CODEX_CONFIG:-${CODEX_HOME:-$HOME/.codex}/config.toml}"
URL=""
wsl_log "This configures Codex inside this WSL distro. For Codex on Windows, run scripts\\windows\\remove-codex.bat from Windows."

python3 - remove "$CONFIG" notdory "$URL" "" "" <<'PY'
import json, os, re, sys, tempfile
def fail(msg):
    print("Error: " + msg, file=sys.stderr)
    sys.exit(1)
def mode_of(path, default):
    try:
        return os.stat(path).st_mode & 0o7777
    except FileNotFoundError:
        return default
def write_atomic(path, text, new_mode=0o600, force_mode=None):
    """Replaces the file in one step. Follows a symlinked config and keeps the file's mode."""
    path = os.path.realpath(path)
    folder = os.path.dirname(path) or "."
    os.makedirs(folder, exist_ok=True)
    mode = force_mode if force_mode is not None else mode_of(path, new_mode)
    fd, tmp = tempfile.mkstemp(prefix="." + os.path.basename(path) + ".", suffix=".tmp", dir=folder)
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as f:
            f.write(text)
        os.chmod(tmp, mode)
        os.replace(tmp, path)
    except BaseException:
        if os.path.exists(tmp):
            os.unlink(tmp)
        raise
def read_text(path):
    with open(path, encoding="utf-8-sig", newline="") as f:
        return f.read()
def check_case(obj, key, where, path):
    for k in obj:
        if k != key and k.lower() == key.lower():
            fail("%s of %s has a key '%s' that differs from '%s' only in case; rename or remove it, then run this again. The file was left unchanged." % (where, path, k, key))
action, path, name, url, hname, hvalue = sys.argv[1:7]
BARE = re.compile(r"[A-Za-z0-9_-]+")
OURS = ["mcp_servers", name]
def parse_key(s, i):
    """A dotted key at s[i:]: (parts, index after it), or (None, i)."""
    parts = []
    while True:
        while i < len(s) and s[i] in " \t":
            i += 1
        if i < len(s) and s[i] == '"':
            j, buf = i + 1, []
            while j < len(s) and s[j] != '"':
                if s[j] == "\\" and j + 1 < len(s):
                    buf.append(s[j + 1])
                    j += 2
                else:
                    buf.append(s[j])
                    j += 1
            if j >= len(s):
                return None, i
            parts.append("".join(buf))
            i = j + 1
        elif i < len(s) and s[i] == "'":
            j = s.find("'", i + 1)
            if j < 0:
                return None, i
            parts.append(s[i + 1:j])
            i = j + 1
        else:
            m = BARE.match(s, i)
            if not m:
                return None, i
            parts.append(m.group(0))
            i = m.end()
        while i < len(s) and s[i] in " \t":
            i += 1
        if i < len(s) and s[i] == ".":
            i += 1
            continue
        return parts, i
def parse_header(line):
    s = line.lstrip(" \t")
    if not s.startswith("["):
        return None
    arr = s.startswith("[[")
    parts, i = parse_key(s, 2 if arr else 1)
    if parts is None:
        return None
    close = "]]" if arr else "]"
    if not s.startswith(close, i):
        return None
    rest = s[i + len(close):].strip()
    if rest and not rest.startswith("#"):
        return None
    return parts
def scan(text, state):
    """Tracks multi-line strings (state[0]) and open [ / { (state[1]) across lines."""
    i = 0
    while i < len(text):
        if state[0]:
            j = text.find(state[0], i)
            if j < 0:
                return
            i = j + 3
            state[0] = None
            continue
        c = text[i]
        if c == "#":
            return
        if text.startswith('"' * 3, i) or text.startswith("'" * 3, i):
            state[0] = text[i:i + 3]
            i += 3
            continue
        if c == '"':
            j = i + 1
            while j < len(text) and text[j] != '"':
                j += 2 if text[j] == "\\" else 1
            i = j + 1
            continue
        if c == "'":
            j = text.find("'", i + 1)
            i = (j if j >= 0 else len(text)) + 1
            continue
        if c in "[{":
            state[1] += 1
        elif c in "]}":
            state[1] -= 1
        i += 1
def classify(lines):
    """(kind, table, full key) per line; kind is header, key, cont (inside a value) or other."""
    out, table, state = [], [], [None, 0]
    for line in lines:
        if state[0] or state[1] > 0:
            scan(line, state)
            out.append(("cont", table, None))
            continue
        h = parse_header(line)
        if h is not None:
            table = h
            out.append(("header", h, None))
            continue
        parts, i = parse_key(line, 0)
        rest = line[i:].lstrip(" \t") if parts is not None else ""
        if parts is not None and rest.startswith("="):
            scan(rest[1:], state)
            out.append(("key", table, table + parts))
            continue
        out.append(("other", table, None))
    return out
def check_structure(lines):
    for kind, table, full in classify(lines):
        if kind != "key":
            continue
        if full == ["mcp_servers"]:
            fail("%s defines mcp_servers as an inline table or a dotted key; this script can only manage a [mcp_servers.%s] table. Edit the file by hand; it was left unchanged." % (path, name))
        if full[:2] == OURS and table[:2] != OURS:
            fail("%s defines mcp_servers.%s with dotted keys or an inline table; this script can only manage a [mcp_servers.%s] table. Edit the file by hand; it was left unchanged." % (path, name, name))
def read_lines():
    if not os.path.exists(path):
        return [], False
    text = read_text(path)
    lines = [l[:-1] if l.endswith("\r") else l for l in text.split("\n")]
    return lines, "\r\n" in text
def strip_table(lines):
    """Drops [mcp_servers.<name>] and its sub-tables. Comments and blank lines just before the next
    table header stay with that table."""
    out, buf, skipping, found = [], [], False, False
    for line, (kind, table, full) in zip(lines, classify(lines)):
        if kind == "header":
            if table[:2] == OURS:
                skipping, found, buf = True, True, []
                continue
            if skipping:
                # Don't double the blank line that already separates the tables.
                while buf and not buf[0].strip() and (not out or not out[-1].strip()):
                    buf.pop(0)
                out.extend(buf)
                buf, skipping = [], False
            out.append(line)
            continue
        if skipping:
            if kind == "other" and (not line.strip() or line.lstrip().startswith("#")):
                buf.append(line)
            else:
                buf = []
            continue
        out.append(line)
    while out and not out[-1].strip():
        out.pop()
    return out, found
def toml_str(s):
    out = ['"']
    for ch in s:
        c = ord(ch)
        if ch == '"':
            out.append('\\"')
        elif ch == "\\":
            out.append("\\\\")
        elif c < 32 or c == 127:
            out.append("\\u%04x" % c)
        else:
            out.append(ch)
    return "".join(out) + '"'
def save(lines, crlf):
    check_structure(lines)
    nl = "\r\n" if crlf else "\n"
    text = nl.join(lines) + nl if lines else ""
    try:
        import tomllib  # Python 3.11+: also refuse anything else Codex could not load.
    except ImportError:
        tomllib = None
    if tomllib is not None:
        try:
            tomllib.loads(text)
        except tomllib.TOMLDecodeError as e:
            fail("%s would not be valid TOML after this change (%s); it was left unchanged. Fix the file, then run this again." % (path, e))
    write_atomic(path, text)
lines, crlf = read_lines()
check_structure(lines)
out, found = strip_table(lines)
messages = []
if action in ("remove", "reinstall"):
    if found:
        save(out, crlf)
        messages.append("Removed '%s' from %s." % (name, path))
    else:
        messages.append("No '%s' entry in %s; %s." % (name, path, "nothing to remove" if action == "remove" else "continuing"))
if action in ("install", "reinstall"):
    if out:
        out.append("")
    out += ["[mcp_servers.%s]" % name, "url = " + toml_str(url)]
    if hname:
        out.append("http_headers = { %s = %s }" % (toml_str(hname), toml_str(hvalue)))
    save(out, crlf)
    if found and action == "install":
        messages.append("Replaced the existing '%s' entry in %s (%s)." % (name, path, url))
    else:
        messages.append("Added '%s' to %s (%s)." % (name, path, url))
print("\n".join(messages))
PY
echo "Restart Codex to pick up the change."
