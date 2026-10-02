@echo off
REM Disconnect Codex from NotDory by removing the 'notdory' entry from %USERPROFILE%\.codex\config.json.
setlocal
if "%NOTDORY_CODEX_CONFIG%"=="" set "NOTDORY_CODEX_CONFIG=%USERPROFILE%\.codex\config.json"
set "NOTDORY_CONFIG=%NOTDORY_CODEX_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:NOTDORY_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $raw=Get-Content -Raw -Path $p; if([string]::IsNullOrWhiteSpace($raw)){ exit }; $root=$raw | ConvertFrom-Json; if($root.mcpServers){ $root.mcpServers.PSObject.Properties.Remove('notdory') }; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Removed notdory from ' + $p)"
endlocal
