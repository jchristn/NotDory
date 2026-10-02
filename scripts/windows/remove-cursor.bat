@echo off
REM Disconnect Cursor from NotDory by removing the 'notdory' entry from %USERPROFILE%\.cursor\mcp.json.
setlocal
if "%NOTDORY_CURSOR_CONFIG%"=="" set "NOTDORY_CURSOR_CONFIG=%USERPROFILE%\.cursor\mcp.json"
set "NOTDORY_CONFIG=%NOTDORY_CURSOR_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:NOTDORY_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $raw=Get-Content -Raw -Path $p; if([string]::IsNullOrWhiteSpace($raw)){ exit }; $root=$raw | ConvertFrom-Json; if($root.mcpServers){ $root.mcpServers.PSObject.Properties.Remove('notdory') }; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Removed notdory from ' + $p)"
endlocal
