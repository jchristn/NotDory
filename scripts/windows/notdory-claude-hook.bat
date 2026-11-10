@echo off
REM Install or remove the NotDory SessionStart hook in the Claude Code user settings (%CLAUDE_CONFIG_DIR%\settings.json).
REM At session start the hook fetches the context for the project Claude Code opened (matched by its git
REM repository name, then its folder name) and Claude Code adds it to the model's context before the first turn.
REM It prints nothing if NotDory is unreachable, so it never blocks a session. Other settings and hooks are kept;
REM the file is backed up to settings.json.bak (same permissions) before each change. The access key and REST
REM URL are kept in %USERPROFILE%\.notdory\session-hook.curlrc, readable only by you; the hook command itself holds no credential.
REM install-claude already installs this hook; use this script to manage the hook on its own.
REM The hook is a bash command line: Claude Code runs it with Git Bash. For Claude Code inside WSL, use
REM scripts/wsl2 or scripts/wsl from the distro instead.
REM Usage: scripts\windows\notdory-claude-hook.bat install [REST_URL] [ACCESS_KEY]
REM        scripts\windows\notdory-claude-hook.bat remove
REM   REST_URL     defaults to NOTDORY_REST_URL, else the NOTDORY_MCP_URL host on port 8700 (http://127.0.0.1:8700)
REM   ACCESS_KEY   defaults to NOTDORY_ACCESS_KEY (default: notdorydefaultkey)
REM Requires Windows PowerShell, and curl.exe for install.
setlocal
if /I "%~1"=="-h" goto help
if /I "%~1"=="--help" goto help
if "%~1"=="/?" goto help
REM The arguments go to PowerShell through the environment and are checked there; batch never echoes them.
set "MCPREG_SELF=%~f0"
set "MCPREG_ARG1=%~1"
set "MCPREG_ARG2=%~2"
set "MCPREG_ARG3=%~3"
set "MCPREG_ARGC=0"
:mcpreg_count
if [%1]==[] goto mcpreg_counted
set /a MCPREG_ARGC+=1
shift
goto mcpreg_count
:mcpreg_counted
where powershell >nul 2>nul
if errorlevel 1 (
    echo Error: powershell was not found on PATH. 1>&2
    endlocal & exit /b 1
)
REM The rest of the work is PowerShell, at the end of this file; cmd never reads past exit /b.
powershell -NoProfile -NonInteractive -Command "$ErrorActionPreference='Stop'; $f=[IO.File]::ReadAllText($env:MCPREG_SELF); $i=$f.IndexOf([string][char]10+'##'+' POWERSHELL'); if ($i -lt 0) { throw 'PowerShell section not found' }; $global:McpRegRc=1; try { Invoke-Expression $f.Substring($i); $global:McpRegRc=0 } catch { [Console]::Error.WriteLine('Error: ' + $_.Exception.Message) }; exit $global:McpRegRc"
set "MCPREG_RC=%errorlevel%"
endlocal & exit /b %MCPREG_RC%

:help
echo Install or remove the NotDory SessionStart hook in the Claude Code user settings (%%CLAUDE_CONFIG_DIR%%\settings.json).
echo At session start the hook fetches the context for the project Claude Code opened (matched by its git
echo repository name, then its folder name) and Claude Code adds it to the model's context before the first turn.
echo It prints nothing if NotDory is unreachable, so it never blocks a session. Other settings and hooks are kept;
echo the file is backed up to settings.json.bak (same permissions) before each change. The access key and REST
echo URL are kept in %%USERPROFILE%%\.notdory\session-hook.curlrc, readable only by you; the hook command itself holds no credential.
echo install-claude already installs this hook; use this script to manage the hook on its own.
echo The hook is a bash command line: Claude Code runs it with Git Bash. For Claude Code inside WSL, use
echo scripts/wsl2 or scripts/wsl from the distro instead.
echo Usage: scripts\windows\notdory-claude-hook.bat install [REST_URL] [ACCESS_KEY]
echo        scripts\windows\notdory-claude-hook.bat remove
echo   REST_URL     defaults to NOTDORY_REST_URL, else the NOTDORY_MCP_URL host on port 8700 (http://127.0.0.1:8700)
echo   ACCESS_KEY   defaults to NOTDORY_ACCESS_KEY (default: notdorydefaultkey)
echo Requires Windows PowerShell, and curl.exe for install.
endlocal & exit /b 0

## POWERSHELL (run by the powershell line above; everything below is PowerShell, not batch)
$ErrorActionPreference = 'Stop'
$argc = [int]$env:MCPREG_ARGC
$arg1 = [string]$env:MCPREG_ARG1
$arg2 = [string]$env:MCPREG_ARG2
$arg3 = [string]$env:MCPREG_ARG3
# Windows PowerShell 5.1 serializes some arrays as {"value":[...],"Count":n} unless this type data is removed.
try { Remove-TypeData System.Array -ErrorAction SilentlyContinue } catch { }
function Get-EnvOr([string]$Name, [string]$Default) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrEmpty($value)) { return $Default }
    return $value
}
function Get-FullPath([string]$Path) { return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path) }
function Assert-Url([string]$Url) {
    if ($Url -cnotmatch '^https?://[^/?#\s]+\S*$') { throw "'$Url' is not an http(s) URL." }
}
# Follows symbolic links to the real file, like Python's os.path.realpath.
function Resolve-Link([string]$Path) {
    $Path = Get-FullPath $Path
    for ($n = 0; $n -lt 32; $n++) {
        $item = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
        if ($null -eq $item -or [string]$item.LinkType -ne 'SymbolicLink') { return $Path }
        $target = [string]@($item.Target)[0]
        if (-not [IO.Path]::IsPathRooted($target)) { $target = Join-Path (Split-Path -Parent $Path) $target }
        $Path = Get-FullPath $target
    }
    throw "${Path}: too many levels of symbolic links."
}
# Replaces the file in one step, through a uniquely named temporary file in the same folder.
function Write-TextAtomic([string]$Path, [string]$Text) {
    $Path = Resolve-Link $Path
    $dir = Split-Path -Parent $Path
    [void][IO.Directory]::CreateDirectory($dir)
    $tmp = Join-Path $dir ('.' + [IO.Path]::GetFileName($Path) + '.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllText($tmp, $Text, (New-Object System.Text.UTF8Encoding($false)))
        if ([IO.File]::Exists($Path)) { [IO.File]::Replace($tmp, $Path, [NullString]::Value) }
        else { [IO.File]::Move($tmp, $Path) }
    } finally {
        if ([IO.File]::Exists($tmp)) { [IO.File]::Delete($tmp) }
    }
}
# Reads UTF-8 text; a byte-order mark is dropped.
function Read-Text([string]$Path) { return [IO.File]::ReadAllText((Get-FullPath $Path)) }
function Test-FileExists([string]$Path) { return [IO.File]::Exists((Get-FullPath $Path)) }
# ConvertFrom-Json output is always wrapped in a PSObject, so test the object underneath.
function Test-JsonObject($Value) { return ($null -ne $Value) -and ($Value.PSObject.BaseObject -is [System.Management.Automation.PSCustomObject]) }
# Property names are matched case-sensitively, like the clients (and the Python scripts) do.
function Get-Prop($Object, [string]$Name) {
    foreach ($p in $Object.PSObject.Properties) { if ($p.Name -ceq $Name) { return $p } }
    return $null
}
function Get-Value($Object, [string]$Name) {
    $p = Get-Prop $Object $Name
    if ($null -eq $p) { return $null }
    # The comma keeps a one-element array an array; PowerShell would unroll it on return.
    return ,$p.Value
}
# PowerShell objects can't hold two keys that differ only in case, so refuse them (the Python scripts do too).
function Assert-NoCaseTwin($Object, [string]$Key, [string]$Where, [string]$Path) {
    foreach ($p in $Object.PSObject.Properties) {
        if ($p.Name -ieq $Key -and $p.Name -cne $Key) { throw "$Where of $Path has a key '$($p.Name)' that differs from '$Key' only in case; rename or remove it, then run this again. The file was left unchanged." }
    }
}
function Read-JsonConfig([string]$Path) {
    if (-not (Test-FileExists $Path)) { return [PSCustomObject]@{} }
    $text = Read-Text $Path
    if ([string]::IsNullOrWhiteSpace($text)) { return [PSCustomObject]@{} }
    try { $cfg = $text | ConvertFrom-Json } catch {
        # PowerShell 7 says "different casing", Windows PowerShell 5.1 "duplicated key".
        if ($_.Exception.Message -match 'different casing|duplicated key') { throw "$Path has JSON keys that differ only in case ($($_.Exception.Message)); rename or remove one, then run this again. The file was left unchanged." }
        throw "$Path is not valid JSON ($($_.Exception.Message)). Fix or move it, then run this again; it was left unchanged."
    }
    if (-not (Test-JsonObject $cfg)) { throw "$Path does not contain a JSON object; it was left unchanged." }
    return $cfg
}
function Save-JsonConfig([string]$Path, $Config) {
    Write-TextAtomic $Path (($Config | ConvertTo-Json -Depth 100) + "`n")
}
function Set-Value($Object, [string]$Name, $Value) { $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value -Force }
function Remove-Value($Object, [string]$Name) { $p = Get-Prop $Object $Name; if ($null -ne $p) { $Object.PSObject.Properties.Remove($p.Name) } }
$hookCommand = 'D="${CLAUDE_PROJECT_DIR:-$PWD}"; curl -fsS -m 8 -G -K "$HOME/.notdory/session-hook.curlrc" --data-urlencode "remote=$(git -C "$D" remote get-url origin 2>/dev/null)" --data-urlencode "directory=$(basename "$D")" --data-urlencode "path=$D" --data "format=text" || true'
$settings = Join-Path (Get-EnvOr 'CLAUDE_CONFIG_DIR' (Join-Path $env:USERPROFILE '.claude')) 'settings.json'
$keyFile = Join-Path (Join-Path $env:USERPROFILE '.notdory') 'session-hook.curlrc'
# Validates the hook settings before anything changes and returns the REST URL to use.
function Get-RestUrl([string]$Rest, [string]$McpUrl, [string]$Key) {
    $bad = $false
    foreach ($ch in $Key.ToCharArray()) { if ([int]$ch -lt 32 -or [int]$ch -eq 127) { $bad = $true } }
    if (-not $Key -or $bad) { throw 'The access key is empty or contains control characters.' }
    if (-not $Rest) {
        # The REST API is on the MCP host at port 8700 unless told otherwise.
        $m = [regex]::Match($McpUrl, '^(https?)://(?:[^/?#@]*@)?(\[[^\]]*\]|[^/?#:]+)')
        if (-not $m.Success) { throw "'$McpUrl' is not an http(s) URL." }
        $Rest = $m.Groups[1].Value + '://' + $m.Groups[2].Value + ':8700'
    }
    $Rest = $Rest.TrimEnd('/')
    Assert-Url $Rest
    return $Rest
}
function Test-NotDoryGroup($Group) {
    if (-not (Test-JsonObject $Group)) { return $false }
    $hooks = Get-Value $Group 'hooks'
    if ($hooks -isnot [Array]) { return $false }
    foreach ($h in $hooks) {
        if (Test-JsonObject $h) {
            $c = Get-Value $h 'command'
            if ($c -is [string] -and ($c.Contains('session-hook.curlrc') -or ($c.Contains('/v1.0/api/session') -and $c.Contains('x-access-key')))) { return $true }
        }
    }
    return $false
}
function ConvertTo-CurlString([string]$s) { return '"' + $s.Replace('\', '\\').Replace('"', '\"') + '"' }
# Mode: install, remove or reinstall. One read, one write and one backup of the original file.
function Update-NotDoryHook([string]$Mode, [string]$Rest, [string]$Key) {
    $cfg = Read-JsonConfig $settings
    Assert-NoCaseTwin $cfg 'hooks' 'The top-level object' $settings
    $hooks = Get-Value $cfg 'hooks'
    if ($null -eq $hooks) { $hooks = [PSCustomObject]@{} }
    elseif (-not (Test-JsonObject $hooks)) { throw "hooks in $settings is not a JSON object; it was left unchanged." }
    Assert-NoCaseTwin $hooks 'SessionStart' 'hooks' $settings
    $groups = Get-Value $hooks 'SessionStart'
    if ($null -ne $groups -and $groups -isnot [Array]) { throw "hooks.SessionStart in $settings is not a JSON array; it was left unchanged." }
    $kept = New-Object System.Collections.ArrayList
    $found = $false
    $list = @()
    if ($null -ne $groups) { $list = $groups }
    foreach ($g in $list) {
        if (Test-NotDoryGroup $g) { $found = $true } else { [void]$kept.Add($g) }
    }
    $messages = New-Object System.Collections.Generic.List[string]
    $changed = $false
    if ($Mode -eq 'remove' -or $Mode -eq 'reinstall') {
        if ($found) { $changed = $true; $messages.Add("Removed the NotDory SessionStart hook from $settings.") }
        elseif ($Mode -eq 'remove') { $messages.Add("No NotDory SessionStart hook in $settings; nothing to remove.") }
        else { $messages.Add("No NotDory SessionStart hook in $settings; continuing.") }
    }
    if ($Mode -eq 'install' -or $Mode -eq 'reinstall') {
        $hook = [PSCustomObject]@{ type = 'command'; command = $hookCommand; timeout = 10 }
        [void]$kept.Add([PSCustomObject]@{ hooks = @($hook) })
        $changed = $true
        $verb = 'Installed'
        if ($found -and $Mode -eq 'install') { $verb = 'Updated' }
        $messages.Add("$verb the NotDory SessionStart hook in $settings (session context from $Rest).")
    }
    if ($changed) {
        if ($kept.Count -gt 0) { Set-Value $hooks 'SessionStart' @($kept.ToArray()) } else { Remove-Value $hooks 'SessionStart' }
        if (@($hooks.PSObject.Properties).Count -gt 0) { Set-Value $cfg 'hooks' $hooks } else { Remove-Value $cfg 'hooks' }
        if (Test-FileExists $settings) {
            # One backup of the file as it was, with the same access rules as the original.
            $src = Get-FullPath $settings
            [IO.File]::Copy($src, $src + '.bak', $true)
            if ($env:OS -eq 'Windows_NT') { Set-Acl -LiteralPath ($src + '.bak') -AclObject (Get-Acl -LiteralPath $src) }
        }
        Save-JsonConfig $settings $cfg
    }
    if ($Mode -eq 'remove') {
        if (Test-FileExists $keyFile) { [IO.File]::Delete((Get-FullPath $keyFile)); $messages.Add("Removed $keyFile.") }
    } else {
        $text = "# NotDory SessionStart hook settings, written by scripts/*/install-claude and notdory-claude-hook. Keep this file private.`n" +
            'url = ' + (ConvertTo-CurlString ($Rest + '/v1.0/api/session')) + "`n" + 'header = ' + (ConvertTo-CurlString ('x-access-key: ' + $Key)) + "`n"
        Write-TextAtomic $keyFile $text
        $messages.Add("Saved the hook's access key in $keyFile.")
    }
    foreach ($m in $messages) { Write-Host $m }
}
$action = $arg1
if ($argc -eq 0) { $action = 'install' }
if ($action -cne 'install' -and $action -cne 'remove') {
    if ($action.Length -gt 1 -and $action.StartsWith('-')) { throw "Unknown option: $action (see --help)" }
    throw "Unknown action: $action (use install or remove; see --help)"
}
foreach ($a in @($arg2, $arg3)) { if ($a.Length -gt 1 -and $a.StartsWith('-')) { throw "Unknown option: $a (see --help)" } }
if ($action -eq 'install') {
    if ($argc -gt 3) { throw 'Unexpected argument (usage: notdory-claude-hook.bat install [REST_URL] [ACCESS_KEY])' }
    if (-not (Get-Command curl.exe -ErrorAction SilentlyContinue)) { throw "'curl.exe' was not found; the SessionStart hook needs it. Windows 10 1803 and later include it in System32." }
    $key = $arg3
    if (-not $key) { $key = Get-EnvOr 'NOTDORY_ACCESS_KEY' 'notdorydefaultkey' }
    $rest = $arg2
    if (-not $rest) { $rest = Get-EnvOr 'NOTDORY_REST_URL' '' }
    $rest = Get-RestUrl $rest (Get-EnvOr 'NOTDORY_MCP_URL' 'http://127.0.0.1:8720/mcp') $key
    Update-NotDoryHook 'install' $rest $key
} else {
    if ($argc -gt 1) { throw "Unexpected argument: $arg2 (usage: notdory-claude-hook.bat remove)" }
    Update-NotDoryHook 'remove' '' ''
}
Write-Host 'Restart Claude Code to pick up the change.'
