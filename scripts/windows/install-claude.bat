@echo off
REM Connect Claude Code to NotDory, for every project, in one step:
REM   1. Registers the NotDory MCP server at user scope (every project sees it).
REM   2. Installs a SessionStart hook that loads the project's memory context before the model's first turn; the project
REM      is matched by its git repository name, then its folder name, and a new repository gets its own scope.
REM Authenticates with the credential ACCESS KEY only (sent as the x-access-key header). The secret key is
REM never sent and never leaves your machine; the access key is a capability token, so use a least-privilege one.
REM Usage: install-claude.bat [ACCESS_KEY]  (arg #1 overrides NOTDORY_ACCESS_KEY)
REM Override defaults with NOTDORY_MCP_URL / NOTDORY_REST_URL / NOTDORY_ACCESS_KEY. NOTDORY_REST_URL defaults to the MCP host on
REM port 8700. Set NOTDORY_SESSION_HOOK=0 to skip the hook.
setlocal
if "%NOTDORY_MCP_URL%"=="" set "NOTDORY_MCP_URL=http://127.0.0.1:8720/mcp"
if "%NOTDORY_ACCESS_KEY%"=="" set "NOTDORY_ACCESS_KEY=notdorydefaultkey"
if not "%~1"=="" set "NOTDORY_ACCESS_KEY=%~1"
if "%NOTDORY_REST_URL%"=="" for /f "usebackq delims=" %%U in (`powershell -NoProfile -Command "$u=[Uri]$env:NOTDORY_MCP_URL; $u.Scheme + '://' + $u.Host + ':8700'"`) do set "NOTDORY_REST_URL=%%U"

where claude >nul 2>nul
if errorlevel 1 (
  echo Claude CLI not found on PATH. Install Claude Code first: https://docs.anthropic.com/claude-code
  exit /b 1
)

REM Re-running replaces the user-scope entry instead of failing on a duplicate. claude is itself a batch file
REM (claude.cmd), so it must be run with "call" or this script would end when it does.
call claude mcp remove --scope user notdory >nul 2>nul
call claude mcp add --scope user --transport http notdory "%NOTDORY_MCP_URL%" --header "x-access-key: %NOTDORY_ACCESS_KEY%"
if errorlevel 1 exit /b 1
echo Added 'notdory' MCP server to Claude Code for every project (%NOTDORY_MCP_URL%).

if not "%NOTDORY_SESSION_HOOK%"=="0" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0notdory-claude-hook.ps1" -Action install -RestUrl "%NOTDORY_REST_URL%" -AccessKey "%NOTDORY_ACCESS_KEY%"
)
echo Restart Claude Code to pick it up.
endlocal
