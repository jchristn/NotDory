@echo off
REM Connect Codex to the NotDory MCP server: writes an [mcp_servers.notdory] table (url, http_headers) in Codex's config.toml.
REM Config: %CODEX_HOME%\config.toml (default %USERPROFILE%\.codex\config.toml)
REM Idempotent: an existing 'notdory' entry is replaced; every other server is kept.
REM Authenticates with the credential ACCESS KEY only: Claude Code, Codex, Cursor and the Gemini CLI
REM send it in the x-access-key header; Mux sends it as a bearer token (Authorization: Bearer <key>),
REM as docs/INSTRUCTIONS_FOR_MUX.md describes. The secret key is never sent. The access key is a
REM capability token, so use a least-privilege credential.
REM For a client running inside WSL, use scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1) from the distro instead.
REM Usage: scripts\windows\install-codex.bat [ACCESS_KEY]
REM   ACCESS_KEY            overrides NOTDORY_ACCESS_KEY (default: notdorydefaultkey)
REM   NOTDORY_MCP_URL       server URL (default: http://127.0.0.1:8720/mcp)
REM   NOTDORY_CODEX_CONFIG  config file (default: as above)
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
echo Connect Codex to the NotDory MCP server: writes an [mcp_servers.notdory] table (url, http_headers) in Codex's config.toml.
echo Config: %%CODEX_HOME%%\config.toml (default %%USERPROFILE%%\.codex\config.toml)
echo Idempotent: an existing 'notdory' entry is replaced; every other server is kept.
echo Authenticates with the credential ACCESS KEY only: Claude Code, Codex, Cursor and the Gemini CLI
echo send it in the x-access-key header; Mux sends it as a bearer token (Authorization: Bearer ^<key^>),
echo as docs/INSTRUCTIONS_FOR_MUX.md describes. The secret key is never sent. The access key is a
echo capability token, so use a least-privilege credential.
echo For a client running inside WSL, use scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1) from the distro instead.
echo Usage: scripts\windows\install-codex.bat [ACCESS_KEY]
echo   ACCESS_KEY            overrides NOTDORY_ACCESS_KEY (default: notdorydefaultkey)
echo   NOTDORY_MCP_URL       server URL (default: http://127.0.0.1:8720/mcp)
echo   NOTDORY_CODEX_CONFIG  config file (default: as above)
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
if ($argc -gt 0 -and $arg1.Length -gt 1 -and $arg1.StartsWith('-')) { throw "Unknown option: $arg1 (see --help)" }
if ($argc -gt 1) { throw "Unexpected argument: $arg2 (usage: install-codex.bat [ACCESS_KEY])" }
$action = 'install'
$name = 'notdory'
$url = Get-EnvOr 'NOTDORY_MCP_URL' 'http://127.0.0.1:8720/mcp'
Assert-Url $url
$token = $arg1
if (-not $token) { $token = Get-EnvOr 'NOTDORY_ACCESS_KEY' 'notdorydefaultkey' }
$headerName = 'x-access-key'
$headerValue = $token
$path = Get-EnvOr 'NOTDORY_CODEX_CONFIG' (Join-Path (Get-EnvOr 'CODEX_HOME' (Join-Path $env:USERPROFILE '.codex')) 'config.toml')
$bareRe = [regex]'\G[A-Za-z0-9_-]+'
# A dotted key at $s[$i..]: @{ Parts; Index } or $null.
function Read-TomlKey([string]$s, [int]$i) {
    $parts = New-Object System.Collections.Generic.List[string]
    while ($true) {
        while ($i -lt $s.Length -and ($s[$i] -eq ' ' -or $s[$i] -eq "`t")) { $i++ }
        if ($i -lt $s.Length -and $s[$i] -eq '"') {
            $j = $i + 1
            $sb = New-Object System.Text.StringBuilder
            while ($j -lt $s.Length -and $s[$j] -ne '"') {
                if ($s[$j] -eq '\' -and $j + 1 -lt $s.Length) { [void]$sb.Append($s[$j + 1]); $j += 2 }
                else { [void]$sb.Append($s[$j]); $j++ }
            }
            if ($j -ge $s.Length) { return $null }
            $parts.Add($sb.ToString())
            $i = $j + 1
        } elseif ($i -lt $s.Length -and $s[$i] -eq "'") {
            $j = $s.IndexOf("'", $i + 1)
            if ($j -lt 0) { return $null }
            $parts.Add($s.Substring($i + 1, $j - $i - 1))
            $i = $j + 1
        } else {
            $m = $bareRe.Match($s, $i)
            if (-not $m.Success) { return $null }
            $parts.Add($m.Value)
            $i += $m.Length
        }
        while ($i -lt $s.Length -and ($s[$i] -eq ' ' -or $s[$i] -eq "`t")) { $i++ }
        if ($i -lt $s.Length -and $s[$i] -eq '.') { $i++; continue }
        return @{ Parts = $parts.ToArray(); Index = $i }
    }
}
function Read-TomlHeader([string]$Line) {
    $s = $Line.TrimStart(' ', "`t")
    if (-not $s.StartsWith('[', [StringComparison]::Ordinal)) { return $null }
    $arr = $s.StartsWith('[[', [StringComparison]::Ordinal)
    $start = 1; if ($arr) { $start = 2 }
    $k = Read-TomlKey $s $start
    if ($null -eq $k) { return $null }
    $close = ']'; if ($arr) { $close = ']]' }
    if ($s.Length -lt $k.Index + $close.Length -or $s.Substring($k.Index, $close.Length) -cne $close) { return $null }
    $rest = $s.Substring($k.Index + $close.Length).Trim()
    if ($rest -and -not $rest.StartsWith('#', [StringComparison]::Ordinal)) { return $null }
    return ,$k.Parts
}
# Tracks multi-line strings ($State.Open) and open [ / { ($State.Depth) across lines.
function Update-TomlState([string]$Text, $State) {
    $i = 0
    while ($i -lt $Text.Length) {
        if ($State.Open) {
            $j = $Text.IndexOf($State.Open, $i, [StringComparison]::Ordinal)
            if ($j -lt 0) { return }
            $i = $j + 3; $State.Open = $null; continue
        }
        $c = $Text[$i]
        if ($c -eq '#') { return }
        if ($Text.Length -ge $i + 3 -and ($Text.Substring($i, 3) -ceq ([string][char]34 * 3) -or $Text.Substring($i, 3) -ceq ([string][char]39 * 3))) { $State.Open = $Text.Substring($i, 3); $i += 3; continue }
        if ($c -eq '"') {
            $j = $i + 1
            while ($j -lt $Text.Length -and $Text[$j] -ne '"') { if ($Text[$j] -eq '\') { $j += 2 } else { $j++ } }
            $i = $j + 1; continue
        }
        if ($c -eq "'") { $j = $Text.IndexOf("'", $i + 1); if ($j -lt 0) { $j = $Text.Length }; $i = $j + 1; continue }
        if ($c -eq '[' -or $c -eq '{') { $State.Depth++ } elseif ($c -eq ']' -or $c -eq '}') { $State.Depth-- }
        $i++
    }
}
# @{ Kind; Table; Full } per line; Kind is header, key, cont (inside a value) or other.
function Get-TomlLines($Lines) {
    $out = New-Object System.Collections.ArrayList
    $table = @()
    $state = @{ Open = $null; Depth = 0 }
    foreach ($line in $Lines) {
        if ($state.Open -or $state.Depth -gt 0) { Update-TomlState $line $state; [void]$out.Add(@{ Kind = 'cont'; Table = $table; Full = $null }); continue }
        $h = Read-TomlHeader $line
        if ($null -ne $h) { $table = $h; [void]$out.Add(@{ Kind = 'header'; Table = $h; Full = $null }); continue }
        $k = Read-TomlKey $line 0
        if ($null -ne $k) {
            $rest = $line.Substring($k.Index).TrimStart(' ', "`t")
            if ($rest.StartsWith('=', [StringComparison]::Ordinal)) {
                Update-TomlState $rest.Substring(1) $state
                [void]$out.Add(@{ Kind = 'key'; Table = $table; Full = @($table) + @($k.Parts) })
                continue
            }
        }
        [void]$out.Add(@{ Kind = 'other'; Table = $table; Full = $null })
    }
    return ,$out
}
function Test-Ours($Parts) { return ($Parts.Count -ge 2) -and ($Parts[0] -ceq 'mcp_servers') -and ($Parts[1] -ceq $name) }
function Assert-TomlStructure($Lines) {
    foreach ($e in (Get-TomlLines $Lines)) {
        if ($e.Kind -ne 'key') { continue }
        $f = $e.Full
        if ($f.Count -eq 1 -and $f[0] -ceq 'mcp_servers') { throw "$path defines mcp_servers as an inline table or a dotted key; this script can only manage a [mcp_servers.$name] table. Edit the file by hand; it was left unchanged." }
        if ((Test-Ours $f) -and -not (Test-Ours $e.Table)) { throw "$path defines mcp_servers.$name with dotted keys or an inline table; this script can only manage a [mcp_servers.$name] table. Edit the file by hand; it was left unchanged." }
    }
}
function Read-Lines {
    if (-not (Test-FileExists $path)) { return @{ Lines = @(); Crlf = $false } }
    $text = Read-Text $path
    $lines = $text.Split([char]10)
    for ($n = 0; $n -lt $lines.Length; $n++) { if ($lines[$n].EndsWith("`r", [StringComparison]::Ordinal)) { $lines[$n] = $lines[$n].Substring(0, $lines[$n].Length - 1) } }
    return @{ Lines = $lines; Crlf = $text.Contains("`r`n") }
}
# Drops [mcp_servers.<name>] and its sub-tables. Comments and blank lines just before the next table
# header stay with that table.
function Remove-Table($Lines) {
    $info = Get-TomlLines $Lines
    $out = New-Object System.Collections.Generic.List[string]
    $buf = New-Object System.Collections.Generic.List[string]
    $skipping = $false; $found = $false
    for ($n = 0; $n -lt $Lines.Count; $n++) {
        $line = $Lines[$n]; $e = $info[$n]
        if ($e.Kind -eq 'header') {
            if (Test-Ours $e.Table) { $skipping = $true; $found = $true; $buf.Clear(); continue }
            if ($skipping) {
                # Don't double the blank line that already separates the tables.
                while ($buf.Count -gt 0 -and [string]::IsNullOrWhiteSpace($buf[0]) -and ($out.Count -eq 0 -or [string]::IsNullOrWhiteSpace($out[$out.Count - 1]))) { $buf.RemoveAt(0) }
                $out.AddRange($buf); $buf.Clear(); $skipping = $false
            }
            $out.Add($line); continue
        }
        if ($skipping) {
            $t = $line.Trim()
            if ($e.Kind -eq 'other' -and ($t -eq '' -or $t.StartsWith('#', [StringComparison]::Ordinal))) { $buf.Add($line) } else { $buf.Clear() }
            continue
        }
        $out.Add($line)
    }
    while ($out.Count -gt 0 -and [string]::IsNullOrWhiteSpace($out[$out.Count - 1])) { $out.RemoveAt($out.Count - 1) }
    return @{ Lines = $out; Found = $found }
}
function ConvertTo-TomlString([string]$s) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($ch in $s.ToCharArray()) {
        $c = [int]$ch
        if ($ch -eq [char]34) { [void]$sb.Append('\"') }
        elseif ($ch -eq [char]92) { [void]$sb.Append('\\') }
        elseif ($c -lt 32 -or $c -eq 127) { [void]$sb.Append(('\u{0:x4}' -f $c)) }
        else { [void]$sb.Append($ch) }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}
function Save-Lines($Lines, [bool]$Crlf) {
    Assert-TomlStructure $Lines
    $nl = "`n"; if ($Crlf) { $nl = "`r`n" }
    $text = ''
    if ($Lines.Count -gt 0) { $text = ($Lines -join $nl) + $nl }
    Write-TextAtomic $path $text
}
$read = Read-Lines
Assert-TomlStructure $read.Lines
$r = Remove-Table $read.Lines
$messages = New-Object System.Collections.Generic.List[string]
if ($action -eq 'remove' -or $action -eq 'reinstall') {
    if ($r.Found) {
        Save-Lines $r.Lines $read.Crlf
        $messages.Add("Removed '$name' from $path.")
    } elseif ($action -eq 'remove') {
        $messages.Add("No '$name' entry in $path; nothing to remove.")
    } else {
        $messages.Add("No '$name' entry in $path; continuing.")
    }
}
if ($action -eq 'install' -or $action -eq 'reinstall') {
    $out = $r.Lines
    if ($out.Count -gt 0) { $out.Add('') }
    $out.Add("[mcp_servers.$name]")
    $out.Add('url = ' + (ConvertTo-TomlString $url))
    if ($headerName) { $out.Add('http_headers = { ' + (ConvertTo-TomlString $headerName) + ' = ' + (ConvertTo-TomlString $headerValue) + ' }') }
    Save-Lines $out $read.Crlf
    if ($r.Found -and $action -eq 'install') { $messages.Add("Replaced the existing '$name' entry in $path ($url).") }
    else { $messages.Add("Added '$name' to $path ($url).") }
}
foreach ($m in $messages) { Write-Host $m }
Write-Host 'Restart Codex to pick up the change.'
