@echo off
REM Disconnect Mux from NotDory by removing the 'notdory' entry from Mux's mcp-servers.json.
setlocal
if "%NOTDORY_MUX_CONFIG%"=="" set "NOTDORY_MUX_CONFIG=%USERPROFILE%\.mux\mcp-servers.json"
set "NOTDORY_CONFIG=%NOTDORY_MUX_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:NOTDORY_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $raw=Get-Content -Raw -Path $p; if([string]::IsNullOrWhiteSpace($raw)){ exit }; $root=$raw | ConvertFrom-Json; if($root.servers){ $root.servers=@($root.servers | Where-Object { $_.name -ne 'notdory' }) }; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Removed notdory from ' + $p)"
endlocal
