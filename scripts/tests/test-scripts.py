"""Exercise the MCP client registration scripts in scripts/{linux,macos,windows,wsl,wsl2}.

Run with: python3 scripts/tests/test-scripts.py

The Unix scripts run against a temporary HOME with fake claude, uname, grep, curl and ip tools and a
real python3 (the one running this test, and again /usr/bin/python3 when that is a different version,
for example macOS's 3.9 without tomllib). The WSL scripts run with WSL_SKIP_CHECK=1 (plus a few runs
without it to check the WSL guards). The Windows .bat files are checked statically, and when pwsh is
installed their PowerShell part runs too, through the same loader line the .bat uses, so the same
scenarios run on every side and the Windows output can be compared with the Unix output.
Nothing outside the temporary directory is touched.
"""
import json
import os
import re
import shutil
import stat
import subprocess
import sys
import tempfile
from pathlib import Path

# ---- repository-specific settings --------------------------------------------------------------
NAME = 'notdory'
PREFIX = 'NOTDORY'
PORT = 8720
TOKEN_VAR = 'NOTDORY_ACCESS_KEY'          # None when the server takes no credential
TOKEN_DEFAULT = 'notdorydefaultkey'
HEADER = ('x-access-key', '{token}')                # (name, value template) or None
MUX_AUTH = 'bearer'
HOOK = True                    # NotDory's SessionStart hook
HOOK_COMMAND = 'D="${CLAUDE_PROJECT_DIR:-$PWD}"; curl -fsS -m 8 -G -K "$HOME/.notdory/session-hook.curlrc" --data-urlencode "remote=$(git -C "$D" remote get-url origin 2>/dev/null)" --data-urlencode "directory=$(basename "$D")" --data-urlencode "path=$D" --data "format=text" || true'
# -------------------------------------------------------------------------------------------------

root = Path(__file__).resolve().parents[2]
UNIX = ['linux', 'macos', 'wsl', 'wsl2']
ALL = UNIX + ['windows']
CLIENTS = ['claude', 'codex', 'cursor', 'gemini', 'mux']
FILE_CLIENTS = ['codex', 'cursor', 'gemini', 'mux']
ACTIONS = ['install', 'remove', 'reinstall']
URL = 'http://127.0.0.1:%d/mcp' % PORT
PWSH = shutil.which('pwsh')
# pwsh may be a dotnet global tool, which needs dotnet on PATH to start.
DOTNET_DIR = os.path.dirname(os.path.realpath(shutil.which('dotnet'))) if shutil.which('dotnet') else ''
SIDES = UNIX + (['windows'] if PWSH else [])

FAKE = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
if name == 'uname':
    print(os.environ.get('MOCK_UNAME', 'Linux') if args == ['-s'] else os.environ.get('MOCK_UNAME_R', '6.1.0-generic')); sys.exit(0)
if name == 'grep' and args and args[-1] == '/proc/version':
    sys.exit(0 if os.environ.get('MOCK_WSL_KERNEL') else 2)
if name == 'grep':
    os.execv('/usr/bin/grep', ['grep'] + args)
if name == 'ip':
    print('default via 172.20.0.1 dev eth0'); sys.exit(0)
with open(os.environ['CALL_LOG'], 'a') as f:
    f.write(json.dumps([name] + args) + '\n')
if name == 'curl':
    url = args[-1]
    ok = [h for h in os.environ.get('MOCK_CURL_OK', '').split(',') if h]
    sys.exit(0 if any('://' + h in url for h in ok) else 7)
if name == 'claude':
    state = pathlib.Path(os.environ['CLAUDE_STATE'])
    servers = json.loads(state.read_text()) if state.exists() else {}
    if args[:2] == ['mcp', 'remove']:
        if os.environ.get('MOCK_CLAUDE_REMOVE_FAIL'):
            print('boom', file=sys.stderr); sys.exit(1)
        n = args[-1]
        assert args[2:4] == ['--scope', 'user'], args
        if n not in servers:
            print('No MCP server named "%s" in user scope' % n, file=sys.stderr); sys.exit(1)
        del servers[n]; state.write_text(json.dumps(servers)); print('Removed MCP server %s' % n); sys.exit(0)
    if args[:2] == ['mcp', 'add']:
        if os.environ.get('MOCK_CLAUDE_ADD_FAIL'):
            print('add failed', file=sys.stderr); sys.exit(1)
        n = args[6]
        if n in servers:
            print('MCP server %s already exists in user config' % n, file=sys.stderr); sys.exit(1)
        servers[n] = args[2:]; state.write_text(json.dumps(servers)); print('Added HTTP MCP server %s' % n); sys.exit(0)
    sys.exit(2)
'''

count = 0


def check(cond, *info):
    global count
    if not cond:
        raise AssertionError(info)
    count += 1


def scripts_in(platform):
    d = root / 'scripts' / platform
    if platform == 'windows':
        return sorted(f.stem for f in d.iterdir() if f.suffix in ('.bat', '.cmd', '.ps1'))
    return sorted(f.stem for f in d.glob('*.sh') if not f.name.startswith('_'))


# ---- the same set everywhere, line endings, self-containment -------------------------------------
expected = sorted(['%s-%s' % (a, c) for a in ACTIONS for c in CLIENTS] + (['notdory-claude-hook'] if HOOK else []))
for platform in ALL:
    check(scripts_in(platform) == expected, platform, set(scripts_in(platform)) ^ set(expected))
check(not list((root / 'scripts/windows').glob('*.ps1')), 'Windows scripts are .bat files')
template = (root / 'scripts/wsl/_wsl-common.sh').read_bytes()
for platform in ['wsl', 'wsl2']:
    check((root / 'scripts' / platform / '_wsl-common.sh').read_bytes() == template)
    check(b'wsl_launcher_file' in template, 'outdated _wsl-common.sh')

RUNS_OTHER = re.compile(r'(\.\./linux/|wsl_run |\bsh "\$|\bbash "\$\(|scripts/common/)')
for platform in UNIX:
    for f in (root / 'scripts' / platform).glob('*.sh'):
        data = f.read_bytes()
        check(b'\r' not in data, f, 'CRLF')
        check(os.access(f, os.X_OK), f, 'not executable')
        if f.name.startswith('_'):
            continue
        text = data.decode()
        check(text.startswith('#!/usr/bin/env bash\n') and 'set -euo pipefail' in text and 'die()' in text, f)
        check(not RUNS_OTHER.search(text), f, 'runs another script')
        check('x-access-key: \' + key' not in text and '-H "x-access-key' not in text, f, 'credential pasted into the hook command')
        if platform in ('wsl', 'wsl2'):
            check('WSL_FLAVOR=%s\n' % platform in text and 'wsl_require\n' in text and 'wsl_sanitize_path\n' in text
                  and 'wsl_check_repo "$REPO_ROOT"' in text, f, 'WSL preamble')
        if platform == 'wsl':
            check('NAT' not in text, f, 'WSL 2-only text in scripts/wsl')
for f in (root / 'scripts/windows').glob('*.bat'):
    data = f.read_bytes()
    check(b'\r\n' in data and data.count(b'\n') == data.count(b'\r\n'), f, 'bat must be CRLF on disk')
    text = data.decode().replace('\r\n', '\n')
    batch = text.split('\n## POWERSHELL', 1)[0]
    check(text.count('\n## POWERSHELL') == 1, f)
    check('setlocal' in batch and 'endlocal & exit /b %MCPREG_RC%' in batch and ':help' in batch, f)
    check('-ExecutionPolicy' not in batch, f, 'must not depend on the execution policy')
    check(not re.search(r'(?i)\bcall\b|-File|\.bat"|\.cmd"|\.ps1', batch), f, 'runs another script')
    for line in batch.split('\n'):
        if line.lstrip().lower().startswith('echo'):
            check(not re.search(r'%~?\d|%\*|%MCPREG_ARG', line), f, 'batch must not echo arguments', line)

# ---- harness -----------------------------------------------------------------------------------
tmp = Path(tempfile.mkdtemp(prefix='mcp scripts '))
try:
    repo = tmp / 'repo'
    shutil.copytree(root / 'scripts', repo / 'scripts', ignore=shutil.ignore_patterns('__pycache__', 'tests'))
    bin_dir = tmp / 'bin'
    bin_dir.mkdir()
    for name in ['uname', 'grep', 'curl', 'ip', 'claude', 'curl.exe']:
        (bin_dir / name).write_text(FAKE)
        (bin_dir / name).chmod(0o755)
    (bin_dir / 'python3').symlink_to(sys.executable)
    (bin_dir / 'git').symlink_to(shutil.which('git') or '/usr/bin/git')
    bare = tmp / 'bare'  # the platform fakes without the client CLIs
    bare.mkdir()
    for name in ['uname', 'grep', 'python3']:
        (bare / name).symlink_to(bin_dir / name)
    BARE_PATH = '%s:/usr/bin:/bin' % bare
    WIN_PATH = '%s:%s/usr/bin:/bin' % (bin_dir, DOTNET_DIR + ':' if DOTNET_DIR else '')
    log = tmp / 'calls.jsonl'
    home = tmp / 'home'
    base_env = {k: v for k, v in os.environ.items() if not (k.startswith(PREFIX + '_') or k.startswith('MCPREG_') or k in (
        'CODEX_HOME', 'MUX_CONFIG_DIR', 'CLAUDE_CONFIG_DIR', 'WSL_NO_HOST_REWRITE', 'MOCK_WSL_KERNEL', 'NOTDORY_SESSION_HOOK'))}
    base_env.update(PATH='%s:/usr/bin:/bin' % bin_dir, CALL_LOG=str(log), HOME=str(home), USERPROFILE=str(home),
                    WSL_SKIP_CHECK='1', WSL_DISTRO_NAME='Test', CLAUDE_STATE=str(tmp / 'claude.json'))

    def use_python(exe):
        (bin_dir / 'python3').unlink()
        (bin_dir / 'python3').symlink_to(exe)

    def reset():
        if home.exists():
            shutil.rmtree(home)
        home.mkdir()
        for p in [log, tmp / 'claude.json']:
            if p.exists():
                p.unlink()

    def calls(tool):
        if not log.exists():
            return []
        return [c[1:] for c in map(json.loads, log.read_text().splitlines()) if c[0] == tool]

    def run_unix(platform, script, *args, **env):
        if log.exists():
            log.unlink()
        e = dict(base_env, MOCK_UNAME='Darwin' if platform == 'macos' else 'Linux')
        e.update(env)
        e = {k: v for k, v in e.items() if v is not None}
        return subprocess.run(['bash', str(repo / 'scripts' / platform / (script + '.sh')), *args], env=e,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)

    def run_win(script, *args, **env):
        """Runs the PowerShell part of a .bat the way the .bat does: same loader, same environment."""
        bat = repo / 'scripts/windows' / (script + '.bat')
        text = bat.read_text().replace('\r\n', '\n')
        loader = next(l for l in text.split('\n') if l.startswith('powershell -NoProfile'))
        command = loader.split(' -Command "', 1)[1][:-1]
        if log.exists():
            log.unlink()
        e = dict(base_env, MCPREG_SELF=str(bat), MCPREG_ARGC=str(len(args)), PATH=WIN_PATH)
        for i in range(3):
            e['MCPREG_ARG%d' % (i + 1)] = args[i] if i < len(args) else None
        e.update(env)
        e = {k: v for k, v in e.items() if v is not None}
        return subprocess.run([PWSH, '-NoProfile', '-NonInteractive', '-Command', command], env=e,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)

    def run(side, script, *args, **env):
        return run_win(script, *args, **env) if side == 'windows' else run_unix(side, script, *args, **env)

    def ok(p, *info):
        check(p.returncode == 0, p.stdout, p.stderr, *info)

    def fails(p, needle=None, *info):
        check(p.returncode != 0 and (needle is None or needle in p.stdout + p.stderr), p.stdout, p.stderr, *info)

    def header_value(token):
        return HEADER[1].replace('{token}', token)

    def cfg_path(client):
        return {'codex': home / '.codex/config.toml', 'cursor': home / '.cursor/mcp.json',
                'gemini': home / '.gemini/settings.json', 'mux': home / '.mux/mcp-servers.json'}[client]

    def entry(client, path=None):
        """Our entry in a client's config, or None."""
        path = path or cfg_path(client)
        if not path.exists():
            return None
        if client == 'codex':
            text = path.read_text()
            m = re.search(r'^\[mcp_servers\.%s\]\r?\n((?:[^\[\r\n].*\r?\n?)*)' % NAME, text, re.M)
            check(text.count('[mcp_servers.%s]' % NAME) <= 1, text)
            return m.group(1).strip().replace('\r\n', '\n') if m else None
        cfg = json.loads(path.read_text())
        if client == 'mux':
            ours = [s for s in cfg.get('servers', []) if s.get('name') == NAME]
            check(len(ours) <= 1, cfg)
            return ours[0] if ours else None
        return cfg.get('mcpServers', {}).get(NAME)

    def expected_entry(client, url=URL, token=TOKEN_DEFAULT):
        if client == 'codex':
            lines = ['url = "%s"' % url]
            if HEADER:
                lines.append('http_headers = { "%s" = "%s" }' % (HEADER[0], header_value(token)))
            return '\n'.join(lines)
        if client == 'mux':
            base, path, query = re.match(r'(https?://[^/?#]+)([^?#]*)(\?[^#]*)?', url).groups()
            auth = {'bearer': {'type': 'bearer', 'bearerToken': token, 'apiKeyHeader': 'X-API-Key', 'apiKeyValue': ''},
                    'apikey': {'type': 'apikey', 'bearerToken': '', 'apiKeyHeader': HEADER and HEADER[0], 'apiKeyValue': token},
                    'none': {'type': 'none', 'bearerToken': '', 'apiKeyHeader': 'X-API-Key', 'apiKeyValue': ''}}[MUX_AUTH]
            return {'name': NAME, 'transport': 'http', 'command': '', 'args': [], 'env': {}, 'url': base,
                    'mcpPath': (path if path not in ('', '/') else '/mcp') + (query or ''), 'auth': auth}
        e = {('url' if client == 'cursor' else 'httpUrl'): url}
        if HEADER:
            e['headers'] = {HEADER[0]: header_value(token)}
        return e

    def seed(client):
        """A config with another server in it, which every script must keep."""
        p = cfg_path(client)
        p.parent.mkdir(parents=True, exist_ok=True)
        if client == 'codex':
            p.write_text('model = "o3"\n\n[mcp_servers.other]\nurl = "http://other/mcp"\n')
        elif client == 'mux':
            p.write_text(json.dumps({'servers': [{'name': 'other', 'transport': 'stdio', 'command': 'x'}]}))
        else:
            p.write_text(json.dumps({'theme': 'dark', 'mcpServers': {'other': {'url': 'http://other/mcp'}}}))

    def other_kept(client):
        text = cfg_path(client).read_text()
        return ('[mcp_servers.other]' in text and 'model = "o3"' in text) if client == 'codex' else '"other"' in text

    def leftovers(directory):
        return [f.name for f in directory.iterdir() if f.name.endswith('.tmp')] if directory.exists() else []

    def claude_entry():
        state = tmp / 'claude.json'
        return json.loads(state.read_text()).get(NAME) if state.exists() else None

    def expected_claude(url=URL, token=TOKEN_DEFAULT):
        e = ['--scope', 'user', '--transport', 'http', NAME, url]
        if HEADER:
            e += ['--header', '%s: %s' % (HEADER[0], header_value(token))]
        return e

    def settings_path():
        return home / '.claude/settings.json'

    def settings():
        p = settings_path()
        return json.loads(p.read_text()) if p.exists() else {}

    def hook_commands():
        return [h['command'] for g in settings().get('hooks', {}).get('SessionStart', []) for h in g['hooks']]

    keyfile = home / '.notdory/session-hook.curlrc'

    def expected_keyfile(rest='http://127.0.0.1:8700', key=TOKEN_DEFAULT):
        q = lambda s: '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'
        return ('# NotDory SessionStart hook settings, written by scripts/*/install-claude and notdory-claude-hook. Keep this file private.\n'
                'url = ' + q(rest + '/v1.0/api/session') + '\nheader = ' + q('x-access-key: ' + key) + '\n')

    # ---- OS guards (Unix) -----------------------------------------------------------------------
    reset()
    for script in ['install-codex', 'remove-claude', 'reinstall-mux']:
        fails(run_unix('linux', script, MOCK_UNAME='Darwin'), 'This script is for Linux')
        fails(run_unix('macos', script, MOCK_UNAME='Linux'), 'This script is for macOS')
        fails(run_unix('linux', script, MOCK_WSL_KERNEL='1'), 'This is WSL')
        # A Docker container on a Microsoft kernel (no WSL interop) is plain Linux.
        ok(run_unix('linux', script, MOCK_WSL_KERNEL='1', WSL_DISTRO_NAME=None))
        for platform in ['wsl', 'wsl2']:
            fails(run_unix(platform, script, WSL_SKIP_CHECK=None), 'This script is for WSL')
        fails(run_unix('wsl', script, WSL_SKIP_CHECK=None, MOCK_UNAME_R='5.15.0-microsoft-standard-WSL2'), 'scripts/wsl2')
        fails(run_unix('wsl2', script, WSL_SKIP_CHECK=None, MOCK_UNAME_R='4.4.0-19041-Microsoft'), 'scripts/wsl')
        ok(run_unix('wsl2', script, WSL_SKIP_CHECK=None, MOCK_UNAME_R='5.15.0-microsoft-standard-WSL2', WSL_NO_HOST_REWRITE='1'))
        ok(run_unix('wsl', script, WSL_SKIP_CHECK=None, MOCK_UNAME_R='4.4.0-19041-Microsoft'))

    # ---- argument handling (the Windows checks run in PowerShell with the same rules) -------------
    reset()
    for side in SIDES:
        for client in CLIENTS:
            for action in ACTIONS:
                script = '%s-%s' % (action, client)
                if side != 'windows':
                    p = run(side, script, '--help')
                    ok(p, side, script)
                    check('Usage: scripts/%s/%s.sh' % (side, script) in p.stdout and not calls('claude'), p.stdout)
                fails(run(side, script, '--bogus'), 'Unknown option: --bogus')
                takes_token = action != 'remove' and TOKEN_VAR
                if takes_token:
                    fails(run(side, script, 'a', 'b'), 'Unexpected argument: b')
                    fails(run(side, script, '', 'b'), 'Unexpected argument: b')
                else:
                    fails(run(side, script, 'a'), 'Unexpected argument: a')
                    fails(run(side, script, '', 'extra'), 'Unexpected argument')
                check(not calls('claude') and not (home / '.codex').exists(), side, script)
        if HOOK:
            if side != 'windows':
                ok(run(side, 'notdory-claude-hook', '--help'))
            fails(run(side, 'notdory-claude-hook', 'bogus'), 'Unknown action')
            fails(run(side, 'notdory-claude-hook', '-x'), 'Unknown option: -x')
            fails(run(side, 'notdory-claude-hook', 'install', '-x'), 'Unknown option: -x')
            fails(run(side, 'notdory-claude-hook', 'install', 'http://r:1', '-x'), 'Unknown option: -x')
            fails(run(side, 'notdory-claude-hook', 'remove', 'x'), 'Unexpected argument')
            fails(run(side, 'notdory-claude-hook', 'install', 'a', 'b', 'c'), 'Unexpected argument')
            check(not settings_path().exists() and not keyfile.exists())

    # ---- config-file clients: install / remove / reinstall semantics ----------------------------
    def file_client_suite(side, clients=FILE_CLIENTS):
        transcript = []
        for client in clients:
            reset()
            seed(client)
            for action in ['install', 'install', 'remove', 'remove', 'reinstall', 'reinstall']:
                p = run(side, '%s-%s' % (action, client))
                ok(p, side, client, action)
                transcript.append(p.stdout)
                check(other_kept(client), side, client, action)
                check(entry(client) == (None if action == 'remove' else expected_entry(client)), side, client, action, entry(client))
                check(not leftovers(cfg_path(client).parent), side, client, 'temporary file left behind')
            t = transcript[-6:]
            check("Added '%s'" % NAME in t[0] and 'Restart' in t[0], t[0])
            check("Replaced the existing '%s' entry" % NAME in t[1], t[1])
            check("Removed '%s'" % NAME in t[2] and 'nothing to remove' in t[3], t[2:4])
            check('continuing' in t[4] and "Added '%s'" % NAME in t[4], t[4])
            check("Removed '%s'" % NAME in t[5] and "Added '%s'" % NAME in t[5], t[5])
            # No config file at all.
            reset()
            p = run(side, 'remove-' + client)
            ok(p)
            check('nothing to remove' in p.stdout and not cfg_path(client).exists(), p.stdout)
            ok(run(side, 'install-' + client))
            check(entry(client) == expected_entry(client))
            if side != 'windows':
                check(cfg_path(client).stat().st_mode & 0o077 == 0, 'a new config must be readable only by its owner')
            # Overrides: URL (explicit port kept), token, config path.
            url = 'https://remote.example:443/custom/mcp?x=1'
            alt = tmp / 'alt' / side / client / 'cfg'
            env = {PREFIX + '_MCP_URL': url, '%s_%s_CONFIG' % (PREFIX, client.upper()): str(alt)}
            args = ['argtoken'] if TOKEN_VAR else []
            if TOKEN_VAR:
                env[TOKEN_VAR] = 'envtoken'
            ok(run(side, 'install-' + client, *args, **env))
            check(entry(client, alt) == expected_entry(client, url, 'argtoken' if TOKEN_VAR else TOKEN_DEFAULT), side, entry(client, alt))
            if TOKEN_VAR:
                ok(run(side, 'install-' + client, **env))
                check(entry(client, alt) == expected_entry(client, url, 'envtoken'))
            # Every client refuses a URL that is not http(s), before changing anything.
            before = cfg_path(client).read_bytes()
            for bad in ['ftp://h/mcp', 'http://', 'not a url', 'http://h/a b']:
                for action in ['install', 'reinstall']:
                    fails(run(side, '%s-%s' % (action, client), **{PREFIX + '_MCP_URL': bad}), 'is not an http(s) URL', side, client, bad)
            if client == 'mux':
                fails(run(side, 'install-mux', **{PREFIX + '_MCP_BASE_URL': 'h:1'}), 'is not an http(s) URL')
            check(cfg_path(client).read_bytes() == before, side, client)
            # A broken or non-object JSON config is reported and left alone, on every side.
            if client != 'codex':
                for broken, needle in [('{ not json', 'not valid JSON'), ('5', 'JSON object'), ('"str"', 'JSON object'), ('[1]', 'JSON object')]:
                    cfg_path(client).write_text(broken)
                    for action in ACTIONS:
                        fails(run(side, '%s-%s' % (action, client)), needle, side, client, broken)
                        check(cfg_path(client).read_text() == broken)
            # A symlinked config keeps its link; the real file is updated.
            reset()
            real = home / 'dotfiles' / ('real-' + client)
            real.parent.mkdir(parents=True)
            real.write_text('' if client == 'codex' else '{}')
            cfg_path(client).parent.mkdir(parents=True, exist_ok=True)
            cfg_path(client).symlink_to(real)
            ok(run(side, 'install-' + client))
            check(cfg_path(client).is_symlink() and entry(client, real) == expected_entry(client), side, client)
            check(not leftovers(real.parent) and not leftovers(cfg_path(client).parent), side, client)
        # Names are matched case-sensitively everywhere; keys that differ only in case are refused.
        for client in ['cursor', 'gemini']:
            reset()
            cfg_path(client).parent.mkdir(parents=True)
            cfg_path(client).write_text(json.dumps({'mcpServers': {NAME.capitalize(): {'url': 'http://x/mcp'}}}))
            for action in ACTIONS:
                fails(run(side, '%s-%s' % (action, client)), 'only in case', side, client)
            cfg_path(client).write_text(json.dumps({'MCPServers': {}, 'mcpServers': {}}))
            fails(run(side, 'install-' + client), 'only in case', side, client)
            cfg_path(client).write_text(json.dumps({'mcpServers': None}))
            ok(run(side, 'install-' + client))
            check(entry(client) == expected_entry(client))
        reset()
        cfg_path('mux').parent.mkdir(parents=True)
        cfg_path('mux').write_text(json.dumps({'servers': [{'name': NAME.capitalize(), 'transport': 'stdio'}, {'Name': NAME}]}))
        p = run(side, 'remove-mux')
        ok(p)
        check('nothing to remove' in p.stdout, p.stdout)
        ok(run(side, 'install-mux'))
        servers = json.loads(cfg_path('mux').read_text())['servers']
        check(len(servers) == 3 and servers[0]['name'] == NAME.capitalize() and servers[1] == {'Name': NAME} and servers[2] == expected_entry('mux'), servers)
        cfg_path('mux').write_text(json.dumps({'servers': {}}))
        fails(run(side, 'install-mux'), 'not a JSON array')
        # The clients' own config-directory variables, and the Mux origin override.
        reset()
        ok(run(side, 'install-codex', CODEX_HOME=str(home / 'ch')))
        check(entry('codex', home / 'ch/config.toml') == expected_entry('codex'))
        ok(run(side, 'install-mux', MUX_CONFIG_DIR=str(home / 'md')))
        check(entry('mux', home / 'md/mcp-servers.json') == expected_entry('mux'))
        ok(run(side, 'install-mux', **{PREFIX + '_MCP_BASE_URL': 'http://h:1/'}))
        check(entry('mux')['url'] == 'http://h:1' and entry('mux')['mcpPath'] == '/mcp', entry('mux'))
        for url, base, path in [('http://h:80/x/mcp', 'http://h:80', '/x/mcp'), ('https://H.example', 'https://H.example', '/mcp'),
                                ('http://[::1]:9/', 'http://[::1]:9', '/mcp')]:
            ok(run(side, 'install-mux', **{PREFIX + '_MCP_URL': url}))
            check((entry('mux')['url'], entry('mux')['mcpPath']) == (base, path), side, url, entry('mux'))
        return transcript

    # ---- Codex TOML edge cases -------------------------------------------------------------------
    def codex_suite(side):
        p = cfg_path('codex')

        def put(text, encoding='utf-8'):
            reset()
            p.parent.mkdir(parents=True)
            p.write_bytes(text.encode(encoding))

        ours = '[mcp_servers.%s]\nurl = "http://old/mcp"\n' % NAME
        # A different server whose quoted name contains a dot is not ours.
        put('[mcp_servers."%s.dev"]\nurl = "http://dev/mcp"\n\n%s' % (NAME, ours))
        ok(run(side, 'remove-codex'))
        check(p.read_text() == '[mcp_servers."%s.dev"]\nurl = "http://dev/mcp"\n' % NAME, side, p.read_text())
        # Quoted and spaced forms of our own table are ours, sub-tables included.
        put('a = 1\n\n[ mcp_servers . "%s" ]\nurl = "x"\n\n[mcp_servers.%s.env]\nK = "v"\n\n[other]\nb = 2\n' % (NAME, NAME))
        ok(run(side, 'remove-codex'))
        check(p.read_text() == 'a = 1\n\n[other]\nb = 2\n', side, p.read_text())
        # Comments just before the next table stay with it; comments inside our table go with it.
        put('%s# inside ours\nenabled = true\n\n# about other\n[other]\nb = 2\n' % ours)
        ok(run(side, 'remove-codex'))
        check('# about other\n[other]' in p.read_text() and '# inside ours' not in p.read_text(), side, p.read_text())
        # A byte-order mark is accepted (and dropped); unusual characters in comments survive.
        put('\ufeff# note \u2028 and \x85 next-line\nmodel = "o3"\n' + ours)
        ok(run(side, 'install-codex'))
        text = p.read_text(encoding='utf-8')
        check(not text.startswith('\ufeff') and '# note \u2028 and \x85 next-line\nmodel = "o3"\n' in text and entry('codex') == expected_entry('codex'), side, text)
        # CRLF files stay CRLF.
        put('model = "o3"\r\n\r\n' + ours.replace('\n', '\r\n'))
        ok(run(side, 'install-codex'))
        raw = p.read_bytes()
        check(raw.count(b'\n') == raw.count(b'\r\n') and entry('codex') == expected_entry('codex'), side, raw)
        # A bracket line inside a multi-line array or string is not a table header.
        put('args = [\n  "a",\n]\nnote = """\n[mcp_servers.%s]\n"""\n' % NAME)
        ok(run(side, 'install-codex'))
        text = p.read_text()
        check(text.startswith('args = [\n  "a",\n]\nnote = """\n[mcp_servers.%s]\n"""\n' % NAME)
              and text.endswith('\n[mcp_servers.%s]\n%s\n' % (NAME, expected_entry('codex'))), side, text)
        # Forms this script cannot edit safely are refused and left unchanged, on every action.
        for text in ['[mcp_servers]\n%s = { url = "x" }\n' % NAME,
                     'mcp_servers.%s.url = "x"\n' % NAME,
                     '[mcp_servers]\n"%s".url = "x"\n' % NAME,
                     'mcp_servers = { other = { url = "x" } }\n']:
            for action in ACTIONS:
                put(text)
                fails(run(side, '%s-codex' % action), 'Edit the file by hand', side, text)
                check(p.read_text() == text, side, text)
        # Other dotted keys under [mcp_servers] are fine.
        put('[mcp_servers]\nother.url = "x"\n')
        ok(run(side, 'install-codex'))
        check('other.url = "x"' in p.read_text() and entry('codex') == expected_entry('codex'), side, p.read_text())

    # ---- Claude Code ----------------------------------------------------------------------------
    def claude_suite(side):
        transcript = []
        reset()
        for action in ['install', 'install', 'remove', 'remove', 'reinstall', 'reinstall']:
            p = run(side, '%s-claude' % action, NOTDORY_SESSION_HOOK='0' if HOOK else None)
            ok(p, side, action)
            transcript.append(p.stdout)
            check(claude_entry() == (None if action == 'remove' else expected_claude()), side, action, claude_entry())
        t = transcript
        check("Added '%s' to Claude Code at user scope" % NAME in t[0], t)
        check('Replaced the existing' in t[1] and "Removed '%s' from Claude Code" % NAME in t[2] and 'nothing to remove' in t[3], t)
        check('continuing' in t[4] and "Removed '%s'" % NAME in t[5], t)
        url = 'https://remote.example/mcp'
        env = {PREFIX + '_MCP_URL': url}
        if TOKEN_VAR:
            env[TOKEN_VAR] = 'envtoken'
        ok(run(side, 'install-claude', **env))
        check(claude_entry() == expected_claude(url, 'envtoken' if TOKEN_VAR else TOKEN_DEFAULT), claude_entry())
        if TOKEN_VAR:
            ok(run(side, 'install-claude', 'argtoken', **env))
            check(claude_entry() == expected_claude(url, 'argtoken'))
        fails(run(side, 'install-claude', MOCK_CLAUDE_ADD_FAIL='1'), 'claude mcp add failed')
        fails(run(side, 'remove-claude', MOCK_CLAUDE_REMOVE_FAIL='1'), 'claude mcp remove failed')
        fails(run(side, 'reinstall-claude', MOCK_CLAUDE_REMOVE_FAIL='1'), 'claude mcp remove failed')
        reset()
        for action in ['install', 'reinstall']:
            fails(run(side, '%s-claude' % action, **{PREFIX + '_MCP_URL': 'ftp://x'}), 'is not an http(s) URL')
        check(not calls('claude') and claude_entry() is None)
        if side != 'windows':
            fails(run(side, 'install-claude', PATH=BARE_PATH), "'claude' was not found")
        return transcript

    # ---- NotDory SessionStart hook --------------------------------------------------------------
    def hook_suite(side):
        transcript = []
        reset()
        (home / '.claude').mkdir()
        other = {'hooks': [{'type': 'command', 'command': 'echo hi'}]}
        legacy = {'hooks': [{'type': 'command', 'command': 'curl -H "x-access-key: old" "http://h:8700/v1.0/api/session"'}]}
        settings_path().write_text(json.dumps({'model': 'x', 'hooks': {'SessionStart': [other, legacy]}}))
        settings_path().chmod(0o600)
        original = settings_path().read_bytes()
        p = run(side, 'install-claude')
        ok(p, side)
        transcript.append(p.stdout)
        check(hook_commands() == ['echo hi', HOOK_COMMAND], side, hook_commands())
        check('Updated the NotDory SessionStart hook' in p.stdout, p.stdout)  # the legacy hook was replaced
        check(settings()['model'] == 'x' and (home / '.claude/settings.json.bak').read_bytes() == original, side)
        check(keyfile.read_text() == expected_keyfile(), side, keyfile.read_text())
        if side != 'windows':
            check(stat.S_IMODE(keyfile.stat().st_mode) == 0o600, 'key file must be 0600')
            check(stat.S_IMODE((home / '.claude/settings.json.bak').stat().st_mode) == 0o600, 'backup keeps the 0600 mode')
        # Reinstall: one backup, of the file as it was before the reinstall.
        before = settings_path().read_bytes()
        p = run(side, 'reinstall-claude', 'k2', NOTDORY_MCP_URL='http://srv:8720/mcp')
        ok(p)
        transcript.append(p.stdout)
        check((home / '.claude/settings.json.bak').read_bytes() == before, side, 'reinstall must back up the original once')
        check(hook_commands() == ['echo hi', HOOK_COMMAND] and keyfile.read_text() == expected_keyfile('http://srv:8700', 'k2'), side)
        check('Removed the NotDory SessionStart hook' in p.stdout and 'Installed the NotDory SessionStart hook' in p.stdout, p.stdout)
        # A key or URL that would break out of quotes is stored quoted in the key file; the command never changes.
        evil = 'a" -o /tmp/x "$(touch /tmp/pwned)'
        ok(run(side, 'install-claude', evil, NOTDORY_REST_URL='http://rest:1/'))
        check(hook_commands()[-1] == HOOK_COMMAND and keyfile.read_text() == expected_keyfile('http://rest:1', evil), side, keyfile.read_text())
        check(evil not in settings_path().read_text(), 'the access key must not be in settings.json')
        for bad_env, needle in [({'NOTDORY_REST_URL': 'ftp://r'}, 'is not an http(s) URL'), ({'NOTDORY_REST_URL': 'http://r/a b'}, 'is not an http(s) URL'),
                                ({TOKEN_VAR: 'line\nbreak'}, 'control characters')]:
            snapshot = (settings_path().read_bytes(), keyfile.read_bytes(), claude_entry())
            fails(run(side, 'install-claude', **bad_env), needle, side, bad_env)
            check((settings_path().read_bytes(), keyfile.read_bytes(), claude_entry()) == snapshot, side, 'no partial install', bad_env)
            check(not calls('claude'), side, bad_env)
        p = run(side, 'remove-claude')
        ok(p)
        transcript.append(p.stdout)
        check(hook_commands() == ['echo hi'] and 'Removed the NotDory SessionStart hook' in p.stdout and not keyfile.exists(), p.stdout)
        p = run(side, 'remove-claude')
        ok(p)
        transcript.append(p.stdout)
        check('No NotDory SessionStart hook' in p.stdout and 'nothing to remove' in p.stdout, p.stdout)
        # Disabled: an older hook (stale key) is removed, the same way on every side.
        ok(run(side, 'install-claude'))
        p = run(side, 'install-claude', NOTDORY_SESSION_HOOK='0')
        ok(p)
        transcript.append(p.stdout)
        check(hook_commands() == ['echo hi'] and not keyfile.exists(), side, hook_commands())
        check('Removed the NotDory SessionStart hook' in p.stdout and 'Skipped installing the SessionStart hook (NOTDORY_SESSION_HOOK=0).' in p.stdout, p.stdout)
        p = run(side, 'reinstall-claude', NOTDORY_SESSION_HOOK='0')
        ok(p)
        transcript.append(p.stdout)
        check(hook_commands() == ['echo hi'] and 'nothing to remove' in p.stdout and 'Skipped installing' in p.stdout, p.stdout)
        # Only the hook, through its own script.
        reset()
        ok(run(side, 'notdory-claude-hook', 'install', 'http://r:2', 'kk'))
        check(hook_commands() == [HOOK_COMMAND] and keyfile.read_text() == expected_keyfile('http://r:2', 'kk'), side)
        check(not (home / '.claude/settings.json.bak').exists(), 'no backup of a file that did not exist')
        ok(run(side, 'notdory-claude-hook'))  # install is the default action
        check(hook_commands() == [HOOK_COMMAND] and keyfile.read_text() == expected_keyfile(), side)
        ok(run(side, 'notdory-claude-hook', 'remove'))
        check('hooks' not in settings() and not keyfile.exists(), settings())
        p = run(side, 'notdory-claude-hook', 'remove')
        ok(p)
        check('nothing to remove' in p.stdout)
        fails(run(side, 'notdory-claude-hook', 'install', 'ftp://r'), 'is not an http(s) URL')
        check(not keyfile.exists())
        ok(run(side, 'notdory-claude-hook', 'install', CLAUDE_CONFIG_DIR=str(home / 'cc')))
        check((home / 'cc/settings.json').exists())
        # Malformed settings are refused the same way everywhere.
        for text, needle in [('[1]', 'JSON object'), ('{"hooks": {"SessionStart": {}}}', 'not a JSON array'),
                             ('{"hooks": []}', 'not a JSON object'), ('{"Hooks": {}, "hooks": {}}', 'only in case')]:
            settings_path().write_text(text)
            for script in ['install-claude', 'notdory-claude-hook']:
                fails(run(side, script), needle, side, text)
                check(settings_path().read_text() == text)
        return transcript

    # ---- run the suites on every side -------------------------------------------------------------
    transcripts = {}
    for side in SIDES:
        transcripts[side] = {'files': file_client_suite(side)}
        codex_suite(side)
        transcripts[side]['claude'] = claude_suite(side)
        if HOOK:
            transcripts[side]['hook'] = hook_suite(side)
    # The Windows scripts print exactly what the Unix scripts print.
    if PWSH:
        for k in transcripts['linux']:
            check(transcripts['windows'][k] == transcripts['linux'][k], k, transcripts['windows'][k], transcripts['linux'][k])
    for side in UNIX[1:]:
        for k in transcripts['linux']:
            check(transcripts[side][k] == transcripts['linux'][k], side, k)

    # The Python parts again under the system Python when it is another version (stock macOS: 3.9, no tomllib).
    other_python = '/usr/bin/python3'
    pythons_tested = [sys.version.split()[0]]
    if os.path.exists(other_python):
        version = subprocess.run([other_python, '-c', 'import sys; print(sys.version.split()[0])'], capture_output=True, text=True).stdout.strip()
        if version and version != pythons_tested[0]:
            use_python(other_python)
            try:
                check(file_client_suite('linux') == transcripts['linux']['files'], 'python %s' % version)
                codex_suite('linux')
                if HOOK:
                    check(hook_suite('linux') == transcripts['linux']['hook'], 'python %s' % version)
            finally:
                use_python(sys.executable)
            pythons_tested.append(version)

    # ---- WSL 2 networking: only a default URL falls back to the Windows host ---------------------
    wsl2_env = dict(WSL_SKIP_CHECK='1', MOCK_UNAME_R='5.15.0-microsoft-standard-WSL2', MOCK_CURL_OK='172.20.0.1')
    host_url = 'http://172.20.0.1:%d/mcp' % PORT
    reset()
    ok(run_unix('wsl2', 'install-cursor', **wsl2_env))
    check(entry('cursor') == expected_entry('cursor', host_url), entry('cursor'))
    ok(run_unix('wsl2', 'install-mux', **wsl2_env))
    check(entry('mux') == expected_entry('mux', host_url), entry('mux'))
    ok(run_unix('wsl2', 'install-claude', **wsl2_env))
    check(claude_entry() == expected_claude(host_url), claude_entry())
    if HOOK:
        check(keyfile.read_text() == expected_keyfile('http://172.20.0.1:8700'), keyfile.read_text())
        ok(run_unix('wsl2', 'notdory-claude-hook', 'install', **wsl2_env))
        check(keyfile.read_text() == expected_keyfile('http://172.20.0.1:8700'), keyfile.read_text())
    ok(run_unix('wsl2', 'install-codex', **dict(wsl2_env, WSL_NO_HOST_REWRITE='1')))
    check(entry('codex') == expected_entry('codex'))
    ok(run_unix('wsl2', 'install-gemini', **dict(wsl2_env, **{PREFIX + '_MCP_URL': URL})))
    check(entry('gemini') == expected_entry('gemini'))
    ok(run_unix('wsl2', 'install-mux', **dict(wsl2_env, **{PREFIX + '_MCP_BASE_URL': 'http://127.0.0.1:%d' % PORT})))
    check(entry('mux') == expected_entry('mux'))
    ok(run_unix('wsl2', 'install-mux', **dict(wsl2_env, MOCK_CURL_OK='127.0.0.1,172.20.0.1')))  # answers inside WSL
    check(entry('mux') == expected_entry('mux'))
    # WSL 1 shares localhost with Windows: never rewritten.
    ok(run_unix('wsl', 'install-cursor', **dict(wsl2_env, MOCK_UNAME_R='4.4.0-19041-Microsoft')))
    check(entry('cursor') == expected_entry('cursor'))
    p = run_unix('wsl2', 'install-gemini')
    check('scripts\\windows\\install-gemini.bat' in p.stderr, p.stderr)

    # ---- Windows: every PowerShell part parses ---------------------------------------------------
    if PWSH:
        parse_dir = tmp / 'parse'
        parse_dir.mkdir()
        targets = []
        for f in sorted((root / 'scripts/windows').glob('*.bat')):
            text = f.read_text().replace('\r\n', '\n')
            target = parse_dir / (f.stem + '.ps1')
            target.write_text('## POWERSHELL' + text.split('\n## POWERSHELL', 1)[1])
            targets.append(str(target))
        r = subprocess.run([PWSH, '-NoProfile', '-NonInteractive', '-Command',
                            'foreach ($f in ($env:PARSE_TARGETS -split "`n")) { $e = $null; [void][System.Management.Automation.Language.Parser]::ParseFile($f, [ref]$null, [ref]$e); '
                            '$e | ForEach-Object { $f + ": " + $_.Message } }'], env=dict(os.environ, PARSE_TARGETS='\n'.join(targets)),
                           capture_output=True, text=True)
        check(r.returncode == 0 and not r.stdout.strip(), r.stdout, r.stderr)
    else:
        print('pwsh not found: skipped running the PowerShell part of the Windows scripts.')
finally:
    shutil.rmtree(tmp, ignore_errors=True)

print('Python versions tested: %s' % ', '.join(pythons_tested))
print('OK: %d checks passed' % count)
