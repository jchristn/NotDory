@echo off
REM Disconnect the Gemini CLI from NotDory by removing the 'notdory' entry from %USERPROFILE%\.gemini\settings.json.
setlocal
if "%NOTDORY_GEMINI_CONFIG%"=="" set "NOTDORY_GEMINI_CONFIG=%USERPROFILE%\.gemini\settings.json"
set "NOTDORY_CONFIG=%NOTDORY_GEMINI_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:NOTDORY_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $raw=Get-Content -Raw -Path $p; if([string]::IsNullOrWhiteSpace($raw)){ exit }; $root=$raw | ConvertFrom-Json; if($root.mcpServers){ $root.mcpServers.PSObject.Properties.Remove('notdory') }; [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20)); Write-Host ('Removed notdory from ' + $p)"
endlocal
