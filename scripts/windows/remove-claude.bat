@echo off
REM Disconnect Claude Code from NotDory: removes the user-scope MCP server and the SessionStart hook.
setlocal
where claude >nul 2>nul
if errorlevel 1 (
  echo Claude CLI not found on PATH.
  exit /b 1
)

call claude mcp remove --scope user notdory
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0notdory-claude-hook.ps1" -Action remove
echo Removed 'notdory' MCP server and the SessionStart hook from Claude Code.
endlocal
