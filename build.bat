@echo off
setlocal enabledelayedexpansion
echo === Backend build (NotDory.sln) ===
dotnet build src\NotDory.sln -c Release
if errorlevel 1 exit /b 1

echo === Dashboard build ===
pushd dashboard
call npm ci
if errorlevel 1 ( popd & exit /b 1 )
call npm run build
if errorlevel 1 ( popd & exit /b 1 )
popd

echo Build complete.
endlocal
