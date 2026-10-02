@echo off
REM Run NotDory.Server from the working tree against the benchmark stack (benchmarks\docker\compose.yaml).
REM REST on 127.0.0.1:18700, Prometheus metrics on 127.0.0.1:19464. Build first: dotnet build src\NotDory.sln -c Release
setlocal
if not exist "%~dp0.run" mkdir "%~dp0.run"
pushd "%~dp0.run"
set NOTDORY_SETTINGS_FILE=notdory.bench.json
set NOTDORY_REST_PORT=18700
set NOTDORY_REST_HOSTNAME=127.0.0.1
set NOTDORY_DB_TYPE=Postgresql
set NOTDORY_DB_SERVER=127.0.0.1
set NOTDORY_DB_PORT=15432
set NOTDORY_DB_DATABASE=notdory
set NOTDORY_DB_USERNAME=notdory
set NOTDORY_DB_PASSWORD=notdory
set NOTDORY_RECALLDB_ENDPOINT=http://127.0.0.1:18600
set NOTDORY_RECALLDB_ADMIN_KEY=recalldbadmin
set NOTDORY_OBS_ENABLED=true
set NOTDORY_OBS_PROM_HOSTNAME=127.0.0.1
set NOTDORY_OBS_PROM_PORT=19464
if "%NOTDORY_DEFAULT_ENDPOINT_BASEURL%"=="" set NOTDORY_DEFAULT_ENDPOINT_BASEURL=http://127.0.0.1:11434
dotnet run --project "%~dp0..\src\NotDory.Server" -c Release --no-build
popd
endlocal
