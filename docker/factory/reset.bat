@echo off
setlocal
set "REPO_ROOT=%~dp0..\.."
pushd "%REPO_ROOT%" >nul

echo [factory-reset] Factory reset will remove Docker data, named volumes, and regenerated Wilson settings.
set /p CONFIRMATION=Type RESET to continue: 
if not "%CONFIRMATION%"=="RESET" (
  echo [factory-reset] Confirmation did not match RESET. Aborting.
  popd >nul
  exit /b 1
)

echo [factory-reset] Stopping Docker Compose services and removing anonymous volumes.
docker compose -f docker\compose.yaml down --volumes --remove-orphans >nul 2>nul

echo [factory-reset] Removing persisted Docker data.
if exist docker\data rmdir /s /q docker\data

echo [factory-reset] Recreating required directory structure.
mkdir docker\data >nul 2>nul

echo [factory-reset] Restoring factory-default Docker settings files.
copy /y docker\factory\wilson.json docker\wilson.json >nul

echo [factory-reset] Factory reset completed.
popd >nul
exit /b 0
