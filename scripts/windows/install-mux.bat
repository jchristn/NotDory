@echo off
REM Connect Mux to the NotDory MCP server by adding an 'notdory' entry to Mux's mcp-servers.json.
REM
REM Mux can send only ONE auth header, so it authenticates with the credential ACCESS KEY carried as a
REM bearer token (Authorization: Bearer <accessKey>). The access key is the public, transferable material;
REM the secret key is NEVER written here and never leaves your machine. Treat the access key as a capability
REM token and use a least-privilege credential.
REM
REM Usage:  install-mux.bat [ACCESS_KEY]
REM   ACCESS_KEY  optional; overrides NOTDORY_ACCESS_KEY, which overrides the default 'notdorydefaultkey'.
REM Override the endpoint with NOTDORY_MCP_BASE_URL and the config path with NOTDORY_MUX_CONFIG.
setlocal
if "%NOTDORY_MCP_BASE_URL%"=="" set "NOTDORY_MCP_BASE_URL=http://127.0.0.1:8720"
if "%NOTDORY_ACCESS_KEY%"=="" set "NOTDORY_ACCESS_KEY=notdorydefaultkey"
if not "%~1"=="" set "NOTDORY_ACCESS_KEY=%~1"
if "%NOTDORY_MUX_CONFIG%"=="" set "NOTDORY_MUX_CONFIG=%USERPROFILE%\.mux\mcp-servers.json"
set "NOTDORY_CONFIG=%NOTDORY_MUX_CONFIG%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:NOTDORY_CONFIG; $d=Split-Path -Parent $p; if(-not (Test-Path $d)){ New-Item -ItemType Directory -Force -Path $d | Out-Null }; $raw=''; if(Test-Path $p){ $raw=Get-Content -Raw -Path $p }; if([string]::IsNullOrWhiteSpace($raw)){ $root=[PSCustomObject]@{} } else { $root=$raw | ConvertFrom-Json }; if($null -eq $root.servers){ $root | Add-Member -NotePropertyName servers -NotePropertyValue @() -Force }; $others=@($root.servers | Where-Object { $_.name -ne 'notdory' }); $auth=[PSCustomObject]@{ type='bearer'; bearerToken=$env:NOTDORY_ACCESS_KEY; apiKeyHeader='X-API-Key'; apiKeyValue='' }; $entry=[PSCustomObject]@{ name='notdory'; transport='http'; url=$env:NOTDORY_MCP_BASE_URL; mcpPath='/mcp'; auth=$auth }; $root.servers=@($others + $entry); [IO.File]::WriteAllText($p, ($root | ConvertTo-Json -Depth 20 -Compress)); Write-Host ('Added notdory to ' + $p + ' (bearer auth, access key only)')"
echo Point Mux at this file with --mcp-config, or add it to your Mux config directory, then restart Mux.
endlocal
