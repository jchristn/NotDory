# Install or remove the NotDory SessionStart hook in the Claude Code user settings (~\.claude\settings.json).
# The hook fetches the session context for the project Claude Code opened (matched by its git repository name, then its
# folder name) and Claude Code adds it to the model's context before the first turn. It prints nothing if NotDory is
# unreachable, so it never blocks a session. Other settings and hooks are kept; the file is backed up to .bak.
# Usage: notdory-claude-hook.ps1 -Action install -RestUrl http://127.0.0.1:8700 -AccessKey <key>
#        notdory-claude-hook.ps1 -Action remove
param(
    [ValidateSet('install', 'remove')][string]$Action = 'install',
    [string]$RestUrl = 'http://127.0.0.1:8700',
    [string]$AccessKey = 'notdorydefaultkey',
    [string]$SettingsPath = ''
)

$ErrorActionPreference = 'Stop'
$path = if ($SettingsPath) { $SettingsPath } else { Join-Path $HOME '.claude\settings.json' }
$dir = Split-Path $path
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$root = [ordered]@{}
if (Test-Path $path) {
    Copy-Item $path "$path.bak" -Force
    $text = Get-Content $path -Raw
    if ($text -and $text.Trim()) {
        $parsed = $text | ConvertFrom-Json
        foreach ($p in $parsed.PSObject.Properties) { $root[$p.Name] = $p.Value }
    }
}

$hooks = [ordered]@{}
if ($root.Contains('hooks') -and $root['hooks']) {
    foreach ($p in $root['hooks'].PSObject.Properties) { $hooks[$p.Name] = $p.Value }
}

# Keep every SessionStart group except an earlier NotDory hook, so re-running updates it instead of adding a second one.
$groups = New-Object System.Collections.ArrayList
if ($hooks.Contains('SessionStart') -and $hooks['SessionStart']) {
    foreach ($group in @($hooks['SessionStart'])) {
        $isNotDory = $false
        foreach ($h in @($group.hooks)) {
            if ($h.command -and $h.command -like '*/v1.0/api/session*' -and $h.command -like '*x-access-key*') { $isNotDory = $true }
        }
        if (-not $isNotDory) { [void]$groups.Add($group) }
    }
}

if ($Action -eq 'install') {
    $url = $RestUrl.TrimEnd('/') + '/v1.0/api/session'
    $command = 'D="${CLAUDE_PROJECT_DIR:-$PWD}"; curl -fsS -m 8 -G -H "x-access-key: ' + $AccessKey + '" ' +
        '--data-urlencode "remote=$(git -C "$D" remote get-url origin 2>/dev/null)" --data-urlencode "directory=$(basename "$D")" ' +
        '--data "format=text" "' + $url + '" || true'
    $hook = [ordered]@{ type = 'command'; command = $command; timeout = 10 }
    [void]$groups.Add([ordered]@{ hooks = @($hook) })
}

if ($groups.Count -gt 0) { $hooks['SessionStart'] = @($groups) } elseif ($hooks.Contains('SessionStart')) { $hooks.Remove('SessionStart') }
if ($hooks.Count -gt 0) { $root['hooks'] = $hooks } elseif ($root.Contains('hooks')) { $root.Remove('hooks') }

$json = $root | ConvertTo-Json -Depth 20
[System.IO.File]::WriteAllText($path, $json, (New-Object System.Text.UTF8Encoding($false)))
if ($Action -eq 'install') { Write-Host "Installed the NotDory SessionStart hook in $path (session context from $RestUrl)." }
else { Write-Host "Removed the NotDory SessionStart hook from $path." }
