@echo off
setlocal
echo === Automated suite (console runner) ===
dotnet run --project src\Test.Automated\Test.Automated.csproj -c Release
if errorlevel 1 exit /b 1

echo Tests complete.
endlocal
