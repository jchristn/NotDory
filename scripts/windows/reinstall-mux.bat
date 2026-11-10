@echo off
REM Re-register Mux with the NotDory MCP server: removes the 'notdory' entry, then adds it again.
REM Config: %MUX_CONFIG_DIR%\mcp-servers.json (default %USERPROFILE%\.mux\mcp-servers.json)
REM Continues when there is no 'notdory' entry to remove; every other server is kept.
REM Authenticates with the credential ACCESS KEY only: Claude Code, Codex, Cursor and the Gemini CLI
REM send it in the x-access-key header; Mux sends it as a bearer token (Authorization: Bearer <key>),
REM as docs/INSTRUCTIONS_FOR_MUX.md describes. The secret key is never sent. The access key is a
REM capability token, so use a least-privilege credential.
REM For a client running inside WSL, use scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1) from the distro instead.
REM Usage: scripts\windows\reinstall-mux.bat [ACCESS_KEY]
REM   ACCESS_KEY            overrides NOTDORY_ACCESS_KEY (default: notdorydefaultkey)
REM   NOTDORY_MCP_URL       server URL; Mux stores its origin and path (default: http://127.0.0.1:8720/mcp)
REM   NOTDORY_MCP_BASE_URL  server origin, path /mcp (overrides NOTDORY_MCP_URL)
REM   NOTDORY_MUX_CONFIG    config file (default: as above)
REM Requires Windows PowerShell.
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
echo Re-register Mux with the NotDory MCP server: removes the 'notdory' entry, then adds it again.
echo Config: %%MUX_CONFIG_DIR%%\mcp-servers.json (default %%USERPROFILE%%\.mux\mcp-servers.json)
echo Continues when there is no 'notdory' entry to remove; every other server is kept.
echo Authenticates with the credential ACCESS KEY only: Claude Code, Codex, Cursor and the Gemini CLI
echo send it in the x-access-key header; Mux sends it as a bearer token (Authorization: Bearer ^<key^>),
echo as docs/INSTRUCTIONS_FOR_MUX.md describes. The secret key is never sent. The access key is a
echo capability token, so use a least-privilege credential.
echo For a client running inside WSL, use scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1) from the distro instead.
echo Usage: scripts\windows\reinstall-mux.bat [ACCESS_KEY]
echo   ACCESS_KEY            overrides NOTDORY_ACCESS_KEY (default: notdorydefaultkey)
echo   NOTDORY_MCP_URL       server URL; Mux stores its origin and path (default: http://127.0.0.1:8720/mcp)
echo   NOTDORY_MCP_BASE_URL  server origin, path /mcp (overrides NOTDORY_MCP_URL)
echo   NOTDORY_MUX_CONFIG    config file (default: as above)
echo Requires Windows PowerShell.
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
if ($argc -gt 1) { throw "Unexpected argument: $arg2 (usage: reinstall-mux.bat [ACCESS_KEY])" }
$action = 'reinstall'
$name = 'notdory'
$url = Get-EnvOr 'NOTDORY_MCP_URL' 'http://127.0.0.1:8720/mcp'
Assert-Url $url
$token = $arg1
if (-not $token) { $token = Get-EnvOr 'NOTDORY_ACCESS_KEY' 'notdorydefaultkey' }
$headerName = 'x-access-key'
$headerValue = $token
$path = Get-EnvOr 'NOTDORY_MUX_CONFIG' (Join-Path (Get-EnvOr 'MUX_CONFIG_DIR' (Join-Path $env:USERPROFILE '.mux')) 'mcp-servers.json')
$base = ''
$base = Get-EnvOr 'NOTDORY_MCP_BASE_URL' ''
if ($base) { Assert-Url $base }
$authType = 'bearer'
if ($action -ne 'remove') {
    if ($base) {
        $base = $base.TrimEnd('/'); $mcpPath = '/mcp'
    } else {
        $m = [regex]::Match($url, '^(https?://[^/?#]+)([^?#]*)(\?[^#]*)?')
        $base = $m.Groups[1].Value
        $mcpPath = $m.Groups[2].Value
        if ($mcpPath -eq '' -or $mcpPath -eq '/') { $mcpPath = '/mcp' }
        $mcpPath += $m.Groups[3].Value
    }
}
function Get-Servers($Config) {
    Assert-NoCaseTwin $Config 'servers' 'The top-level object' $path
    $servers = Get-Value $Config 'servers'
    if ($null -eq $servers) { return $null }
    if ($servers -isnot [Array]) { throw "servers in $path is not a JSON array; it was left unchanged." }
    return ,$servers
}
function Test-Ours($Server) {
    if (-not (Test-JsonObject $Server)) { return $false }
    $n = Get-Value $Server 'name'
    return ($n -is [string]) -and ($n -ceq $name)
}
$cfg = Read-JsonConfig $path
$servers = Get-Servers $cfg
$messages = New-Object System.Collections.Generic.List[string]
# @($null) would be a one-element array, so start from an empty list when there is no servers array.
$list = @()
if ($null -ne $servers) { $list = $servers }
$present = $false
foreach ($s in $list) { if (Test-Ours $s) { $present = $true } }
if ($action -eq 'remove' -or $action -eq 'reinstall') {
    if ($present) {
        $kept = New-Object System.Collections.ArrayList
        foreach ($s in $servers) { if (-not (Test-Ours $s)) { [void]$kept.Add($s) } }
        $servers = $kept.ToArray()
        Set-Value $cfg 'servers' $servers
        Save-JsonConfig $path $cfg
        $messages.Add("Removed '$name' from $path.")
    } elseif ($action -eq 'remove') {
        $messages.Add("No '$name' entry in $path; nothing to remove.")
    } else {
        $messages.Add("No '$name' entry in $path; continuing.")
    }
}
if ($action -eq 'install' -or $action -eq 'reinstall') {
    if ($authType -eq 'bearer') { $auth = [PSCustomObject]@{ type = 'bearer'; bearerToken = $token; apiKeyHeader = 'X-API-Key'; apiKeyValue = '' } }
    elseif ($authType -eq 'apikey') { $auth = [PSCustomObject]@{ type = 'apikey'; bearerToken = ''; apiKeyHeader = $headerName; apiKeyValue = $token } }
    else { $auth = [PSCustomObject]@{ type = 'none'; bearerToken = ''; apiKeyHeader = 'X-API-Key'; apiKeyValue = '' } }
    $entry = [PSCustomObject]@{ name = $name; transport = 'http'; command = ''; args = @(); env = [PSCustomObject]@{}; url = $base; mcpPath = $mcpPath; auth = $auth }
    # Replace an existing entry in place (dropping any duplicates); otherwise append.
    $kept = New-Object System.Collections.ArrayList
    $replaced = $false
    foreach ($s in $list) {
        if (Test-Ours $s) {
            if (-not $replaced) { [void]$kept.Add($entry); $replaced = $true }
        } else { [void]$kept.Add($s) }
    }
    if (-not $replaced) { [void]$kept.Add($entry) }
    Set-Value $cfg 'servers' @($kept.ToArray())
    Save-JsonConfig $path $cfg
    if ($present -and $action -eq 'install') { $messages.Add("Replaced the existing '$name' entry in $path ($base$mcpPath).") }
    else { $messages.Add("Added '$name' to $path ($base$mcpPath).") }
}
foreach ($m in $messages) { Write-Host $m }
Write-Host 'Restart Mux to pick up the change.'
