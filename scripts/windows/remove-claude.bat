@echo off
REM Disconnect Claude Code from Isis: removes the user-scope MCP server and the SessionStart hook.
setlocal
where claude >nul 2>nul
if errorlevel 1 (
  echo Claude CLI not found on PATH.
  exit /b 1
)

call claude mcp remove --scope user isis
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0isis-claude-hook.ps1" -Action remove
echo Removed 'isis' MCP server and the SessionStart hook from Claude Code.
endlocal
