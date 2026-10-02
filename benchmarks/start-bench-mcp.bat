@echo off
REM Run NotDory.McpServer from the working tree, proxying the benchmark REST server (start-bench-server.bat).
REM MCP on http://127.0.0.1:18720/mcp. Used by the agent benchmark.
setlocal
if not exist "%~dp0.run" mkdir "%~dp0.run"
pushd "%~dp0.run"
set NOTDORY_MCP_SETTINGS_FILE=notdory-mcp.bench.json
set NOTDORY_MCP_HOSTNAME=127.0.0.1
set NOTDORY_MCP_PORT=18720
set NOTDORY_MCP_REST_HOSTNAME=127.0.0.1
set NOTDORY_MCP_REST_PORT=18700
dotnet run --project "%~dp0..\src\NotDory.McpServer" -c Release --no-build
popd
endlocal
