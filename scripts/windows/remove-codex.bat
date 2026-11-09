@echo off
REM Disconnect Codex from NotDory by removing the [mcp_servers.notdory] table from %USERPROFILE%\.codex\config.toml.
setlocal
if "%NOTDORY_CODEX_CONFIG%"=="" set "NOTDORY_CODEX_CONFIG=%USERPROFILE%\.codex\config.toml"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:NOTDORY_CODEX_CONFIG; if(-not (Test-Path $p)){ Write-Host ('Nothing to remove at ' + $p); exit }; $lines=@(Get-Content -Path $p); $out=New-Object System.Collections.Generic.List[string]; $skip=$false; $found=$false; foreach($l in $lines){ if($l -match '^\s*\[\[?\s*([^\]]+?)\s*\]\]?\s*(#.*)?$'){ $n=$Matches[1].Replace([string][char]34,''); $skip=($n -eq 'mcp_servers.notdory' -or $n.StartsWith('mcp_servers.notdory.')); if($skip){ $found=$true } }; if(-not $skip){ $out.Add($l) } }; while($out.Count -gt 0 -and [string]::IsNullOrWhiteSpace($out[$out.Count-1])){ $out.RemoveAt($out.Count-1) }; if(-not $found){ Write-Host ('No notdory entry found in ' + $p); exit }; [IO.File]::WriteAllText($p, (($out -join [Environment]::NewLine) + [Environment]::NewLine)); Write-Host ('Removed notdory from ' + $p)"
endlocal
