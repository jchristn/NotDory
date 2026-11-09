@echo off
REM Connect Codex to the NotDory MCP server by writing an [mcp_servers.notdory] table to %USERPROFILE%\.codex\config.toml.
REM Authenticates with the credential ACCESS KEY only (x-access-key header); the secret key is never sent.
REM Usage: install-codex.bat [ACCESS_KEY]  (arg #1 overrides NOTDORY_ACCESS_KEY)
REM Override with NOTDORY_MCP_URL / NOTDORY_ACCESS_KEY / NOTDORY_CODEX_CONFIG.
setlocal
if "%NOTDORY_MCP_URL%"=="" set "NOTDORY_MCP_URL=http://127.0.0.1:8720/mcp"
if "%NOTDORY_ACCESS_KEY%"=="" set "NOTDORY_ACCESS_KEY=notdorydefaultkey"
if not "%~1"=="" set "NOTDORY_ACCESS_KEY=%~1"
if "%NOTDORY_CODEX_CONFIG%"=="" set "NOTDORY_CODEX_CONFIG=%USERPROFILE%\.codex\config.toml"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:NOTDORY_CODEX_CONFIG; $d=Split-Path -Parent $p; if(-not (Test-Path $d)){ New-Item -ItemType Directory -Force -Path $d | Out-Null }; $lines=@(); if(Test-Path $p){ $lines=@(Get-Content -Path $p) }; $out=New-Object System.Collections.Generic.List[string]; $skip=$false; $found=$false; foreach($l in $lines){ if($l -match '^\s*\[\[?\s*([^\]]+?)\s*\]\]?\s*(#.*)?$'){ $n=$Matches[1].Replace([string][char]34,''); $skip=($n -eq 'mcp_servers.notdory' -or $n.StartsWith('mcp_servers.notdory.')); if($skip){ $found=$true } }; if(-not $skip){ $out.Add($l) } }; while($out.Count -gt 0 -and [string]::IsNullOrWhiteSpace($out[$out.Count-1])){ $out.RemoveAt($out.Count-1) }; if($out.Count -gt 0){ $out.Add('') }; $out.Add('[mcp_servers.notdory]'); $out.Add('url = ' + (ConvertTo-Json $env:NOTDORY_MCP_URL)); $out.Add('http_headers = { ' + (ConvertTo-Json 'x-access-key') + ' = ' + (ConvertTo-Json $env:NOTDORY_ACCESS_KEY) + ' }'); [IO.File]::WriteAllText($p, (($out -join [Environment]::NewLine) + [Environment]::NewLine)); Write-Host ('Added notdory to ' + $p)"
if errorlevel 1 exit /b 1
echo Restart Codex to pick up the change.
endlocal
