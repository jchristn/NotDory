@echo off
REM Connect Claude Code to Isis, for every project, in one step:
REM   1. Registers the Isis MCP server at user scope (every project sees it).
REM   2. Installs a SessionStart hook that loads the project's memory context before the model's first turn; the project
REM      is matched by its git repository name, then its folder name, and a new repository gets its own scope.
REM Authenticates with the credential ACCESS KEY only (sent as the x-access-key header). The secret key is
REM never sent and never leaves your machine; the access key is a capability token, so use a least-privilege one.
REM Usage: install-claude.bat [ACCESS_KEY]  (arg #1 overrides ISIS_ACCESS_KEY)
REM Override defaults with ISIS_MCP_URL / ISIS_REST_URL / ISIS_ACCESS_KEY. ISIS_REST_URL defaults to the MCP host on
REM port 8700. Set ISIS_SESSION_HOOK=0 to skip the hook.
setlocal
if "%ISIS_MCP_URL%"=="" set "ISIS_MCP_URL=http://127.0.0.1:8720/mcp"
if "%ISIS_ACCESS_KEY%"=="" set "ISIS_ACCESS_KEY=isisdefaultkey"
if not "%~1"=="" set "ISIS_ACCESS_KEY=%~1"
if "%ISIS_REST_URL%"=="" for /f "usebackq delims=" %%U in (`powershell -NoProfile -Command "$u=[Uri]$env:ISIS_MCP_URL; $u.Scheme + '://' + $u.Host + ':8700'"`) do set "ISIS_REST_URL=%%U"

where claude >nul 2>nul
if errorlevel 1 (
  echo Claude CLI not found on PATH. Install Claude Code first: https://docs.anthropic.com/claude-code
  exit /b 1
)

REM Re-running replaces the user-scope entry instead of failing on a duplicate.
claude mcp remove --scope user isis >nul 2>nul
claude mcp add --scope user --transport http isis "%ISIS_MCP_URL%" --header "x-access-key: %ISIS_ACCESS_KEY%"
if errorlevel 1 exit /b 1
echo Added 'isis' MCP server to Claude Code for every project (%ISIS_MCP_URL%).

if not "%ISIS_SESSION_HOOK%"=="0" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0isis-claude-hook.ps1" -Action install -RestUrl "%ISIS_REST_URL%" -AccessKey "%ISIS_ACCESS_KEY%"
)
echo Restart Claude Code to pick it up.
endlocal
