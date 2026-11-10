@echo off
REM Disconnect Claude Code from the NotDory MCP server (user scope), via the claude CLI.
REM Also removes the NotDory SessionStart hook from %CLAUDE_CONFIG_DIR%\settings.json and its key file.
REM Succeeds with "nothing to remove" when there is no 'notdory' entry; every other server is kept.
REM For a client running inside WSL, use scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1) from the distro instead.
REM Usage: scripts\windows\remove-claude.bat
REM Requires the claude CLI and Windows PowerShell.
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
echo Disconnect Claude Code from the NotDory MCP server (user scope), via the claude CLI.
echo Also removes the NotDory SessionStart hook from %%CLAUDE_CONFIG_DIR%%\settings.json and its key file.
echo Succeeds with "nothing to remove" when there is no 'notdory' entry; every other server is kept.
echo For a client running inside WSL, use scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1) from the distro instead.
echo Usage: scripts\windows\remove-claude.bat
echo Requires the claude CLI and Windows PowerShell.
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
if ($argc -gt 0 -and $arg1.Length -gt 1 -and $arg1.StartsWith('-')) { throw "Unknown option: $arg1 (see --help)" }
if ($argc -gt 0) { throw "Unexpected argument: $arg1 (this script takes no arguments; see --help)" }
$action = 'remove'
$name = 'notdory'
$url = ''
$token = ''
$headerName = ''
$headerValue = ''
if (-not (Get-Command claude -ErrorAction SilentlyContinue)) { throw "'claude' was not found. Install Claude Code: irm https://claude.ai/install.ps1 | iex (see https://code.claude.com/docs/en/setup)." }
# True when an entry was removed, false when there was none; throws on any other failure.
function Remove-ClaudeEntry {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $out = (& claude mcp remove --scope user $name 2>&1 | Out-String)
        $rc = $LASTEXITCODE
    } finally { $ErrorActionPreference = $prev }
    if ($rc -eq 0) { return $true }
    if ($out -match 'No MCP server named') { return $false }
    throw "claude mcp remove failed: $($out.Trim())"
}
function Add-ClaudeEntry {
    $headerArgs = @()
    if ($headerName) { $headerArgs = @('--header', "${headerName}: $headerValue") }
    & claude mcp add --scope user --transport http $name $url @headerArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'claude mcp add failed.' }
}
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
if ($action -eq 'remove' -or $action -eq 'reinstall') {
    if (Remove-ClaudeEntry) { Write-Host "Removed '$name' from Claude Code at user scope." }
    elseif ($action -eq 'remove') { Write-Host "No '$name' entry in Claude Code at user scope; nothing to remove." }
    else { Write-Host "No '$name' entry in Claude Code at user scope; continuing." }
}
if ($action -eq 'install' -or $action -eq 'reinstall') {
    $replaced = $false
    if ($action -eq 'install') { $replaced = Remove-ClaudeEntry }
    Add-ClaudeEntry
    if ($replaced) { Write-Host "Replaced the existing '$name' entry in Claude Code at user scope ($url)." }
    else { Write-Host "Added '$name' to Claude Code at user scope ($url)." }
}
Update-NotDoryHook 'remove' '' ''
Write-Host 'Restart Claude Code to pick up the change.'
