@echo off
REM Connect Codex to the NotDory MCP server by adding an 'notdory' entry to %USERPROFILE%\.codex\config.json.
REM Authenticates with the credential ACCESS KEY only (x-access-key header); the secret key is never sent.
REM Usage: install-codex.bat [ACCESS_KEY]  (arg #1 overrides NOTDORY_ACCESS_KEY)
REM Override with NOTDORY_MCP_URL / NOTDORY_ACCESS_KEY / NOTDORY_CODEX_CONFIG.
setlocal
if "%NOTDORY_MCP_URL%"=="" set "NOTDORY_MCP_URL=http://127.0.0.1:8720/mcp"
if "%NOTDORY_ACCESS_KEY%"=="" set "NOTDORY_ACCESS_KEY=notdorydefaultkey"
if not "%~1"=="" set "NOTDORY_ACCESS_KEY=%~1"
if "%NOTDORY_CODEX_CONFIG%"=="" set "NOTDORY_CODEX_CONFIG=%USERPROFILE%\.codex\config.json"
set "NOTDORY_CONFIG=%NOTDORY_CODEX_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:NOTDORY_CONFIG; $d=Split-Path -Parent $p; if(-not (Test-Path $d)){ New-Item -ItemType Directory -Force -Path $d | Out-Null }; $raw=''; if(Test-Path $p){ $raw=Get-Content -Raw -Path $p }; if([string]::IsNullOrWhiteSpace($raw)){ $root=[PSCustomObject]@{} } else { $root=$raw | ConvertFrom-Json }; if($null -eq $root.mcpServers){ $root | Add-Member -NotePropertyName mcpServers -NotePropertyValue ([PSCustomObject]@{}) -Force }; $h=[PSCustomObject]@{ 'x-access-key'=$env:NOTDORY_ACCESS_KEY }; $entry=[PSCustomObject]@{ type='http'; url=$env:NOTDORY_MCP_URL; headers=$h }; $root.mcpServers | Add-Member -NotePropertyName notdory -NotePropertyValue $entry -Force; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Added notdory to ' + $p)"
echo Restart Codex to pick up the change.
endlocal
